# Azure Blob to Local Backup

Azure Blob to Local Backup is a MIT-licensed .NET Core library with a console app, which can backup millions of Azure Blobs to any local storage (e. g. local hard drives, USB sticks, network drives). (c) Smart In Media 2020

**Direct download to the console application**
(run with blobtolocal job...json): https://spreader.blob.core.windows.net/blobtolocal/BlobToLocalConsole.zip

## Why?
We have an Azure account with millions of blobs of our customers with 50 - 100 TB. We were confronted with the challenge of backing up files, because if we would lose our customer data, our company would be dead. Of course, Azure keeps redundant backups of each blobs and why not trust Microsoft? However, what happens, when a hacker or a drunk/insane employee willingly or unwillingly deletes all data with an admin account? Solutions like "soft delete" or any backup apps on Azure do not seem to be an alternative, because with full admin access, you could also delete those.
Thus, I started investigating tools like azcopy, different backup programs, but nothing fulfilled all of the items on my wish list. Azcopy does not support versioning, but what happens, if a hacker encrypts data, we synchronize them to our backup (and overwrite the target files)? In many backup programs, which support versioning (like the excellent syncback), you could only set backups per container and not per storage account. But, what do you do, if you have approximately 100-200 containers like we do? Create 100-200 copy jobs? Insane! 
Well, welcome to Blob to Local Backup - although I cannot guarantee that it will work reliably, at least it is a (pretty good) start for our purposes. If you like to contribute code and create pull requests, you are very welcome! :-)

## Features

 - Backup complete storage accounts or complete containers or specifiy fully granular down to individual blob level
 - Multiple download threads
 - Versioning of backup files
 - Authentication on entire Azure subscription or "granular" per storage account
 - Easy JSON structure for job setup
 - Backing up of millions of files possible by using serialization
   technology instead of database
 - Bandwidth throttling (setting bandwidth limit)
 - Backup specific list of files by CSV list
 - Including/excluding files by search strings / wildcards
 - Cool GUI in the console to see the progress

## Warning
We do not provide any liability / responsibility for any damages, loss of data, etc. (s. MIT license)! 
Use READ-ONLY access for authentication to Azure with this application!

# Setting up everything in your Azure account

To use the Blob To Local Backup (BTLB), you need to grant the application access to your Azure subscription or to individual storage accounts.
MAKE SURE that you ALWAYS ONLY configure for READ ONLY access to protect your storages from any identity / password theft and potentially compromising your account!
What you need to know about Azure:
There are Azure subscriptions, which have "Storage Accounts", which have "Containers", which contain "Blobs". Blobs can have "/" in their filenames to "mimic" folders. The slashes are translated to subfolders in your target backup with BTLB.

You have currently 2 options for authentication with BTLB on Azure:
1. Access the entire subscription with an app registration (has to be entered into the appsettings.json of the BTLB app).
2. Create "Shared access signatures" (SAS) for an individual storage account (has to be entered into the job JSON file per storage account).

### How to create an app registration to access the entire subscription (option #1)
If you use an app registration within Azure AD auth to access the Azure Blob as a "Storage Blob Data Reader", you have 2 options: 
a) store the credentials with environment variables in the backup computer or 
b) write them within the appsettings.json of BTLB.

These are the steps on Azure:

	- Open "Azure Active Directory"
	- Open "App registrations"
	- Add a new registration, e. g. "BackupApplication"
	- Select single tenant or multi tenant
	- Create this. 
	- Go to "Certificates & secrets" in that new app registration
	- Create a new "Client secret"
	- Now open your subscription, from which you want to backup
	- There, select "Access control (IAM)"
	- Go to the tab "role assignments"
	- Now add --> "Add role assignment" --> Select "Storage Blob Data Reader" as role, then search your new user by name (the app registration) and add it.
	- That's it - your app registration is "READ ONLY", which is important to not compromise your security.

If you now want to store the credentials on your backup machine (and not in the appsettings.json of BTLB), install ["Azure CLI"](https://docs.microsoft.com/de-de/cli/azure/install-azure-cli) locally and run these commands in the CMD terminal:

    az login

(assign role at the description level the sp can manage all storage account in the subscription)

    az ad sp create-for-rbac -n "ManageStorage" --role "Storage Blob Data Reader"

To store in environment variables:

    setx AZURE_CLIENT_ID <your-clientID>
    
    setx AZURE_CLIENT_SECRET <your-clientSecret>
    
    setx AZURE_TENANT_ID <your-tenantId>

b) Write them down and put them in appsettings.json

