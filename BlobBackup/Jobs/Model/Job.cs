using BlobBackupLib.Database.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace BlobBackupLib.Jobs.Model
{
    public class Job
    {
        public string Name { get; set; }
        public string DestinationFolder { get; set; }
        public bool ResumeOnRestartedJob { get; set; }
        public int NumberOfRetries { get; set; }
        public int NumberCopyThreads { get; set; }
        //public int NumberCopyRetries { get; set; }
        public int StopJobAfterFailures { get; set; }
        public List<StorageAccount> StorageAccounts { get; set; }
        public int KeepNumberVersions { get; set; }
        public int DaysToKeepVersion { get; set; }
        public string FilenameContains { get; set; }
        public string FilenameWithout { get; set; }
        public string FilesToBackupCsvList { get; set; }
        public bool ReplaceInvalidTargetFilenameChars { get; set; }
        public decimal TotalDownloadSpeedMbPerSecond { get; set; }

        public Job()
        {
            StorageAccounts = new List<StorageAccount>();
            KeepNumberVersions = 0;
        }
    }
}
