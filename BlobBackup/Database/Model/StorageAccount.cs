using System;
using System.Collections.Generic;
using System.Text;
using BlobBackupLib.Logging;
using LiteDB;

namespace BlobBackupLib.Database.Model
{
    public class StorageAccount
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string SasConnectionString { get; set; }
        public ReportItem FileInfos { get; set; }
        public string FilenameContains { get; set; }
        public string FilenameWithout { get; set; }

        public List<Container> Containers { get; set; }


        public StorageAccount()
        {
            Containers = new List<Container>();
            FileInfos = new ReportItem();
        }
    }

}
