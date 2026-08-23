using Potato.Domain.ValueObjects;
using Potato.ManifestApi.Models;

namespace Potato.ManifestApi.Client;

/// <summary>
/// Injectable client for interacting with the Hubcap / Morrenus Manifest API.
/// </summary>
public interface IHubcapApiClient
{
    /// <summary>
    /// Executes the 4-tier manifest resolution pipeline (Tier 0 local cache -> Tier 1 single -> Tier 2 bundle -> Tier 3 classic).
    /// </summary>
    Task<ManifestResolutionResult> ResolveManifestAsync(
        AppId appId,
        string branch,
        IReadOnlyDictionary<DepotId, ManifestGid> requiredDepots,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tier 1: Generates a single depot manifest file (/generate/manifest).
    /// </summary>
    Task<byte[]?> GenerateSingleManifestAsync(
        DepotId depotId,
        ManifestGid manifestGid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tier 2: Generates an app bundle manifest zip (/generate/appmanifest).
    /// </summary>
    Task<byte[]?> GenerateBundleManifestAsync(
        AppId appId,
        string branch = "public",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tier 3: Downloads the classic full manifest zip (/manifest/{appid}).
    /// </summary>
    Task<byte[]?> DownloadClassicZipAsync(
        AppId appId,
        string branch = "public",
        CancellationToken cancellationToken = default);
}
