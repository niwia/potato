using Potato.Core.Models;

namespace Potato.Core.Steam;

public class LibraryScanner
{
    private static readonly HashSet<uint> DefaultExcludedAppIds = new()
    {
        228980, // Steamworks Common Redistributables
        1070560, // Steam Linux Runtime
        1391110, // Steam Linux Runtime - Soldier
        1628350, // Steam Linux Runtime - Sniper
        894760,  // Proton 4.11
        1493710, // Proton Experimental
        1887720, // Proton Hotfix
    };

    public static async Task<List<SteamApp>> ScanLibrariesAsync(
        IEnumerable<string> libraryPaths,
        bool includeRuntimes = false,
        CancellationToken ct = default)
    {
        var games = new List<SteamApp>();

        await Task.Run(() =>
        {
            foreach (var lib in libraryPaths)
            {
                if (ct.IsCancellationRequested) break;
                if (!Directory.Exists(lib)) continue;

                var steamappsDir = Path.Combine(lib, "steamapps");
                if (!Directory.Exists(steamappsDir)) continue;

                var acfFiles = Directory.GetFiles(steamappsDir, "appmanifest_*.acf");
                foreach (var acf in acfFiles)
                {
                    if (ct.IsCancellationRequested) break;

                    var app = AcfManager.ReadAcf(acf);
                    if (app != null)
                    {
                        if (!includeRuntimes && DefaultExcludedAppIds.Contains(app.AppId))
                        {
                            continue;
                        }
                        games.Add(app);
                    }
                }
            }
        }, ct);

        return games.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
