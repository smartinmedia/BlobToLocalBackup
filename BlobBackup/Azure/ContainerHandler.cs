using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using BlobBackupLib.Database.Model;
using BlobBackupLib.Jobs.Model;
using BlobBackupLib.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace BlobBackupLib.AzureObjects
{
    public class ContainerHandler
    {

        private string _containerName;
        private BlobServiceClient _blobServiceClient;
        private OutputHandler _oH;
        private Job _job;


        public ContainerHandler(BlobServiceClient blobServiceClient, string containerName, OutputHandler oH, Job job)
        {
            _containerName = containerName;
            _blobServiceClient = blobServiceClient;
            _oH = oH;
            _job = job;
        }

        public BlobContainerClient GetBlobContainerClient()
        {
            return _blobServiceClient.GetBlobContainerClient(_containerName);
        }

        public async Task DownloadBlob(Blob blob, string targetPathFilename, int threadNumber, int downloadSpeedInBytes)
        {
            var bH = new BlobHandler(GetBlobContainerClient());

            await bH.DownloadBlob(blob, targetPathFilename, threadNumber, downloadSpeedInBytes, _oH);
        }

        public Task GetBlobs(Action<Blob> storeBlobInDb, int accountId, int containerId, List<Blob> blobs)
        {
            if(blobs.Count == 0)
            {
                return GetAllBlobsFromContainer(storeBlobInDb, accountId, containerId);
            }
            else
            {
                GetSpecificBlobs(storeBlobInDb, accountId, containerId, blobs);
                return Task.CompletedTask;
            }
        }

        private async Task GetAllBlobsFromContainer(Action<Blob> storeBlobInDb, int accountId, int containerId)
        {
            string blobContinuationToken = null;
            //list blobs in the container

            var blobList = new List<Blob>();

            do
            {
               
                var blobResultSegment = _blobServiceClient.GetBlobContainerClient(_containerName).GetBlobsAsync().AsPages(blobContinuationToken, pageSizeHint: 5000);
                await foreach (Page<BlobItem> blobPage in blobResultSegment)
                {
                    
                    foreach (var blob in blobPage.Values)
                    {
                       
                        storeBlobInDb((new Blob() { Filename = blob.Name,  
                            Container = containerId, CreateTime = (DateTimeOffset)blob.Properties.CreatedOn,  
                            IsBackedUp = false, ModifyTime = (DateTimeOffset)blob.Properties.LastModified, Size = blob.Properties.ContentLength, 
                            StorageAccount = accountId, AccessTier = blob.Properties.AccessTier.ToString(),
                        FullPathFilename = _blobServiceClient.AccountName + "/" + _containerName + "/" + blob.Name,
                        TargetFilename = _blobServiceClient.AccountName + "/" + _containerName + "/" + blob.Name
                        }));
                        //OutputHandler.WriteToConsole(blob.Name);
                       
                    }

                }
            } while (blobContinuationToken != null);

        }


        private void GetSpecificBlobs(Action<Blob> storeBlobInDb, int accountId, int containerId, List<Blob> blobs)
        {
            var bH = new BlobHandler(GetBlobContainerClient());


            Parallel.ForEach(blobs, new ParallelOptions { MaxDegreeOfParallelism = _job.NumberCopyThreads }, blob =>
            {
                try
                {
                    var properties = bH.GetBlobProperties(blob);

                    Object lockObj = new Object();

                    lock (lockObj)
                    {
                        storeBlobInDb(new Blob()
                        {
                            Filename = blob.Filename,
                            Container = containerId,
                            CreateTime = (DateTimeOffset)properties.CreatedOn,
                            IsBackedUp = false,
                            ModifyTime = (DateTimeOffset)properties.LastModified,
                            Size = properties.ContentLength,
                            StorageAccount = accountId,
                            AccessTier = properties.AccessTier.ToString(),
                            FullPathFilename = _blobServiceClient.AccountName + "/" + _containerName + "/" + blob.Filename,
                            TargetFilename = blob.TargetFilename
                        });
                    }
                    
                }
                catch (Exception e)
                {

                    _oH.WriteToLogAndConsole(LoggingConsoleActions.Actions.Error, "The properties of the specific blob " + blob.Filename + " could not be retrieved from Azure!", e);
                    return;
                }
            });
        }
    }
}
