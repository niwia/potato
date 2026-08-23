using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using Potato.Domain.ValueObjects;
using Potato.ManifestApi.Cache;
using Potato.ManifestApi.Models;
using Potato.ManifestApi.Quota;

namespace Potato.ManifestApi.Client;

/// <summary>
/// Implementation of IHubcapApiClient managing the 4-tier Steam manifest resolution pipeline.
/// </summary>
public sealed class HubcapApiClient : IHubcapApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IManifestCacheStore _cacheStore;
    private readonly QuotaTracker _quotaTracker;
    private readonly HubcapApiOptions _options;

    public HubcapApiClient(
        HttpClient httpClient,
        IManifestCacheStore? cacheStore = null,
        QuotaTracker? quotaTracker = null,
        HubcapApiOptions? options = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? new HubcapApiOptions();
        _cacheStore = cacheStore ?? new FileManifestCacheStore();
        _quotaTracker = quotaTracker ?? new QuotaTracker();
    }

    public async Task<ManifestResolutionResult> ResolveManifestAsync(
        AppId appId,
        string branch,
        IReadOnlyDictionary<DepotId, ManifestGid> requiredDepots,
        CancellationToken cancellationToken = default)
    {
        if (requiredDepots == null || requiredDepots.Count == 0)
        {
            return ManifestResolutionResult.CreateFailure(appId, branch, "No required depots specified.");
        }

        string normalizedBranch = string.IsNullOrWhiteSpace(branch) ? "public" : branch.Trim();

        // ── TIER 0: Local GID Cache Check ──────────────────────────────────────
        var cached = await _cacheStore.TryGetCachedManifestsAsync(appId, normalizedBranch, requiredDepots, cancellationToken);
        if (cached != null && cached.Count == requiredDepots.Count)
        {
            return ManifestResolutionResult.CreateSuccess(appId, normalizedBranch, ManifestTier.Tier0LocalCache, cached);
        }

        // ── Single Depot Target Path ──────────────────────────────────────────
        if (requiredDepots.Count == 1)
        {
            var (depotId, gid) = requiredDepots.First();

            // Tier 1A: Single Manifest API (/generate/manifest)
            byte[]? singleBytes = await GenerateSingleManifestAsync(depotId, gid, cancellationToken);
            if (singleBytes != null && singleBytes.Length > 0)
            {
                var entries = new List<ManifestEntry> { new(depotId, gid, singleBytes) };
                await _cacheStore.SaveManifestsAsync(appId, normalizedBranch, entries, cancellationToken);
                return ManifestResolutionResult.CreateSuccess(appId, normalizedBranch, ManifestTier.Tier1SingleManifest, entries);
            }

            // Tier 1B: Bundle Manifest API (/generate/appmanifest)
            byte[]? bundleBytes = await GenerateBundleManifestAsync(appId, normalizedBranch, cancellationToken);
            if (bundleBytes != null && bundleBytes.Length > 0)
            {
                var extracted = ExtractManifestsFromZipBytes(bundleBytes, requiredDepots);
                if (extracted.Count == requiredDepots.Count)
                {
                    await _cacheStore.SaveManifestsAsync(appId, normalizedBranch, extracted, cancellationToken);
                    return ManifestResolutionResult.CreateSuccess(appId, normalizedBranch, ManifestTier.Tier2BundleManifest, extracted);
                }
            }

            // Tier 1C: Classic Full Manifest Zip (/manifest/{appid})
            byte[]? classicBytes = await DownloadClassicZipAsync(appId, normalizedBranch, cancellationToken);
            if (classicBytes != null && classicBytes.Length > 0)
            {
                var extracted = ExtractManifestsFromZipBytes(classicBytes, requiredDepots);
                if (extracted.Count == requiredDepots.Count)
                {
                    await _cacheStore.SaveManifestsAsync(appId, normalizedBranch, extracted, cancellationToken);
                    return ManifestResolutionResult.CreateSuccess(appId, normalizedBranch, ManifestTier.Tier3ClassicZip, extracted);
                }
            }

            return ManifestResolutionResult.CreateFailure(
                appId, normalizedBranch, $"Failed to resolve manifest for Depot {depotId} across all generation and fallback tiers.");
        }

        // ── Multi-Depot Target Path ───────────────────────────────────────────
        // Tier 2A: Bundle Manifest API (/generate/appmanifest)
        byte[]? multiBundleBytes = await GenerateBundleManifestAsync(appId, normalizedBranch, cancellationToken);
        if (multiBundleBytes != null && multiBundleBytes.Length > 0)
        {
            var extracted = ExtractManifestsFromZipBytes(multiBundleBytes, requiredDepots);
            if (extracted.Count == requiredDepots.Count)
            {
                await _cacheStore.SaveManifestsAsync(appId, normalizedBranch, extracted, cancellationToken);
                return ManifestResolutionResult.CreateSuccess(appId, normalizedBranch, ManifestTier.Tier2BundleManifest, extracted);
            }
        }

        // Tier 2B: Multi-Single Loop (/generate/manifest for each depot)
        var multiSingleEntries = new List<ManifestEntry>(requiredDepots.Count);
        bool allSinglesSuccess = true;

        foreach (var (depotId, gid) in requiredDepots)
        {
            byte[]? sBytes = await GenerateSingleManifestAsync(depotId, gid, cancellationToken);
            if (sBytes != null && sBytes.Length > 0)
            {
                multiSingleEntries.Add(new ManifestEntry(depotId, gid, sBytes));
            }
            else
            {
                allSinglesSuccess = false;
                break;
            }
        }

        if (allSinglesSuccess && multiSingleEntries.Count == requiredDepots.Count)
        {
            await _cacheStore.SaveManifestsAsync(appId, normalizedBranch, multiSingleEntries, cancellationToken);
            return ManifestResolutionResult.CreateSuccess(appId, normalizedBranch, ManifestTier.Tier1SingleManifest, multiSingleEntries);
        }

        // Tier 2C: Classic Full Manifest Zip (/manifest/{appid})
        byte[]? multiClassicBytes = await DownloadClassicZipAsync(appId, normalizedBranch, cancellationToken);
        if (multiClassicBytes != null && multiClassicBytes.Length > 0)
        {
            var extracted = ExtractManifestsFromZipBytes(multiClassicBytes, requiredDepots);
            if (extracted.Count == requiredDepots.Count)
            {
                await _cacheStore.SaveManifestsAsync(appId, normalizedBranch, extracted, cancellationToken);
                return ManifestResolutionResult.CreateSuccess(appId, normalizedBranch, ManifestTier.Tier3ClassicZip, extracted);
            }
        }

        return ManifestResolutionResult.CreateFailure(
            appId, normalizedBranch, $"Failed to resolve {requiredDepots.Count} depots for AppID {appId} across all fallback tiers.");
    }

    public async Task<byte[]?> GenerateSingleManifestAsync(
        DepotId depotId,
        ManifestGid manifestGid,
        CancellationToken cancellationToken = default)
    {
        string url = $"{_options.BaseUrl.TrimEnd('/')}/generate/manifest?depot_id={depotId}&manifest_id={manifestGid}";
        return await SendGetRequestAsync(url, ManifestTier.Tier1SingleManifest, cancellationToken);
    }

    public async Task<byte[]?> GenerateBundleManifestAsync(
        AppId appId,
        string branch = "public",
        CancellationToken cancellationToken = default)
    {
        string normalizedBranch = string.IsNullOrWhiteSpace(branch) ? "public" : branch.Trim();
        string url = $"{_options.BaseUrl.TrimEnd('/')}/generate/appmanifest/{appId}?branch={Uri.EscapeDataString(normalizedBranch)}";
        return await SendGetRequestAsync(url, ManifestTier.Tier2BundleManifest, cancellationToken);
    }

    public async Task<byte[]?> DownloadClassicZipAsync(
        AppId appId,
        string branch = "public",
        CancellationToken cancellationToken = default)
    {
        string normalizedBranch = string.IsNullOrWhiteSpace(branch) ? "public" : branch.Trim();
        string url = $"{_options.BaseUrl.TrimEnd('/')}/manifest/{appId}?branch={Uri.EscapeDataString(normalizedBranch)}";
        return await SendGetRequestAsync(url, ManifestTier.Tier3ClassicZip, cancellationToken);
    }

    private async Task<byte[]?> SendGetRequestAsync(
        string url,
        ManifestTier tier,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey.Trim());
            }

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _quotaTracker.RecordRateLimit(tier);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            byte[] content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (content.Length > 0)
            {
                _quotaTracker.RecordCall(tier);
                return content;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public static List<ManifestEntry> ExtractManifestsFromZipBytes(
        byte[] zipBytes,
        IReadOnlyDictionary<DepotId, ManifestGid> requiredDepots)
    {
        var result = new List<ManifestEntry>();
        if (zipBytes == null || zipBytes.Length == 0) return result;

        try
        {
            using var stream = new MemoryStream(zipBytes);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            var archiveMap = new Dictionary<DepotId, (ManifestGid Gid, ZipArchiveEntry Entry)>();

            foreach (var entry in archive.Entries)
            {
                if (!entry.FullName.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase)) continue;

                string stem = Path.GetFileNameWithoutExtension(entry.FullName);
                var parts = stem.Split('_');
                if (parts.Length == 2 &&
                    DepotId.TryParse(parts[0], out var dId) &&
                    ManifestGid.TryParse(parts[1], out var gid))
                {
                    archiveMap[dId] = (gid, entry);
                }
            }

            foreach (var (reqDepotId, reqGid) in requiredDepots)
            {
                if (archiveMap.TryGetValue(reqDepotId, out var match) && match.Gid == reqGid)
                {
                    using var es = match.Entry.Open();
                    using var ms = new MemoryStream((int)match.Entry.Length);
                    es.CopyTo(ms);
                    result.Add(new ManifestEntry(reqDepotId, reqGid, ms.ToArray()));
                }
            }
        }
        catch
        {
            // Invalid zip archive
        }

        return result;
    }
}
