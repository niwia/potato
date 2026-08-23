using Potato.Downloader.Options;
using Potato.Downloader.Progress;

namespace Potato.Downloader.Process;

/// <summary>
/// Interface for orchestrating the DepotDownloader subprocess.
/// </summary>
public interface IDepotDownloaderProcess : IDisposable
{
    bool IsRunning { get; }
    bool IsPaused { get; }
    int? ProcessId { get; }

    Task<int> RunAsync(
        DepotDownloaderOptions options,
        IProgress<DownloadProgressReport>? progress = null,
        CancellationToken cancellationToken = default);

    bool Pause();
    bool Resume();
    void Stop();
}
