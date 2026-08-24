using Potato.Configuration.Models;

namespace Potato.Configuration.Services;

/// <summary>
/// Service contract for loading, persisting, mutating, and observing application configuration.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// The currently active in-memory settings snapshot.
    /// </summary>
    PotatoSettings Current { get; }

    /// <summary>
    /// Absolute path to the active settings.json file.
    /// </summary>
    string SettingsFilePath { get; }

    /// <summary>
    /// Event fired whenever settings are changed or reloaded.
    /// </summary>
    event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

    /// <summary>
    /// Loads settings from disk. If the file does not exist, attempts automatic legacy migration or initializes defaults.
    /// </summary>
    Task<PotatoSettings> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the provided settings instance atomically to disk.
    /// </summary>
    Task SaveAsync(PotatoSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies an atomic mutation callback to the current settings and persists them to disk.
    /// </summary>
    Task<PotatoSettings> UpdateAsync(Action<PotatoSettings> mutateAction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets all settings back to pristine application defaults and saves to disk.
    /// </summary>
    Task<PotatoSettings> ResetToDefaultsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Manually triggers an import from an ACCELA.conf file.
    /// </summary>
    Task<PotatoSettings> ImportFromLegacyConfigAsync(string? explicitPath = null, CancellationToken cancellationToken = default);
}
