using System;
using System.Collections.Generic;
using System.Text;

namespace BlobBackupLib.Database.Model
{
    public class SerializerFile
    {
        public string Filename { get; set; }
        public bool BackedUp { get; set; }
        public int BackedUpUntilPointer { get; set; }
        public SerializerFile()
        {
            BackedUp = false;
        }
    }
}
