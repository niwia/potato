using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Potato.Domain.ValueObjects;
using Potato.SteamMetadata.Models;
using Potato.SteamMetadata.Storage;

namespace Potato.SteamMetadata.Clients;

/// <summary>
/// Implementation of ISteamCmdRestClient matching the retry and timeout parameters from the reference code.
/// 2 retries, 0.3s delay between, 5s timeout per attempt.
/// </summary>
public sealed class SteamCmdRestClient : ISteamCmdRestClient
{
    private const string BaseUrl = "https://api.steamcmd.net/v1/info/";
    private const int MaxRetries = 2;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    private readonly HttpClient _httpClient;

    public SteamCmdRestClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<SteamAppMetadata?> FetchAppInfoAsync(
        AppId appId,
        CancellationToken cancellationToken = default)
    {
        if (!appId.IsValid) return null;

        string url = $"{BaseUrl}{appId}";

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(RequestTimeout);

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await _httpClient.SendAsync(request, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var rootNode = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cts.Token);
                    if (rootNode is JsonObject root &&
                        string.Equals(root["status"]?.ToString(), "success", StringComparison.OrdinalIgnoreCase))
                    {
                        var parsed = ParseSteamCmdPayload(appId, root);
                        if (parsed != null && parsed.Depots.Count > 0)
                        {
                            return parsed;
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout on this attempt, continue to next retry
            }
            catch
            {
                // Network error on this attempt, continue to next retry
            }

            if (attempt < MaxRetries - 1)
            {
                try
                {
                    await Task.Delay(RetryDelay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        return null;
    }

    private static SteamAppMetadata? ParseSteamCmdPayload(AppId appId, JsonObject root)
    {
        var dataNode = root["data"] as JsonObject;
        var appData = dataNode?[appId.ToString()] as JsonObject;
        if (appData == null) return null;

        string? appName = appData["common"]?["name"]?.ToString();
        string? installDir = appData["config"]?["installdir"]?.ToString();
        string? headerUrl = SqliteSteamMetadataStore.ConstructFullUrl($"{appId}/header.jpg");

        var depotsNode = appData["depots"] as JsonObject;
        var branchesNode = depotsNode?["branches"] as JsonObject;

        var branches = new Dictionary<string, SteamBranchInfo>();
        string? buildId = null;
        string? timeUpdated = null;

        if (branchesNode != null)
        {
            foreach (var (bName, bVal) in branchesNode)
            {
                if (bVal is JsonObject bObj)
                {
                    string? bBuildId = bObj["buildid"]?.ToString();
                    string? bTimeUpdated = bObj["timeupdated"]?.ToString();
                    bool pwdRequired = bObj["pwdrequired"]?.ToString() == "1";
                    branches[bName] = new SteamBranchInfo(bName, bBuildId, bTimeUpdated, pwdRequired);

                    if (string.Equals(bName, "public", StringComparison.OrdinalIgnoreCase))
                    {
                        buildId = bBuildId;
                        timeUpdated = bTimeUpdated;
                    }
                }
            }
        }

        var depots = new Dictionary<DepotId, SteamDepotInfo>();
        if (depotsNode != null)
        {
            foreach (var (dKey, dVal) in depotsNode)
            {
                if (string.Equals(dKey, "branches", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(dKey, "workshopdepots", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(dKey, "branches_public", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!DepotId.TryParse(dKey, out var depotId) || dVal is not JsonObject dObj)
                {
                    continue;
                }

                var config = dObj["config"] as JsonObject;
                var manifests = dObj["manifests"] as JsonObject;

                string? name = dObj["name"]?.ToString();
                string? osList = config?["oslist"]?.ToString();
                string? language = config?["language"]?.ToString();
                bool steamdeck = config?["steamdeck"]?.ToString() == "1";

                ManifestGid? publicManifestGid = null;
                var manifestsMap = new Dictionary<string, ManifestGid>();

                if (manifests != null)
                {
                    foreach (var (mBranch, mVal) in manifests)
                    {
                        string? gidStr = null;
                        if (mVal is JsonObject mDict)
                        {
                            gidStr = mDict["gid"]?.ToString();
                        }
                        else if (mVal != null)
                        {
                            gidStr = mVal.ToString();
                        }

                        if (ManifestGid.TryParse(gidStr, out var gid))
                        {
                            manifestsMap[mBranch] = gid;
                            if (string.Equals(mBranch, "public", StringComparison.OrdinalIgnoreCase))
                            {
                                publicManifestGid = gid;
                            }
                        }
                    }
                }

                depots[depotId] = new SteamDepotInfo(
                    depotId,
                    name,
                    osList,
                    language,
                    steamdeck,
                    size: null,
                    publicManifestGid,
                    manifestsMap);
            }
        }

        return new SteamAppMetadata(
            appId,
            appName,
            installDir,
            headerUrl,
            buildId,
            timeUpdated,
            depots,
            branches,
            source: "steamcmd");
    }
}
