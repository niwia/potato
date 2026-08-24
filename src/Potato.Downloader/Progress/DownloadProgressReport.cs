namespace Potato.Downloader.Progress;

/// <summary>
/// Immutable progress report containing current download completion, calculated speed, and ETA.
/// </summary>
public sealed record DownloadProgressReport
{
    public double Percentage { get; init; }
    public int DisplayPercentage => Math.Clamp((int)Math.Round(Percentage), 0, 100);
    public double SpeedBytesPerSecond { get; init; }
    public string FormattedSpeed { get; init; } = "0.00 B/s";
    public TimeSpan? EstimatedTimeRemaining { get; init; }
    public string FormattedEta { get; init; } = "Calculating...";
    public ulong DownloadedBytes { get; init; }
    public ulong TotalBytes { get; init; }
    public bool IsValidating { get; init; }
    public string? CurrentFile { get; init; }
    public uint? DepotId { get; init; }
    public string RawLine { get; init; } = string.Empty;
}
