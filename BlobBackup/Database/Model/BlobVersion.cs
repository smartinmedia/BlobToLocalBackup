using System;
using System.Collections.Generic;
using System.Text;

namespace BlobBackupLib.Database.Model
{
    public class BlobVersion
    {
        public DateTimeOffset CreateTime { get; set; }
        public DateTimeOffset ModifyTime { get; set; }
        public long? Size { get; set; }
        public bool IsBackedUp { get; set; }
        public string Signature { get; set; }
    }
}
