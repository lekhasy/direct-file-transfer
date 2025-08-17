using direct_file_transfer_client;
using direct_file_transfer.shared;

public class FileStatusManagerFactory
{
    private readonly ClientConfig _config;
    public FileStatusManagerFactory(ClientConfig config)
    {
        _config = config;
    }

    public FileStatusManager Create(FileMetadata metadata)
    {
        return new FileStatusManager(_config, metadata);
    }
}
