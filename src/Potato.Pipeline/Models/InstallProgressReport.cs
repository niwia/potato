using Potato.Downloader.Progress;

namespace Potato.Pipeline.Models;

/// <summary>
/// Aggregated real-time progress update for the installation pipeline.
/// </summary>
public sealed record InstallProgressReport
{
    public InstallStep Step { get; init; }
    public string Message { get; init; }
    public DownloadProgressReport? DownloadProgress { get; init; }

    public InstallProgressReport(InstallStep step, string message, DownloadProgressReport? downloadProgress = null)
    {
        Step = step;
        Message = message;
        DownloadProgress = downloadProgress;
    }
}
