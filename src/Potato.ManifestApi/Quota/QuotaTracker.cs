using Potato.ManifestApi.Models;

namespace Potato.ManifestApi.Quota;

public sealed record QuotaSnapshot(
    int SingleManifestCalls,
    int BundleManifestCalls,
    int ClassicZipCalls,
    int RateLimitHits,
    DateTime LastResetUtc);

/// <summary>
/// Thread-safe in-memory and daily quota tracking for Hubcap API tiers.
/// Resets daily at UTC midnight.
/// </summary>
public sealed class QuotaTracker
{
    private readonly object _lock = new();

    public const int DefaultSingleManifestDailyLimit = 1500;
    public const int DefaultBundleManifestDailyLimit = 100;
    public const int DefaultClassicZipDailyLimit = 55;

    private int _singleManifestCalls;
    private int _bundleManifestCalls;
    private int _classicZipCalls;
    private int _rateLimitHits;
    private DateTime _lastResetDateUtc;

    public QuotaTracker()
    {
        _lastResetDateUtc = DateTime.UtcNow.Date;
    }

    public void RecordCall(ManifestTier tier)
    {
        lock (_lock)
        {
            EnsureDateRoll();
            switch (tier)
            {
                case ManifestTier.Tier1SingleManifest:
                    _singleManifestCalls++;
                    break;
                case ManifestTier.Tier2BundleManifest:
                    _bundleManifestCalls++;
                    break;
                case ManifestTier.Tier3ClassicZip:
                    _classicZipCalls++;
                    break;
            }
        }
    }

    public void RecordRateLimit(ManifestTier tier)
    {
        lock (_lock)
        {
            EnsureDateRoll();
            _rateLimitHits++;
        }
    }

    public QuotaSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            EnsureDateRoll();
            return new QuotaSnapshot(
                _singleManifestCalls,
                _bundleManifestCalls,
                _classicZipCalls,
                _rateLimitHits,
                _lastResetDateUtc);
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _singleManifestCalls = 0;
            _bundleManifestCalls = 0;
            _classicZipCalls = 0;
            _rateLimitHits = 0;
            _lastResetDateUtc = DateTime.UtcNow.Date;
        }
    }

    private void EnsureDateRoll()
    {
        var today = DateTime.UtcNow.Date;
        if (today > _lastResetDateUtc)
        {
            _singleManifestCalls = 0;
            _bundleManifestCalls = 0;
            _classicZipCalls = 0;
            _rateLimitHits = 0;
            _lastResetDateUtc = today;
        }
    }
}
