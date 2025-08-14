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
        private readonly int _blockSize;

        public FileStatusManager(string filePath, int blockSize)
        {
            _filePath = filePath;
            _blockSize = blockSize;
        }

        // Returns a list of block indices that are missing (hash does not match expected)
        public List<int> GetMissingBlocks(List<string> expectedHashes)
        {
            var missingBlocks = new List<int>();
            using (var stream = new FileStream(_filePath, FileMode.OpenOrCreate, FileAccess.Read))
            {
                for (int i = 0; i < expectedHashes.Count; i++)
                {
                    var hash = GetBlockHash(stream, i);
                    if (hash != expectedHashes[i])
                    {
                        missingBlocks.Add(i);
                    }
                }
            }
            return missingBlocks;
        }

        // Returns the hash of a block at the given index
        private string GetBlockHash(FileStream stream, int blockIndex)
        {
            long offset = blockIndex * _blockSize;
            if (offset >= stream.Length)
                return string.Empty;

            stream.Seek(offset, SeekOrigin.Begin);
            int bytesToRead = (int)Math.Min(_blockSize, stream.Length - offset);
            byte[] buffer = new byte[bytesToRead];
            stream.ReadExactly(buffer, 0, bytesToRead);
            return FileTransferDataHasher.GetBlockHash(buffer);
        }

        // Writes a block to disk at the given index
        public void WriteBlock(int blockIndex, byte[] data)
        {
            using (var stream = new FileStream(_filePath, FileMode.OpenOrCreate, FileAccess.Write))
            {
                long offset = blockIndex * _blockSize;
                stream.Seek(offset, SeekOrigin.Begin);
                stream.Write(data, 0, data.Length);
            }
        }
    }
}
