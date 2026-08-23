using Potato.Domain.ValueObjects;
using Potato.SteamMetadata.Models;

namespace Potato.SteamMetadata.Resolver;

/// <summary>
/// Orchestrates the 4-layer Steam metadata resolution pipeline (DB cache -> SteamCMD/Storefront parallel -> SteamKit2 PICS fallback -> Storefront backfill -> DB cache write).
/// </summary>
public interface ISteamMetadataResolver
{
    Task<SteamAppMetadata?> ResolveAppMetadataAsync(
        AppId appId,
        AppToken? appToken = null,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);
}
