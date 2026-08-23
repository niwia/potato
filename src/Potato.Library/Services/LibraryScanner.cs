using System.Diagnostics;
using System.Text.RegularExpressions;
using Potato.Domain.Acf;
using Potato.Domain.ValueObjects;
using Potato.Library.Models;
using Potato.SlsSteam.Paths;

namespace Potato.Library.Services;

/// <summary>
/// Default implementation of ILibraryScanner.
/// </summary>
public sealed class LibraryScanner : ILibraryScanner
{
    private static readonly Regex AcfFileNameRegex = new(@"^appmanifest_(\d+)\.acf$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
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
