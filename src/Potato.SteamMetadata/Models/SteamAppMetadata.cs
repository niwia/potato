using Potato.Domain.ValueObjects;

namespace Potato.SteamMetadata.Models;

/// <summary>
/// Complete aggregated metadata for a Steam application.
/// </summary>
public sealed record SteamAppMetadata
{
    public AppId AppId { get; init; }
    public string? Name { get; init; }
    public string? InstallDir { get; init; }
    public string? HeaderUrl { get; init; }
    public string? BuildId { get; init; }
    public string? TimeUpdated { get; init; }
    public IReadOnlyDictionary<DepotId, SteamDepotInfo> Depots { get; init; }
    public IReadOnlyDictionary<string, SteamBranchInfo> Branches { get; init; }
    public string Source { get; init; }

    public SteamAppMetadata(
        AppId appId,
        string? name = null,
        string? installDir = null,
        string? headerUrl = null,
        string? buildId = null,
        string? timeUpdated = null,
        IReadOnlyDictionary<DepotId, SteamDepotInfo>? depots = null,
        IReadOnlyDictionary<string, SteamBranchInfo>? branches = null,
        string source = "unknown")
    {
        AppId = appId;
        Name = name;
        InstallDir = installDir;
        HeaderUrl = headerUrl;
        BuildId = buildId;
        TimeUpdated = timeUpdated;
        Depots = depots ?? new Dictionary<DepotId, SteamDepotInfo>();
        Branches = branches ?? new Dictionary<string, SteamBranchInfo>();
        Source = source;
    }
}
