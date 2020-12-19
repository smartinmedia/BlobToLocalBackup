using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using BlobBackupLib;
using BlobBackupLib.Azure.Model;
using BlobBackupLib.AzureObjects;
using BlobBackupLib.Database.Model;
using BlobBackupLib.Jobs.Model;
using BlobBackupLib.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace AzureBlobToLocalBackupConsole.AzureObjects
{
    public class StorageAccountHandler
    {
        private readonly OutputHandler _oH;

        public StorageAccountHandler(OutputHandler oH)
        {
            _oH = oH;
        }

        private BlobServiceClient _blobServiceClient;

        public BlobServiceClient Authenticate(StorageAccount account,  LoginCredentialsConfiguration loginCreds = null, int retries = 3)
        {
            string url = string.Format("https://{0}.blob.core.windows.net/", account.Name);
            BlobServiceClient blobServiceClient;
            
            if (loginCreds.GetType().GetProperties().All(p => p.GetValue(loginCreds) != null && !string.IsNullOrWhiteSpace((string)p.GetValue(loginCreds))))
            {
                var options = new BlobClientOptions();
                //options.Diagnostics.IsLoggingEnabled = false;
                //options.Diagnostics.IsTelemetryEnabled = false;
                //options.Diagnostics.IsDistributedTracingEnabled = false;
                options.Retry.MaxRetries = retries;

                //Authenticate with principal account, which is not saved in environment vars locally
                blobServiceClient = new BlobServiceClient(new Uri(url), new ClientSecretCredential(loginCreds.TenantId, loginCreds.ClientId, loginCreds.ClientSecret), options);
                _blobServiceClient = blobServiceClient;

            }

            else if(System.Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") != null && !string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable("AZURE_CLIENT_ID")))
            {
                blobServiceClient = new BlobServiceClient(new Uri(url), new DefaultAzureCredential());
                _blobServiceClient = blobServiceClient;

            }
            else if(!string.IsNullOrEmpty( account.SasConnectionString ))
            {
                blobServiceClient = new BlobServiceClient(account.SasConnectionString);
                _blobServiceClient = blobServiceClient;

            }
            else
            {
                _oH.WriteToConsole(LoggingConsoleActions.Actions.Error, "Error: no authentication method provided for Storage account: " + account.Name + ". Either have LoginCredentials in appsettings.json OR have them as environment variables OR provide a SAS Connection String in your Storage Accounts in the job's json-files");
                _oH.WriteToLog(LoggingConsoleActions.Actions.Error, "Error: no authentication method provided for Storage account: " + account.Name + ". Either have LoginCredentials in appsettings.json OR have them as environment variables OR provide a SAS Connection String in your Storage Accounts in the job's json-files");
            }
            return _blobServiceClient;

        }

        public async Task<List<Container>> GetAllContainers()
        {
            // As seen on: https://stackoverflow.com/questions/62808331/net-core-console-app-read-list-of-azure-storage-blobs-as-read-only-user


            var containers = new List<Container>();
            string containerContinuationToken = null;
            int pageSizeHint = 5; //The size of Page<T>s that should be requested 
            // list containers in the storage account
            do
            {
                var containerResultSegment = _blobServiceClient.GetBlobContainersAsync().AsPages(containerContinuationToken, pageSizeHint);
                await foreach (Page<BlobContainerItem> containerPage in containerResultSegment)
                {

                    foreach (var container in containerPage.Values)
                    {
                        containers.Add(new Container() { Name = container.Name });
                        
                        

                    }
                }
            } while (containerContinuationToken != null);
            return containers;
        }

        public void ScanSourceStorageAccount(StorageAccount account, LoginCredentialsConfiguration logCred, List<StorageAccount> _storageAccounts,
            ConsoleParameters _cP, Job _job, string _filenameContains, string _filenameWithout, Action<Blob> StoreBlobInList)
        {
            
            var blobServiceClient = Authenticate(account, logCred, _job.NumberOfRetries);
            var containers = GetAllContainers().GetAwaiter().GetResult();

            int saId = _storageAccounts.FindIndex(c => c.Name == account.Name);

            if (_job.StorageAccounts[saId].Containers.Count == 0)
            {
                foreach (var container in containers)
                {
                    _storageAccounts[saId].Containers.Add(container);
                }
            }

            if (_cP.ContinueLastJob)
            {
                return;
            }

            Parallel.ForEach(_job.StorageAccounts[saId].Containers, new ParallelOptions { MaxDegreeOfParallelism = _job.NumberCopyThreads }, container =>
            {
                if (_job.StorageAccounts[saId].Containers.FindIndex(c => c.Name == container.Name) == -1)
                {
                    //Only take containers, that are within the job - if no container is listed
                    //(empty array) - take all
                    return;
                }
                int cId = _storageAccounts[saId].Containers.FindIndex(c => c.Name == container.Name);
                if (!string.IsNullOrWhiteSpace(container.FilenameContains) || !string.IsNullOrWhiteSpace(container.FilenameWithout))
                {
                    _filenameContains = container.FilenameContains;
                    _filenameWithout = container.FilenameWithout;
                }


                _oH.WriteToConsole(LoggingConsoleActions.Actions.Information, "Container: " + container.Name + " in account: " + account.Name + "...", false);
                _oH.WriteToLog(LoggingConsoleActions.Actions.Information, "Start scanning container: " + container.Name + " in account: " + account.Name);

                var ch = new ContainerHandler(blobServiceClient, container.Name, _oH, _job);
                var blobContainerClient = ch.GetBlobContainerClient();
                ch.GetBlobs(StoreBlobInList, saId, cId, container.Blobs).Wait();


                _oH.WriteToConsole(LoggingConsoleActions.Actions.Information, "DONE: " + _storageAccounts[saId].Containers[cId].FileInfo.NumberOfFiles
                    + " files ('Hot': " + _storageAccounts[saId].Containers[cId].FileInfo.NumberOfHotFiles + ") with "
                    + _oH.GetBytesReadable(_storageAccounts[saId].Containers[cId].FileInfo.TotalSize)
                    + " ('Hot': " + _oH.GetBytesReadable(_storageAccounts[saId].Containers[cId].FileInfo.SizeOfHotFiles) + ")", true);

                _oH.WriteToLog(LoggingConsoleActions.Actions.Information, "Finished scanning container: " + container.Name + " in account: " + account.Name
                    + ", with: " + _storageAccounts[saId].Containers[cId].FileInfo.NumberOfFiles
                    + " files ('Hot': " + _storageAccounts[saId].Containers[cId].FileInfo.NumberOfHotFiles + ") with "
                    + _oH.GetBytesReadable(_storageAccounts[saId].Containers[cId].FileInfo.TotalSize)
                    + " ('Hot': " + _oH.GetBytesReadable(_storageAccounts[saId].Containers[cId].FileInfo.SizeOfHotFiles) + ")");
            });
        }

    }
}
