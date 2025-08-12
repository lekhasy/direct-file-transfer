using System.Text.Json.Serialization;

public class FilePartInfo
{
    [JsonPropertyName("FileName")]
    public string FileName { get; set; }
    [JsonPropertyName("partnumber")]
    public int partnumber { get; set; }
    [JsonPropertyName("partHash")]
    public string partHash { get; set; }
    [JsonPropertyName("partData")]
    public string partData { get; set; }
}
