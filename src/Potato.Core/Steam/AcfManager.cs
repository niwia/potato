using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using SteamKit2;
using Potato.Core.Models;

namespace Potato.Core.Steam;

public class AcfManager
{
    public static string SanitizeGameName(string name)
    {
        return Regex.Replace(name ?? string.Empty, @"[\\/:*?""<>|]", "").Trim();
    }

    public static string GetInstallFolderName(uint appId, string? gameName, string? explicitInstallDir = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitInstallDir)) return explicitInstallDir;
        var safe = SanitizeGameName(gameName ?? string.Empty);
        return string.IsNullOrWhiteSpace(safe) ? $"App_{appId}" : safe;
    }

    public static string GetGameDirectory(string libraryPath, uint appId, string? gameName, string? explicitInstallDir = null)
    {
        return Path.Combine(libraryPath, "steamapps", "common", GetInstallFolderName(appId, gameName, explicitInstallDir));
    }

    public static string GetAcfPath(string libraryPath, uint appId)
    {
        return Path.Combine(libraryPath, "steamapps", $"appmanifest_{appId}.acf");
    }

    public static SteamApp? ReadAcf(string acfPath)
    {
        if (!File.Exists(acfPath)) return null;

        try
        {
            var kv = KeyValue.LoadAsText(acfPath);
            if (kv == null || kv.Name != "AppState") return null;

            uint appId = uint.TryParse(kv["appid"].Value, out var id) ? id : 0;
            if (appId == 0) return null;

            string name = kv["name"].Value ?? $"App {appId}";
            string? installDir = kv["installdir"].Value;
            uint stateFlags = uint.TryParse(kv["StateFlags"].Value, out var sf) ? sf : 4;
            long sizeOnDisk = long.TryParse(kv["SizeOnDisk"].Value, out var sz) ? sz : 0;
            ulong buildId = ulong.TryParse(kv["buildid"].Value, out var bid) ? bid : 0;

            var mountedDepots = new Dictionary<uint, ulong>();
            var mountedKv = kv["MountedDepots"];
            if (mountedKv != null)
            {
                foreach (var depotKv in mountedKv.Children)
                {
                    if (uint.TryParse(depotKv.Name, out var depotId) && ulong.TryParse(depotKv.Value, out var manifestId))
                    {
                        mountedDepots[depotId] = manifestId;
                    }
                }
            }

            var libraryPath = Directory.GetParent(Path.GetDirectoryName(acfPath)!)?.FullName ?? string.Empty;

            return new SteamApp
            {
                AppId = appId,
                Name = name,
                InstallDir = installDir,
                StateFlags = stateFlags,
                SizeOnDisk = sizeOnDisk,
                BuildId = buildId,
                LibraryPath = libraryPath,
                MountedDepots = mountedDepots
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading ACF {acfPath}: {ex.Message}");
            return null;
        }
    }

    public static bool WriteAcf(
        string libraryPath,
        uint appId,
        string gameName,
        string installDir,
        IEnumerable<DepotInfo> selectedDepots,
        ulong buildId = 0,
        long totalSize = 0)
    {
        try
        {
            var steamappsDir = Path.Combine(libraryPath, "steamapps");
            Directory.CreateDirectory(steamappsDir);

            var acfPath = GetAcfPath(libraryPath, appId);
            var tmpPath = acfPath + ".tmp";

            var kv = new KeyValue("AppState");
            kv["appid"] = new KeyValue("appid", appId.ToString());
            kv["Universe"] = new KeyValue("Universe", "1");
            kv["name"] = new KeyValue("name", gameName);
            kv["StateFlags"] = new KeyValue("StateFlags", "4");
            kv["installdir"] = new KeyValue("installdir", installDir);
            kv["LastUpdated"] = new KeyValue("LastUpdated", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
            kv["SizeOnDisk"] = new KeyValue("SizeOnDisk", totalSize.ToString());
            kv["StagingSize"] = new KeyValue("StagingSize", "0");
            kv["buildid"] = new KeyValue("buildid", buildId.ToString());
            kv["LastOwner"] = new KeyValue("LastOwner", "0");
            kv["UpdateResult"] = new KeyValue("UpdateResult", "0");
            kv["BytesToDownload"] = new KeyValue("BytesToDownload", "0");
            kv["BytesDownloaded"] = new KeyValue("BytesDownloaded", "0");
            kv["AutoUpdateBehavior"] = new KeyValue("AutoUpdateBehavior", "0");
            kv["AllowOtherDownloadsWhileRunning"] = new KeyValue("AllowOtherDownloadsWhileRunning", "0");
            kv["ScheduledAutoUpdate"] = new KeyValue("ScheduledAutoUpdate", "0");

            var installedDepotsKv = new KeyValue("InstalledDepots");
            var mountedDepotsKv = new KeyValue("MountedDepots");

            bool hasWindowsDepots = false;
            foreach (var depot in selectedDepots)
            {
                var depotIdStr = depot.DepotId.ToString();
                var dKv = new KeyValue(depotIdStr);
                dKv["manifest"] = new KeyValue("manifest", depot.ManifestId.ToString());
                dKv["size"] = new KeyValue("size", depot.SizeBytes.ToString());
                installedDepotsKv.Children.Add(dKv);

                mountedDepotsKv[depotIdStr] = new KeyValue(depotIdStr, depot.ManifestId.ToString());

                if (depot.OsList.Contains("windows", StringComparison.OrdinalIgnoreCase))
                {
                    hasWindowsDepots = true;
                }
            }

            kv.Children.Add(installedDepotsKv);
            kv.Children.Add(mountedDepotsKv);

            // Linux Proton overrides
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && hasWindowsDepots)
            {
                var userConfigKv = new KeyValue("UserConfig");
                userConfigKv["platform_override_dest"] = new KeyValue("platform_override_dest", "linux");
                userConfigKv["platform_override_source"] = new KeyValue("platform_override_source", "windows");
                kv.Children.Add(userConfigKv);

                var mountedConfigKv = new KeyValue("MountedConfig");
                mountedConfigKv["platform_override_dest"] = new KeyValue("platform_override_dest", "linux");
                mountedConfigKv["platform_override_source"] = new KeyValue("platform_override_source", "windows");
                kv.Children.Add(mountedConfigKv);
            }

            // Atomic write
            kv.SaveToFile(tmpPath, false);
            File.Move(tmpPath, acfPath, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed writing ACF for App {appId}: {ex.Message}");
            return false;
        }
    }
}
