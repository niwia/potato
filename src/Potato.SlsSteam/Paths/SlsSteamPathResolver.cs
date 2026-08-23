using System.Text.RegularExpressions;

namespace Potato.SlsSteam.Paths;

/// <summary>
/// Default implementation of ISlsSteamPathResolver.
/// </summary>
public sealed class SlsSteamPathResolver : ISlsSteamPathResolver
{
    private static readonly Regex LibraryIndexRegex = new(@"^\s*""(\d+)""\s*$", RegexOptions.Compiled);
    private static readonly Regex LibraryPathRegex = new(@"^\s*""path""\s*""([^""]+)""", RegexOptions.Compiled);

    public bool IsFlatpakSteam { get; }
    public string SteamPath { get; }
    public string ConfigPath { get; }
    public string LogPath { get; }
    public string ApiPipePath => "/tmp/SLSsteam.API";
    public IReadOnlyList<string> SteamAppsPaths { get; }

    public SlsSteamPathResolver(string? explicitConfigPath = null, string? explicitSteamPath = null)
    {
        string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        string flatpakSteamDir = Path.Combine(userHome, ".var", "app", "com.valvesoftware.Steam", ".steam", "steam");
        string flatpakConfig = Path.Combine(userHome, ".var", "app", "com.valvesoftware.Steam", ".config", "SLSsteam", "config.yaml");
        string flatpakLog = Path.Combine(userHome, ".var", "app", "com.valvesoftware.Steam", ".SLSsteam.log");

        string nativeSteamDir = Path.Combine(userHome, ".local", "share", "Steam");
        if (!Directory.Exists(nativeSteamDir))
        {
            string altNative = Path.Combine(userHome, ".steam", "steam");
            if (Directory.Exists(altNative)) nativeSteamDir = altNative;
        }

        string xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? Path.Combine(userHome, ".config");
        string nativeConfig = Path.Combine(xdgConfig, "SLSsteam", "config.yaml");
        string nativeLog = Path.Combine(userHome, ".SLSsteam.log");

        if (!string.IsNullOrWhiteSpace(explicitSteamPath) && Directory.Exists(explicitSteamPath))
        {
            SteamPath = explicitSteamPath;
            IsFlatpakSteam = SteamPath.Contains(".var/app/com.valvesoftware.Steam");
        }
        else if (Directory.Exists(flatpakSteamDir))
        {
            SteamPath = flatpakSteamDir;
            IsFlatpakSteam = true;
        }
        else
        {
            SteamPath = nativeSteamDir;
            IsFlatpakSteam = false;
        }

        if (!string.IsNullOrWhiteSpace(explicitConfigPath))
        {
            ConfigPath = explicitConfigPath;
        }
        else
        {
            ConfigPath = IsFlatpakSteam ? flatpakConfig : nativeConfig;
        }

        LogPath = IsFlatpakSteam ? flatpakLog : nativeLog;

        SteamAppsPaths = DiscoverSteamAppsPaths(SteamPath, userHome);
    }

    private static List<string> DiscoverSteamAppsPaths(string mainSteamPath, string userHome)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string mainApps = Path.Combine(mainSteamPath, "steamapps");
        if (Directory.Exists(mainApps)) paths.Add(mainApps);

        // Parse libraryfolders.vdf
        string vdfPath = Path.Combine(mainSteamPath, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdfPath))
        {
            try
            {
                foreach (string line in File.ReadAllLines(vdfPath))
                {
                    var match = LibraryPathRegex.Match(line);
                    if (match.Success)
                    {
                        string libPath = match.Groups[1].Value.Replace(@"\\", @"\");
                        string appsDir = Path.Combine(libPath, "steamapps");
                        if (Directory.Exists(appsDir))
                        {
                            paths.Add(appsDir);
                        }
                    }
                }
            }
            catch { }
        }

        return paths.ToList();
    }

    public int GetLibraryIndex(string targetLibraryPath)
    {
        if (string.IsNullOrWhiteSpace(targetLibraryPath) || string.IsNullOrWhiteSpace(SteamPath))
        {
            return 0;
        }

        string vdfPath = Path.Combine(SteamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdfPath)) return 0;

        try
        {
            string targetFull = Path.GetFullPath(targetLibraryPath).TrimEnd(Path.DirectorySeparatorChar);
            string[] lines = File.ReadAllLines(vdfPath);
            int currentIndex = 0;

            foreach (string line in lines)
            {
                var idxMatch = LibraryIndexRegex.Match(line);
                if (idxMatch.Success && int.TryParse(idxMatch.Groups[1].Value, out int idx))
                {
                    currentIndex = idx;
                    continue;
                }

                var pathMatch = LibraryPathRegex.Match(line);
                if (pathMatch.Success)
                {
                    string libPath = Path.GetFullPath(pathMatch.Groups[1].Value.Replace(@"\\", @"\")).TrimEnd(Path.DirectorySeparatorChar);
                    if (string.Equals(libPath, targetFull, StringComparison.OrdinalIgnoreCase))
                    {
                        return currentIndex;
                    }
                }
            }
        }
        catch { }

        return 0;
    }
}
