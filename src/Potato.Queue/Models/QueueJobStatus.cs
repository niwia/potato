namespace Potato.Queue.Models;

/// <summary>
/// Execution state of a job in the download queue.
/// </summary>
public enum QueueJobStatus
{
    Queued,
    Running,
    Paused,
    Completed,
    Failed,
    Cancelled
}
