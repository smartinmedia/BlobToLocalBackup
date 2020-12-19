using System;
using System.Collections.Generic;
using System.Text;

namespace BlobBackupLib.Logging
{
    public class ReportItem
    {
        public long NumberOfFiles { get; set; }
        public long TotalSize { get; set; }
        public long NumberOfFilesOfVersioned { get; set; }
        public long TotalSizeOfVersioned { get; set; }
        public long NumberOfHotFiles;
        public long SizeOfHotFiles;

        public ReportItem()
        {
            NumberOfFiles = 0;
            TotalSize = 0;
            NumberOfFilesOfVersioned = 0;
            TotalSizeOfVersioned = 0;
            NumberOfHotFiles = 0;
            SizeOfHotFiles = 0;

        }
    }
    
}
