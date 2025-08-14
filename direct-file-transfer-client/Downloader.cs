using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

public static class Downloader
{
    public class DownloadProgress
    {
        public int TotalParts { get; set; }
        public int CompletedParts { get; set; }
        public double Percentage => TotalParts == 0 ? 0 : (CompletedParts * 100.0 / TotalParts);
        public List<int> FailedParts { get; set; } = new List<int>();
    }

    public static async Task<FileMetadata?> GetFileMetadataAsync(string fileName, string serverUrl)
    {
                using var httpClient = new HttpClient();
        var metadataResp = await httpClient.GetAsync($"{serverUrl}/Download/metadata?FileName={Uri.EscapeDataString(fileName)}");
        if (!metadataResp.IsSuccessStatusCode)
        {
            Console.WriteLine($"Failed to get metadata: {metadataResp.StatusCode}");
            return null;
        }
        var metadataJson = await metadataResp.Content.ReadAsStringAsync();
        Console.WriteLine($"Received metadata: {metadataJson}");
        var metadata = JsonSerializer.Deserialize<FileMetadata>(metadataJson);
        return metadata;
    }

    public static void CreateEmptyFile(string fileName, long fileSize)
    {
        Console.WriteLine($"Creating empty file: {fileName} ({fileSize} bytes)");
        using var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
        fs.SetLength(fileSize);
    }

    public static ISourceBlock<DownloadProgress> DownloadAllPartsAsync(string fileName, string savePath, FileMetadata metadata, int connections, string serverUrl)
    {
        var progressBlock = new BroadcastBlock<DownloadProgress>(p => p);
        var errors = new List<int>();
        int completed = 0;
        var progress = new DownloadProgress { TotalParts = metadata.PartCount };
        var httpClient = new HttpClient();

        var options = new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = connections
        };

        var actionBlock = new ActionBlock<int>(async partNum =>
        {
            bool success = false;
            int retries = 0;
            while (!success && retries < 3)
            {
                success = await DownloadAndVerifyPartAsync(httpClient, fileName, savePath, metadata, partNum, serverUrl);
                if (!success)
                {
                    Console.WriteLine($"Hash mismatch or download error for part {partNum}, retrying...");
                    retries++;
                }
            }
            lock (progress)
            {
                if (success)
                {
                    completed++;
                    progress.CompletedParts = completed;
                }
                else
                {
                    errors.Add(partNum);
                    progress.FailedParts.Add(partNum);
                }
                progressBlock.Post(new DownloadProgress
                {
                    TotalParts = progress.TotalParts,
                    CompletedParts = progress.CompletedParts,
                    FailedParts = new List<int>(progress.FailedParts)
                });
            }
        }, options);

        for (int i = 0; i < metadata.PartCount; i++)
        {
            actionBlock.Post(i);
        }

        Task.Run(async () =>
        {
            actionBlock.Complete();
            await actionBlock.Completion;
            progressBlock.Complete();
        });

        return progressBlock;
    }

    public static async Task<bool> DownloadAndVerifyPartAsync(HttpClient httpClient, string fileName, string savePath, FileMetadata metadata, int partNum, string serverUrl)
    {
         var partUrl = $"{serverUrl}/Download?FileName={Uri.EscapeDataString(fileName)}&partnumber={partNum}";
        var partResp = await httpClient.GetAsync(partUrl);
        if (!partResp.IsSuccessStatusCode)
        {
            Console.WriteLine($"Failed to download part {partNum}: {partResp.StatusCode}");
            return false;
        }
        var partJson = await partResp.Content.ReadAsStringAsync();
        var partInfo = JsonSerializer.Deserialize<FilePartInfo>(partJson);
        if (partInfo == null)
        {
            Console.WriteLine($"Failed to parse part {partNum} info.");
            return false;
        }
        var partData = Convert.FromBase64String(partInfo.partData);
        using (var fs = new FileStream(savePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
        {
            fs.Seek((long)partNum * metadata.ChunkSize, SeekOrigin.Begin);
            fs.Write(partData, 0, partData.Length);
        }
        using (var fs = new FileStream(savePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            fs.Seek((long)partNum * metadata.ChunkSize, SeekOrigin.Begin);
            var buffer = new byte[partData.Length];
            int read = await fs.ReadAsync(buffer, 0, buffer.Length);
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(buffer, 0, read);
            var hashHex = Convert.ToHexString(hash);
            if (hashHex != partInfo.partHash)
            {
                return false;
            }
        }
        return true;
    }
}
