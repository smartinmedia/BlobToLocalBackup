using BlobBackupLib.Azure.Model;
using BlobBackupLib.Jobs.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using BlobBackupLib.Destination;
using BlobBackupLib.Database.Model;
using AzureBlobToLocalBackupConsole.AzureObjects;
using BlobBackupLib.AzureObjects;
using System.Collections.Concurrent;
using System.Linq;
using BlobBackupLib.Database;
using System.IO;
using BlobBackupLib.Logging;
using Blob = BlobBackupLib.Database.Model.Blob;
using Azure.Storage.Blobs;
using BlobBackupLib.Jobs;

namespace BlobBackupLib
{
    public class JobHandler
    {
        private Job _job;
        private string _jobFilename;
        private string _appsettingsFile;
        private List<Blob> _sourceBlobs;
        private List<LocalFile> _localFiles;
        private List<StorageAccount> _storageAccounts;
        private AppSettings _appSettings;
        private DbHandler _dH;
        private ConsoleParameters _cP;
        private OutputHandler _oH;
        private LoginCredentialsConfiguration _logCred;
        private int _errorCounter;
        private ReportItem _sourceReport;
        private string _filenameContains;
        private string _filenameWithout;
        private ProgressItem _progressItem;

        


        public JobHandler(string jobFile, string appsettingsFile)
        {

            _job = GetJobSettings(jobFile);
            _jobFilename = jobFile;
            _appsettingsFile = appsettingsFile;
            _sourceBlobs = new List<Blob>();
            _localFiles = new List<LocalFile>();
            _storageAccounts = new List<StorageAccount>();
            _errorCounter = 0;
            _sourceReport = new ReportItem();
           
        }

        public Job GetJobSettings()
        {
            return _job;
        }

