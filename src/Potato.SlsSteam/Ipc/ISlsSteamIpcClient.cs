using Potato.Domain.ValueObjects;

namespace Potato.SlsSteam.Ipc;

/// <summary>
/// Client communicating with SLSsteam via /tmp/SLSsteam.API named pipe and log inspection.
/// </summary>
public interface ISlsSteamIpcClient
{
    bool IsPipeAvailable { get; }
    bool IsSlsSteamActive { get; }

    Task<bool> SendCommandAsync(string command, CancellationToken cancellationToken = default);

    Task<bool> InstallAppAsync(AppId appId, int libraryIndex = 0, CancellationToken cancellationToken = default);

    Task<bool> UninstallAppAsync(AppId appId, CancellationToken cancellationToken = default);

    Task<bool> WaitForLicenseAsync(AppId appId, TimeSpan timeout, CancellationToken cancellationToken = default);
}
