using System;
using System.Collections.Generic;
using System.Text;
using BlobBackupLib.Logging;
using LiteDB;

namespace BlobBackupLib.Database.Model
{
    public class Container
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string FilenameContains { get; set; }
        public string FilenameWithout { get; set; }
        public ReportItem FileInfo { get; set; }

        //[BsonRef("Blob")]
        public List<Blob>  Blobs { get; set; }

        public Container()
        {
            Blobs = new List<Blob>();
            FileInfo = new ReportItem();
        }
    }
}