### How to create a shared access signature (option #2)
In the Azure portal, go to your storage account and select "shared access signature" (SAS). When you create a new access signature signature, make sure you deactivate all rights except for listing and reading. Also think about limiting that SAS to only a specific server IP address and to a limited time span to heighten security and to limit harm. 
Then add this SAS to the job JSON file (in storage account, put the entire connection string into the field "SasConnectionString":"BlobEndpoint=https:...blob.core.windows.net/;QueueEndpoint=....".

# Setting up the application
You have to prepare 2 things:
1. The appsettings.json, which is inside the application folder
2. A job file (also in JSON format). 

Let's start with appsettings.json.
Remember, the **LoginCredentials are optional here** - use them here, if you want to have full READ access to all storage accounts, which may make things easier. **You can leave them away, if you provide access on a storage account level with shared access signatures in the job JSON**!

    {
     "App": {

	    "ConsoleWidth": 150,
	    "ConsoleHeight":  42,

	    "LoginCredentials": {
	        "ClientId": "2ab11a63-2e93-2ea3-abba-aa33714a36aa",
	        "ClientSecret": "ABCe3dabb7247aDUALIPAa-anc.aacx.4",
		"TenantId": "d666aacc-1234-1234-aaaa-1234abcdef38"
	    },
	    "DataBase": {
	      "PathToDatabases": "D:/temp/azurebackup"
	    },
	    "General": {
	      "PathToLogFiles": "D:/temp/azurebackup"
	    }
	  }
	}

In the appsettings.json, assign a PathToDatabases, where a LITEDB database is stored (only for keeping track of runs (jobs run).
Also, give a PathToLogFiles, where you can find log files (1 per day).

Now, for each job, you need a JSON file, where you put the settings for that job inside (and the shared access signatures, if you haven't provided access within the appsettings.json):

    {
	  "Job": {
	    "Name": "Job1",
	    "DestinationFolder": "D:/temp/azurebackup",
	    "ResumeOnRestartedJob": true,
	    "NumberOfRetries": 0, 
	    "NumberCopyThreads": 1,
	    "KeepNumberVersions": 5,
	    "DaysToKeepVersion": 0, 
	    "FilenameContains": "", 
	    "FilenameWithout": "", 
	    "ReplaceInvalidTargetFilenameChars": false,
	    "TotalDownloadSpeedMbPerSecond": 0.5,
    
	    "StorageAccounts": [
	      {

	        "Name": "abc",
	        "SasConnectionString": "BlobEndpoint=https://abc.blob.core.windows.net/;QueueEndpoint=https://abc.queue.core.windows.net/;FileEndpoint=https://abc.file.core.windows.net/;TableEndpoint=https://abc.table.core.windows.net/;SharedAccessSignature=sv=2019-12-12&ss=bfqt&srt=sco&sp=rl&se=2020-12-20T04:37:08Z&st=2020-12-19T20:37:08Z&spr=https&sig=abce3e399jdkjs30fjsdlkD",
	        "FilenameContains": "",
	        "FilenameWithout": "",
	        "Containers": [
	          {
	            "Name": "test",
	            "FilenameContains": "",
	            "FilenameWithout": "",
	            "Blobs": [
	              {
	                "Filename": "2007 EasyRadiology.pdf",
	                "TargetFilename": "projects/radiology/Brochure3.pdf"
	              }
	            ]
	          },
	          {
	            "Name": "test2"
	          }
	        ]

	      },
	      {
	        "Name": "martintest3",
	        "SasConnectionString": "",
	        "Containers": [] 
	      }
	    ]
	  }
	  
	}

OK, let's go through this item by item:

"Name": Can be any name for the job
"DestinationFolder": "C:/backup" // folder to write to, in the form (also Windows): "D:/temp/azurebackup" - network folders like "//192.168.178.1"
"ResumeOnRestartedJob": true, //If a job fails and you run it again - go on where the job left of?
"NumberOfRetries": 0, // How often retry if download of file fails?
"NumberCopyThreads": 1, //How many downloads from Azure simultaneously
"KeepNumberVersions": 5, // This is versioning - maximum is 99
"DaysToKeepVersion": 0, // 0 or null = forever
"FilenameContains": "\*test", //Optional - Only backup filenames, which have e. g. the word "test" in them - use "\*" as wildcard before and / or after string
"FilenameWithout": "", //Optional - Exclude files with filenames, which have a certain string,  "\*" acts as wildcard before and / or after string
"ReplaceInvalidTargetFilenameChars": false, //Will replace illegal characters in target filename with "_" 
 "TotalDownloadSpeedMbPerSecond": 0.5, // With this, you can limit the total used bandwidth (MB per second),

"FilesToBackupCsvList": "D:\\vmc\\abc\\abc.csv", //If you provide a Csv list (one or 2 columns, has to be in the form of <Storage Account>/<Container>/<Filename>
//You can provide a second column with the target filename (with folders, if you like)

An example for the CSV list: In the first column, put e. g. MyStorageAccount/MyContainer/MyBlob.txt
You don't have to use a second column, but if you want to have a target file name different from the source, you can add it here. Please be aware, that this is appended to the job's target path. E. g., here you could add "this/is/a/test.txt", it would be put into the job's target path.

Now, we have the "StorageAccounts". You have to list all the storage accounts, which you want to backup in an array. For each storage account, you have to include "Containers" and an array of the containers you want to backup. If you want to backup ALL containers, just leave the array EMPTY, so just enter [ ]

Within the containers, you can add "Blobs" as an array, but you don't have to. If you don't add specific blobs, all blobs are backed up from that container. The blobs must be of the Tier "Hot" - this software cannot backup "cold" blobs.

# Run the application
To start a job, just start the program with:

    blobtolocal job1.json

There are options, but they are not well supported. One is "scanonly", which can be added as 

    blobtolocal job1.json --scanonly
Also, there is the option --continuelastjob, if the backup failed / exited. Then, this should pick up the last run, but again - this may not work well.
