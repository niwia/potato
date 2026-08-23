using Potato.Domain.Acf;
using Potato.Domain.Models;
using Potato.Domain.ValueObjects;

namespace Potato.Library.Models;

/// <summary>
/// Represents an installed game discovered in a Steam library folder.
/// </summary>
public sealed class InstalledGame
{
    public AppId AppId { get; init; }
    public string Name { get; set; } = string.Empty;
    public string InstallDir { get; init; } = string.Empty;
    public string FullGamePath { get; init; } = string.Empty;
    public string AcfPath { get; init; } = string.Empty;
    public string SteamAppsPath { get; init; } = string.Empty;
    public string BuildId { get; set; } = "0";
    public ulong SizeOnDisk { get; set; }
    public IReadOnlyList<InstalledDepotInfo> InstalledDepots { get; set; } = Array.Empty<InstalledDepotInfo>();
    public UpdateStatus UpdateStatus { get; set; } = UpdateStatus.Unknown;
    public IReadOnlyList<DepotUpdateDiff> PendingDepotUpdates { get; set; } = Array.Empty<DepotUpdateDiff>();
    public DateTime LastScannedAt { get; set; } = DateTime.UtcNow;

    public bool HasPendingUpdates => UpdateStatus == UpdateStatus.UpdateAvailable;

    public override string ToString() => $"{Name} ({AppId}) - Build {BuildId} [{UpdateStatus}]";
}
