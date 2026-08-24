namespace Potato.Configuration.Models;

/// <summary>
/// Settings for logging, remote interfaces, and experimental features.
/// </summary>
public sealed class AdvancedSettings
{
    /// <summary>
    /// Logging filter level ("Debug", "Info", "Warning", "Error").
    /// </summary>
    public string LogLevel { get; set; } = "Info";

    /// <summary>
    /// Whether to enable the optional local network remote Web UI server.
    /// </summary>
    public bool EnableRemoteWebUi { get; set; } = false;

    /// <summary>
    /// Port for the remote Web UI server.
    /// </summary>
    public int WebUiPort { get; set; } = 8765;

    /// <summary>
    /// Maximum number of days to keep cached manifest files before automatic pruning (0 for indefinite).
    /// </summary>
    public int ManifestCacheRetentionDays { get; set; } = 30;

    /// <summary>
    /// Custom Steam Workshop cell ID override.
    /// </summary>
    public string? WorkshopCellId { get; set; }

    /// <summary>
    /// Maximum concurrent downloads for Steam Workshop items.
    /// </summary>
    public int WorkshopMaxDownloads { get; set; } = 8;
}
