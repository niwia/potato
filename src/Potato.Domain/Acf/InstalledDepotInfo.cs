using Potato.Domain.ValueObjects;

namespace Potato.Domain.Acf;

/// <summary>
/// Represents a depot entry inside the InstalledDepots section of an appmanifest.acf file.
/// </summary>
public sealed record InstalledDepotInfo
{
    public DepotId DepotId { get; init; }
    public ManifestGid ManifestGid { get; init; }
    public ulong SizeBytes { get; init; }

    public InstalledDepotInfo(DepotId depotId, ManifestGid manifestGid, ulong sizeBytes = 0)
    {
        DepotId = depotId;
        ManifestGid = manifestGid;
        SizeBytes = sizeBytes;
    }
}
