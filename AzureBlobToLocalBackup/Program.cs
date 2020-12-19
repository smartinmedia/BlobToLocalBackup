using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using AzureBlobToLocalBackupConsole;
using BlobBackupLib;
using BlobBackupLib.Jobs;
using BlobBackupLib.Logging;
using Serilog;

namespace AzureBlobToLocalBackup
{
    class Program
    {

        /*
         * Command line option: --job \test\test.json --restart 
         */
        static void Main(string[] args)
        {
            string jobFile = "";
            var cP = new ConsoleParameters();

            var paramList = new List<string>() { "scanonly", "continuelastjob" };
            
            for (int i = 0; i < args.Length; i++)
            {
                var arg = Regex.Replace(args[i], "^--", "");

                if (arg.Contains(".json"))
                {
                    jobFile = arg;
                }
                else if(arg == "scanonly")
                {
                    cP.ScanOnly = true;
                }
                else if(arg == "continuelastjob")
                {
                    cP.ContinueLastJob = true;
                }
            }


            if (!File.Exists(jobFile))
            {
                Console.WriteLine("\r\n\r\nError: Please execute this program with a valid job file as a parameter, e.g. 'job1.json'");
                return;
            }
            else
            {
                string jobText = File.ReadAllText(jobFile);
                if (!JsonHandler.IsValidJson(jobText))
                {
                    Console.WriteLine("\r\n\r\nError: The job json file does not seem to be a correct json file or contains errors.");
                    return;
                }

            }

            if (!File.Exists("appsettings.json"))
            {
                Console.WriteLine("\r\n\r\nError: Please execute this program with a valid 'appsettings.json' in the program folder");
                return;
            }
            else
            {
                string appText = File.ReadAllText("appsettings.json");
                if (!JsonHandler.IsValidJson(appText))
                {
                    Console.WriteLine("\r\n\r\nError: The job apssetings.json file does not seem to be a correct json file or contains errors.");
                    return;
                }
            }

            var aH = new AppSettingsHandler("appsettings.json");
            var aS = aH.GetAppSettings();

            
            var log = new Logging(aS.General.PathToLogFiles);
            
            var jH = new JobHandler(jobFile, "appsettings.json");
            var jS = jH.GetJobSettings();

            if(aS.ConsoleWidth < 40)
            {
                aS.ConsoleWidth = 80;
            }
            else if (aS.ConsoleWidth > Console.LargestWindowWidth)
            {
                aS.ConsoleWidth = Console.LargestWindowWidth;
            }
            
            if (aS.ConsoleHeight < 30)
            {
                aS.ConsoleHeight = 30;
            }
            else if (aS.ConsoleHeight > Console.LargestWindowHeight)
            {
                aS.ConsoleHeight = Console.LargestWindowHeight;
            }
            Console.Clear();
            Console.CursorVisible = false;
            Console.SetWindowSize(aS.ConsoleWidth, aS.ConsoleHeight);

            var cO = new ConsoleOutput(jS.NumberCopyThreads, jS, aS);

            var oH = new OutputHandler(jS.NumberCopyThreads, cO.OutputToConsole, log.LogMessage);



            try
            {

                jH.Start(cP, oH).Wait();
            }
            catch(Exception e)
            {

                oH.WriteToLog(LoggingConsoleActions.Actions.Error, "Something went wrong", e);
                oH.WriteToConsole(LoggingConsoleActions.Actions.Error, "Something went wrong: " + e.Message, true, -1, -1);
            }


            oH.WriteToConsole(LoggingConsoleActions.Actions.Information, "Job done");

            Console.SetCursorPosition(10, aS.ConsoleHeight - 2);
            Console.WriteLine("(c) Smart In Media 2020 rocks! ");
            Log.CloseAndFlush();
        }

    }
}
