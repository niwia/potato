using Potato.Domain.ValueObjects;

namespace Potato.Domain.Models;

/// <summary>
/// Represents manifest identification and decryption metadata for a specific depot.
/// </summary>
public sealed record Manifest
{
    public DepotId DepotId { get; init; }
    public ManifestGid ManifestGid { get; init; }
    public AppToken? AppToken { get; init; }
    public string? DecryptionKey { get; init; }

    public Manifest(
        DepotId depotId,
        ManifestGid manifestGid,
        AppToken? appToken = null,
        string? decryptionKey = null)
    {
        DepotId = depotId;
        ManifestGid = manifestGid;
        AppToken = appToken;
        DecryptionKey = decryptionKey;
    }
}
