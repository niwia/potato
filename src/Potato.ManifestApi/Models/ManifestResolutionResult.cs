using Potato.Domain.ValueObjects;

namespace Potato.ManifestApi.Models;

/// <summary>
/// Result of resolving manifest files for an application.
/// </summary>
public sealed record ManifestResolutionResult
{
    public AppId AppId { get; init; }
    public string Branch { get; init; }
    public ManifestTier TierUsed { get; init; }
    public IReadOnlyList<ManifestEntry> Manifests { get; init; }
    public string? ErrorMessage { get; init; }

    public bool Success => string.IsNullOrEmpty(ErrorMessage) && Manifests.Count > 0;

    public ManifestResolutionResult(
        AppId appId,
        string branch,
        ManifestTier tierUsed,
        IReadOnlyList<ManifestEntry> manifests,
        string? errorMessage = null)
    {
        AppId = appId;
        Branch = branch;
        TierUsed = tierUsed;
        Manifests = manifests ?? Array.Empty<ManifestEntry>();
        ErrorMessage = errorMessage;
    }

    public static ManifestResolutionResult CreateSuccess(
        AppId appId,
        string branch,
        ManifestTier tierUsed,
        IReadOnlyList<ManifestEntry> manifests) =>
        new(appId, branch, tierUsed, manifests);

    public static ManifestResolutionResult CreateFailure(
        AppId appId,
        string branch,
        string errorMessage,
        ManifestTier attemptedTier = ManifestTier.Tier3ClassicZip) =>
        new(appId, branch, attemptedTier, Array.Empty<ManifestEntry>(), errorMessage);
}
