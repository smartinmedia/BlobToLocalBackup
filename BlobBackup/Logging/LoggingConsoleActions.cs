using System;
using System.Collections.Generic;
using System.Text;

namespace BlobBackupLib.Logging
{
    public static class LoggingConsoleActions
    {
        public enum Actions
        {
            Information,
            Error,
            ProgressUpdate,
            Important
        }
    }
}
