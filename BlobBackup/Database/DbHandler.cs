using BlobBackupLib.Database.Model;
using LiteDB;
using Microsoft.Extensions.Azure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace BlobBackupLib.Database
{
    public class DbHandler
    {
        private readonly LiteDatabase _db;
        private ILiteCollection<LocalFile> _fileCol;
        private ILiteCollection<Model.Blob> _blobCol;
        private ILiteCollection<Container> _containerCol;
        private ILiteCollection<Run> _runCol;
        private ILiteCollection<StorageAccount> _storageCol;
        private BsonValue _runId;
        private int _indexSerializedFile;



        public DbHandler(string filename)
        {
           _db = new LiteDatabase(filename);
           InitDb();
           
        }

        private void InitDb()
        {
            // Get a collection (create it if it doesn't exist)
            _fileCol = _db.GetCollection<LocalFile>("LocalFiles");
            _fileCol.EnsureIndex(x => x.FilenameWithRelativePath);


            _blobCol = _db.GetCollection<Model.Blob>("Blobs");
            _blobCol.EnsureIndex(x => x.FullPathFilename);
            //_blobCol.EnsureIndex(x => x.IsArchived);
            _blobCol.EnsureIndex(x => x.IsBackedUp);

            _containerCol = _db.GetCollection<Container>("Container");

            _runCol = _db.GetCollection<Run>("Run");
            _runCol.EnsureIndex(x => x.BlobIds);

            _storageCol = _db.GetCollection<StorageAccount>("StorageAccount");

        }

        public int GetNumberOfSerializedLists()
        {
            var run = _runCol.FindById(_runId);
            return run.SerializerFiles.Count;
        }

        public Run GetCurrentRun()
        {
            return _runCol.FindById(_runId);
        }

        public void SetLastRunAsCurrent()
        {
            var run = _runCol.FindOne(Query.All(Query.Descending));
            _runId = run.Id;
        }

        public object GetNextUnbackedUpSerializedList()
        {
            var run = _runCol.FindById(_runId);
            _indexSerializedFile = run.SerializerFiles.FindIndex(c => c.BackedUp == false);
            
            if(_indexSerializedFile != -1)
            {
                var serializedFile = run.SerializerFiles[_indexSerializedFile].Filename;
                return Serializer.Deserialize(serializedFile);
            }
            else
            {
                return null;
            }
        }

        public void DeleteSerializedListOfCurrentAndLastRuns()
        {
            var run = _runCol.Find(Query.All(Query.Descending)).ToList();
            foreach (var r in run)
            {
                var counter = 0;
                foreach (var ser in r.SerializerFiles)
                {
                    if (File.Exists(ser.Filename))
                    {
                        File.Delete(ser.Filename);
                    }
                }
                counter++;
                if(counter >= 9)
                {
                    break;
                }
            }
            
        }

        public void MarkSerializedListAsBackedUp()
        {
            var run = _runCol.FindById(_runId);
            run.SerializerFiles[_indexSerializedFile].BackedUp = true;
            _runCol.Update(run);
        }

        public void CreateNewRun()
        {
            var run = new Run();
            _runId =  _runCol.Insert(run);
            
        }

        public void AddBlobToRun(Model.Blob blob)
        {
            var run = _runCol.FindById(_runId);
            run.BlobIds.Add(blob.Id);
            _runCol.Update(run);
        }

        public void AddLocalFileToRun(LocalFile lF)
        {
            var run = _runCol.FindById(_runId);
            run.LocalFilesId.Add(lF.Id);
            _runCol.Update(run);
        }

        public void AddSerializedFileToRun(string file)
        {
            var run = _runCol.FindById(_runId);
            run.SerializerFiles.Add(new SerializerFile() { Filename = file });
            _runCol.Update(run);
        }

        public LocalFile GetLocalFileByMatchToBlob(Model.Blob blob)
        {
            return _fileCol.FindOne(c => c.FilenameWithRelativePath == blob.FullPathFilename && c.Size == blob.Size && c.CreateTime == blob.ModifyTime);
        }

        public List<int> GetAllBlobsFromRun()
        {
            var run = _runCol.FindById(_runId);
            return run.BlobIds;
        }

        public void ClearLocalFileStoreData()
        {
            _fileCol.DeleteAll();
        }

        public void ClearBlobStorage()
        {
            _blobCol.DeleteAll();

        }
        public Model.Blob GetBlobFromBlobStore(Model.Blob blob)
        {
            return _blobCol.FindOne(c => c.FullPathFilename == blob.FullPathFilename);
        }

        public Model.Blob GetBlobFromBlobStore(int blobId)
        {
            return _blobCol.FindOne(c => c.Id == blobId);
        }

        public IEnumerable<Model.Blob> GetAllUnbackedupBlobs()
        {
            var blobs =  _blobCol.Find(c => c.IsBackedUp == false);
            return blobs.OrderBy(c => c.FullPathFilename);
        }

        public IEnumerable<Model.Blob> GetAllBlobs()
        {
            //var test = _blobCol.
            return _blobCol.FindAll();
        }

        public void AddBlobToBlobStore(Model.Blob blob)
        {
            _blobCol.Insert(blob);
        }


        public void AddBulkBlobsToBlobStore(List<Model.Blob> blobs, int count)
        {
            _blobCol.InsertBulk(blobs, count);
        }

        public void AddBulkLocalFilesToLocalFileStore(List<Model.LocalFile> localFiles, int count)
        {
            _fileCol.InsertBulk(localFiles, count);
        }

        public void AddLocalFileToLocalFileStore(LocalFile localFile)
        {


            if (!localFile.FilenameWithRelativePath.Contains("___VersionedBlobBackups") && _fileCol.FindOne(c => c.FilenameWithRelativePath == localFile.FilenameWithRelativePath && c.CreateTime == localFile.CreateTime && c.Size == localFile.Size) == null)
            {
                _fileCol.Insert(localFile);
                
            }
        }

        public List<int> GetAllLocalFilesFromRun()
        {
            var run = _runCol.FindById(_runId);
            return run.LocalFilesId;
        }

        public void AddBlobToFilesToBackupToRun(int blobId)
        {
            var run = _runCol.FindById(_runId);
            run.FilesToBackup.Add(blobId);
            _runCol.Update(run);
        }

        public List<int> GetBlobsToBackupFromRun()
        {
            var run = _runCol.FindById(_runId);
            return run.FilesToBackup;
        }

        public void DeleteBlob(Model.Blob blob)
        {
            _blobCol.Delete(blob.Id);
        }

        public bool BlobExistsInBlobStore(Model.Blob blob)
        {
            var result = _blobCol.FindOne(c => c.FullPathFilename == blob.Filename );
            return result != null;
        }

        public Model.Blob GetNextUnarchivedFromBlobStore()
        {
            return _blobCol.FindOne(c => c.IsBackedUp == false);
        }

        public bool BlobExistsInLocalFileStore(Model.Blob blob)
        {
            var file = _fileCol.FindOne(c => (c.FilenameWithRelativePath == blob.FullPathFilename && c.CreateTime == blob.ModifyTime && c.Size == blob.Size));
            if (file != null)
            {
                return true;
            }
            return false;
        }

        public void UpdateBlob(Model.Blob blob)
        {
            _blobCol.Update(blob);
        }
    }
}
