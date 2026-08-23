using Potato.Domain.ValueObjects;
using Potato.Domain.Vdf;

namespace Potato.Domain.Acf;

/// <summary>
/// Strongly-typed representation of the AppState object in an appmanifest.acf file.
/// </summary>
public sealed class AcfAppState
{
    public AppId AppId { get; set; }
    public int Universe { get; set; } = 1;
    public string Name { get; set; } = string.Empty;
    public int StateFlags { get; set; } = 4; // 4 = Fully Installed
    public string InstallDir { get; set; } = string.Empty;
    public long LastUpdated { get; set; }
    public long LastPlayed { get; set; }
    public ulong SizeOnDisk { get; set; }
    public ulong StagingSize { get; set; }
    public string BuildId { get; set; } = string.Empty;
    public ulong LastOwner { get; set; }
    public int DownloadType { get; set; }
    public int UpdateResult { get; set; }
    public ulong BytesToDownload { get; set; }
    public ulong BytesDownloaded { get; set; }
    public ulong BytesToStage { get; set; }
    public ulong BytesStaged { get; set; }
    public string TargetBuildID { get; set; } = string.Empty;
    public int AutoUpdateBehavior { get; set; }
    public int AllowOtherDownloadsWhileRunning { get; set; }
    public int ScheduledAutoUpdate { get; set; }

    public List<InstalledDepotInfo> InstalledDepots { get; set; } = new();
    public Dictionary<string, string> InstallScripts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> UserConfig { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> MountedConfig { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Preserves any unmapped / custom fields from the original ACF file for round-trip parity.
    /// </summary>
    public Dictionary<string, VdfNode> AdditionalFields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
