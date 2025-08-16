using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using direct_file_transfer.shared;
using direct_file_transfer.shared.ValueTypes;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Diagnostics;
using direct_file_transfer_client;

public class FileDownloader
{

    private readonly ClientConfig _config;

    public FileDownloader(ClientConfig config)
    {
        _config = config;
    }

    public class DownloadProgress : IDownloadProgress
    {
        public int TotalParts { get; set; }
        public int CompletedParts { get; set; }
        public List<PartIndex> FailedParts { get; set; } = new List<PartIndex>();
        public double Percentage => TotalParts == 0 ? 0 : (CompletedParts * 100.0 / TotalParts);

        public void UpdateProgress(ProgressUpdate update)
        {
            if (update.Success)
            {
                CompletedParts++;
            }
            else
            {
                FailedParts.Add(update.PartNum!);
            }

            ProgressUpdated?.Invoke(this);
        }

        public void ResetProgress()
        {
            TotalParts = 0;
            CompletedParts = 0;
            FailedParts.Clear();
        }

        public event Action<DownloadProgress>? ProgressUpdated;
    }



    public class ProgressUpdate
    {
        public bool Success { get; set; }
        public PartIndex? PartNum { get; set; }
    }

    public IDownloadProgress Progress { get; private set; } = new DownloadProgress()
    {
        TotalParts = 0
    };

    private async Task<FileMetadata?> GetFileMetadataAsync(string fileName)
    {
        using var httpClient = new HttpClient();
        var metadataResp = await httpClient.GetAsync($"{_config.ServerUrl}/Download/metadata?FileName={Uri.EscapeDataString(fileName)}");
        if (!metadataResp.IsSuccessStatusCode)
        {
            Console.WriteLine($"Failed to get metadata: {metadataResp.StatusCode}");
            return null;
        }
        var metadataJson = await metadataResp.Content.ReadAsStringAsync();
        var metadata = JsonSerializer.Deserialize<FileMetadata>(metadataJson);
        return metadata;
    }

    public async Task DownloadAllPartsAsync(string relativeFilePath, string savingDirectory, int connections)
    {
        var metadata = await GetFileMetadataAsync(relativeFilePath);
        if (metadata == null)
        {
            throw new Exception("Failed to get or parse metadata.");
        }
        Console.WriteLine($"Parsed metadata: {metadata.FileName}, {metadata.FileSize}, {metadata.PartCount}");

        ((DownloadProgress)Progress).ResetProgress();
        Progress.TotalParts = metadata.PartCount;
        var httpClient = new HttpClient();

        // Use FileStatusManager to check missing blocks
        var fileStatusManager = new direct_file_transfer_client.FileStatusManager(savingDirectory, metadata);
        var missingBlocks = fileStatusManager.GetMissingBlocks();

        Progress.CompletedParts = metadata.PartCount - missingBlocks.Count;

        Console.WriteLine($"Completed parts: {Progress.CompletedParts}");

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
                    var partData = await DownloadPartAsync(httpClient, metadata, partNum);
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
                    ((DownloadProgress)Progress).UpdateProgress(new ProgressUpdate { Success = false, PartNum = partNum });
                }
            }
            // Post progress update to progressBlock
            ((DownloadProgress)Progress).UpdateProgress(new ProgressUpdate { Success = true, PartNum = partNum });
        }, options);

        foreach (var partNum in missingBlocks)
        {
            actionBlock.Post(partNum);
        }

        actionBlock.Complete();

        // Complete progressBlock after all downloads are done
        await actionBlock.Completion;
        fileStatusManager.FinishWrite();
    }

    // Downloads a part and returns the raw data, or null if failed
    public async Task<byte[]?> DownloadPartAsync(HttpClient httpClient, FileMetadata metadata, PartIndex partNum)
    {
        var partUrl = $"{_config.ServerUrl}/Download?FileName={Uri.EscapeDataString(metadata.FileRelativePath)}&partnumber={partNum.Value}";
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
