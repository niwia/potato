using System.IO.Compression;
using System.Net;
using System.Text;
using FluentAssertions;
using Potato.Domain.ValueObjects;
using Potato.ManifestApi.Cache;
using Potato.ManifestApi.Client;
using Potato.ManifestApi.Models;
using Potato.ManifestApi.Quota;
using Xunit;

namespace Potato.ManifestApi.Tests.Client;

public class HubcapApiClientTests
{
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Handler { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.NotFound);
        public List<HttpRequestMessage> RecordedRequests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RecordedRequests.Add(request);
            return Task.FromResult(Handler(request));
        }
    }

    private static byte[] CreateZipArchiveBytes(Dictionary<string, byte[]> files)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (filename, content) in files)
            {
                var entry = archive.CreateEntry(filename);
                using var es = entry.Open();
                es.Write(content);
            }
        }
        return ms.ToArray();
    }

    [Fact]
    public async Task ResolveManifestAsync_ShouldReturnTier0_WhenCacheMatchesAllRequiredDepots()
    {
        var handler = new MockHttpMessageHandler
        {
            Handler = _ => throw new InvalidOperationException("Network should NOT be touched during Tier 0 hit!")
        };

        var appId = new AppId(746850);
        var depotId = new DepotId(746851);
        var gid = new ManifestGid(5225699216215765938);

        string tempCacheDir = Path.Combine(Path.GetTempPath(), $"potato_test_{Guid.NewGuid():N}");
        var cacheStore = new FileManifestCacheStore(tempCacheDir);
        await cacheStore.SaveManifestsAsync(appId, "public", new List<ManifestEntry>
        {
            new(depotId, gid, Encoding.UTF8.GetBytes("sample_manifest_data"))
        });

        try
        {
            var httpClient = new HttpClient(handler);
            var client = new HubcapApiClient(httpClient, cacheStore);

            var required = new Dictionary<DepotId, ManifestGid> { [depotId] = gid };

            var result = await client.ResolveManifestAsync(appId, "public", required);

            result.Success.Should().BeTrue();
            result.TierUsed.Should().Be(ManifestTier.Tier0LocalCache);
            result.Manifests.Should().HaveCount(1);
            handler.RecordedRequests.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(tempCacheDir)) Directory.Delete(tempCacheDir, recursive: true);
        }
    }

    [Fact]
    public async Task ResolveManifestAsync_SingleDepot_ShouldReturnTier1_WhenGenerateManifestSucceeds()
    {
        var appId = new AppId(746850);
        var depotId = new DepotId(746851);
        var gid = new ManifestGid(5225699216215765938);
        byte[] expectedBytes = Encoding.UTF8.GetBytes("decrypted_manifest_binary");

        var handler = new MockHttpMessageHandler
        {
            Handler = req =>
            {
                req.RequestUri!.ToString().Should().Contain("/generate/manifest");
                req.Headers.Authorization!.Scheme.Should().Be("Bearer");
                req.Headers.Authorization!.Parameter.Should().Be("test_api_key");

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(expectedBytes)
                };
            }
        };

        string tempCacheDir = Path.Combine(Path.GetTempPath(), $"potato_test_{Guid.NewGuid():N}");
        var cacheStore = new FileManifestCacheStore(tempCacheDir);
        var quotaTracker = new QuotaTracker();
        var options = new HubcapApiOptions { ApiKey = "test_api_key" };

        try
        {
            var httpClient = new HttpClient(handler);
            var client = new HubcapApiClient(httpClient, cacheStore, quotaTracker, options);

            var required = new Dictionary<DepotId, ManifestGid> { [depotId] = gid };

            var result = await client.ResolveManifestAsync(appId, "public", required);

            result.Success.Should().BeTrue();
            result.TierUsed.Should().Be(ManifestTier.Tier1SingleManifest);
            result.Manifests.Should().HaveCount(1);
            result.Manifests[0].Content.Should().BeEquivalentTo(expectedBytes);

            quotaTracker.GetSnapshot().SingleManifestCalls.Should().Be(1);

            // Verify it was saved to cache store
            var cached = await cacheStore.TryGetCachedManifestsAsync(appId, "public", required);
            cached.Should().NotBeNull();
        }
        finally
        {
            if (Directory.Exists(tempCacheDir)) Directory.Delete(tempCacheDir, recursive: true);
        }
    }

    [Fact]
    public async Task ResolveManifestAsync_SingleDepot_ShouldFallbackToTier2_WhenTier1RateLimited()
    {
        var appId = new AppId(746850);
        var depotId = new DepotId(746851);
        var gid = new ManifestGid(5225699216215765938);
        byte[] manifestBytes = Encoding.UTF8.GetBytes("bundle_manifest_binary");

        byte[] zipBytes = CreateZipArchiveBytes(new Dictionary<string, byte[]>
        {
            [$"{depotId}_{gid}.manifest"] = manifestBytes
        });

        var handler = new MockHttpMessageHandler
        {
            Handler = req =>
            {
                if (req.RequestUri!.ToString().Contains("/generate/manifest"))
                {
                    return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                }

                if (req.RequestUri.ToString().Contains("/generate/appmanifest"))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(zipBytes)
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        };

        string tempCacheDir = Path.Combine(Path.GetTempPath(), $"potato_test_{Guid.NewGuid():N}");
        var cacheStore = new FileManifestCacheStore(tempCacheDir);
        var quotaTracker = new QuotaTracker();
        var options = new HubcapApiOptions { ApiKey = "test_api_key" };

        try
        {
            var httpClient = new HttpClient(handler);
            var client = new HubcapApiClient(httpClient, cacheStore, quotaTracker, options);

            var required = new Dictionary<DepotId, ManifestGid> { [depotId] = gid };

            var result = await client.ResolveManifestAsync(appId, "public", required);

            result.Success.Should().BeTrue();
            result.TierUsed.Should().Be(ManifestTier.Tier2BundleManifest);
            result.Manifests.Should().HaveCount(1);
            result.Manifests[0].Content.Should().BeEquivalentTo(manifestBytes);

            var snapshot = quotaTracker.GetSnapshot();
            snapshot.RateLimitHits.Should().Be(1);
            snapshot.BundleManifestCalls.Should().Be(1);
        }
        finally
        {
            if (Directory.Exists(tempCacheDir)) Directory.Delete(tempCacheDir, recursive: true);
        }
    }

    [Fact]
    public async Task ResolveManifestAsync_MultiDepot_ShouldFallbackToMultiSingle_WhenBundleFails()
    {
        var appId = new AppId(228980);
        var depot1 = new DepotId(228981);
        var gid1 = new ManifestGid(7613356809904826842);
        var depot2 = new DepotId(228982);
        var gid2 = new ManifestGid(6413394087650432851);

        var handler = new MockHttpMessageHandler
        {
            Handler = req =>
            {
                if (req.RequestUri!.ToString().Contains("/generate/appmanifest"))
                {
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                }

                if (req.RequestUri.ToString().Contains("depot_id=228981"))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(Encoding.UTF8.GetBytes("depot1_data"))
                    };
                }

                if (req.RequestUri.ToString().Contains("depot_id=228982"))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(Encoding.UTF8.GetBytes("depot2_data"))
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        };

        string tempCacheDir = Path.Combine(Path.GetTempPath(), $"potato_test_{Guid.NewGuid():N}");
        var cacheStore = new FileManifestCacheStore(tempCacheDir);
        var quotaTracker = new QuotaTracker();

        try
        {
            var httpClient = new HttpClient(handler);
            var client = new HubcapApiClient(httpClient, cacheStore, quotaTracker);

            var required = new Dictionary<DepotId, ManifestGid>
            {
                [depot1] = gid1,
                [depot2] = gid2
            };

            var result = await client.ResolveManifestAsync(appId, "public", required);

            result.Success.Should().BeTrue();
            result.TierUsed.Should().Be(ManifestTier.Tier1SingleManifest);
            result.Manifests.Should().HaveCount(2);
        }
        finally
        {
            if (Directory.Exists(tempCacheDir)) Directory.Delete(tempCacheDir, recursive: true);
        }
    }

    [Fact]
    public async Task ResolveManifestAsync_ShouldFallbackToTier3ClassicZip_WhenAllGenerationEndpointsFail()
    {
        var appId = new AppId(746850);
        var depotId = new DepotId(746851);
        var gid = new ManifestGid(5225699216215765938);

        byte[] zipBytes = CreateZipArchiveBytes(new Dictionary<string, byte[]>
        {
            [$"{depotId}_{gid}.manifest"] = Encoding.UTF8.GetBytes("classic_zip_manifest")
        });

        var handler = new MockHttpMessageHandler
        {
            Handler = req =>
            {
                if (req.RequestUri!.ToString().Contains("/generate/"))
                {
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                }

                if (req.RequestUri.ToString().Contains($"/manifest/{appId}"))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(zipBytes)
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        };

        string tempCacheDir = Path.Combine(Path.GetTempPath(), $"potato_test_{Guid.NewGuid():N}");
        var cacheStore = new FileManifestCacheStore(tempCacheDir);
        var quotaTracker = new QuotaTracker();

        try
        {
            var httpClient = new HttpClient(handler);
            var client = new HubcapApiClient(httpClient, cacheStore, quotaTracker);

            var required = new Dictionary<DepotId, ManifestGid> { [depotId] = gid };

            var result = await client.ResolveManifestAsync(appId, "public", required);

            result.Success.Should().BeTrue();
            result.TierUsed.Should().Be(ManifestTier.Tier3ClassicZip);
            result.Manifests.Should().HaveCount(1);
            quotaTracker.GetSnapshot().ClassicZipCalls.Should().Be(1);
        }
        finally
        {
            if (Directory.Exists(tempCacheDir)) Directory.Delete(tempCacheDir, recursive: true);
        }
    }
}
