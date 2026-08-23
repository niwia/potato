using Potato.Domain.ValueObjects;

namespace Potato.SteamMetadata.Clients;

public sealed record SteamStoreDetails(
    string? Name,
    string? HeaderUrl,
    string? InstallDir,
    IReadOnlyDictionary<DepotId, string?>? DepotSizes);

/// <summary>
/// Client for querying public Storefront details from Steam Web API.
/// </summary>
public interface ISteamStoreWebClient
{
    Task<SteamStoreDetails?> FetchStoreDetailsAsync(
        AppId appId,
        CancellationToken cancellationToken = default);
}
