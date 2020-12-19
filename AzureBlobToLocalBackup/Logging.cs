using BlobBackupLib.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;

namespace AzureBlobToLocalBackupConsole
{
    public class Logging
    {
        public Logging(string filePath)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(filePath + "/AzureBackupLog_.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

        }

        public void LogMessage(LoggingConsoleActions.Actions action, string message, Exception e = null)
        {
            if (action == LoggingConsoleActions.Actions.Information)
            {
                Log.Information(message);
            }
            else
            {
                if(e != null)
                {
                    Log.Error(e, message);

                }
                else
                {
                    Log.Error(message);
                }
            }
        }

    }
}
