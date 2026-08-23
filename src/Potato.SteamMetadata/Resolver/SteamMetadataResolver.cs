using System.Text.RegularExpressions;
using Potato.Domain.ValueObjects;
using Potato.SteamMetadata.Clients;
using Potato.SteamMetadata.Models;
using Potato.SteamMetadata.Storage;

namespace Potato.SteamMetadata.Resolver;

/// <summary>
/// Implementation of ISteamMetadataResolver matching the 4-layer resolution strategy.
/// </summary>
public sealed class SteamMetadataResolver : ISteamMetadataResolver
{
    private readonly ISteamMetadataStore _store;
    private readonly ISteamCmdRestClient _steamCmdClient;
    private readonly ISteamPicsClient _picsClient;
    private readonly ISteamStoreWebClient _storeWebClient;

    public SteamMetadataResolver(
        ISteamMetadataStore store,
        ISteamCmdRestClient steamCmdClient,
        ISteamPicsClient picsClient,
        ISteamStoreWebClient storeWebClient)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _steamCmdClient = steamCmdClient ?? throw new ArgumentNullException(nameof(steamCmdClient));
        _picsClient = picsClient ?? throw new ArgumentNullException(nameof(picsClient));
        _storeWebClient = storeWebClient ?? throw new ArgumentNullException(nameof(storeWebClient));
    }

    public async Task<SteamAppMetadata?> ResolveAppMetadataAsync(
        AppId appId,
        AppToken? appToken = null,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (!appId.IsValid) return null;

        // ── 1. LOCAL DB CACHE CHECK ───────────────────────────────────────────
        if (!forceRefresh)
        {
            var cached = await _store.GetAppInfoAsync(appId, bypassExpiration: false, cancellationToken);
            if (cached != null && cached.Depots.Count > 0 && !IsGenericName(cached.Name, appId))
            {
                return cached;
            }
        }

        // ── 2. STEAMCMD REST API & STOREFRONT WEB API (IN PARALLEL) ───────────
        var steamCmdTask = _steamCmdClient.FetchAppInfoAsync(appId, cancellationToken);
        var storeDetailsTask = _storeWebClient.FetchStoreDetailsAsync(appId, cancellationToken);

        await Task.WhenAll(steamCmdTask, storeDetailsTask);

        var steamCmdData = await steamCmdTask;
        var storeDetails = await storeDetailsTask;

        SteamAppMetadata? primaryData = null;

        // ── 3. STEAM PICS FALLBACK (IF STEAMCMD HAD NO DEPOTS) ─────────────────
        if (steamCmdData != null && steamCmdData.Depots.Count > 0)
        {
            primaryData = steamCmdData;
        }
        else
        {
            primaryData = await _picsClient.FetchProductInfoAsync(appId, appToken, cancellationToken);
        }

        // ── 4. STOREFRONT BACKFILL & MERGE ────────────────────────────────────
        if (primaryData == null && storeDetails != null)
        {
            primaryData = new SteamAppMetadata(
                appId,
                name: storeDetails.Name,
                installDir: storeDetails.InstallDir,
                headerUrl: storeDetails.HeaderUrl,
                source: "web_api");
        }
        else if (primaryData != null && storeDetails != null)
        {
            string? mergedName = string.IsNullOrWhiteSpace(primaryData.Name) || IsGenericName(primaryData.Name, appId)
                ? (storeDetails.Name ?? primaryData.Name)
                : primaryData.Name;

            string? mergedHeaderUrl = !string.IsNullOrWhiteSpace(storeDetails.HeaderUrl)
                ? storeDetails.HeaderUrl
                : primaryData.HeaderUrl;

            string? mergedInstallDir = !string.IsNullOrWhiteSpace(primaryData.InstallDir)
                ? primaryData.InstallDir
                : storeDetails.InstallDir;

            // Backfill depot sizes if missing
            var mergedDepots = new Dictionary<DepotId, SteamDepotInfo>();
            foreach (var (dId, dInfo) in primaryData.Depots)
            {
                string? size = dInfo.Size;
                if (string.IsNullOrWhiteSpace(size) && storeDetails.DepotSizes != null && storeDetails.DepotSizes.TryGetValue(dId, out var sSize))
                {
                    size = sSize;
                }

                mergedDepots[dId] = dInfo with { Size = size };
            }

            primaryData = primaryData with
            {
                Name = mergedName,
                HeaderUrl = mergedHeaderUrl,
                InstallDir = mergedInstallDir,
                Depots = mergedDepots
            };
        }

        // ── 5. PERSIST TO DB CACHE ────────────────────────────────────────────
        if (primaryData != null && (primaryData.Depots.Count > 0 || !string.IsNullOrWhiteSpace(primaryData.Name)))
        {
            await _store.UpsertAppInfoAsync(appId, primaryData, cancellationToken);
        }

        return primaryData;
    }

    public static bool IsGenericName(string? name, AppId appId)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;
        return Regex.IsMatch(name.Trim(), $@"^App[ _]?{appId}$", RegexOptions.IgnoreCase);
    }
}
