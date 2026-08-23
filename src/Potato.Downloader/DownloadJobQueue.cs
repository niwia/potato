using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Potato.Core.Models;
using Potato.Core.Steam;
using Potato.Core.Storage;
using Potato.Core.Slssteam;

namespace Potato.Downloader;

public class DownloadTaskItem
{
    public uint AppId { get; init; }
    public string GameName { get; init; } = string.Empty;
    public string? InstallDir { get; init; }
    public string LibraryPath { get; init; } = string.Empty;
    public List<DepotInfo> SelectedDepots { get; init; } = new();
    public DownloadStatus Status { get; set; } = DownloadStatus.Queued;
    public double ProgressPercent { get; set; }
    public string StatusMessage { get; set; } = "In Queue";
    public CancellationTokenSource Cts { get; } = new();
}

public class DownloadJobQueue
{
    private readonly ConcurrentQueue<DownloadTaskItem> _queue = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly DepotDownloaderService _downloaderService = new();
    private readonly DatabaseManager _dbManager = new();
    private DownloadTaskItem? _activeJob;

    public event Action<DownloadTaskItem>? JobStarted;
    public event Action<DownloadTaskItem, DownloadProgress>? JobProgress;
    public event Action<DownloadTaskItem>? JobCompleted;
    public event Action<DownloadTaskItem, string>? JobFailed;
    public event Action<string>? LogMessage;

    public DownloadTaskItem? ActiveJob => _activeJob;
    public IReadOnlyCollection<DownloadTaskItem> QueuedJobs => _queue.ToArray();

    public void Enqueue(DownloadTaskItem item)
    {
        _queue.Enqueue(item);
        LogMessage?.Invoke($"📥 Enqueued '{item.GameName}' (AppID: {item.AppId}) with {item.SelectedDepots.Count} depot(s).");
        _ = ProcessQueueAsync();
    }

    public void CancelActiveJob()
    {
        if (_activeJob != null && !_activeJob.Cts.IsCancellationRequested)
        {
            _activeJob.Status = DownloadStatus.Cancelled;
            _activeJob.Cts.Cancel();
            LogMessage?.Invoke($"🛑 Cancelling active job '{_activeJob.GameName}'...");
        }
    }

    private async Task ProcessQueueAsync()
    {
        if (!await _semaphore.WaitAsync(0))
        {
            return; // Already running a worker loop
        }

        try
        {
            while (_queue.TryDequeue(out var job))
            {
                _activeJob = job;
                job.Status = DownloadStatus.Downloading;
                JobStarted?.Invoke(job);

                var gameDir = AcfManager.GetGameDirectory(job.LibraryPath, job.AppId, job.GameName, job.InstallDir);
                bool allSucceeded = true;
                string failureReason = string.Empty;

                for (int i = 0; i < job.SelectedDepots.Count; i++)
                {
                    if (job.Cts.IsCancellationRequested)
                    {
                        allSucceeded = false;
                        failureReason = "Cancelled by user";
                        break;
                    }

                    var depot = job.SelectedDepots[i];
                    LogMessage?.Invoke($"📦 [{i + 1}/{job.SelectedDepots.Count}] Downloading Depot {depot.DepotId} ({depot.Name})...");

                    var success = await _downloaderService.DownloadDepotAsync(
                        job.AppId,
                        depot.DepotId,
                        depot.ManifestId,
                        gameDir,
                        progress =>
                        {
                            // Calculate overall progress across all depots
                            double overallPercent = ((i * 100.0) + progress.Percent) / job.SelectedDepots.Count;
                            job.ProgressPercent = overallPercent;
                            job.StatusMessage = progress.StatusMessage;

                            JobProgress?.Invoke(job, new DownloadProgress
                            {
                                AppId = job.AppId,
                                Percent = overallPercent,
                                SpeedBytesPerSecond = progress.SpeedBytesPerSecond,
                                StatusMessage = progress.StatusMessage
                            });
                        },
                        log => LogMessage?.Invoke(log),
                        dbManager: _dbManager,
                        ct: job.Cts.Token
                    );

                    if (!success)
                    {
                        allSucceeded = false;
                        failureReason = $"Depot {depot.DepotId} failed to download.";
                        break;
                    }
                }

                if (allSucceeded)
                {
                    job.Status = DownloadStatus.Completed;
                    job.ProgressPercent = 100.0;
                    job.StatusMessage = "Download completed successfully.";

                    // Auto-write ACF
                    LogMessage?.Invoke($"📝 Writing appmanifest_{job.AppId}.acf...");
                    var installFolderName = AcfManager.GetInstallFolderName(job.AppId, job.GameName, job.InstallDir);
                    AcfManager.WriteAcf(job.LibraryPath, job.AppId, job.GameName, installFolderName, job.SelectedDepots);

                    // Auto-register in SLSsteam config
                    var slsConfigPath = SlsConfigManager.GetDefaultConfigPath();
                    LogMessage?.Invoke($"🔧 Updating SLSsteam config ({slsConfigPath})...");
                    SlsConfigManager.AddAdditionalApp(slsConfigPath, job.AppId, job.GameName);

                    JobCompleted?.Invoke(job);
                }
                else
                {
                    job.Status = job.Cts.IsCancellationRequested ? DownloadStatus.Cancelled : DownloadStatus.Failed;
                    job.StatusMessage = failureReason;
                    JobFailed?.Invoke(job, failureReason);
                }

                _activeJob = null;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
