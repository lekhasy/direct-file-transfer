public class WatchFolderProvider
{
    public string WatchFolder { get; }

    public WatchFolderProvider(AppConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.FileDirectory))
            throw new Exception("Missing required configuration value: FileDirectory");
        WatchFolder = config.FileDirectory;
    }
}
