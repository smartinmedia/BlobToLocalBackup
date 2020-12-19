using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using BlobBackupLib.Database;
using BlobBackupLib.Database.Model;
using BlobBackupLib.Jobs.Model;
using BlobBackupLib.Logging;

namespace BlobBackupLib.Destination
{
    public class FileHandler
    {
        private DbHandler _db;
        private string _baseDir;
        private ReportItem _rI;
        private Job _job;

        public FileHandler(string folder, Job job)
        {
            //_db = db;
            _baseDir = folder.Replace("\\", "/");
            _rI = new ReportItem() { TotalSize = 0, NumberOfFiles = 0, NumberOfFilesOfVersioned = 0, TotalSizeOfVersioned = 0 };
            _job = job;
        }

        public ReportItem RescanFolder(Action<LocalFile> addFile = null)
        {
            return DirSearch(_baseDir, addFile);
            
        }

        private bool CheckToDeleteExpiredVersion(LocalFile file)
        {
            if (_job.DaysToKeepVersion > 0)
            {
                var now = System.DateTime.UtcNow;
                var difference = now.Subtract(file.CreateTime.UtcDateTime);
                if(difference.TotalDays > _job.DaysToKeepVersion)
                {
                    File.Delete(Path.Combine(_baseDir, file.FilenameWithRelativePath));
                    return true;
                }
            }
            return false;
        }

        private void RemoveBaseDirFromPath(List<String> files)
        {
            var files2 = new List<string>();
            for(var i=0; i < files.Count; i++)
            {
                string temp = files[i].Replace("\\", "/").Replace(_baseDir, "").Trim('/');
                files2.Add(temp);
                //OutputHandler.WriteToConsole("File: " + temp);

            }
        }



        private string RemoveBaseDirFromSingleFile(string file)
        {
            return file.Replace("\\", "/").Replace(_baseDir, "").Trim('/');
        }


        private ReportItem DirSearch(string sDir, Action<LocalFile> addFile)

        {
            foreach (string f in Directory.GetFiles(sDir))
            {
                string file = f.Replace("\\", "/");
                var info = new FileInfo(file);

                var fileObj = new LocalFile { FilenameWithRelativePath = RemoveBaseDirFromSingleFile(file), Size = info.Length, CreateTime = info.CreationTimeUtc };
                if (!fileObj.FilenameWithRelativePath.Contains("___VersionedBlobBackups"))
                {
                    if(addFile != null)
                    {
                        addFile(fileObj);
                    }
                        
                    _rI.NumberOfFiles++;
                    _rI.TotalSize += fileObj.Size;
                }
                else
                {
                    if (!CheckToDeleteExpiredVersion(fileObj))
                            
                    {
                        //If we did not delete the versioned file, then we add it to the stats
                        _rI.NumberOfFilesOfVersioned++;
                        _rI.TotalSizeOfVersioned += fileObj.Size;
                    }
                }


                }
                foreach (string d in Directory.GetDirectories(sDir))
                {
                    DirSearch(d, addFile);
                }
            return _rI;
        }
    }
}
