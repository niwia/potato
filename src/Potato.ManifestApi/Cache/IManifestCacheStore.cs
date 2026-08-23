using Potato.Domain.ValueObjects;
using Potato.ManifestApi.Models;

namespace Potato.ManifestApi.Cache;

/// <summary>
/// Interface for reading and persisting Steam manifest files locally (Tier 0 local cache).
/// </summary>
public interface IManifestCacheStore
{
    /// <summary>
    /// Checks the local cache for all required depots of an application and branch.
    /// Returns the cached manifests if ALL required depots match their exact target GIDs, otherwise null.
    /// </summary>
    Task<IReadOnlyList<ManifestEntry>?> TryGetCachedManifestsAsync(
        AppId appId,
        string branch,
        IReadOnlyDictionary<DepotId, ManifestGid> requiredDepots,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists manifest entries to the local cache store.
    /// </summary>
    Task SaveManifestsAsync(
        AppId appId,
        string branch,
        IReadOnlyList<ManifestEntry> manifests,
        CancellationToken cancellationToken = default);
}
