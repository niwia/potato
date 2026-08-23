using FluentAssertions;
using Potato.ManifestApi.Models;
using Potato.ManifestApi.Quota;
using Xunit;

namespace Potato.ManifestApi.Tests.Quota;

public class QuotaTrackerTests
{
    [Fact]
    public void RecordCall_ShouldIncrementCorrectTierPools()
    {
        var tracker = new QuotaTracker();

        tracker.RecordCall(ManifestTier.Tier1SingleManifest);
        tracker.RecordCall(ManifestTier.Tier1SingleManifest);
        tracker.RecordCall(ManifestTier.Tier2BundleManifest);
        tracker.RecordCall(ManifestTier.Tier3ClassicZip);
        tracker.RecordRateLimit(ManifestTier.Tier1SingleManifest);

        var snapshot = tracker.GetSnapshot();

        snapshot.SingleManifestCalls.Should().Be(2);
        snapshot.BundleManifestCalls.Should().Be(1);
        snapshot.ClassicZipCalls.Should().Be(1);
        snapshot.RateLimitHits.Should().Be(1);
    }

    [Fact]
    public void Reset_ShouldClearAllCounters()
    {
        var tracker = new QuotaTracker();
        tracker.RecordCall(ManifestTier.Tier1SingleManifest);
        tracker.RecordRateLimit(ManifestTier.Tier1SingleManifest);

        tracker.Reset();

        var snapshot = tracker.GetSnapshot();
        snapshot.SingleManifestCalls.Should().Be(0);
        snapshot.RateLimitHits.Should().Be(0);
    }
}
