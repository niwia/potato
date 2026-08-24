using Potato.Downloader.Process;
using Potato.Pipeline.Models;
using Potato.Pipeline.Orchestrator;
using Potato.Queue.Events;
using Potato.Queue.Models;

namespace Potato.Queue.Manager;

/// <summary>
/// Thread-safe implementation of IDownloadQueueManager managing concurrent download execution and ordering.
/// </summary>
public sealed class DownloadQueueManager : IDownloadQueueManager
{
    private readonly Func<IDepotDownloaderProcess, IInstallGameOrchestrator> _orchestratorFactory;
    private readonly List<QueueJob> _jobs = new();
    private readonly object _lock = new();
    private readonly CancellationTokenSource _managerCts = new();
    private int _maxConcurrentDownloads = 1;
    private bool _isPaused;
    private bool _disposed;

    public int MaxConcurrentDownloads
    {
        get
        {
            lock (_lock) return _maxConcurrentDownloads;
        }
        set
        {
            lock (_lock)
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(value), "Max concurrent downloads must be at least 1.");
                _maxConcurrentDownloads = value;
            }
            TriggerQueueProcessing();
        }
    }

    public bool IsPaused
    {
        get
        {
            lock (_lock) return _isPaused;
        }
    }

    public event EventHandler<QueueJobEventArgs>? JobEnqueued;
    public event EventHandler<QueueJobEventArgs>? JobStarted;
    public event EventHandler<QueueJobProgressEventArgs>? JobProgressUpdated;
    public event EventHandler<QueueJobCompletedEventArgs>? JobCompleted;
    public event EventHandler<QueueJobFailedEventArgs>? JobFailed;
    public event EventHandler<QueueJobEventArgs>? JobStateChanged;
    public event EventHandler<QueueSummaryEventArgs>? QueueSummaryUpdated;

    public DownloadQueueManager(IInstallGameOrchestrator orchestrator)
        : this(_ => orchestrator)
    {
    }

    public DownloadQueueManager(Func<IDepotDownloaderProcess, IInstallGameOrchestrator> orchestratorFactory)
    {
        _orchestratorFactory = orchestratorFactory ?? throw new ArgumentNullException(nameof(orchestratorFactory));
    }

    public IReadOnlyList<QueueJob> GetAllJobs()
    {
        lock (_lock)
        {
            return _jobs.ToList();
        }
    }

    public QueueJob? GetJob(Guid jobId)
    {
        lock (_lock)
        {
            return _jobs.FirstOrDefault(j => j.Id == jobId);
        }
    }

    public QueueJob Enqueue(InstallRequest request, string? title = null)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var job = new QueueJob(request, title);

        lock (_lock)
        {
            _jobs.Add(job);
        }

        JobEnqueued?.Invoke(this, new QueueJobEventArgs(job));
        JobStateChanged?.Invoke(this, new QueueJobEventArgs(job));
        EmitSummaryUpdate();

        TriggerQueueProcessing();
        return job;
    }

    public bool PauseJob(Guid jobId)
    {
        QueueJob? job;
        lock (_lock)
        {
            job = _jobs.FirstOrDefault(j => j.Id == jobId);
            if (job == null) return false;

            if (job.Status == QueueJobStatus.Queued)
            {
                job.Status = QueueJobStatus.Paused;
            }
            else if (job.Status == QueueJobStatus.Running)
            {
                bool paused = job.PauseHandler?.Invoke() ?? true;
                if (paused)
                {
                    job.Status = QueueJobStatus.Paused;
                }
            }
            else
            {
                return false;
            }
        }

        JobStateChanged?.Invoke(this, new QueueJobEventArgs(job));
        EmitSummaryUpdate();
        return true;
    }

    public bool ResumeJob(Guid jobId)
    {
        QueueJob? job;
        lock (_lock)
        {
            job = _jobs.FirstOrDefault(j => j.Id == jobId);
            if (job == null || job.Status != QueueJobStatus.Paused) return false;

            if (job.StartedAt != null && job.ResumeHandler != null)
            {
                // Already started process: resume execution
                job.ResumeHandler.Invoke();
                job.Status = QueueJobStatus.Running;
            }
            else
            {
                // Not yet started: return to queued state
                job.Status = QueueJobStatus.Queued;
            }
        }

        JobStateChanged?.Invoke(this, new QueueJobEventArgs(job));
        EmitSummaryUpdate();
        TriggerQueueProcessing();
        return true;
    }

    public bool CancelJob(Guid jobId)
    {
        QueueJob? job;
        lock (_lock)
        {
            job = _jobs.FirstOrDefault(j => j.Id == jobId);
            if (job == null || job.IsTerminal) return false;

            job.Cts?.Cancel();
            job.Status = QueueJobStatus.Cancelled;
            job.CompletedAt = DateTime.UtcNow;
        }

        JobStateChanged?.Invoke(this, new QueueJobEventArgs(job));
        EmitSummaryUpdate();
        TriggerQueueProcessing();
        return true;
    }

    public bool RemoveJob(Guid jobId)
    {
        QueueJob? job;
        lock (_lock)
        {
            job = _jobs.FirstOrDefault(j => j.Id == jobId);
            if (job == null) return false;

            if (!job.IsTerminal)
            {
                CancelJob(jobId);
            }

            _jobs.Remove(job);
        }

        JobStateChanged?.Invoke(this, new QueueJobEventArgs(job));
        EmitSummaryUpdate();
        TriggerQueueProcessing();
        return true;
    }

    public bool MoveJobUp(Guid jobId)
    {
        lock (_lock)
        {
            int idx = _jobs.FindIndex(j => j.Id == jobId);
            if (idx <= 0) return false;

            var job = _jobs[idx];
            if (job.Status != QueueJobStatus.Queued) return false;

            var prev = _jobs[idx - 1];
            if (prev.Status != QueueJobStatus.Queued) return false;

            _jobs[idx] = prev;
            _jobs[idx - 1] = job;
            return true;
        }
    }

    public bool MoveJobDown(Guid jobId)
    {
        lock (_lock)
        {
            int idx = _jobs.FindIndex(j => j.Id == jobId);
            if (idx == -1 || idx >= _jobs.Count - 1) return false;

            var job = _jobs[idx];
            if (job.Status != QueueJobStatus.Queued) return false;

            var next = _jobs[idx + 1];
            if (next.Status != QueueJobStatus.Queued) return false;

            _jobs[idx] = next;
            _jobs[idx + 1] = job;
            return true;
        }
    }

    public void PauseAll()
    {
        lock (_lock)
        {
            _isPaused = true;
            foreach (var job in _jobs.Where(j => j.Status is QueueJobStatus.Running or QueueJobStatus.Queued))
            {
                PauseJob(job.Id);
            }
        }
        EmitSummaryUpdate();
    }

    public void ResumeAll()
    {
        lock (_lock)
        {
            _isPaused = false;
            foreach (var job in _jobs.Where(j => j.Status == QueueJobStatus.Paused))
            {
                ResumeJob(job.Id);
            }
        }
        EmitSummaryUpdate();
        TriggerQueueProcessing();
    }

    public void CancelAll()
    {
        lock (_lock)
        {
            foreach (var job in _jobs.Where(j => !j.IsTerminal).ToList())
            {
                CancelJob(job.Id);
            }
        }
    }

    public void ClearCompleted()
    {
        lock (_lock)
        {
            _jobs.RemoveAll(j => j.IsTerminal);
        }
        EmitSummaryUpdate();
    }

    public QueueSummary GetSummary()
    {
        lock (_lock)
        {
            int total = _jobs.Count;
            int queued = _jobs.Count(j => j.Status == QueueJobStatus.Queued);
            int running = _jobs.Count(j => j.Status == QueueJobStatus.Running);
            int paused = _jobs.Count(j => j.Status == QueueJobStatus.Paused);
            int completed = _jobs.Count(j => j.Status == QueueJobStatus.Completed);
            int failed = _jobs.Count(j => j.Status == QueueJobStatus.Failed);
            int cancelled = _jobs.Count(j => j.Status == QueueJobStatus.Cancelled);

            double aggSpeed = _jobs
                .Where(j => j.Status == QueueJobStatus.Running)
                .Sum(j => j.DownloadSpeedBytesPerSecond);

            return new QueueSummary(total, queued, running, paused, completed, failed, cancelled, aggSpeed);
        }
    }

    private void TriggerQueueProcessing()
    {
        if (_disposed || _managerCts.IsCancellationRequested) return;

        Task.Run(() =>
        {
            while (true)
            {
                QueueJob? nextJob = null;

                lock (_lock)
                {
                    if (_isPaused) break;

                    int currentRunning = _jobs.Count(j => j.Status == QueueJobStatus.Running);
                    if (currentRunning >= _maxConcurrentDownloads) break;

                    nextJob = _jobs.FirstOrDefault(j => j.Status == QueueJobStatus.Queued);
                    if (nextJob == null) break;

                    nextJob.Status = QueueJobStatus.Running;
                    nextJob.StartedAt = DateTime.UtcNow;
                    nextJob.Cts = CancellationTokenSource.CreateLinkedTokenSource(_managerCts.Token);
                }

                if (nextJob != null)
                {
                    _ = ExecuteJobAsync(nextJob);
                }
            }
        });
    }

    private async Task ExecuteJobAsync(QueueJob job)
    {
        JobStarted?.Invoke(this, new QueueJobEventArgs(job));
        JobStateChanged?.Invoke(this, new QueueJobEventArgs(job));
        EmitSummaryUpdate();

        var progress = new Progress<InstallProgressReport>(report =>
        {
            job.LatestProgress = report;
            JobProgressUpdated?.Invoke(this, new QueueJobProgressEventArgs(job, report));
            EmitSummaryUpdate();
        });

        IDepotDownloaderProcess? activeProcess = null;
        var orchestrator = _orchestratorFactory(activeProcess = new DepotDownloaderProcess());

        job.PauseHandler = () => activeProcess?.Pause() ?? false;
        job.ResumeHandler = () => activeProcess?.Resume() ?? false;

        try
        {
            var result = await orchestrator.InstallGameAsync(job.Request, progress, job.Cts?.Token ?? CancellationToken.None);

            lock (_lock)
            {
                job.CompletedAt = DateTime.UtcNow;
                job.Result = result;

                if (result.Success)
                {
                    job.Status = QueueJobStatus.Completed;
                }
                else
                {
                    job.Status = QueueJobStatus.Failed;
                    job.ErrorMessage = result.ErrorMessage;
                }
            }

            if (result.Success)
            {
                JobCompleted?.Invoke(this, new QueueJobCompletedEventArgs(job, result));
            }
            else
            {
                JobFailed?.Invoke(this, new QueueJobFailedEventArgs(job, result.ErrorMessage ?? "Installation failed."));
            }
        }
        catch (OperationCanceledException)
        {
            lock (_lock)
            {
                job.Status = QueueJobStatus.Cancelled;
                job.CompletedAt = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            lock (_lock)
            {
                job.Status = QueueJobStatus.Failed;
                job.ErrorMessage = ex.Message;
                job.CompletedAt = DateTime.UtcNow;
            }
            JobFailed?.Invoke(this, new QueueJobFailedEventArgs(job, ex.Message, ex));
        }
        finally
        {
            activeProcess?.Dispose();
            JobStateChanged?.Invoke(this, new QueueJobEventArgs(job));
            EmitSummaryUpdate();

            TriggerQueueProcessing();
        }
    }

    private void EmitSummaryUpdate()
    {
        var summary = GetSummary();
        QueueSummaryUpdated?.Invoke(this, new QueueSummaryEventArgs(summary));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _managerCts.Cancel();
        _managerCts.Dispose();
    }
}
