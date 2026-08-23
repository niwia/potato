using Potato.Domain.ValueObjects;

namespace Potato.SteamMetadata.Models;

/// <summary>
/// Detailed depot information retrieved from Steam metadata sources.
/// </summary>
public sealed record SteamDepotInfo
{
    public DepotId DepotId { get; init; }
    public string? Name { get; init; }
    public string? OsList { get; init; }
    public string? Language { get; init; }
    public bool IsSteamDeck { get; init; }
    public string? Size { get; init; }
    public ManifestGid? ManifestGid { get; init; }
    public IReadOnlyDictionary<string, ManifestGid> Manifests { get; init; }

    public SteamDepotInfo(
        DepotId depotId,
        string? name = null,
        string? osList = null,
        string? language = null,
        bool isSteamDeck = false,
        string? size = null,
        ManifestGid? manifestGid = null,
        IReadOnlyDictionary<string, ManifestGid>? manifests = null)
    {
        DepotId = depotId;
        Name = name;
        OsList = osList;
        Language = language;
        IsSteamDeck = isSteamDeck;
        Size = size;
        ManifestGid = manifestGid;
        Manifests = manifests ?? new Dictionary<string, ManifestGid>();
    }
}
