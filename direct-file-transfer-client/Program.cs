using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Threading.Tasks.Dataflow;

class Program
{
    static async Task Main(string[] args)
    {
        // Step 1: Parse arguments
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: direct-file-transfer-client <FileName> <Connections>");
            return;
        }

        string fileName = args[0];
        if (!int.TryParse(args[1], out int connections) || connections < 1)
        {
            Console.WriteLine("Invalid number of connections.");
            return;
        }

        // Step 2: Read server URL from appsettings.json
        string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

        if (!File.Exists(configPath))
        {
            throw new Exception("appsettings.json not found. Please create it with the required configuration.");
        }

        var configJson = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<ClientConfig>(configJson);

        if(config == null)
        {
            throw new Exception("Invalid or missing configuration in appsettings.json. Ensure ServerUrl is set.");
        }

        string saveFolder = config?.DefaultSavingFolder ?? AppDomain.CurrentDomain.BaseDirectory;
        string savePath = Path.Combine(saveFolder, fileName);

        var downloader = new FileDownloader();

        List<int> failedParts = new List<int>();

        downloader.Progress.ProgressUpdated += (progress) =>
        {
            Console.Write($"\rProgress: {progress.Percentage:F2}% ({progress.CompletedParts}/{progress.TotalParts})");
        };

        // Step 5: Download all parts concurrently and display progress using BufferBlock and ActionBlock
        await downloader.DownloadAllPartsAsync(fileName, savePath, connections, config!.ServerUrl);
        Console.WriteLine();

        // Step 6: Report result
        if (failedParts.Count > 0)
        {
            Console.WriteLine($"Failed to download parts: {string.Join(", ", failedParts)}");
        }
        else
        {
            Console.WriteLine("Download complete and verified.");
        }
    }

    public class ClientConfig
    {
        public required string ServerUrl { get; set; }
        public required string DefaultSavingFolder { get; set; }
    }
}
