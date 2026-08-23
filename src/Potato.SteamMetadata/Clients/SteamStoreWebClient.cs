using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Potato.Domain.ValueObjects;

namespace Potato.SteamMetadata.Clients;

/// <summary>
/// Implementation of ISteamStoreWebClient querying store.steampowered.com/api/appdetails.
/// </summary>
public sealed class SteamStoreWebClient : ISteamStoreWebClient
{
    private const string BaseUrl = "https://store.steampowered.com/api/appdetails?appids=";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private readonly HttpClient _httpClient;

    public SteamStoreWebClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<SteamStoreDetails?> FetchStoreDetailsAsync(
        AppId appId,
        CancellationToken cancellationToken = default)
    {
        if (!appId.IsValid) return null;

        string url = $"{BaseUrl}{appId}";

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(Timeout);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await _httpClient.SendAsync(request, cts.Token);

            if (!response.IsSuccessStatusCode) return null;

            var rootNode = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cts.Token);
            if (rootNode is not JsonObject root) return null;

            var appWrapper = root[appId.ToString()] as JsonObject;
            if (appWrapper == null || appWrapper["success"]?.GetValue<bool>() != true) return null;

            var dataObj = appWrapper["data"] as JsonObject;
            if (dataObj == null) return null;

            string? name = dataObj["name"]?.ToString();
            string? headerImage = dataObj["header_image"]?.ToString();
            string? installDir = dataObj["install_dir"]?.ToString();

            var depotSizes = new Dictionary<DepotId, string?>();
            if (dataObj["depots"] is JsonObject depotsNode)
            {
                foreach (var (dKey, dVal) in depotsNode)
                {
                    if (DepotId.TryParse(dKey, out var dId) && dVal is JsonObject dObj)
                    {
                        depotSizes[dId] = dObj["max_size"]?.ToString();
                    }
                }
            }

            return new SteamStoreDetails(name, headerImage, installDir, depotSizes);
        }
        catch
        {
            return null;
        }
    }
}
