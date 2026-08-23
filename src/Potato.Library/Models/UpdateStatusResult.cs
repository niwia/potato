using Potato.Domain.ValueObjects;

namespace Potato.Library.Models;

/// <summary>
/// Result of an update check for an installed game.
/// </summary>
public sealed record UpdateStatusResult(
    AppId AppId,
    UpdateStatus Status,
    string? InstalledBuildId = null,
    string? TargetBuildId = null,
    IReadOnlyList<DepotUpdateDiff>? DepotDiffs = null,
    string? Reason = null)
{
    public static UpdateStatusResult UpToDate(AppId appId, string? buildId = null) =>
        new(appId, UpdateStatus.UpToDate, InstalledBuildId: buildId, TargetBuildId: buildId, Reason: "All installed depot manifests match latest upstream GIDs.");

    public static UpdateStatusResult UpdateAvailable(
        AppId appId,
        string? localBuildId,
        string? targetBuildId,
        IReadOnlyList<DepotUpdateDiff> diffs) =>
        new(appId, UpdateStatus.UpdateAvailable, localBuildId, targetBuildId, diffs, $"{diffs.Count(d => d.HasUpdate)} depot(s) have newer manifests available.");

    public static UpdateStatusResult CannotDetermine(AppId appId, string reason) =>
        new(appId, UpdateStatus.CannotDetermine, Reason: reason);
}
