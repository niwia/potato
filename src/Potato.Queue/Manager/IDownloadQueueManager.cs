using Potato.Pipeline.Models;
using Potato.Queue.Events;
using Potato.Queue.Models;

namespace Potato.Queue.Manager;

/// <summary>
/// Service managing the download and installation queue.
/// </summary>
public interface IDownloadQueueManager : IDisposable
{
    int MaxConcurrentDownloads { get; set; }
    bool IsPaused { get; }

    IReadOnlyList<QueueJob> GetAllJobs();
    QueueJob? GetJob(Guid jobId);

    QueueJob Enqueue(InstallRequest request, string? title = null);
    bool PauseJob(Guid jobId);
    bool ResumeJob(Guid jobId);
    bool CancelJob(Guid jobId);
    bool RemoveJob(Guid jobId);
    bool MoveJobUp(Guid jobId);
    bool MoveJobDown(Guid jobId);

    void PauseAll();
    void ResumeAll();
    void CancelAll();
    void ClearCompleted();

    QueueSummary GetSummary();

    event EventHandler<QueueJobEventArgs>? JobEnqueued;
    event EventHandler<QueueJobEventArgs>? JobStarted;
    event EventHandler<QueueJobProgressEventArgs>? JobProgressUpdated;
    event EventHandler<QueueJobCompletedEventArgs>? JobCompleted;
    event EventHandler<QueueJobFailedEventArgs>? JobFailed;
    event EventHandler<QueueJobEventArgs>? JobStateChanged;
    event EventHandler<QueueSummaryEventArgs>? QueueSummaryUpdated;
}