        public async Task Start(ConsoleParameters cP, OutputHandler oH = null)
        {
            _cP = cP;

            _oH = oH;

            var appConfig = new AppSettingsHandler(_appsettingsFile);
            
            _appSettings = appConfig.GetAppSettings();
            _logCred = appConfig.GetLoginCredentials();
            string errormsg = "";
            if(!JobHelpers.AreJobSettingsValid(_job, out errormsg))
            {
                _oH.WriteToLogAndConsole(LoggingConsoleActions.Actions.Error, "Error with your job json file: " + _jobFilename + ":" + errormsg);
                return;
            }
            

            _job.Name = JobHelpers.ReplaceInvalidFilenameChars(_job.Name); //Make filename safe

            _dH = new DbHandler(_appSettings.DataBase.PathToDatabases + "/" + _job.Name + ".litedb");
          
            if (_cP.ContinueLastJob || _cP.RescanTarget)
            {
                _dH.SetLastRunAsCurrent();
            }
            else
            {
                _dH.CreateNewRun();
            }

            _oH.WriteToConsole(LoggingConsoleActions.Actions.Information, "Please be advised that the times stored with files are in UTC. Logged times are local times.");

            _progressItem = _oH.GetProgressItem();
            _oH.StartJobStopwatch();

            _oH.WriteToLogAndConsole(LoggingConsoleActions.Actions.Information, "Job started at " + _progressItem.StartTimeOfJob.ToString("yyyy-MM-dd HH:mm:ss"));

            if (_cP.ContinueLastJob == false)//_rescanTarget)
            {
                //Is there a csv list with files to backup?
                if (!string.IsNullOrWhiteSpace(_job.FilesToBackupCsvList))
                {
                    if (!File.Exists(_job.FilesToBackupCsvList))
                    {
                        _oH.WriteToLog(LoggingConsoleActions.Actions.Error, "The CSV list file does not exist!");
                        _oH.WriteToConsole(LoggingConsoleActions.Actions.Error, "The CSV list file does not exist!");

                        return;
                    }
                    string csv = "";
                    try
                    {
                        csv = File.ReadAllText(_job.FilesToBackupCsvList);
                    }
                    catch(Exception e)
                    {
                        _oH.WriteToLogAndConsole(LoggingConsoleActions.Actions.Error, "Cannot open the CSV file - is it in use by other application?", e);
                        return;
                    }
                    if (CsvHandler.IsValidCsv(csv))
                    {
                        var files = CsvHandler.CsvToList(csv);
                        JobHelpers.IncludeSingleFilesIntoJob(_job, files);
                    }
                    else
                    {
                        _oH.WriteToConsole(LoggingConsoleActions.Actions.Error, "The provided CSV file list is not valid!");
                        _oH.WriteToLog(LoggingConsoleActions.Actions.Error, "The provided CSV file list is not valid!");
                        return;
                    }

                }


                _oH.WriteToConsole(LoggingConsoleActions.Actions.Information, "Start rescanning target folder.");
                _oH.WriteToLog(LoggingConsoleActions.Actions.Information, "\r\n-------STARTING JOB -------\r\nStart rescanning target folder.");
                var targetReport = RescanTarget(_job.DestinationFolder);
                string info = "Target has " + (targetReport.NumberOfFiles) + " files (plus versioned files: " + targetReport.NumberOfFilesOfVersioned 
                    + ") with " + _oH.GetBytesReadable(targetReport.TotalSize) + " (plus versioned files: " + _oH.GetBytesReadable(targetReport.TotalSizeOfVersioned) + ".)";
                _oH.WriteToConsole(LoggingConsoleActions.Actions.Important, info);
                _oH.WriteToLog(LoggingConsoleActions.Actions.Information, info);
                //FlushLocalFileList();
            }

            foreach (var account in _job.StorageAccounts)
            {

                _filenameContains = _job.FilenameContains;
                _filenameWithout = _job.FilenameWithout;

                _storageAccounts.Add(account);
                if (!string.IsNullOrWhiteSpace(account.FilenameContains) || !string.IsNullOrWhiteSpace(account.FilenameWithout))
                {
                    _filenameContains = account.FilenameContains;
                    _filenameWithout = account.FilenameWithout;
                }
                
                
                var sA = _storageAccounts.Find(c => c.Name == account.Name);
                _oH.WriteToConsole(LoggingConsoleActions.Actions.Information, "Start scanning storage account: " + account.Name);
                _oH.WriteToLog(LoggingConsoleActions.Actions.Information, "Start scanning storage account: " + account.Name);

                var sah = new StorageAccountHandler(_oH);
                sah.ScanSourceStorageAccount(account, _logCred, _storageAccounts, _cP, _job, _filenameContains, _filenameWithout, StoreBlobInList);

                sA = _storageAccounts.Find(c => c.Name == account.Name); 
                _oH.WriteToConsole(LoggingConsoleActions.Actions.Important, "Storage account: " + account.Name + " has " + sA.FileInfos.NumberOfFiles + " files ('Hot': " + sA.FileInfos.NumberOfHotFiles + ") with " + _oH.GetBytesReadable(sA.FileInfos.TotalSize) + " size ('Hot': " + _oH.GetBytesReadable(sA.FileInfos.SizeOfHotFiles) + ") for the containers you selected.");
                _oH.WriteToLog(LoggingConsoleActions.Actions.Information, "Storage account: " + account.Name + " has " + sA.FileInfos.NumberOfFiles + " files with " + _oH.GetBytesReadable(sA.FileInfos.TotalSize) + " size for the containers you selected.");

            }

            if(_cP.ContinueLastJob == false)
            {
                FlushBlobList();
            }

            var info2 = GetInfoAboutJobSize();
            _oH.WriteToConsole(LoggingConsoleActions.Actions.Important, "Total files to backup: " + info2.NumberOfFiles + " with " + _oH.GetBytesReadable(info2.TotalSize));

            _progressItem.TotalFilesToBackup = info2.NumberOfFiles;
            _progressItem.TotalBytesToBackup = info2.TotalSize;
        
                       
            if(_cP.ScanOnly == false)
            {
                _oH.StartSpeedStopwatch();
                await BlobCopyAgent.BackupBlobsToTarget(_job, _dH, _oH, _progressItem, _storageAccounts, _logCred, _errorCounter);
                _dH.DeleteSerializedListOfCurrentAndLastRuns();
            }
            _oH.WriteToLogAndConsole(LoggingConsoleActions.Actions.Information, "Job finalized at " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));



        }


        private Job GetJobSettings(string jobFile)
        {
            var job = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile(jobFile, false, true)
                .Build();

            return job.GetSection("Job").Get<Job>();
        }

        private ReportItem RescanTarget(string targetFolder)
        {
            _oH.WriteToConsole(LoggingConsoleActions.Actions.Information, "Rescanning target folder.");
            _oH.WriteToLog(LoggingConsoleActions.Actions.Information, "Rescanning target folder.");
            _dH.ClearLocalFileStoreData();
            var fh = new FileHandler(targetFolder, _job);
            return fh.RescanFolder();
        }

        private void SerializeBlobsToDisc(List<Blob> blobs)
        {
            var serializeFile = _appSettings.DataBase.PathToDatabases + "/" + _job.Name + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss-ff") + ".ser";
            Serializer.Serialize(blobs, serializeFile);
            _dH.AddSerializedFileToRun(serializeFile);
        }


