using Potato.Domain.ValueObjects;

namespace Potato.ManifestApi.Models;

/// <summary>
/// Represents a decrypted Steam manifest file payload associated with a depot.
/// </summary>
public sealed record ManifestEntry
{
    public DepotId DepotId { get; init; }
    public ManifestGid ManifestGid { get; init; }
    public byte[] Content { get; init; }

    public string FileName => $"{DepotId}_{ManifestGid}.manifest";

    public ManifestEntry(DepotId depotId, ManifestGid manifestGid, byte[] content)
    {
        DepotId = depotId;
        ManifestGid = manifestGid;
        Content = content ?? Array.Empty<byte>();
    }
}
