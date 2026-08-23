using System.Text.Json;
using Potato.Core.Models;

namespace Potato.Core.Services;

public record StoreSearchResult
{
    public uint AppId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? HeaderUrl { get; init; }
    public string? Price { get; init; }
}

public class SteamApiClient
{
    private readonly HttpClient _httpClient;

    public SteamApiClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Potato; Linux x86_64)");
        _httpClient.Timeout = TimeSpan.FromSeconds(15);
    }

    public async Task<List<StoreSearchResult>> SearchStoreAsync(string term, CancellationToken ct = default)
    {
        var results = new List<StoreSearchResult>();
        if (string.IsNullOrWhiteSpace(term)) return results;

        try
        {
            var url = $"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString(term)}&l=english&cc=US";
            var response = await _httpClient.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(response);

            if (doc.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (item.TryGetProperty("id", out var idProp) && idProp.TryGetUInt32(out var appId))
                    {
                        var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? $"App {appId}" : $"App {appId}";
                        var headerUrl = item.TryGetProperty("tiny_image", out var img) ? img.GetString() : null;
                        
                        // Convert tiny image to header image
                        if (!string.IsNullOrEmpty(headerUrl) && headerUrl.Contains("capsule"))
                        {
                            headerUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/header.jpg";
                        }

                        results.Add(new StoreSearchResult
                        {
                            AppId = appId,
                            Name = name,
                            HeaderUrl = headerUrl ?? $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/header.jpg"
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Store search error: {ex.Message}");
        }

        return results;
    }

    public async Task<(string Name, string? HeaderUrl, List<uint> DlcAppIds, string? ShortDescription)?> GetAppDetailsAsync(uint appId, CancellationToken ct = default)
    {
        try
        {
            var url = $"https://store.steampowered.com/api/appdetails?appids={appId}";
            var response = await _httpClient.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(response);

            if (doc.RootElement.TryGetProperty(appId.ToString(), out var appElement) &&
                appElement.TryGetProperty("success", out var successProp) &&
                successProp.GetBoolean() &&
                appElement.TryGetProperty("data", out var data))
            {
                var name = data.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? $"App {appId}" : $"App {appId}";
                var headerUrl = data.TryGetProperty("header_image", out var headerProp) ? headerProp.GetString() : $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/header.jpg";
                var shortDesc = data.TryGetProperty("short_description", out var descProp) ? descProp.GetString() : null;

                var dlcIds = new List<uint>();
                if (data.TryGetProperty("dlc", out var dlcArray) && dlcArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var dlcItem in dlcArray.EnumerateArray())
                    {
                        if (dlcItem.TryGetUInt32(out var dlcId))
                        {
                            dlcIds.Add(dlcId);
                        }
                    }
                }

                return (name, headerUrl, dlcIds, shortDesc);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching app details for {appId}: {ex.Message}");
        }

        // Fallback default
        return ($"App {appId}", $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/header.jpg", new List<uint>(), null);
    }

    public async Task<List<DepotInfo>> GetDepotsForAppAsync(uint appId, CancellationToken ct = default)
    {
        var depots = new List<DepotInfo>();

        try
        {
            var url = $"https://api.steamcmd.net/v1/info/{appId}";
            var response = await _httpClient.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(response);

            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty(appId.ToString(), out var appData) &&
                appData.TryGetProperty("depots", out var depotsElement) &&
                depotsElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in depotsElement.EnumerateObject())
                {
                    if (uint.TryParse(prop.Name, out var depotId))
                    {
                        var depotVal = prop.Value;
                        if (depotVal.ValueKind != JsonValueKind.Object) continue;

                        string depotName = depotVal.TryGetProperty("name", out var n) ? n.GetString() ?? $"Depot {depotId}" : $"Depot {depotId}";
                        string osList = "";
                        string language = "";
                        ulong manifestId = 0;
                        long sizeBytes = 0;

                        if (depotVal.TryGetProperty("config", out var config) && config.ValueKind == JsonValueKind.Object)
                        {
                            if (config.TryGetProperty("oslist", out var os)) osList = os.GetString() ?? "";
                            if (config.TryGetProperty("language", out var lang)) language = lang.GetString() ?? "";
                        }

                        if (depotVal.TryGetProperty("manifests", out var manifests) && manifests.ValueKind == JsonValueKind.Object)
                        {
                            if (manifests.TryGetProperty("public", out var pubManifest) && pubManifest.ValueKind == JsonValueKind.Object)
                            {
                                if (pubManifest.TryGetProperty("gid", out var gidStr) && ulong.TryParse(gidStr.GetString(), out var gid))
                                {
                                    manifestId = gid;
                                }
                            }
                        }

                        if (depotVal.TryGetProperty("maxsize", out var maxsizeProp) && maxsizeProp.TryGetInt64(out var sz))
                        {
                            sizeBytes = sz;
                        }

                        bool isDlc = depotVal.TryGetProperty("dlcappid", out var dlcAppIdProp);
                        uint dlcAppId = isDlc && dlcAppIdProp.TryGetUInt32(out var did) ? did : 0;

                        depots.Add(new DepotInfo
                        {
                            DepotId = depotId,
                            Name = depotName,
                            ManifestId = manifestId,
                            SizeBytes = sizeBytes,
                            OsList = osList,
                            Language = language,
                            IsDlc = isDlc,
                            DlcAppId = dlcAppId,
                            IsSelected = true
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to fetch depots from steamcmd.net for {appId}: {ex.Message}");
        }

        return depots;
    }
}
