using BlobBackupLib.Database.Model;
using BlobBackupLib.Jobs.Model;
using BlobBackupLib.Logging;
using Microsoft.Azure.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace BlobBackupLib.Jobs
{
    public static class JobHelpers
    {

        public static bool AreJobSettingsValid(Job job, out string errormsg)
        {
            errormsg = "";

            if(job.TotalDownloadSpeedMbPerSecond < 0 )
            {
                job.TotalDownloadSpeedMbPerSecond = 0; // 0 means full speed
            }

            if (ReplaceInvalidFoldernameChars(job.DestinationFolder) != job.DestinationFolder)
            {
                errormsg += "\r\nUnallowed characters in destination folder!";
            }
            if(job.NumberOfRetries < 0 || job.NumberOfRetries > 10)
            {
                errormsg += "\r\nNumber of retries must be 0 or max 10!";
            }
            if(job.NumberCopyThreads < 1 || job.NumberCopyThreads > 10)
            {
                errormsg += "\r\nNumber of copy threads must be 0 or max 10!";
            }

            if(job.StopJobAfterFailures < 0 || job.StopJobAfterFailures > 10)
            {
                errormsg += "\r\nNumber of 'stop after failures' must be 0 or max 10!";
            }

            if(job.KeepNumberVersions > 0 &&  job.DaysToKeepVersion < 0)
            {
                errormsg += "\r\nDays to keep versions must be 0 or above!";
            }

            if(job.KeepNumberVersions < 0)
            {
                errormsg += "\r\nNumber of versions must be above 0!";
            }
            if(job.TotalDownloadSpeedMbPerSecond < 0)
            {
                errormsg += "\r\nTotal download speed must be 0 or above!";
            }

            if (!string.IsNullOrWhiteSpace(job.FilesToBackupCsvList))
            {
                if (ReplaceInvalidFoldernameChars(job.FilesToBackupCsvList) != job.FilesToBackupCsvList && ReplaceInvalidFilenameChars(Path.GetFileName(job.FilesToBackupCsvList)) != Path.GetFileName(job.FilesToBackupCsvList))
                {
                    errormsg += "\r\nThe CSV backup list filename contains unallowed characters!";
                }

            }
            if (errormsg != "")
            {
                return false;
            }
            return true;
        }

    public static void IncludeSingleFilesIntoJob(Job job, List<Blob> files)
        {
            for (int i = 0; i < files.Count; i++)
            {
                files[i].FullPathFilename = files[i].FullPathFilename.Replace(@"\\", "/");
                files[i].FullPathFilename = files[i].FullPathFilename.Replace(@"\", "/");


                if (string.IsNullOrWhiteSpace(files[i].FullPathFilename))
                {
                    continue;
                }
                string storageAccount = GetStorageAccount(files[i].FullPathFilename);
                string container = GetContainer(files[i].FullPathFilename);
                files[i].Filename = GetBlobFilename(files[i].FullPathFilename);

                if(string.IsNullOrWhiteSpace(storageAccount)
                    || string.IsNullOrWhiteSpace(container)
                    || string.IsNullOrWhiteSpace(files[i].Filename)
                    || !IsAzureBlobFilenameValid(files[i].FullPathFilename))
                {
                    throw new Exception("The CSV file list file contains not correctly formatted data. It has to be <storage account name>/<container name>/<blob name>!");
                }

                if (string.IsNullOrWhiteSpace(files[i].TargetFilename))
                {
                    files[i].TargetFilename = files[i].FullPathFilename;
                }

                System.IO.FileInfo fi = null;
                try
                {
                    fi = new System.IO.FileInfo(files[i].TargetFilename);
                }
                catch (ArgumentException) { }
                catch (System.IO.PathTooLongException) { }
                catch (NotSupportedException) { }
                if (ReferenceEquals(fi, null))
                {
                    throw new Exception("The CSV file list contains a target filename, which is not OK: " + files[i].TargetFilename);
                }


                if (job.StorageAccounts.Find(c => c.Name == storageAccount) == null)
                {
                    job.StorageAccounts.Add(new StorageAccount() { Name = storageAccount });
                }
                int sAIndex = job.StorageAccounts.FindIndex(c => c.Name == storageAccount);
                if(job.StorageAccounts[sAIndex].Containers.Find(c => c.Name == container) == null)
                {
                    job.StorageAccounts[sAIndex].Containers.Add(new Container() { Name = container });
                }
                int cIndex = job.StorageAccounts[sAIndex].Containers.FindIndex(c => c.Name == container);
                if (job.StorageAccounts[sAIndex].Containers[cIndex].Blobs.Find(c => c.Filename == files[i].Filename) == null)
                {
                    job.StorageAccounts[sAIndex].Containers[cIndex].Blobs.Add(new Blob() { 
                        Filename = files[i].Filename, FullPathFilename = files[i].FullPathFilename, 
                        TargetFilename = files[i].TargetFilename});
                }
                //int bIndex = job.StorageAccounts[sAIndex].Containers[cIndex].Blobs.FindIndex(c => c.Filename == blobFilename);
            }
        }



        public static string ReplaceInvalidFilenameChars(string filename)
        {
            return string.Join("_", filename.Split(Path.GetInvalidFileNameChars()));
        }

        public static string ReplaceInvalidFoldernameChars(string filename)
        {
            return string.Join("_", filename.Split(Path.GetInvalidPathChars()));
        }

        private static bool IsAzureBlobFilenameValid(string filename)
        {
            if (filename.Count(c => c == '/') < 2)
            {
                return false;
            }
            var storageAccount = GetStorageAccount(filename);
            var container = GetContainer(filename);
            var blob = GetBlobFilename(filename);

            string patternAccount = @"^[a-z0-9]{3,24}$";
            string patternContainer = @"^[a-z0-9-]{3,63}$";
            string patternBlob = @"^.{1,1024}$";

            if (!Regex.IsMatch(container, patternContainer)
                || !Regex.IsMatch(storageAccount, patternAccount)
                || !Regex.IsMatch(blob, patternBlob))
            {
                return false;
            }
            return true;
        }

        private static string GetStorageAccount(string file)
        {

            int charLocation = file.IndexOf('/', StringComparison.Ordinal);
            if (charLocation > 0)
            {
                return file.Substring(0, charLocation);
            }
            return string.Empty;

        }

        private static string GetContainer(string file)
        {
            int firstSlash = file.IndexOf('/', StringComparison.Ordinal);
            int secondSlash = IndexOfSecond(file, "/");
            if (secondSlash > 0)
            {
                return file.Substring(firstSlash + 1, secondSlash - (firstSlash + 1));
            }
            return string.Empty;
        }

        private static string GetBlobFilename(string file)
        {
            int charLocation = IndexOfSecond(file, "/");
            if (charLocation > 0)
            {
                return file.Substring(charLocation + 1, file.Length - (charLocation + 1));
            }
            return string.Empty;
        }

        private static int IndexOfSecond(string theString, string toFind)
        {
            int first = theString.IndexOf(toFind);

            if (first == -1) return -1;

            // Find the "next" occurrence by starting just past the first
            return theString.IndexOf(toFind, first + 1);
        }

    }
}
