using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

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

        // Step 3: Request file metadata from server
        var metadata = await Downloader.GetFileMetadataAsync(fileName, config!.ServerUrl);
        if (metadata == null)
        {
            Console.WriteLine("Failed to get or parse metadata.");
            return;
        }
        Console.WriteLine($"Parsed metadata: {metadata.FileName}, {metadata.FileSize}, {metadata.PartCount}");

        // Step 4: Create file with correct size
        Downloader.CreateEmptyFile(savePath, metadata.FileSize);

        // Step 5: Download all parts concurrently
        var errors = await Downloader.DownloadAllPartsAsync(fileName, savePath, metadata, connections, config.ServerUrl);

        // Step 6: Report result
        if (errors.Count > 0)
        {
            Console.WriteLine($"Failed to download parts: {string.Join(", ", errors)}");
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
