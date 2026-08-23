namespace Potato.Domain.Models;

/// <summary>
/// State machine status for game and depot download operations.
/// </summary>
public enum DownloadStatus
{
    Queued,
    Downloading,
    Paused,
    Verifying,
    Complete,
    Failed,
    Cancelled
}
