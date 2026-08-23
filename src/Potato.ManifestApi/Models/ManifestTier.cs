namespace Potato.ManifestApi.Models;

/// <summary>
/// Strategy tier utilized to resolve Steam manifests.
/// </summary>
public enum ManifestTier
{
    /// <summary>
    /// Tier 0: Resolved from local archive cache without any network requests.
    /// </summary>
    Tier0LocalCache = 0,

    /// <summary>
    /// Tier 1: Generated via Single Depot Manifest API (/generate/manifest) using 1,500/day pool.
    /// </summary>
    Tier1SingleManifest = 1,

    /// <summary>
    /// Tier 2: Generated via Bundle AppManifest API (/generate/appmanifest) using 100/day pool.
    /// </summary>
    Tier2BundleManifest = 2,

    /// <summary>
    /// Tier 3: Downloaded via Classic Full Manifest Zip (/manifest/{appid}) using 55/day pool.
    /// </summary>
    Tier3ClassicZip = 3
}
