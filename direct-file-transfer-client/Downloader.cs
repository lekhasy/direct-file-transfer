using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using direct_file_transfer.shared;
using direct_file_transfer.shared.ValueTypes;

public static class Downloader
{
    public class DownloadProgress
    {
        public int TotalParts { get; set; }
        public int CompletedParts { get; set; }
        public double Percentage => TotalParts == 0 ? 0 : (CompletedParts * 100.0 / TotalParts);
        public List<int> FailedParts { get; set; } = new List<int>();
    }

    public class ProgressUpdate
    {
        public bool Success { get; set; }
        public PartIndex? PartNum { get; set; }
    }

    private static async Task<FileMetadata?> GetFileMetadataAsync(string fileName, string serverUrl)
    {
        using var httpClient = new HttpClient();
        var metadataResp = await httpClient.GetAsync($"{serverUrl}/Download/metadata?FileName={Uri.EscapeDataString(fileName)}");
        if (!metadataResp.IsSuccessStatusCode)
        {
            Console.WriteLine($"Failed to get metadata: {metadataResp.StatusCode}");
            return null;
        }
        var metadataJson = await metadataResp.Content.ReadAsStringAsync();
        var metadata = JsonSerializer.Deserialize<FileMetadata>(metadataJson);
        return metadata;
    }

    public static async Task<ISourceBlock<DownloadProgress>> DownloadAllPartsAsync(string fileName, string savePath, int connections, string serverUrl)
    {
        var metadata = await GetFileMetadataAsync(fileName, serverUrl);
        if (metadata == null)
        {
            throw new Exception("Failed to get or parse metadata.");
        }
        Console.WriteLine($"Parsed metadata: {metadata.FileName}, {metadata.FileSize}, {metadata.PartCount}");

        var errors = new List<int>();
        var progress = new DownloadProgress { TotalParts = metadata.PartCount };
        var httpClient = new HttpClient();

        // Use FileStatusManager to check missing blocks
        var fileStatusManager = new direct_file_transfer_client.FileStatusManager(savePath, metadata);
        var missingBlocks = fileStatusManager.GetMissingBlocks();

        progress.CompletedParts = metadata.PartCount - missingBlocks.Count;

        Console.WriteLine($"Completed parts: {progress.CompletedParts}");

        var progressBlock = new TransformBlock<ProgressUpdate, DownloadProgress>(update =>
        {
            if (update.PartNum is null)
            {
                // Initial progress update
                return new DownloadProgress
                {
                    TotalParts = progress.TotalParts,
                    CompletedParts = progress.CompletedParts,
                    FailedParts = progress.FailedParts
                };
            }

            if (update.Success)
            {
                progress.CompletedParts++;
            }
            else
            {
                errors.Add(update.PartNum.Value);
                progress.FailedParts.Add(update.PartNum.Value);
            }
            return new DownloadProgress
            {
                TotalParts = progress.TotalParts,
                CompletedParts = progress.CompletedParts,
                FailedParts = progress.FailedParts
            };
        });

        // Post initial progress
        progressBlock.Post(new ProgressUpdate { Success = true, PartNum = null });

        var options = new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = connections
        };

        var actionBlock = new ActionBlock<PartIndex>(async partNum =>
        {

            bool success = false;
            int retries = 0;
            while (!success && retries < 3)
            {
                try
                {
                    var partData = await DownloadPartAsync(httpClient, fileName, metadata, partNum, serverUrl);
                    if (partData != null)
                    {
                        // Write block using FileStatusManager
                        fileStatusManager.WriteBlock(partNum, partData);
                        
                        // Verify hash
                        var hash = Hasher.GetHash(partData);
                        if (hash == metadata.PartHashes[partNum.Value])
                        {
                            success = true;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Failed to download part {partNum}, retrying...");
                    }

                    if (!success)
                    {
                        Console.WriteLine($"Hash mismatch or download error for part {partNum}, retrying...");
                        retries++;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception in ActionBlock for part {partNum}: {ex}");
                    await progressBlock.SendAsync(new ProgressUpdate { Success = false, PartNum = partNum });
                }
            }
            // Post progress update to progressBlock
            await progressBlock.SendAsync(new ProgressUpdate { Success = success, PartNum = partNum });

        }, options);

        foreach (var partNum in missingBlocks)
        {
            actionBlock.Post(partNum);
        }

        actionBlock.Complete();

        // Complete progressBlock after all downloads are done
        _ = Task.Run(async () =>
        {
            await actionBlock.Completion;
            progressBlock.Complete();
            fileStatusManager.FinishWrite();
        });

        return progressBlock;
    }

    // Downloads a part and returns the raw data, or null if failed
    public static async Task<byte[]?> DownloadPartAsync(HttpClient httpClient, string fileName, FileMetadata metadata, PartIndex partNum, string serverUrl)
    {
        var partUrl = $"{serverUrl}/Download?FileName={Uri.EscapeDataString(fileName)}&partnumber={partNum.Value}";
        var partResp = await httpClient.GetAsync(partUrl);
        if (!partResp.IsSuccessStatusCode)
        {
            Console.WriteLine($"Failed to download part {partNum}: {partResp.StatusCode}");
            return null;
        }
        var partJson = await partResp.Content.ReadAsStringAsync();
        var partInfo = JsonSerializer.Deserialize<FilePartInfo>(partJson);
        if (partInfo == null)
        {
            Console.WriteLine($"Failed to parse part {partNum} info.");
            return null;
        }
        return Convert.FromBase64String(partInfo.partData);
    }
}
