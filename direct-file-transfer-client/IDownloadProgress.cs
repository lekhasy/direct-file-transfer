
using direct_file_transfer.shared.ValueTypes;

public interface IDownloadProgress
{
    int CompletedParts { get; set; }
    List<PartIndex> FailedParts { get; set; }
    double Percentage { get; }
    int TotalParts { get; set; }

    event Action<FileDownloader.DownloadProgress>? ProgressUpdated;
}