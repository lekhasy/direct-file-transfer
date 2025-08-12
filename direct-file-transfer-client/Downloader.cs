using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

public static class Downloader
{
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

    public static async Task<List<int>> DownloadAllPartsAsync(string fileName, FileMetadata metadata, int connections, string serverUrl)
    {
        var tasks = new Task[metadata.PartCount];
        var errors = new List<int>();
        var semaphore = new System.Threading.SemaphoreSlim(connections);
        using var httpClient = new HttpClient();

        for (int i = 0; i < metadata.PartCount; i++)
        {
            int partNum = i;
            tasks[i] = Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    bool success = false;
                    int retries = 0;
                    while (!success && retries < 3)
                    {
                        success = await DownloadAndVerifyPartAsync(httpClient, fileName, metadata, partNum, serverUrl);
                        if (!success)
                        {
                            Console.WriteLine($"Hash mismatch or download error for part {partNum}, retrying...");
                            retries++;
                        }
                    }
                    if (!success)
                    {
                        lock (errors)
                        {
                            errors.Add(partNum);
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });
        }
        await Task.WhenAll(tasks);
        return errors;
    }

    public static async Task<bool> DownloadAndVerifyPartAsync(HttpClient httpClient, string fileName, FileMetadata metadata, int partNum, string serverUrl)
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
        using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
        {
            fs.Seek((long)partNum * metadata.ChunkSize, SeekOrigin.Begin);
            fs.Write(partData, 0, partData.Length);
        }
        using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
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
