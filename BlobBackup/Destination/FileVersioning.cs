using BlobBackupLib.Jobs.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BlobBackupLib.Destination
{
    public static class FileVersioning
    {
        /// <summary>
        /// Make a numbered backup copy of the specified files.  Backup files have the name filename.exe.yymmdd##, where yymmdd is the date and ## is a zero justified sequence number starting at 1
        /// </summary>
        /// <param name="fileName">Name of the file to backup.</param>
        /// <param name="maxBackups">The maximum backups to keep.</param>
        /// 

       
        public static string MakeBackupAndMove(string fileName, Job job)
        {

            // Make sure that the file exists, you don't backup a new file
            if (File.Exists(fileName) && job.KeepNumberVersions > 0)
            {
                // First backup copy of the day starts at 1
                int newSequence = 1;

                string versionFolder = Path.GetDirectoryName(FolderManipulation.AddVersionFolderIntoTarget(fileName, job.DestinationFolder));

                if (Directory.Exists(versionFolder))
                {
                    // Get the list of previous backups of the file, skipping the current file
                    var backupFiles = Directory.GetFiles(versionFolder, Path.GetFileName(fileName) + ".*")
                        .ToList()
                        .Where(d => !d.Equals(fileName))
                        .OrderBy(d => d);

                    // Get the name of the last backup performed
                    var lastBackupFilename = backupFiles.LastOrDefault();

                    // If we have at least one previous backup copy
                    if (lastBackupFilename != null)
                    {
                        // Get the last sequence number back taking the last 2 characters and convert them to an int. And add 1 to that number
                        if (Int32.TryParse(Path.GetExtension(lastBackupFilename).GetLast(2), out newSequence))
                            newSequence++;

                        // If we have more backups than we need to keep
                        if (backupFiles.Count() >= job.KeepNumberVersions)
                        {
                            // Get a list of the oldest files to delele
                            var expiredFiles = backupFiles.Take(backupFiles.Count() - job.KeepNumberVersions + 1);

                            foreach (var expiredFile in expiredFiles)
                            {
                                File.Delete(expiredFile);
                            }
                        }
                    }

                }

                // Create the file name for the newest back up file.
                var latestBackup = String.Format("{0}._v{1:yyMMdd}{2:00}", FolderManipulation.AddVersionFolderIntoTarget(fileName, job.DestinationFolder), DateTime.Now, newSequence);

                if (!Directory.Exists(Path.GetDirectoryName(latestBackup)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(latestBackup));
                }
                // Copy the current file to the new backup name and overwrite any existing copy
                File.Move(fileName, latestBackup, true);
                return latestBackup;
            }
            else
            {
                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }
            }
            return "";
        }
    }
    // String Extension that was used in the code but left out when I first published
    public static class StringExtension
    {
        public static string GetLast(this string source, int tail_length)
        {
            if (tail_length >= source.Length)
                return source;
            return source.Substring(source.Length - tail_length);
        }
    }

    public static class FolderManipulation
    {
        public static string AddVersionFolderIntoTarget(string filename, string destFolder)
        {
            filename = filename.Replace("\\", "/");
            destFolder = destFolder.Replace("\\", "/");
            filename = filename.Replace(destFolder, "").TrimStart('/');
            var res = Path.Combine(destFolder, "___VersionedBlobBackups", filename);
            return res;
        }
    }
}
