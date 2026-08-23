using System.Diagnostics;
using System.Text.RegularExpressions;
using Potato.Core.Models;

namespace Potato.Downloader;

public class DepotDownloaderService
{
    private static readonly Regex PercentRegex = new(@"(\d+(?:\.\d+)?)%", RegexOptions.Compiled);
    private static readonly Regex ChunkProgressRegex = new(@"Got chunk (\d+)\s*/\s*(\d+)", RegexOptions.Compiled);
    private static readonly Regex BytesDownloadedRegex = new(@"(\d+)\s*bytes", RegexOptions.Compiled);

    public static string? LocateDepotDownloader(string? customPath = null)
    {
        if (!string.IsNullOrEmpty(customPath) && (File.Exists(customPath) || Directory.Exists(customPath)))
        {
            return customPath;
        }

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new[]
        {
            Path.Combine(home, ".local", "share", "ACCELA", "DepotDownloaderMod-patched", "DepotDownloader", "bin", "Release", "net9.0", "DepotDownloaderMod.dll"),
            Path.Combine(home, ".local", "share", "ACCELA", "DepotDownloaderMod-patched", "DepotDownloader", "bin", "Release", "net9.0", "DepotDownloaderMod"),
            Path.Combine(home, ".local", "share", "ACCELA", "DepotDownloaderMod-patched", "DepotDownloader", "bin", "Release", "net8.0", "DepotDownloaderMod.dll"),
            Path.Combine(home, ".local", "share", "ACCELA", "bin", "DepotDownloader.dll"),
            Path.Combine(home, ".local", "share", "ACCELA", "DepotDownloaderMod-patched", "DepotDownloader", "DepotDownloaderMod.csproj"),
            Path.Combine(AppContext.BaseDirectory, "DepotDownloaderMod.dll"),
            Path.Combine(AppContext.BaseDirectory, "DepotDownloaderMod")
        };

        foreach (var c in candidates)
        {
            if (File.Exists(c)) return c;
        }

        return null;
    }

    public async Task<bool> DownloadDepotAsync(
        uint appId,
        uint depotId,
        ulong manifestId,
        string destinationDir,
        Action<DownloadProgress>? onProgress,
        Action<string>? onLog,
        string? customToolPath = null,
        CancellationToken ct = default)
    {
        var toolPath = LocateDepotDownloader(customToolPath);
        if (string.IsNullOrEmpty(toolPath))
        {
            onLog?.Invoke("❌ Could not locate DepotDownloader executable or project in standard directories.");
            onLog?.Invoke("💡 Please configure Custom DepotDownloader Path in Settings.");
            return false;
        }

        Directory.CreateDirectory(destinationDir);

        var isDll = toolPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
        var isCsproj = toolPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);

        var fileName = (isDll || isCsproj) ? "dotnet" : toolPath;
        var argsList = new List<string>();

        if (isCsproj)
        {
            argsList.Add("run");
            argsList.Add("--project");
            argsList.Add(toolPath);
            argsList.Add("--");
        }
        else if (isDll)
        {
            argsList.Add(toolPath);
        }

        argsList.Add("-app");
        argsList.Add(appId.ToString());
        argsList.Add("-depot");
        argsList.Add(depotId.ToString());

        if (manifestId > 0)
        {
            argsList.Add("-manifest");
            argsList.Add(manifestId.ToString());
        }

        argsList.Add("-dir");
        argsList.Add(destinationDir);
        argsList.Add("-remember-password");

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = string.Join(" ", argsList.Select(a => a.Contains(' ') ? $"\"{a}\"" : a)),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        onLog?.Invoke($"🚀 Running: {psi.FileName} {psi.Arguments}");
        onLog?.Invoke($"📁 Destination: {destinationDir}");

        var speedMonitor = new SpeedMonitor();
        speedMonitor.Reset();

        using var process = new Process { StartInfo = psi };

        process.OutputDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data)) return;

            onLog?.Invoke(e.Data);

            // Parse percent
            var percentMatch = PercentRegex.Match(e.Data);
            if (percentMatch.Success && double.TryParse(percentMatch.Groups[1].Value, out var pct))
            {
                var speed = speedMonitor.UpdateSpeed((long)(pct * 1000000));
                onProgress?.Invoke(new DownloadProgress
                {
                    AppId = appId,
                    Percent = pct,
                    SpeedBytesPerSecond = speed,
                    StatusMessage = e.Data
                });
            }
            else if (ChunkProgressRegex.Match(e.Data) is { Success: true } chunkMatch)
            {
                if (long.TryParse(chunkMatch.Groups[1].Value, out var current) &&
                    long.TryParse(chunkMatch.Groups[2].Value, out var total) && total > 0)
                {
                    double pctCalculated = (double)current / total * 100.0;
                    var speed = speedMonitor.UpdateSpeed(current * 1024 * 1024);
                    onProgress?.Invoke(new DownloadProgress
                    {
                        AppId = appId,
                        Percent = pctCalculated,
                        SpeedBytesPerSecond = speed,
                        StatusMessage = $"Chunk {current}/{total}"
                    });
                }
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                onLog?.Invoke($"⚠️ {e.Data}");
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using (ct.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        onLog?.Invoke("🛑 Cancelling download process tree...");
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch { }
            }))
            {
                await process.WaitForExitAsync(ct);
            }

            if (process.ExitCode == 0)
            {
                onLog?.Invoke($"✅ Depot {depotId} completed successfully.");
                return true;
            }

            onLog?.Invoke($"❌ DepotDownloader exited with code {process.ExitCode}.");
            return false;
        }
        catch (OperationCanceledException)
        {
            onLog?.Invoke("⛔ Download operation was cancelled.");
            return false;
        }
        catch (Exception ex)
        {
            onLog?.Invoke($"❌ Failed to execute DepotDownloader: {ex.Message}");
            return false;
        }
    }
}
