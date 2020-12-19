using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace BlobBackupLib.Logging
{
    public class ProgressItem
    {
        public long TotalFilesToBackup { get; set; }
        public long TotalBytesToBackup { get; set; }
        public long FilesBackedUp { get; set; }
        public long BytesBackedUp { get; set; }
        public DateTime StartTimeOfJob { get; set; }
        public Stopwatch SpeedSw { get; set; }
        public Stopwatch JobSw { get; set; }
        public Stopwatch DownloadSw { get; set; }
        public List<ThreadItem> BytesTransferredPerThread { get; set; }
        public double Speed { get; set; }
    }

    public class ThreadItem
    {
        public long BytesTransferred { get; set; }
        public long SpeedMeasuringStartBytes { get; set; }
        public long SpeedMeasuringCurrentBytes { get; set; }

        public ThreadItem()
        {
            BytesTransferred = 0;
            SpeedMeasuringCurrentBytes = 0;
            SpeedMeasuringStartBytes = 0;
        }
    }
}
