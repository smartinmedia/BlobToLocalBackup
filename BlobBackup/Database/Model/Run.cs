using System;
using System.Collections.Generic;
using System.Text;
using BlobBackupLib.Logging;
using LiteDB;

namespace BlobBackupLib.Database.Model
{
    public class Run
    {
        public int Id { get; set; }
        public bool RunCompleted { get; set; }
        public DateTimeOffset TimeStamp { get; set; }

        public List<SerializerFile> SerializerFiles { get; set; }
        public List<int> BlobIds { get; set; }
        public List<int> LocalFilesId { get; set; }
        public List<int> FilesToBackup { get; set; }

        public List<StorageAccount> StorageAccounts { get; set; }
        public ReportItem BlobReport { get; set; }
        

        public Run()
        {
            BlobIds = new List<int>();
            LocalFilesId = new List<int>();
            FilesToBackup = new List<int>();
            SerializerFiles = new List<SerializerFile>();
            RunCompleted = false;
            TimeStamp = DateTimeOffset.UtcNow;
            StorageAccounts = new List<StorageAccount>();
        }
    }
}
