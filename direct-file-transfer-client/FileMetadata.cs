using System.Collections.Generic;
using System.Text.Json.Serialization;

public class FileMetadata
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; }

    [JsonPropertyName("fileSize")]
    public long FileSize { get; set; }

    [JsonPropertyName("partCount")]
    public int PartCount { get; set; }

    [JsonPropertyName("partHashes")]
    public List<string> PartHashes { get; set; }

    [JsonPropertyName("chunkSize")]
    public int ChunkSize { get; set; }

    [JsonPropertyName("fileRelativePath")]
    public string FileRelativePath { get; set; }
}
