using Potato.Domain.ValueObjects;

namespace Potato.SlsSteam.Config;

/// <summary>
/// Service managing reading, healing, updating, and saving SLSsteam config.yaml.
/// </summary>
public interface ISlsConfigManager
{
    Task<SlsConfigModel> LoadAsync(string? configPath = null, CancellationToken cancellationToken = default);

    Task SaveAsync(SlsConfigModel model, string? configPath = null, CancellationToken cancellationToken = default);

    Task<bool> AddAdditionalAppAsync(AppId appId, string gameName = "", string? configPath = null, CancellationToken cancellationToken = default);

    Task<bool> RemoveAdditionalAppAsync(AppId appId, string? configPath = null, CancellationToken cancellationToken = default);

    Task<bool> AddAppTokenAsync(AppId appId, AppToken token, string? comment = null, string? configPath = null, CancellationToken cancellationToken = default);

    Task<bool> AddDlcDataAsync(AppId appId, DepotId dlcId, string dlcName, string? configPath = null, CancellationToken cancellationToken = default);

    Task<bool> SetFakeAppIdAsync(AppId appId, AppId fakeAppId, string? comment = null, string? configPath = null, CancellationToken cancellationToken = default);

    Task<bool> EnsurePrerequisitesAsync(string? configPath = null, CancellationToken cancellationToken = default);

    Task<string?> CreateBackupAsync(string? configPath = null, CancellationToken cancellationToken = default);
}
