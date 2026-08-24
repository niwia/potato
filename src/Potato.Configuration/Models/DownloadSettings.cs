namespace Potato.Configuration.Models;

/// <summary>
/// Configuration for DepotDownloader process execution and download queue behavior.
/// </summary>
public sealed class DownloadSettings
{
    /// <summary>
    /// Maximum concurrent chunk download threads per download process (1 to 30, default 8).
    /// </summary>
    public int MaxDownloadsPerJob { get; set; } = 8;

    /// <summary>
    /// Maximum concurrent active game downloads in the queue (1 to 5, default 2).
    /// </summary>
    public int MaxConcurrentQueueJobs { get; set; } = 2;

    /// <summary>
    /// Whether to check local network LAN Cache DNS servers before public Steam CDNs.
    /// </summary>
    public bool UseLanCache { get; set; } = true;

    /// <summary>
    /// Whether to perform SHA-1 chunk verification on all downloaded files.
    /// </summary>
    public bool ValidateDownloads { get; set; } = true;

    /// <summary>
    /// Whether to automatically select the primary branch/depots if only one choice exists.
    /// </summary>
    public bool AutoSkipSingleChoice { get; set; } = true;

    /// <summary>
    /// Whether to automatically pre-select primary executable & OS depots while filtering non-game assets.
    /// </summary>
    public bool SmartDepotSelection { get; set; } = true;

    /// <summary>
    /// Whether to hide macOS-specific depot packages on Linux/Windows.
    /// </summary>
    public bool FilterMacOsDepots { get; set; } = true;

    /// <summary>
    /// Whether to hide standalone soundtrack/OST depots from game installations by default.
    /// </summary>
    public bool FilterSoundtracks { get; set; } = true;

    /// <summary>
    /// Enforce installing games directly into detected Steam libraries (with automatic ACF registration).
    /// </summary>
    public bool LimitToSteamLibraries { get; set; } = true;

    /// <summary>
    /// Fallback custom download directory if not installing directly to a Steam library folder.
    /// </summary>
    public string? DefaultDownloadDirectory { get; set; }
}
