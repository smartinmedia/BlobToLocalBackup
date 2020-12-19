using BlobBackupLib.Azure.Model;
using BlobBackupLib.Jobs.Model;
using BlobBackupLib.Logging;
using Konsole;
using Konsole.Platform;
using Microsoft.Azure.Management.Network.Fluent;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace AzureBlobToLocalBackupConsole
{
    public class ConsoleOutput
    {
        private IConsole _topWindow;
        private IConsole _lowerWindow;
        private readonly List<ProgressBar> _pB;
        private readonly Job _job;
        private int _numberOfThreads;
        private AppSettings _aS;
        private HighSpeedWriter _writer;


        public ConsoleOutput(int num, Job job, AppSettings aS)
        {
            _pB = new List<ProgressBar>();
            _job = job;
            _aS = aS;
            _writer = new HighSpeedWriter();
            CreateConsoleElements(num);
            _numberOfThreads = num;
        }

        private void CreateConsoleElements(int num)
        {
            var window = new Window(_writer);
            window.CursorVisible = false;
            _topWindow = window.OpenBox("Azure Blob Backup - Info Console", 5, 1, _aS.ConsoleWidth - 10, _aS.ConsoleHeight - (num + 5), new BoxStyle() { ThickNess = LineThickNess.Double, Body = new Colors(ConsoleColor.White, ConsoleColor.Blue) });
            _lowerWindow = window.OpenBox("Current file(s) to backup", 5, _aS.ConsoleHeight - (num + 4), _aS.ConsoleWidth - 10, num + 3, new BoxStyle() { ThickNess = LineThickNess.Single });
            _topWindow.WriteLine("");
            _topWindow.WriteLine("      **** COMMODORE 64 BASIC V2 ****");
            _topWindow.WriteLine(" 64K RAM SYSTEM 38911 BASIC BYTES FREE");
            _topWindow.WriteLine("READY.");
            _topWindow.WriteLine("");
            _topWindow.WriteLine("Azure Blob to local Backup was programmed by (c) Smart In Media 2020");
            _topWindow.WriteLine("by Martin Weihrauch :)");

            for (var i = 0; i < num + 1; i++)

            {
                _pB.Add(new ProgressBar(_lowerWindow, 100, _aS.ConsoleWidth - (int)(0.3 * (double)_aS.ConsoleWidth)));
            }
            _writer.Flush();
        }

        public void Flush()
        {
            _writer.Flush();
        }

        public void OutputToConsole(LoggingConsoleActions.Actions action, string text, bool lineFeed = true,  int id = -1, int progress = -1)
        {
            if(action == LoggingConsoleActions.Actions.Important)
            {
                if (lineFeed)
                {
                    _topWindow.WriteLine(ConsoleColor.Yellow, text);
                }
                else
                {
                    _topWindow.Write(ConsoleColor.Yellow, text);
                }
            }
            else if(action == LoggingConsoleActions.Actions.Error)
            {
                if (lineFeed)
                {
                    _topWindow.WriteLine(ConsoleColor.Magenta, text);
                }
                else
                {
                    _topWindow.Write(ConsoleColor.Magenta, text);

                }
            }


            else if(action == LoggingConsoleActions.Actions.Information)
            {
                if (lineFeed)
                {
                    _topWindow.WriteLine(text);
                }
                else
                {
                    _topWindow.Write(text);
                }
            }
            else
            {
                if(id == -1)
                {
                    id = _numberOfThreads;
                }
                //Progress Update and text is filename
                text = text.Replace("\\", "/");
                string targetFolder = _job.DestinationFolder.Replace("\\", "/");
                text = text.Replace(targetFolder, "");
                _pB[id].Refresh(progress, text);

            }
            Flush();

        }
    }
}
