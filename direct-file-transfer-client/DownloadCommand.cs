using Spectre.Console.Cli;
using Spectre.Console;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using direct_file_transfer_client;

public class DownloadCommand : AsyncCommand<DownloadCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<FileName>")]
        [Description("Name of the file to download")]
        public string FileName { get; set; } = string.Empty;

        [CommandOption("--connections|-c")]
        [Description("Number of parallel connections (default: 10)")]
        public int Connections { get; set; } = 10;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var configManager = new ConfigurationManager();
        configManager.SetBasePath(AppDomain.CurrentDomain.BaseDirectory);
        configManager.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        var config = configManager.Get<ClientConfig>();
        if(config == null)
        {
            AnsiConsole.MarkupLine("[red]Invalid or missing configuration in appsettings.json. Ensure ServerUrl is set.[/]");
            return -1;
        }
        var services = new ServiceCollection();
        services.AddSingleton<ClientConfig>(config);
        services.AddTransient<FileDownloader, FileDownloader>();
        var provider = services.BuildServiceProvider();

        string saveFolder = config.DefaultSavingFolder ?? AppDomain.CurrentDomain.BaseDirectory;
        string savePath = Path.Combine(saveFolder, settings.FileName);

        var downloader = provider.GetRequiredService<FileDownloader>();

        downloader.Progress.ProgressUpdated += (progress) =>
        {
            AnsiConsole.Markup($"\r[green]Progress: {progress.Percentage:F2}% ({progress.CompletedParts}/{progress.TotalParts})[/]");
        };

        await downloader.DownloadAllPartsAsync(settings.FileName, savePath, settings.Connections);
        AnsiConsole.WriteLine("");

        if (downloader.Progress.FailedParts.Count > 0)
        {
            AnsiConsole.MarkupLine($"[red]Failed to download parts: {string.Join(", ", downloader.Progress.FailedParts)}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[green]Download complete and verified.[/]");
        }
        return 0;
    }
}
