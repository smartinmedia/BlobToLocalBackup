using BlobBackupLib.Database.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace BlobBackupLib.Logging
{
    public class OutputHandler
    {
        private Action<LoggingConsoleActions.Actions, string, bool, int, int> _consoleOutput;
        private Action<LoggingConsoleActions.Actions, string, Exception> _logOutput;
        private ProgressItem _progressItem;
        private int _numberCopyThreads;
        private int _refreshMilliseconds; // When to refresh the speed measurement
        
        public OutputHandler(int numberCopyThreads, Action <LoggingConsoleActions.Actions, string, bool, int, int> consoleOutput, Action<LoggingConsoleActions.Actions, string, Exception> logOutput)
        {
            _logOutput = logOutput;
            _consoleOutput = consoleOutput;
            _numberCopyThreads = numberCopyThreads;
            _progressItem = new ProgressItem() 
            { 
                TotalFilesToBackup = 0, 
                Speed = 0, 
                TotalBytesToBackup = 0, 
                BytesBackedUp = 0, 
                FilesBackedUp = 0, 
                StartTimeOfJob = DateTime.Now,
                BytesTransferredPerThread = new List<ThreadItem>(),
                SpeedSw = new System.Diagnostics.Stopwatch(),
                JobSw = new System.Diagnostics.Stopwatch(),

            };
            _refreshMilliseconds = 5000;
            for (var i = 0; i < numberCopyThreads; i++)
            {
                _progressItem.BytesTransferredPerThread.Add(new ThreadItem());
            }
        }

        public ProgressItem GetProgressItem()
        {
            return _progressItem;
        }

        public void StartSpeedStopwatch()
        {
            _progressItem.SpeedSw.Start();
        }

        public void StartJobStopwatch()
        {
            _progressItem.JobSw.Start();
                
        }

        public void WriteToLogAndConsole(LoggingConsoleActions.Actions action, string text, Exception exc = null, bool lineFeed = true, int ThreadId = -1, int progress = -1)
        {
            WriteToConsole(action, text, lineFeed, ThreadId, progress);
            WriteToLog(action, text, exc);
        }


        public void WriteToConsole(LoggingConsoleActions.Actions action, string text, bool lineFeed = true, int threadId = -1, int progress = -1)
        {
            _consoleOutput(action, text, lineFeed, threadId, progress);
        }

        public void UpdateProgress(Blob blob, int threadId, long bytesTransferred, int progress)
        {

            long totalProgressInBytes = _progressItem.BytesBackedUp;

            Object lockObj1 = new object();
            lock (lockObj1)
            {
                _progressItem.BytesTransferredPerThread[threadId].SpeedMeasuringCurrentBytes = bytesTransferred;
                if (_progressItem.SpeedSw.Elapsed.TotalMilliseconds > _refreshMilliseconds)
                {
                    long totalTraffic = 0;
                    for (var i = 0; i < _progressItem.BytesTransferredPerThread.Count; i++ )
                    {
                        totalTraffic += _progressItem.BytesTransferredPerThread[i].SpeedMeasuringCurrentBytes - _progressItem.BytesTransferredPerThread[i].SpeedMeasuringStartBytes;
                        _progressItem.BytesTransferredPerThread[i].SpeedMeasuringStartBytes = _progressItem.BytesTransferredPerThread[i].SpeedMeasuringCurrentBytes; // initial bytes transferred
                    }
                    _progressItem.Speed = (totalTraffic / _progressItem.SpeedSw.Elapsed.TotalMilliseconds) / 1000;
                    _progressItem.SpeedSw.Restart();

                }


                if (progress == 101)
                {
                    // 101 only to show that file is complete and can be added to the total progress bar
                    progress = 100;
                    _progressItem.FilesBackedUp++;
                    _progressItem.BytesBackedUp += (long)blob.Size;
                    for (var i = 0; i < _progressItem.BytesTransferredPerThread.Count; i++)
                    {
                        if(i == threadId)
                        {
                            totalProgressInBytes += (long)blob.Size;
                            _progressItem.BytesTransferredPerThread[i].SpeedMeasuringCurrentBytes = 0;
                        }
                        else
                        {
                            totalProgressInBytes += _progressItem.BytesTransferredPerThread[i].SpeedMeasuringCurrentBytes;
                        }
                        
                    }

                }
                else
                {
                    totalProgressInBytes = _progressItem.BytesBackedUp;
                    for (var i = 0; i < _progressItem.BytesTransferredPerThread.Count; i++)
                    {
                        totalProgressInBytes += _progressItem.BytesTransferredPerThread[i].SpeedMeasuringCurrentBytes;
                    }
                }
            }
            var text = blob.FullPathFilename + " " + GetBytesReadable(bytesTransferred) + " of " + GetBytesReadable((long)blob.Size);
            WriteToConsole(LoggingConsoleActions.Actions.ProgressUpdate, text, true, threadId, progress);

            var text2 = "TOTAL: " + _progressItem.FilesBackedUp + " of " + _progressItem.TotalFilesToBackup + " files (" + GetBytesReadable(totalProgressInBytes) + " of " + GetBytesReadable(_progressItem.TotalBytesToBackup) + ") at " + _progressItem.Speed.ToString("0.0") + " MB/s";
            WriteToConsole(LoggingConsoleActions.Actions.ProgressUpdate, text2, true, -1, (int)((totalProgressInBytes / (double)_progressItem.TotalBytesToBackup) * 100));
        }

        public void UpdateProgress(string text, int ThreadId, int progress)
        {
            WriteToConsole(LoggingConsoleActions.Actions.ProgressUpdate, text, true, ThreadId, progress);
        }

        public void WriteToLog(LoggingConsoleActions.Actions action, string text, Exception e = null)
        {
            _logOutput(action, text, e);
        }

        public string GetBytesReadable(long i)
        {
            // Get absolute value
            long absolute_i = (i < 0 ? -i : i);
            // Determine the suffix and readable value
            string suffix;
            double readable;
            if (absolute_i >= 0x1000000000000000) // Exabyte
            {
                suffix = "EB";
                readable = (i >> 50);
            }
            else if (absolute_i >= 0x4000000000000) // Petabyte
            {
                suffix = "PB";
                readable = (i >> 40);
            }
            else if (absolute_i >= 0x10000000000) // Terabyte
            {
                suffix = "TB";
                readable = (i >> 30);
            }
            else if (absolute_i >= 0x40000000) // Gigabyte
            {
                suffix = "GB";
                readable = (i >> 20);
            }
            else if (absolute_i >= 0x100000) // Megabyte
            {
                suffix = "MB";
                readable = (i >> 10);
            }
            else if (absolute_i >= 0x400) // Kilobyte
            {
                suffix = "KB";
                readable = i;
            }
            else
            {
                return i.ToString("0 B"); // Byte
            }
            // Divide by 1024 to get fractional value
            readable = (readable / 1024);
            // Return formatted number with suffix
            string readFormat = "0.#";
            if(readable > 10 && (suffix == "MB" || suffix == "KB"))
            {
                readFormat = "0";
            }
            return readable.ToString(readFormat) + suffix;
        }
    }
}
