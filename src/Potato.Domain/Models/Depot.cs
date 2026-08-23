using Potato.Domain.ValueObjects;

namespace Potato.Domain.Models;

/// <summary>
/// Represents a Steam Depot and its associated manifest and metadata.
/// </summary>
public sealed record Depot
{
    public DepotId DepotId { get; init; }
    public ManifestGid ManifestGid { get; init; }
    public ulong SizeBytes { get; init; }
    public string? Language { get; init; }
    public string? OsTag { get; init; }
    public string? Description { get; init; }

    public Depot(
        DepotId depotId,
        ManifestGid manifestGid = default,
        ulong sizeBytes = 0,
        string? language = null,
        string? osTag = null,
        string? description = null)
    {
        DepotId = depotId;
        ManifestGid = manifestGid;
        SizeBytes = sizeBytes;
        Language = language;
        OsTag = osTag;
        Description = description;
    }
}
