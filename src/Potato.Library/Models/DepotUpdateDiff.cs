using Potato.Domain.ValueObjects;

namespace Potato.Library.Models;

/// <summary>
/// Detailed manifest diff for a single depot between local install and upstream.
/// </summary>
public sealed record DepotUpdateDiff(
    DepotId DepotId,
    ManifestGid InstalledGid,
    ManifestGid TargetGid,
    string? DepotName = null)
{
    public bool HasUpdate => InstalledGid != TargetGid;

    public override string ToString() =>
        $"Depot {DepotId} ({(DepotName ?? "Depot")}): Installed {InstalledGid} -> Target {TargetGid}";
}
