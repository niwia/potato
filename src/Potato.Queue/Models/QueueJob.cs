using Potato.Domain.ValueObjects;
using Potato.Pipeline.Models;

namespace Potato.Queue.Models;

/// <summary>
/// Represents a game installation or update job in the download queue.
/// </summary>
public sealed class QueueJob
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public AppId AppId => Request.AppId;
    public string Title { get; set; }
    public InstallRequest Request { get; init; }
    public QueueJobStatus Status { get; set; } = QueueJobStatus.Queued;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public InstallProgressReport? LatestProgress { get; set; }
    public InstallResult? Result { get; set; }
    public string? ErrorMessage { get; set; }

    internal CancellationTokenSource? Cts { get; set; }
    internal Func<bool>? PauseHandler { get; set; }
    internal Func<bool>? ResumeHandler { get; set; }

    public QueueJob(InstallRequest request, string? title = null)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        Title = !string.IsNullOrWhiteSpace(title) ? title : $"App {request.AppId}";
    }

    public bool IsActive => Status is QueueJobStatus.Running or QueueJobStatus.Paused;
    public bool IsTerminal => Status is QueueJobStatus.Completed or QueueJobStatus.Failed or QueueJobStatus.Cancelled;

    public double ProgressPercentage => LatestProgress?.DownloadProgress?.Percentage ?? 0.0;
    public double DownloadSpeedBytesPerSecond => LatestProgress?.DownloadProgress?.SpeedBytesPerSecond ?? 0.0;
    public TimeSpan? EstimatedTimeRemaining => LatestProgress?.DownloadProgress?.EstimatedTimeRemaining;
    public string FormattedEta => LatestProgress?.DownloadProgress?.FormattedEta ?? "N/A";

    public override string ToString() => $"[{Status}] {Title} ({AppId}) - {ProgressPercentage:F1}%";
}
