namespace Potato.Configuration.Models;

/// <summary>
/// Settings for Steam library scanning, game update detection, and display order.
/// </summary>
public sealed class LibrarySettings
{
    /// <summary>
    /// Whether to automatically check for game updates in the background on application startup.
    /// </summary>
    public bool CheckUpdatesOnStartup { get; set; } = true;

    /// <summary>
    /// Periodic background update check interval in minutes (0 to disable periodic check).
    /// </summary>
    public int UpdateCheckIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Sorting option for the library view ("RecentlyInstalled", "Alphabetical", "SizeOnDisk", "LastPlayed").
    /// </summary>
    public string LibrarySortOption { get; set; } = "RecentlyInstalled";

    /// <summary>
    /// AppIDs explicitly excluded from batch update checks.
    /// </summary>
    public HashSet<uint> ExcludedFromUpdateAll { get; set; } = new();
}
