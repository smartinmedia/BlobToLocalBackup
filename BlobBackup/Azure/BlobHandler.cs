using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using BlobBackupLib.Azure;
using BlobBackupLib.Database;
using BlobBackupLib.Database.Model;
using BlobBackupLib.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BlobBackupLib.AzureObjects
{
    public class BlobHandler
    {
        // private BlobServiceClient _blobServiceClient;
        private BlobContainerClient _blobContainerClient;

        public BlobHandler(BlobContainerClient blobContainerClient)
        {
            //_blobServiceClient = blobServiceClient;
            _blobContainerClient = blobContainerClient;
        }

        public async Task DownloadBlob(Blob blob, string targetPathFilename, int threadNumber, int downloadSpeedInBytes, OutputHandler oH = null)
        {
            
            var blobClient = _blobContainerClient.GetBlobClient(blob.Filename);
            if (blobClient.GetProperties().Value.AccessTier == "Hot")
            {
                BlobDownloadInfo download = await blobClient.DownloadAsync();

                if (!Directory.Exists(Path.GetDirectoryName(targetPathFilename)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPathFilename));
                }
                FileStream downloadFileStream = File.OpenWrite(targetPathFilename);
                var throttle = new ThrottledStream(downloadFileStream, downloadSpeedInBytes);

                var fileToDownload = download.Content.CopyToAsync(throttle);
                    
                int lastProgress = -1;
                //oH.WriteToConsole(LoggingConsoleActions.Actions.Information, blob.FullPathFilename + " - Download started");
                //oH.WriteToLog(LoggingConsoleActions.Actions.Information, blob.Filename + " - Download started");


                while (!fileToDownload.IsCompleted)
                {
                    int progress = (int)((double)downloadFileStream.Position / (double)download.ContentLength * 100);
                    if (progress != lastProgress && blob.Size > 1000000)
                    {
                        //Only update progress, if the file is larger than 1 MB, else too many updates and slowing down the process
                        if (oH != null)
                        {
                                    
                            oH.UpdateProgress(blob, threadNumber, downloadFileStream.Position, progress);
                        }

                        lastProgress = progress;
                    }

                }
                if (oH != null)
                {
                    oH.UpdateProgress(blob, threadNumber, (long)blob.Size, 101);
                    //oH.WriteToConsole(LoggingConsoleActions.Actions.Information, targetPathFilename + " - Download finished");
                    //oH.WriteToLog(LoggingConsoleActions.Actions.Information, targetPathFilename + " - Download finished");

                }


                downloadFileStream.Close();
                
                
            }
        }

        public BlobProperties GetBlobProperties(Blob blob)
        {
            var blobClient = _blobContainerClient.GetBlobClient(blob.Filename);
            return blobClient.GetProperties();
        }
    }
}

