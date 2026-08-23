namespace Potato.SlsSteam.Paths;

/// <summary>
/// Service resolving Steam and SLSsteam filesystem locations across Native and Flatpak installations.
/// </summary>
public interface ISlsSteamPathResolver
{
    bool IsFlatpakSteam { get; }
    string SteamPath { get; }
    string ConfigPath { get; }
    string LogPath { get; }
    string ApiPipePath { get; }
    IReadOnlyList<string> SteamAppsPaths { get; }

    int GetLibraryIndex(string targetLibraryPath);
}
