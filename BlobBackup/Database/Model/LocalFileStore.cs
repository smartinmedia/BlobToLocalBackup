using System;
using System.Collections.Generic;
using System.Text;
using LiteDB;

namespace BlobBackupLib.Database.Model
{
    public class LocalFileStore
    {
        public int Id { get; set; }
        public string LocalFolder { get; set; }
        
        public DateTime LastScan { get; set; }

        [BsonRef("Blob")]
        public List<Blob> Blobs { get; set; }
    }
}
