namespace Potato.Queue.Models;

/// <summary>
/// Summary snapshot of the download queue state.
/// </summary>
public sealed record QueueSummary(
    int TotalJobs,
    int QueuedCount,
    int RunningCount,
    int PausedCount,
    int CompletedCount,
    int FailedCount,
    int CancelledCount,
    double AggregateDownloadSpeedBytesPerSecond)
{
    public bool IsIdle => RunningCount == 0 && QueuedCount == 0;
    public bool HasActiveDownloads => RunningCount > 0;

    public string FormattedSpeed
    {
        get
        {
            if (AggregateDownloadSpeedBytesPerSecond <= 0) return "0 B/s";
            string[] suffixes = { "B/s", "KB/s", "MB/s", "GB/s" };
            int i = 0;
            double spd = AggregateDownloadSpeedBytesPerSecond;
            while (spd >= 1024 && i < suffixes.Length - 1)
            {
                spd /= 1024;
                i++;
            }
            return $"{spd:0.##} {suffixes[i]}";
        }
    }
}
