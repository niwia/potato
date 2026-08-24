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
    private readonly Func<HubcapApiOptions> _optionsProvider;

    public HubcapApiClient(
        HttpClient httpClient,
        IManifestCacheStore? cacheStore = null,
        QuotaTracker? quotaTracker = null,
        HubcapApiOptions? options = null)
        : this(httpClient, cacheStore, quotaTracker, () => options ?? new HubcapApiOptions())
    {
    }

    public HubcapApiClient(
        HttpClient httpClient,
        IManifestCacheStore? cacheStore,
        QuotaTracker? quotaTracker,
        Func<HubcapApiOptions> optionsProvider)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _optionsProvider = optionsProvider ?? throw new ArgumentNullException(nameof(optionsProvider));
        _cacheStore = cacheStore ?? new FileManifestCacheStore();
        _quotaTracker = quotaTracker ?? new QuotaTracker();
    }

    private HubcapApiOptions CurrentOptions => _optionsProvider();

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
        string url = $"{CurrentOptions.BaseUrl.TrimEnd('/')}/generate/manifest?depot_id={depotId}&manifest_id={manifestGid}";
        return await SendGetRequestAsync(url, ManifestTier.Tier1SingleManifest, cancellationToken);
    }

    public async Task<byte[]?> GenerateBundleManifestAsync(
        AppId appId,
        string branch = "public",
        CancellationToken cancellationToken = default)
    {
        string normalizedBranch = string.IsNullOrWhiteSpace(branch) ? "public" : branch.Trim();
        string url = $"{CurrentOptions.BaseUrl.TrimEnd('/')}/generate/appmanifest/{appId}?branch={Uri.EscapeDataString(normalizedBranch)}";
        return await SendGetRequestAsync(url, ManifestTier.Tier2BundleManifest, cancellationToken);
    }

    public async Task<byte[]?> DownloadClassicZipAsync(
        AppId appId,
        string branch = "public",
        CancellationToken cancellationToken = default)
    {
        string normalizedBranch = string.IsNullOrWhiteSpace(branch) ? "public" : branch.Trim();
        string url = $"{CurrentOptions.BaseUrl.TrimEnd('/')}/manifest/{appId}?branch={Uri.EscapeDataString(normalizedBranch)}";
        return await SendGetRequestAsync(url, ManifestTier.Tier3ClassicZip, cancellationToken);
    }

    public async Task<IReadOnlyList<HubcapSearchResult>> SearchGamesAsync(
        string query,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<HubcapSearchResult>();

        string trimmedQuery = query.Trim();
        int normalizedLimit = Math.Clamp(limit, 1, 100);
        var results = new List<HubcapSearchResult>();

        try
        {
            // ── Path 1: Numeric Query - Try exact AppID search first ──────────
            if (uint.TryParse(trimmedQuery, out uint appIdNum))
            {
                string appIdUrl = $"{CurrentOptions.BaseUrl.TrimEnd('/')}/search?q={appIdNum}&appid=true&limit={normalizedLimit}";
                var appIdResults = await FetchSearchResultsFromUrlAsync(appIdUrl, cancellationToken);
                if (appIdResults.Count > 0)
                {
                    return appIdResults;
                }
            }

            // ── Path 2: Name Search via /search endpoint ─────────────────────
            string searchUrl = $"{CurrentOptions.BaseUrl.TrimEnd('/')}/search?q={Uri.EscapeDataString(trimmedQuery)}&limit={normalizedLimit}";
            var nameResults = await FetchSearchResultsFromUrlAsync(searchUrl, cancellationToken);
            if (nameResults.Count > 0)
            {
                return nameResults;
            }

            // ── Path 3: Fallback to /library endpoint ─────────────────────────
            string libUrl = $"{CurrentOptions.BaseUrl.TrimEnd('/')}/library?search={Uri.EscapeDataString(trimmedQuery)}&limit={normalizedLimit}&sort_by=name";
            return await FetchSearchResultsFromUrlAsync(libUrl, cancellationToken);
        }
        catch
        {
            return Array.Empty<HubcapSearchResult>();
        }
    }

    private async Task<IReadOnlyList<HubcapSearchResult>> FetchSearchResultsFromUrlAsync(
        string url,
        CancellationToken cancellationToken)
    {
        var list = new List<HubcapSearchResult>();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrWhiteSpace(CurrentOptions.ApiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CurrentOptions.ApiKey.Trim());
            }

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode) return list;

            var jsonNode = await System.Text.Json.Nodes.JsonNode.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            if (jsonNode == null) return list;

            System.Text.Json.Nodes.JsonArray? resultsArr = null;
            if (jsonNode is System.Text.Json.Nodes.JsonObject obj)
            {
                resultsArr = obj["results"] as System.Text.Json.Nodes.JsonArray ?? obj["games"] as System.Text.Json.Nodes.JsonArray;
            }
            else if (jsonNode is System.Text.Json.Nodes.JsonArray arr)
            {
                resultsArr = arr;
            }

            if (resultsArr == null) return list;

            foreach (var item in resultsArr)
            {
                if (item is not System.Text.Json.Nodes.JsonObject gObj) continue;

                string? rawId = gObj["app_id"]?.ToString() ?? gObj["appid"]?.ToString();
                if (!AppId.TryParse(rawId, out var appId)) continue;

                string name = gObj["name"]?.ToString() ?? gObj["title"]?.ToString() ?? $"App {appId}";
                ulong size = 0;
                if (ulong.TryParse(gObj["manifest_size"]?.ToString() ?? gObj["size"]?.ToString(), out ulong parsedSize))
                {
                    size = parsedSize;
                }

                bool available = true;
                if (gObj["manifest_available"] != null && bool.TryParse(gObj["manifest_available"]?.ToString(), out bool parsedAvail))
                {
                    available = parsedAvail;
                }

                string? image = gObj["image"]?.ToString() ?? gObj["header_image"]?.ToString() ?? gObj["thumbnail"]?.ToString();
                if (string.IsNullOrWhiteSpace(image))
                {
                    image = $"https://shared.cloudflare.steamstatic.com/store_item_assets/steam/apps/{appId.Value}/header.jpg";
                }

                string? denuvo = gObj["denuvo"]?.ToString();
                string? protonDb = gObj["protondb"]?.ToString();

                list.Add(new HubcapSearchResult(appId, name, size, available, image, denuvo, protonDb));
            }
        }
        catch { }

        return list;
    }

    public async Task<HubcapAllStats> GetAllStatsAsync(CancellationToken cancellationToken = default)
    {
        var userStats = new HubcapUserStats();
        var genUsage = new HubcapGenerateUsage();
        bool healthy = true;

        try
        {
            string baseUrl = CurrentOptions.BaseUrl.TrimEnd('/');

            // 1. User stats (/user/stats)
            if (!string.IsNullOrWhiteSpace(CurrentOptions.ApiKey))
            {
                string userStatsUrl = $"{baseUrl}/user/stats?api_key={Uri.EscapeDataString(CurrentOptions.ApiKey.Trim())}";
                using var req1 = new HttpRequestMessage(HttpMethod.Get, userStatsUrl);
                req1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CurrentOptions.ApiKey.Trim());

                using var res1 = await _httpClient.SendAsync(req1, cancellationToken);
                if (res1.IsSuccessStatusCode)
                {
                    var node = await System.Text.Json.Nodes.JsonNode.ParseAsync(await res1.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
                    if (node is System.Text.Json.Nodes.JsonObject obj)
                    {
                        int downloads = obj["daily_manifest_downloads"]?.GetValue<int>() ?? obj["manifest_downloads_today"]?.GetValue<int>() ?? 0;
                        int limit = obj["daily_manifest_limit"]?.GetValue<int>() ?? 55;
                        string? expires = obj["expires_at"]?.ToString() ?? obj["expiry"]?.ToString();
                        int? days = null;
                        if (DateTime.TryParse(expires, out var dt))
                        {
                            days = Math.Max(0, (int)(dt - DateTime.UtcNow).TotalDays);
                        }
                        string? plan = obj["plan"]?.ToString();

                        userStats = new HubcapUserStats(downloads, limit, expires, days, plan);
                    }
                }
            }

            // 2. Generate usage (/generate/usage)
            if (!string.IsNullOrWhiteSpace(CurrentOptions.ApiKey))
            {
                string genUrl = $"{baseUrl}/generate/usage";
                using var req2 = new HttpRequestMessage(HttpMethod.Get, genUrl);
                req2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CurrentOptions.ApiKey.Trim());

                using var res2 = await _httpClient.SendAsync(req2, cancellationToken);
                if (res2.IsSuccessStatusCode)
                {
                    var node = await System.Text.Json.Nodes.JsonNode.ParseAsync(await res2.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
                    if (node is System.Text.Json.Nodes.JsonObject obj)
                    {
                        int bUse = obj["app_bundle_usage"]?.GetValue<int>() ?? obj["bundle_usage"]?.GetValue<int>() ?? 0;
                        int bLim = obj["app_bundle_limit"]?.GetValue<int>() ?? 100;
                        int sUse = obj["single_depot_usage"]?.GetValue<int>() ?? obj["single_usage"]?.GetValue<int>() ?? 0;
                        int sLim = obj["single_depot_limit"]?.GetValue<int>() ?? 1500;

                        genUsage = new HubcapGenerateUsage(bUse, bLim, sUse, sLim);
                    }
                }
            }

            healthy = await CheckHealthAsync(cancellationToken);
        }
        catch
        {
            healthy = false;
        }

        return new HubcapAllStats(userStats, genUsage, healthy);
    }

    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            string healthUrl = $"{CurrentOptions.BaseUrl.TrimEnd('/')}/health";
            using var req = new HttpRequestMessage(HttpMethod.Get, healthUrl);
            using var res = await _httpClient.SendAsync(req, cancellationToken);
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<byte[]?> SendGetRequestAsync(
        string url,
        ManifestTier tier,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrWhiteSpace(CurrentOptions.ApiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CurrentOptions.ApiKey.Trim());
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
