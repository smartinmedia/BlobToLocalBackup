using System;
using System.Collections.Generic;
using System.Text;

namespace BlobBackupLib.Database.Model
{
    [Serializable]

    public class Blob
    {
        public int Id { get; set; }
        public int StorageAccount { get; set; }
        public int Container { get; set; }
        public string Filename { get; set; }
        public string FullPathFilename { get; set; } // StorageAccount/Container/Filename (Filename in itself eventually contains /)
        public DateTimeOffset CreateTime { get; set; }
        public DateTimeOffset ModifyTime { get; set; }
        public string AccessTier { get; set; }
        public long? Size { get; set; }
        public bool IsBackedUp { get; set; }
        public string TargetFilename { get; set; }
        // public byte[] ContentHash { get; set; }
        // public string Signature { get; set; } //ContentMd5 + "_" + Filename//should be unique
        //public bool IsArchived { get; set; }
        //public List<BlobVersion> Versions { get; set; } //Is null, if this was never moved to an old version

        public Blob()
        {
            //IsArchived = false;
            TargetFilename = FullPathFilename;
            IsBackedUp = false;
            CreateTime = new DateTimeOffset(DateTime.UtcNow);
            ModifyTime = new DateTimeOffset(DateTime.UtcNow);
            //Versions = new List<BlobVersion>();

        }
    }

}
