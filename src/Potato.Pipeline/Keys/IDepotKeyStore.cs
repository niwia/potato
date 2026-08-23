using Potato.Domain.ValueObjects;

namespace Potato.Pipeline.Keys;

/// <summary>
/// Store for AES depot decryption keys and app tokens.
/// </summary>
public interface IDepotKeyStore : IDisposable
{
    Task<IReadOnlyDictionary<DepotId, string>> GetDepotKeysAsync(
        AppId appId,
        CancellationToken cancellationToken = default);

    Task<AppToken?> GetAppTokenAsync(
        AppId appId,
        CancellationToken cancellationToken = default);

    Task SaveDepotKeysAsync(
        AppId appId,
        IReadOnlyDictionary<DepotId, string> keys,
        CancellationToken cancellationToken = default);

    Task SaveAppTokenAsync(
        AppId appId,
        AppToken token,
        CancellationToken cancellationToken = default);
}
