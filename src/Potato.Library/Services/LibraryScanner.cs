using System.Diagnostics;
using System.Text.RegularExpressions;
using Potato.Domain.Acf;
using Potato.Domain.ValueObjects;
using Potato.Library.Models;
using Potato.SlsSteam.Paths;

namespace Potato.Library.Services;

/// <summary>
/// Scans Steam library folders for games downloaded and managed by ACCELA / Potato.
/// </summary>
public sealed class LibraryScanner : ILibraryScanner
{
    private static readonly Regex AcfFileNameRegex = new(@"^appmanifest_(\d+)\.acf$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex IniAppIdRegex = new(@"^(\d+)", RegexOptions.Compiled);

    private readonly ISlsSteamPathResolver _pathResolver;

    public LibraryScanner(ISlsSteamPathResolver? pathResolver = null)
    {
        _pathResolver = pathResolver ?? new SlsSteamPathResolver();
    }

    public Task<LibraryScanResult> ScanLibrariesAsync(
        IReadOnlyList<string>? customLibraryPaths = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var sw = Stopwatch.StartNew();

            var libraryPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (customLibraryPaths != null && customLibraryPaths.Count > 0)
            {
                foreach (var p in customLibraryPaths)
                {
                    if (Directory.Exists(p)) libraryPaths.Add(p);
                }
            }
            else
            {
                foreach (var p in _pathResolver.SteamAppsPaths)
                {
                    if (Directory.Exists(p)) libraryPaths.Add(p);
                }
            }

            var trackedAppIds = LoadTrackedAppIdsFromConfig();
            var scannedGames = new List<InstalledGame>();
            var seenAppIds = new HashSet<AppId>();

            foreach (string steamAppsDir in libraryPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string commonDir = Path.Combine(steamAppsDir, "common");

                try
                {
                    foreach (string acfFile in Directory.EnumerateFiles(steamAppsDir, "appmanifest_*.acf"))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        string fileName = Path.GetFileName(acfFile);
                        var match = AcfFileNameRegex.Match(fileName);
                        if (!match.Success || !AppId.TryParse(match.Groups[1].Value, out var appId))
                        {
                            continue;
                        }

                        if (seenAppIds.Contains(appId))
                        {
                            continue; // Game already found in a prioritized library
                        }

                        try
                        {
                            var acfState = AcfManager.LoadFromFile(acfFile);
                            if (acfState == null || !acfState.AppId.IsValid)
                            {
                                continue;
                            }

                            string installDir = acfState.InstallDir;
                            string gamePath = Path.Combine(commonDir, installDir);

                            // Check if directory exists and has files
                            if (!Directory.Exists(gamePath) || !HasGameFiles(gamePath))
                            {
                                continue;
                            }

                            // Only show games downloaded / managed by Potato / ACCELA
                            bool isManaged = IsManagedGame(appId, gamePath, trackedAppIds);
                            if (!isManaged)
                            {
                                continue;
                            }

                            ulong sizeOnDisk = acfState.SizeOnDisk;
                            if (sizeOnDisk == 0)
                            {
                                sizeOnDisk = (ulong)CalculateDirectorySize(gamePath);
                            }

                            var game = new InstalledGame
                            {
                                AppId = acfState.AppId,
                                Name = !string.IsNullOrWhiteSpace(acfState.Name) ? acfState.Name : installDir,
                                InstallDir = installDir,
                                FullGamePath = gamePath,
                                AcfPath = acfFile,
                                SteamAppsPath = steamAppsDir,
                                BuildId = !string.IsNullOrWhiteSpace(acfState.BuildId) ? acfState.BuildId : "0",
                                SizeOnDisk = sizeOnDisk,
                                InstalledDepots = acfState.InstalledDepots,
                                UpdateStatus = UpdateStatus.Unknown,
                                LastScannedAt = DateTime.UtcNow
                            };

                            scannedGames.Add(game);
                            seenAppIds.Add(appId);
                        }
                        catch
                        {
                            // Skip corrupted or unreadable ACF files
                        }
                    }
                }
                catch
                {
                    // Skip inaccessible library folders
                }
            }

            sw.Stop();

            var sorted = scannedGames.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToList();
            return new LibraryScanResult(sorted, libraryPaths.ToList(), sw.Elapsed);
        }, cancellationToken);
    }

    private static bool IsManagedGame(AppId appId, string gamePath, HashSet<AppId> trackedAppIds)
    {
        // 1. Check for marker folders created by Potato / ACCELA / DepotDownloader
        if (Directory.Exists(Path.Combine(gamePath, ".potato")) ||
            Directory.Exists(Path.Combine(gamePath, ".ACCELA")) ||
            Directory.Exists(Path.Combine(gamePath, ".DepotDownloader")))
        {
            return true;
        }

        // 2. Check if tracked in ACCELA.conf
        if (trackedAppIds.Contains(appId))
        {
            return true;
        }

        return false;
    }

    private static HashSet<AppId> LoadTrackedAppIdsFromConfig()
    {
        var result = new HashSet<AppId>();

        string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] candidatePaths =
        {
            Path.Combine(userHome, ".config", "Tachibana Labs", "ACCELA.conf"),
            Path.Combine(userHome, ".config", "ACCELA", "ACCELA.conf"),
            Path.Combine(userHome, ".config", "potato", "settings.json")
        };

        foreach (string path in candidatePaths)
        {
            if (!File.Exists(path)) continue;

            try
            {
                using var reader = new StreamReader(path);
                string? line;
                bool inTrackedSection = false;

                while ((line = reader.ReadLine()) != null)
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        string section = trimmed[1..^1].Trim().ToLowerInvariant();
                        inTrackedSection = section is "installed_buildid" or "selected_branch" or "fetched_buildid" or "depot_selection";
                        continue;
                    }

                    if (inTrackedSection)
                    {
                        int eq = trimmed.IndexOf('=');
                        string key = eq > 0 ? trimmed[..eq].Trim() : trimmed;
                        var m = IniAppIdRegex.Match(key);
                        if (m.Success && AppId.TryParse(m.Groups[1].Value, out var appId))
                        {
                            result.Add(appId);
                        }
                    }
                }
            }
            catch
            {
                // Ignore unreadable config
            }
        }

        return result;
    }

    private static bool HasGameFiles(string directoryPath)
    {
        try
        {
            foreach (string entry in Directory.EnumerateFileSystemEntries(directoryPath))
            {
                string name = Path.GetFileName(entry);
                if (name.StartsWith(".", StringComparison.OrdinalIgnoreCase)) continue;
                return true;
            }
        }
        catch { }

        return false;
    }

    private static long CalculateDirectorySize(string directoryPath)
    {
        try
        {
            var dirInfo = new DirectoryInfo(directoryPath);
            return dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
        }
        catch
        {
            return 0;
        }
    }
}
