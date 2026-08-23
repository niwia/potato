using System.Text;
using System.Text.RegularExpressions;
using Potato.Domain.ValueObjects;
using Potato.SlsSteam.Paths;

namespace Potato.SlsSteam.Ipc;

/// <summary>
/// Default implementation of ISlsSteamIpcClient communicating with SLSsteam named pipe.
/// </summary>
public sealed class SlsSteamIpcClient : ISlsSteamIpcClient
{
    private readonly ISlsSteamPathResolver _pathResolver;
    private DateTime _lastProcCheck = DateTime.MinValue;
    private bool _cachedIsSlsActive;
    private readonly object _procLock = new();

    public bool IsPipeAvailable => File.Exists(_pathResolver.ApiPipePath);

    public bool IsSlsSteamActive
    {
        get
        {
            lock (_procLock)
            {
                if ((DateTime.UtcNow - _lastProcCheck).TotalSeconds < 2.0)
                {
                    return _cachedIsSlsActive;
                }

                _lastProcCheck = DateTime.UtcNow;
                _cachedIsSlsActive = CheckSlsSteamProcess();
                return _cachedIsSlsActive;
            }
        }
    }

    public SlsSteamIpcClient(ISlsSteamPathResolver? pathResolver = null)
    {
        _pathResolver = pathResolver ?? new SlsSteamPathResolver();
    }

    public async Task<bool> SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command) || !IsPipeAvailable)
        {
            return false;
        }

        try
        {
            byte[] bytes = Encoding.ASCII.GetBytes(command);
            await using var stream = new FileStream(
                _pathResolver.ApiPipePath,
                FileMode.Open,
                FileAccess.Write,
                FileShare.ReadWrite,
                bufferSize: 1024,
                useAsync: true);

            await stream.WriteAsync(bytes, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public Task<bool> InstallAppAsync(AppId appId, int libraryIndex = 0, CancellationToken cancellationToken = default)
    {
        if (!appId.IsValid) return Task.FromResult(false);
        string cmd = $"install|{appId}|{libraryIndex}";
        return SendCommandAsync(cmd, cancellationToken);
    }

    public Task<bool> UninstallAppAsync(AppId appId, CancellationToken cancellationToken = default)
    {
        if (!appId.IsValid) return Task.FromResult(false);
        string cmd = $"uninstall|{appId}";
        return SendCommandAsync(cmd, cancellationToken);
    }

    public async Task<bool> WaitForLicenseAsync(AppId appId, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (!appId.IsValid || !File.Exists(_pathResolver.LogPath)) return false;

        string regexPattern = $@"(?:AppLicensesChanged callback invoked for {Regex.Escape(appId.ToString())}|Unlocked {Regex.Escape(appId.ToString())})";
        var regex = new Regex(regexPattern, RegexOptions.Compiled);

        long startOffset = 0;
        try
        {
            startOffset = new FileInfo(_pathResolver.LogPath).Length;
        }
        catch { }

        var deadline = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var fileInfo = new FileInfo(_pathResolver.LogPath);
                if (fileInfo.Exists && fileInfo.Length > startOffset)
                {
                    await using var fs = new FileStream(_pathResolver.LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    fs.Seek(startOffset, SeekOrigin.Begin);
                    using var reader = new StreamReader(fs, Encoding.UTF8);

                    while (!reader.EndOfStream)
                    {
                        string? line = await reader.ReadLineAsync(cancellationToken);
                        if (line != null && regex.IsMatch(line))
                        {
                            return true;
                        }
                    }

                    startOffset = fs.Position;
                }
            }
            catch { }

            await Task.Delay(300, cancellationToken);
        }

        return false;
    }

    private static bool CheckSlsSteamProcess()
    {
        if (!OperatingSystem.IsLinux()) return false;

        try
        {
            string procDir = "/proc";
            if (!Directory.Exists(procDir)) return false;

            foreach (var dir in Directory.EnumerateDirectories(procDir))
            {
                string pidStr = Path.GetFileName(dir);
                if (!int.TryParse(pidStr, out _)) continue;

                string commPath = Path.Combine(dir, "comm");
                if (File.Exists(commPath))
                {
                    string comm = File.ReadAllText(commPath).Trim();
                    if (comm == "steam")
                    {
                        string mapsPath = Path.Combine(dir, "maps");
                        if (File.Exists(mapsPath))
                        {
                            string maps = File.ReadAllText(mapsPath);
                            if (maps.Contains("SLSsteam.so", StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
        }
        catch { }

        return false;
    }
}
