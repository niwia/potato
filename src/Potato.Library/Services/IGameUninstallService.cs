using Potato.Library.Models;

namespace Potato.Library.Services;

/// <summary>
/// Service that uninstalls a game, deletes files and ACF manifests, and unregisters from SLSsteam.
/// </summary>
public interface IGameUninstallService
{
    Task<bool> UninstallGameAsync(
        InstalledGame game,
        bool removeFromSlsConfig = true,
        CancellationToken cancellationToken = default);
}