        private void StoreBlobInList(Blob blob)
        {
            if(blob.TargetFilename == null || string.IsNullOrWhiteSpace(blob.TargetFilename))
            {
                blob.TargetFilename = blob.FullPathFilename;
            }
            string path = Path.GetDirectoryName(blob.TargetFilename);
            string filename = Path.GetFileName(blob.TargetFilename);

            string tempTargetFoldername = JobHelpers.ReplaceInvalidFoldernameChars(path);
            string tempTargetFilename = JobHelpers.ReplaceInvalidFilenameChars(filename);
            string tempfullPathAndFilename = Path.Combine(tempTargetFoldername, tempTargetFilename);
            tempfullPathAndFilename = tempfullPathAndFilename.Replace('\\', '/');


            if (tempfullPathAndFilename != blob.TargetFilename)
            {
                if (_job.ReplaceInvalidTargetFilenameChars)
                {
                    blob.TargetFilename = tempfullPathAndFilename;
                }
                else
                {
                    _oH.WriteToLogAndConsole(LoggingConsoleActions.Actions.Error, "Target Filename contains invalid characters: " + blob.TargetFilename);
                    return;
                }
            }

            _sourceReport.NumberOfFiles++;
            _sourceReport.TotalSize += (long)blob.Size;
            if(blob.AccessTier.ToLower() == "hot")
            {
                _sourceReport.NumberOfHotFiles++;
                _sourceReport.SizeOfHotFiles += (long)blob.Size;
            }
            
            var saId = _storageAccounts.FindIndex(c => c.Name == _storageAccounts[blob.StorageAccount].Name);
            if (saId != -1)
            {
                _storageAccounts[saId].FileInfos.NumberOfFiles++;
                _storageAccounts[saId].FileInfos.TotalSize += (long)blob.Size;
                if (blob.AccessTier.ToLower() == "hot")
                {
                    _storageAccounts[saId].FileInfos.NumberOfHotFiles++;
                    _storageAccounts[saId].FileInfos.SizeOfHotFiles += (long)blob.Size;
                }

            }

            var cId = _storageAccounts[saId].Containers.FindIndex(c => c.Name == _storageAccounts[saId].Containers[blob.Container].Name);
            _storageAccounts[saId].Containers[cId].FileInfo.NumberOfFiles++;
            _storageAccounts[saId].Containers[cId].FileInfo.TotalSize += (long)blob.Size;
            if (blob.AccessTier.ToLower() == "hot")
            {
                _storageAccounts[saId].Containers[cId].FileInfo.NumberOfHotFiles++;
                _storageAccounts[saId].Containers[cId].FileInfo.SizeOfHotFiles += (long)blob.Size;
            }


            _sourceBlobs.Add(blob);

            if(_storageAccounts[saId].Containers[cId].FileInfo.NumberOfFiles % 50 == 0)
            {
                _oH.UpdateProgress("Collecting file informations, file #" + _storageAccounts[saId].Containers[cId].FileInfo.NumberOfFiles + " in container '" + _storageAccounts[saId].Containers[blob.Container].Name + "'", -1, 0);

            }
            if(_sourceBlobs.Count % 1000000 == 0)
            {
                FlushBlobList();
            }
        }

        private void FlushBlobList()
        {
            //BulkAddBlobToDb(_sourceBlobs);
            SerializeBlobsToDisc(_sourceBlobs);
            _sourceBlobs.Clear();
        }


        private ReportItem GetInfoAboutJobSize()
        {
            var infoAboutJobSize = new ReportItem();

            var run = _dH.GetCurrentRun();
            foreach (var ser in run.SerializerFiles)
            {
                var blobs = (List<Blob>)Serializer.Deserialize(ser.Filename);

                foreach (var blob in blobs)
                {
                    if (blob.AccessTier == "Hot")
                    {
                        if (File.Exists(Path.Combine(_job.DestinationFolder, blob.TargetFilename)))
                        {
                            var f = new FileInfo(Path.Combine(_job.DestinationFolder, blob.TargetFilename));
                            if (f.Length != blob.Size || f.CreationTime != blob.ModifyTime)
                            {
                                infoAboutJobSize.NumberOfFiles++;
                                infoAboutJobSize.TotalSize += (long)blob.Size;
                            }
                        }
                        else
                        {
                            infoAboutJobSize.NumberOfFiles++;
                            infoAboutJobSize.TotalSize += (long)blob.Size;
                        }
                    }
                }
            }
            return infoAboutJobSize;
        }
    }
}
