using Potato.Domain.ValueObjects;
using Potato.SteamMetadata.Models;

namespace Potato.SteamMetadata.Clients;

/// <summary>
/// Client for fetching metadata from the SteamCMD REST API (https://api.steamcmd.net/v1/info/{appid}).
/// </summary>
public interface ISteamCmdRestClient
{
    Task<SteamAppMetadata?> FetchAppInfoAsync(
        AppId appId,
        CancellationToken cancellationToken = default);
}
