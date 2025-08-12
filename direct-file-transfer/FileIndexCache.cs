using System.Collections.Concurrent;
using System.Security.Cryptography;

public class FileIndexCache
{
    private readonly string _directory;
    private readonly ConcurrentDictionary<string, FileIndexEntry> _index = new();
    private readonly FileSystemWatcher _watcher;
    private readonly WatchFolderProvider _watchFolderProvider;

    public FileIndexCache(WatchFolderProvider watchFolderProvider)
    {
        _watchFolderProvider = watchFolderProvider;
        _directory = _watchFolderProvider.WatchFolder;
        BuildInitialIndex();
        _watcher = new FileSystemWatcher(_directory)
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };
        _watcher.Created += (s, e) => UpdateFile(e.FullPath);
        _watcher.Changed += (s, e) => UpdateFile(e.FullPath);
        _watcher.Deleted += (s, e) => RemoveFile(e.FullPath);
        _watcher.Renamed += (s, e) => { RemoveFile(e.OldFullPath); UpdateFile(e.FullPath); };
    }

    private void BuildInitialIndex()
    {
        foreach (var filePath in Directory.GetFiles(_directory, "*", SearchOption.AllDirectories))
        {
            UpdateFile(filePath);
        }
    }

    private void UpdateFile(string filePath)
    {
        if (!File.Exists(filePath)) return;
        var info = new FileInfo(filePath);
        string hash;
        using (var stream = File.OpenRead(filePath))
        {
            using var sha256 = SHA256.Create();
            hash = Convert.ToHexString(sha256.ComputeHash(stream));
        }
        _index[filePath] = new FileIndexEntry
        {
            FileName = Path.GetRelativePath(_directory, filePath),
            CreatedDate = info.CreationTimeUtc,
            ModifiedDate = info.LastWriteTimeUtc,
            Hash = hash
        };
    }

    private void RemoveFile(string filePath)
    {
        _index.TryRemove(filePath, out _);
    }

    public IEnumerable<FileIndexEntry> GetIndex() => _index.Values;
}

public class FileIndexEntry
{
    public string? FileName { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public string? Hash { get; set; }
}
