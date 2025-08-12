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
        string serverUrl = "http://localhost:5027";
        if (File.Exists(configPath))
        {
            var configJson = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<ClientConfig>(configJson);
            if (config != null && !string.IsNullOrWhiteSpace(config.ServerUrl))
            {
                serverUrl = config.ServerUrl;
            }
            else
            {
                throw new InvalidOperationException("Invalid or missing ServerUrl in appsettings.json");
            }
        }
        else
        {
            Console.WriteLine("appsettings.json not found, using default server URL.");
        }

        // Step 3: Request file metadata from server
        var metadata = await Downloader.GetFileMetadataAsync(fileName, serverUrl);
        if (metadata == null)
        {
            Console.WriteLine("Failed to get or parse metadata.");
            return;
        }
        Console.WriteLine($"Parsed metadata: {metadata.FileName}, {metadata.FileSize}, {metadata.PartCount}");

        // Step 4: Create file with correct size
        Downloader.CreateEmptyFile(fileName, metadata.FileSize);

        // Step 5: Download all parts concurrently
        var errors = await Downloader.DownloadAllPartsAsync(fileName, metadata, connections, serverUrl);

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
        public string ServerUrl { get; set; }
    }
}