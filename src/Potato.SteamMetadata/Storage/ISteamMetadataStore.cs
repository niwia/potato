using Potato.Domain.ValueObjects;
using Potato.SteamMetadata.Models;

namespace Potato.SteamMetadata.Storage;

/// <summary>
/// Interface for caching Steam application metadata locally.
/// </summary>
public interface ISteamMetadataStore : IDisposable
{
    /// <summary>
    /// Retrieves cached application metadata if present and not expired (&lt; 14 days).
    /// </summary>
    Task<SteamAppMetadata?> GetAppInfoAsync(
        AppId appId,
        bool bypassExpiration = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates full application metadata, compressing the depots JSON blob using Zstandard.
    /// </summary>
    Task UpsertAppInfoAsync(
        AppId appId,
        SteamAppMetadata metadata,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fast partial-update path: updates only the header URL and last_updated timestamp if app exists.
    /// </summary>
    Task<bool> UpdateHeaderUrlAsync(
        AppId appId,
        string headerUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes cached application metadata from the store.
    /// </summary>
    Task DeleteAppInfoAsync(
        AppId appId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the raw unix last_updated timestamp for an application without checking expiration.
    /// </summary>
    Task<long?> GetCacheTimeAsync(
        AppId appId,
        CancellationToken cancellationToken = default);
}
