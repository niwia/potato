using Potato.Domain.ValueObjects;

namespace Potato.Library.Models;

public enum ActivityStatus
{
    Success,
    Running,
    Failed,
    Queued
}

/// <summary>
/// Model representing a completed or ongoing download / update event in the activity log.
/// </summary>
public sealed record ActivityLogEntry(
    Guid Id,
    AppId AppId,
    string GameName,
    ActivityStatus Status,
    DateTime Timestamp,
    ulong TotalBytes = 0,
    TimeSpan ElapsedTime = default,
    string? Details = null,
    string? ErrorMessage = null)
{
    public string FormattedTimestamp => Timestamp.ToLocalTime().ToString("MMM dd, HH:mm");

    public string FormattedSize => TotalBytes > 0 ? FormatBytes(TotalBytes) : "0 B";

    public string FormattedDuration => ElapsedTime.TotalSeconds >= 60
        ? $"{(int)ElapsedTime.TotalMinutes}m {ElapsedTime.Seconds}s"
        : $"{(int)ElapsedTime.TotalSeconds}s";

    public string FormattedSpeed
    {
        get
        {
            if (ElapsedTime.TotalSeconds <= 0 || TotalBytes == 0) return "0 MB/s";
            double bytesPerSec = TotalBytes / ElapsedTime.TotalSeconds;
            return $"{FormatBytes((ulong)bytesPerSec)}/s";
        }
    }

    public string StatusSummary
    {
        get
        {
            return Status switch
            {
                ActivityStatus.Success => $"Success • {FormattedSize} in {FormattedDuration} ({FormattedSpeed})",
                ActivityStatus.Running => $"Downloading • {FormattedSize}",
                ActivityStatus.Queued => "Queued in download line",
                ActivityStatus.Failed => $"Failed • {ErrorMessage ?? "Unknown error"}",
                _ => "Unknown"
            };
        }
    }

    public static string FormatBytes(ulong bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int idx = 0;
        double dBytes = bytes;
        while (dBytes >= 1024 && idx < suffixes.Length - 1)
        {
            dBytes /= 1024;
            idx++;
        }
        return $"{dBytes:0.##} {suffixes[idx]}";
    }
}
