using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using direct_file_transfer.shared;

namespace direct_file_transfer_client
{
    using System.Threading.Tasks.Dataflow;
    using direct_file_transfer.shared.ValueTypes;

    public class FileStatusManager
    {
        private readonly string _filePath;
        private readonly FileMetadata _metadata;
        private readonly ActionBlock<(PartIndex blockIndex, byte[] data)> _writeBlock;
        private readonly BufferBlock<(PartIndex blockIndex, byte[] data)> _writeBuffer = new BufferBlock<(PartIndex blockIndex, byte[] data)>();

        public FileStatusManager(string filePath, FileMetadata metadata)
        {
            _filePath = filePath;
            _metadata = metadata;
            _writeBlock = new ActionBlock<(PartIndex blockIndex, byte[] data)>(item =>
            {

                using (var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Write))
                {
                    PartOffset offset = new PartOffset(item.blockIndex, new PartSize(_metadata.ChunkSize));
                    stream.Seek(offset.Value, SeekOrigin.Begin);
                    stream.Write(item.data, 0, item.data.Length);
                }
            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 });
            _writeBuffer.LinkTo(_writeBlock, new DataflowLinkOptions { PropagateCompletion = true });
        }

        // Returns a list of block indices that are missing (hash does not match expected)
        public List<PartIndex> GetMissingBlocks()
        {
            var fileAlreadyExist = EnsureFileExists();

            if (!fileAlreadyExist)
            {
                return _metadata.PartHashes.Select((hash, index) => new PartIndex(index)).ToList();
            }

            var missingBlocks = new List<PartIndex>();

            using (var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read))
            {
                for (int i = 0; i < _metadata.PartHashes.Count; i++)
                {
                    var hash = GetBlockHash(stream, new PartIndex(i));
                    if (hash != _metadata.PartHashes[i])
                    {
                        missingBlocks.Add(new PartIndex(i));
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
        public string GetBlockHash(FileStream stream, PartIndex blockIndex)
        {
            PartOffset offset = new PartOffset(blockIndex, new PartSize(_metadata.ChunkSize));
            if (offset.Value >= stream.Length)
                return string.Empty;

            stream.Seek(offset.Value, SeekOrigin.Begin);
            long bytesToRead = Math.Min(_metadata.ChunkSize, stream.Length - offset.Value);
            byte[] buffer = new byte[bytesToRead];
            stream.ReadExactly(buffer, 0, (int)bytesToRead);
            return Hasher.GetHash(buffer);
        }

        // Writes a block to disk at the given index
        public void WriteBlock(PartIndex blockIndex, byte[] data)
        {
            _writeBuffer.Post((blockIndex, data));
        }

        public void FinishWrite()
        {
            _writeBuffer?.Complete();
            _writeBuffer?.Completion.Wait();
            _writeBlock?.Complete();
            _writeBlock?.Completion.Wait();
        }
    }
}
