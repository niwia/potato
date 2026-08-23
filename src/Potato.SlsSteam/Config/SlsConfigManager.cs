using Potato.Domain.ValueObjects;
using Potato.SlsSteam.Paths;

namespace Potato.SlsSteam.Config;

/// <summary>
/// Default implementation of ISlsConfigManager.
/// </summary>
public sealed class SlsConfigManager : ISlsConfigManager
{
    private readonly ISlsSteamPathResolver _pathResolver;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SlsConfigManager(ISlsSteamPathResolver? pathResolver = null)
    {
        _pathResolver = pathResolver ?? new SlsSteamPathResolver();
    }

    private string ResolvePath(string? explicitPath) =>
        !string.IsNullOrWhiteSpace(explicitPath) ? explicitPath : _pathResolver.ConfigPath;

    public async Task<SlsConfigModel> LoadAsync(string? configPath = null, CancellationToken cancellationToken = default)
    {
        string path = ResolvePath(configPath);
        if (!File.Exists(path))
        {
            var defaultModel = new SlsConfigModel();
            SlsConfigHealer.EnsurePrerequisites(defaultModel);
            return defaultModel;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            string content = await File.ReadAllTextAsync(path, cancellationToken);
            return SlsConfigHealer.ParseAndHeal(content);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(SlsConfigModel model, string? configPath = null, CancellationToken cancellationToken = default)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        string path = ResolvePath(configPath);
        await CreateBackupAsync(path, cancellationToken);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            SlsConfigHealer.EnsurePrerequisites(model);
            string yaml = SlsConfigHealer.Serialize(model);
            await SlsConfigHealer.AtomicWriteInPlaceAsync(path, yaml, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> AddAdditionalAppAsync(AppId appId, string gameName = "", string? configPath = null, CancellationToken cancellationToken = default)
    {
        if (!appId.IsValid) return false;

        var model = await LoadAsync(configPath, cancellationToken);
        string idStr = appId.ToString();

        // Check if already present
        if (model.AdditionalApps.Any(a => a.Value.Split('#')[0].Trim() == idStr))
        {
            return false;
        }

        model.AdditionalApps.Add(new SlsConfigEntry(null, idStr, !string.IsNullOrWhiteSpace(gameName) ? gameName : null));
        await SaveAsync(model, configPath, cancellationToken);
        return true;
    }

    public async Task<bool> RemoveAdditionalAppAsync(AppId appId, string? configPath = null, CancellationToken cancellationToken = default)
    {
        if (!appId.IsValid) return false;

        var model = await LoadAsync(configPath, cancellationToken);
        string idStr = appId.ToString();

        int removed = model.AdditionalApps.RemoveAll(a => a.Value.Split('#')[0].Trim() == idStr);
        if (removed > 0)
        {
            await SaveAsync(model, configPath, cancellationToken);
            return true;
        }

        return false;
    }

    public async Task<bool> AddAppTokenAsync(AppId appId, AppToken token, string? comment = null, string? configPath = null, CancellationToken cancellationToken = default)
    {
        if (!appId.IsValid || !token.IsValid) return false;

        var model = await LoadAsync(configPath, cancellationToken);
        string idStr = appId.ToString();

        model.AppTokens[idStr] = new SlsConfigEntry(idStr, token.ToString(), comment);
        await SaveAsync(model, configPath, cancellationToken);
        return true;
    }

    public async Task<bool> AddDlcDataAsync(AppId appId, DepotId dlcId, string dlcName, string? configPath = null, CancellationToken cancellationToken = default)
    {
        if (!appId.IsValid || !dlcId.IsValid) return false;

        var model = await LoadAsync(configPath, cancellationToken);
        string idStr = appId.ToString();
        string dlcStr = dlcId.ToString();

        if (!model.DlcData.TryGetValue(idStr, out var dlcMap))
        {
            dlcMap = new Dictionary<string, SlsConfigEntry>(StringComparer.OrdinalIgnoreCase);
            model.DlcData[idStr] = dlcMap;
        }

        dlcMap[dlcStr] = new SlsConfigEntry(dlcStr, $"\"{dlcName}\"");
        await SaveAsync(model, configPath, cancellationToken);
        return true;
    }

    public async Task<bool> SetFakeAppIdAsync(AppId appId, AppId fakeAppId, string? comment = null, string? configPath = null, CancellationToken cancellationToken = default)
    {
        if (!appId.IsValid || !fakeAppId.IsValid) return false;

        var model = await LoadAsync(configPath, cancellationToken);
        string idStr = appId.ToString();

        model.FakeAppIds[idStr] = new SlsConfigEntry(idStr, fakeAppId.ToString(), comment);
        await SaveAsync(model, configPath, cancellationToken);
        return true;
    }

    public async Task<bool> EnsurePrerequisitesAsync(string? configPath = null, CancellationToken cancellationToken = default)
    {
        var model = await LoadAsync(configPath, cancellationToken);
        bool needsUpdate = !model.Api || string.IsNullOrWhiteSpace(model.LogLevels) || !model.LogLevels.Contains("0x2", StringComparison.OrdinalIgnoreCase);

        if (needsUpdate)
        {
            await SaveAsync(model, configPath, cancellationToken);
            return true;
        }

        return false;
    }

    public Task<string?> CreateBackupAsync(string? configPath = null, CancellationToken cancellationToken = default)
    {
        string path = ResolvePath(configPath);
        if (!File.Exists(path)) return Task.FromResult<string?>(null);

        try
        {
            string bakPath = path + ".bak";
            if (!File.Exists(bakPath))
            {
                File.Copy(path, bakPath);
                return Task.FromResult<string?>(bakPath);
            }

            // Backup rotation
            int i = 2;
            while (i < 100)
            {
                string rotPath = path + $".bak{i}";
                if (!File.Exists(rotPath))
                {
                    File.Copy(path, rotPath);
                    return Task.FromResult<string?>(rotPath);
                }
                i++;
            }

            File.Copy(path, bakPath, overwrite: true);
            return Task.FromResult<string?>(bakPath);
        }
        catch
        {
            return Task.FromResult<string?>(null);
        }
    }
}
