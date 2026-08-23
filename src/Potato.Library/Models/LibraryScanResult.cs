namespace Potato.Library.Models;

/// <summary>
/// Aggregated result of scanning Steam library directories.
/// </summary>
public sealed record LibraryScanResult(
    IReadOnlyList<InstalledGame> InstalledGames,
    IReadOnlyList<string> ScannedLibraries,
    TimeSpan Elapsed)
{
    public int TotalGames => InstalledGames.Count;
    public ulong TotalSizeBytes => (ulong)InstalledGames.Sum(g => (long)g.SizeOnDisk);
}
