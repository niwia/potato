using Potato.Domain.ValueObjects;
using Potato.SteamMetadata.Models;

namespace Potato.SteamMetadata.Clients;

/// <summary>
/// Client for querying live Steam PICS product information via SteamKit2.
/// </summary>
public interface ISteamPicsClient : IDisposable
{
    Task<SteamAppMetadata?> FetchProductInfoAsync(
        AppId appId,
        AppToken? appToken = null,
        CancellationToken cancellationToken = default);
}
