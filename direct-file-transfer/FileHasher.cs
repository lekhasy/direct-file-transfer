using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

public class FileHasher
{
    private const int ChunkSize = 4 * 1024 * 1024; // 4MB
    private readonly ConcurrentDictionary<string, List<string>> hashTables = new();

    public List<string> GetOrCalculateHashTable(string filePath)
    {
        if (hashTables.TryGetValue(filePath, out var hashes))
        {
            return hashes;
        }

        if (!File.Exists(filePath))
            throw new FileNotFoundException();

        hashes = new List<string>();
        using var stream = File.OpenRead(filePath);
        var buffer = new byte[ChunkSize];
        int bytesRead;
        while ((bytesRead = stream.Read(buffer, 0, ChunkSize)) > 0)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(buffer, 0, bytesRead);
            hashes.Add(Convert.ToHexString(hash));
        }
        hashTables[filePath] = hashes;
        return hashes;
    }

    public byte[] ReadFilePart(string filePath, long partNumber)
    {
        using var stream = File.OpenRead(filePath);
        stream.Seek(partNumber * ChunkSize, SeekOrigin.Begin);
        var buffer = new byte[ChunkSize];
        int bytesRead = stream.Read(buffer, 0, ChunkSize);
        if (bytesRead < ChunkSize)
        {
            Array.Resize(ref buffer, bytesRead);
        }
        return buffer;
    }
}
