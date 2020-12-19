using Azure.Storage.Blobs;
using AzureBlobToLocalBackupConsole.AzureObjects;
using BlobBackupLib.Azure.Model;
using BlobBackupLib.AzureObjects;
using BlobBackupLib.Database;
using BlobBackupLib.Database.Model;
using BlobBackupLib.Destination;
using BlobBackupLib.Jobs.Model;
using BlobBackupLib.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace BlobBackupLib.Jobs
{
    public static class BlobCopyAgent
    {
        public static async Task BackupBlobsToTarget(Job _job, DbHandler _dH, OutputHandler _oH, ProgressItem _progressItem,
            List<StorageAccount> _storageAccounts, LoginCredentialsConfiguration _logCred, int _errorCounter)
        {

            var maxThreads = _job.NumberCopyThreads;
            string backUpFileName = "";

            //var unbackedUp = _dH.GetBlobsToBackupFromRun();
            int numberOfSerializedLists = _dH.GetNumberOfSerializedLists();

            int fileBackupCounter = 0;

            for (var i = 0; i < numberOfSerializedLists; i++)
            {
                var unbackedUp = (List<Database.Model.Blob>)_dH.GetNextUnbackedUpSerializedList();
                if (unbackedUp == null)
                {
                    continue;
                }
                var text2 = "TOTAL: " + _progressItem.FilesBackedUp + " of " + _progressItem.TotalFilesToBackup + " files (" + _oH.GetBytesReadable(_progressItem.BytesBackedUp) + " of " + _oH.GetBytesReadable(_progressItem.TotalBytesToBackup) + ")";
                int percent = 0;
                if(_progressItem.TotalBytesToBackup > 0)
                {
                    percent = (int)(((double)_progressItem.BytesBackedUp / (double)_progressItem.TotalBytesToBackup) * 100);
                }
                _oH.WriteToConsole(LoggingConsoleActions.Actions.ProgressUpdate, text2, true, -1, percent);
                var q = new ConcurrentQueue<Database.Model.Blob>(unbackedUp);
                var tasks = new List<Task>();
                for (int n = 0; n < maxThreads; n++)
                {
                    tasks.Add(Task.Factory.StartNew((state) =>
                    {
                        var threadNumber = (int)state;
                        StorageAccount currentAccount = null;
                        StorageAccountHandler sH;
                        ContainerHandler cH = null;
                        Container currentContainer = null;
                        BlobServiceClient blobServiceClient = null;

                        while (q.TryDequeue(out Database.Model.Blob blobObj))
                        {
                            var blob = blobObj;// _dH.GetBlobFromBlobStore(blobObj);

                            sH = new StorageAccountHandler(_oH);

                            if (currentAccount == null || currentAccount.Name != _storageAccounts[blob.StorageAccount].Name)
                            {
                                currentAccount = _storageAccounts.Find(c => c.Name == _storageAccounts[blob.StorageAccount].Name);
                                blobServiceClient = sH.Authenticate(currentAccount, _logCred);

                            }
                            if (currentContainer == null || currentContainer.Name != _storageAccounts[blob.StorageAccount].Containers[blob.Container].Name)
                            {
                                currentContainer = _storageAccounts[blob.StorageAccount].Containers[blob.Container];
                                cH = new ContainerHandler(blobServiceClient, currentContainer.Name, _oH, _job);
                            }

                            //if the file exists in an older version in the target, move away to versioned
                            string targetFile = Path.Combine(_job.DestinationFolder, blob.TargetFilename);
                            string tempFilename = Path.Combine(Path.GetDirectoryName(targetFile), "__temp_" + Path.GetFileName(targetFile) + ".tmp");
                            if (File.Exists(tempFilename))
                            {
                                File.Delete(tempFilename); //If there is an old version of temp, delete it
                            }

                            if (File.Exists(targetFile))
                            {
                                var fi = new FileInfo(targetFile);
                                if (fi.CreationTimeUtc == blob.ModifyTime.UtcDateTime && fi.Length == blob.Size)
                                {
                                    //file exists in the exact same fashion --> do nothing exept update in the database that this file is backedup
                                    //blob.IsBackedUp = true;
                                    //_dH.UpdateBlob(blob);
                                    continue;
                                }

                            }

                            int speedInBytes = (int)(_job.TotalDownloadSpeedMbPerSecond * 1000000 / _job.NumberCopyThreads);

                            try
                            {
                                cH.DownloadBlob(blob, tempFilename, threadNumber, speedInBytes).Wait();
                            }
                            catch (Exception e)
                            {
                                _oH.WriteToLog(LoggingConsoleActions.Actions.Error, blob.FullPathFilename + " backup failed", e);
                                _oH.WriteToConsole(LoggingConsoleActions.Actions.Error, blob.FullPathFilename + " backup failed");
                                _errorCounter++;
                                if (_errorCounter >= _job.StopJobAfterFailures)
                                {
                                    _oH.WriteToLogAndConsole(LoggingConsoleActions.Actions.Error, "Job stopped after more than " + _job.StopJobAfterFailures + " failures", null);
                                }
                            }
                            if (File.Exists(tempFilename))
                            {
                                //Check, if file is OK
                                var test = new DateTimeOffset(DateTime.Now);
                                var fi = new FileInfo(tempFilename);

                                File.SetCreationTimeUtc(tempFilename, blob.ModifyTime.UtcDateTime);
                                fi = new FileInfo(tempFilename);

                                if (fi.Length == blob.Size)
                                {
                                    //blob.IsBackedUp = true;
                                    //_dH.UpdateBlob(blob);
                                    //_oH.WriteToLog(LoggingConsoleActions.Actions.Information, blob.FullPathFilename + " backed up", null);
                                    fileBackupCounter++;
                                }
                                else
                                {
                                    //Temp File is not OK
                                    File.Delete(tempFilename);
                                    return;
                                }
                                if (File.Exists(targetFile))
                                {
                                    backUpFileName = FileVersioning.MakeBackupAndMove(targetFile, _job);
                                }
                                File.Move(tempFilename, targetFile);
                            }
                        }

                    }, n));
                }
                await Task.WhenAll(tasks);
            }
            if (fileBackupCounter == 0)
            {
                _oH.WriteToConsole(LoggingConsoleActions.Actions.Important, "Nothing to transfer - all in synch.");
                _oH.WriteToLog(LoggingConsoleActions.Actions.Important, "Nothing to transfer - all in synch.");
            }
        }

    }
}
