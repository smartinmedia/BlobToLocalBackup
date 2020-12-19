using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text;

namespace BlobBackupLib.Database.Model
{
    public class LocalFile
    {
        public int Id { get; set; }

        public string FilenameWithRelativePath { get; set; }
        //public string CompletePathFilename { get; set; }
        public DateTimeOffset CreateTime { get; set; }
        public long Size { get; set; }
    }
}
