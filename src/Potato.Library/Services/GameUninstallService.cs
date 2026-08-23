using Potato.Library.Models;
using Potato.SlsSteam.Config;
using Potato.SlsSteam.Ipc;
using Potato.SlsSteam.Paths;

namespace Potato.Library.Services;

/// <summary>
/// Default implementation of IGameUninstallService.
/// </summary>
public sealed class GameUninstallService : IGameUninstallService
{
    private readonly ISlsConfigManager _slsConfigManager;
    private readonly ISlsSteamIpcClient _slsIpcClient;

    public GameUninstallService(
        ISlsConfigManager? slsConfigManager = null,
        ISlsSteamIpcClient? slsIpcClient = null,
        ISlsSteamPathResolver? pathResolver = null)
    {
        var resolver = pathResolver ?? new SlsSteamPathResolver();
        _slsConfigManager = slsConfigManager ?? new SlsConfigManager(resolver);
        _slsIpcClient = slsIpcClient ?? new SlsSteamIpcClient(resolver);
    }

    public async Task<bool> UninstallGameAsync(
        InstalledGame game,
        bool removeFromSlsConfig = true,
        CancellationToken cancellationToken = default)
    {
        if (game == null || !game.AppId.IsValid) return false;

        bool success = true;

        // 1. Delete ACF manifest file
        if (File.Exists(game.AcfPath))
        {
            try
            {
                File.Delete(game.AcfPath);
            }
            catch
            {
                success = false;
            }
        }

        // 2. Delete game installation directory
        if (Directory.Exists(game.FullGamePath))
        {
            try
            {
                Directory.Delete(game.FullGamePath, recursive: true);
            }
            catch
            {
                success = false;
            }
        }

        // 3. SLSsteam deregistration & IPC command
        if (removeFromSlsConfig)
        {
            try
            {
                await _slsConfigManager.RemoveAdditionalAppAsync(game.AppId, cancellationToken: cancellationToken);

                if (_slsIpcClient.IsPipeAvailable)
                {
                    await _slsIpcClient.UninstallAppAsync(game.AppId, cancellationToken);
                }
            }
            catch
            {
                // Non-fatal error during SLS deregistration
            }
        }

        return success;
    }
}
