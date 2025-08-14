using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using direct_file_transfer.shared;

namespace direct_file_transfer_client
{
    public class FileStatusManager
    {
        private readonly string _filePath;
        private readonly FileMetadata _metadata;

        public FileStatusManager(string filePath, FileMetadata metadata)
        {
            _filePath = filePath;
            _metadata = metadata;
        }

        // Returns a list of block indices that are missing (hash does not match expected)
        public List<int> GetMissingBlocks()
        {
            var fileAlreadyExist = EnsureFileExists();

            if (!fileAlreadyExist)
            {
                return _metadata.PartHashes.Select((hash, index) => index).ToList();
            }

            var missingBlocks = new List<int>();

            using (var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read))
            {
                for (int i = 0; i < _metadata.PartHashes.Count; i++)
                {
                    var hash = GetBlockHash(stream, i);
                    if (hash != _metadata.PartHashes[i])
                    {
                        missingBlocks.Add(i);
                    }
                }
            }
            return missingBlocks;
        }

        private bool EnsureFileExists()
        {
            var fileInfo = new FileInfo(_filePath);
            var fileNotExistOrCorrupted = !fileInfo.Exists || fileInfo.Length != _metadata.FileSize;
            if (fileNotExistOrCorrupted)
            {
                if (fileInfo.Exists)
                {
                    fileInfo.Delete();
                }

                using (var stream = new FileStream(_filePath, FileMode.Create, FileAccess.Write))
                {
                    stream.SetLength(_metadata.FileSize);
                }
                return false;
            }

            return true;
        }

        // Returns the hash of a block at the given index
        private string GetBlockHash(FileStream stream, int blockIndex)
        {
            long offset = blockIndex * _metadata.ChunkSize;
            if (offset >= stream.Length)
                return string.Empty;

            stream.Seek(offset, SeekOrigin.Begin);
            int bytesToRead = (int)Math.Min(_metadata.ChunkSize, stream.Length - offset);
            byte[] buffer = new byte[bytesToRead];
            stream.ReadExactly(buffer, 0, bytesToRead);
            return FileTransferDataHasher.GetBlockHash(buffer);
        }

        // Writes a block to disk at the given index
        public void WriteBlock(int blockIndex, byte[] data)
        {
            using (var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Write))
            {
                long offset = blockIndex * _metadata.ChunkSize;
                stream.Seek(offset, SeekOrigin.Begin);
                stream.Write(data, 0, data.Length);
            }
        }
    }
}
