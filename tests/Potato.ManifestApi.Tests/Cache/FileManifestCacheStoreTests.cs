using System.Text;
using FluentAssertions;
using Potato.Domain.ValueObjects;
using Potato.ManifestApi.Cache;
using Potato.ManifestApi.Models;
using Xunit;

namespace Potato.ManifestApi.Tests.Cache;

public class FileManifestCacheStoreTests
{
    [Fact]
    public async Task SaveAndGet_ShouldRoundTripManifestEntries()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"potato_cache_{Guid.NewGuid():N}");
        try
        {
            var store = new FileManifestCacheStore(tempDir);
            var appId = new AppId(746850);
            var depot1 = new DepotId(746851);
            var gid1 = new ManifestGid(5225699216215765938);
            byte[] content1 = Encoding.UTF8.GetBytes("manifest_data_1");

            var entries = new List<ManifestEntry>
            {
                new(depot1, gid1, content1)
            };

            // Save
            await store.SaveManifestsAsync(appId, "public", entries);

            // Verify full cache hit
            var required = new Dictionary<DepotId, ManifestGid>
            {
                [depot1] = gid1
            };

            var loaded = await store.TryGetCachedManifestsAsync(appId, "public", required);

            loaded.Should().NotBeNull();
            loaded!.Should().HaveCount(1);
            loaded![0].DepotId.Should().Be(depot1);
            loaded[0].ManifestGid.Should().Be(gid1);
            loaded[0].Content.Should().BeEquivalentTo(content1);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task TryGetCachedManifestsAsync_ShouldReturnNull_WhenGidDiffers()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"potato_cache_{Guid.NewGuid():N}");
        try
        {
            var store = new FileManifestCacheStore(tempDir);
            var appId = new AppId(746850);
            var depot1 = new DepotId(746851);
            var oldGid = new ManifestGid(1111111111);
            var newGid = new ManifestGid(2222222222);

            var entries = new List<ManifestEntry>
            {
                new(depot1, oldGid, Encoding.UTF8.GetBytes("old_manifest"))
            };

            await store.SaveManifestsAsync(appId, "public", entries);

            // Request with new GID
            var required = new Dictionary<DepotId, ManifestGid>
            {
                [depot1] = newGid
            };

            var loaded = await store.TryGetCachedManifestsAsync(appId, "public", required);

            loaded.Should().BeNull();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }
}
