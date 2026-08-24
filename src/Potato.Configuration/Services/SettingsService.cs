using System.Text.Json;
using Potato.Configuration.Migration;
using Potato.Configuration.Models;

namespace Potato.Configuration.Services;

/// <summary>
/// Thread-safe configuration service that reads and writes directly to ACCELA.conf or settings.json.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _settingsFilePath;
    private readonly bool _autoMigrateLegacy;
    private readonly object _lock = new();
    private PotatoSettings _currentSettings;

    public PotatoSettings Current
    {
        get
        {
            lock (_lock)
            {
                return _currentSettings;
            }
        }
    }

    public string SettingsFilePath => _settingsFilePath;

    public event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

    public SettingsService(string? explicitFilePath = null, bool autoMigrateLegacy = true)
    {
        _settingsFilePath = explicitFilePath ?? ResolveDefaultSettingsPath();
        _autoMigrateLegacy = autoMigrateLegacy;
        _currentSettings = new PotatoSettings();
    }

    public async Task<PotatoSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(_settingsFilePath))
        {
            try
            {
                string content;
                lock (_lock)
                {
                    content = File.ReadAllText(_settingsFilePath);
                }

                if (_settingsFilePath.EndsWith(".conf", StringComparison.OrdinalIgnoreCase) ||
                    _settingsFilePath.EndsWith(".ini", StringComparison.OrdinalIgnoreCase))
                {
                    var parsed = AccelaConfigImporter.ImportFromIni(content);
                    SetCurrent(parsed);
                    return parsed;
                }
                else
                {
                    var loaded = JsonSerializer.Deserialize<PotatoSettings>(content, JsonOptions);
                    if (loaded != null)
                    {
                        SetCurrent(loaded);
                        return loaded;
                    }
                }
            }
            catch
            {
                // Fall back to defaults/migration
            }
        }

        // File does not exist -> Check for legacy ACCELA.conf if enabled
        if (_autoMigrateLegacy)
        {
            string? legacyPath = AccelaConfigImporter.FindLegacyConfigFile();
            if (!string.IsNullOrEmpty(legacyPath) && File.Exists(legacyPath))
            {
                try
                {
                    string ini = await File.ReadAllTextAsync(legacyPath, cancellationToken);
                    var imported = AccelaConfigImporter.ImportFromIni(ini);
                    SetCurrent(imported);
                    return imported;
                }
                catch
                {
                    // Fallback to fresh defaults
                }
            }
        }

        // Fresh default settings
        var defaults = new PotatoSettings();
        await SaveAsync(defaults, cancellationToken);
        return defaults;
    }

    public async Task SaveAsync(PotatoSettings settings, CancellationToken cancellationToken = default)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));

        string dir = Path.GetDirectoryName(_settingsFilePath)!;
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string fileContent;
        if (_settingsFilePath.EndsWith(".conf", StringComparison.OrdinalIgnoreCase) ||
            _settingsFilePath.EndsWith(".ini", StringComparison.OrdinalIgnoreCase))
        {
            string existing = "";
            if (File.Exists(_settingsFilePath))
            {
                lock (_lock)
                {
                    existing = File.ReadAllText(_settingsFilePath);
                }
            }

            fileContent = AccelaConfigImporter.UpdateIni(existing, settings);
        }
        else
        {
            fileContent = JsonSerializer.Serialize(settings, JsonOptions);
        }

        string tempPath = _settingsFilePath + ".tmp";

        // Atomic write: write to temp file then rename/replace
        await File.WriteAllTextAsync(tempPath, fileContent, cancellationToken);

        lock (_lock)
        {
            File.Move(tempPath, _settingsFilePath, overwrite: true);
        }

        SetCurrent(settings);
    }

    public async Task<PotatoSettings> UpdateAsync(Action<PotatoSettings> mutateAction, CancellationToken cancellationToken = default)
    {
        if (mutateAction == null) throw new ArgumentNullException(nameof(mutateAction));

        PotatoSettings clone;
        lock (_lock)
        {
            clone = _currentSettings.Clone();
        }

        mutateAction(clone);
        await SaveAsync(clone, cancellationToken);
        return clone;
    }

    public async Task<PotatoSettings> ResetToDefaultsAsync(CancellationToken cancellationToken = default)
    {
        var defaults = new PotatoSettings();
        await SaveAsync(defaults, cancellationToken);
        return defaults;
    }

    public async Task<PotatoSettings> ImportFromLegacyConfigAsync(string? explicitPath = null, CancellationToken cancellationToken = default)
    {
        string? targetPath = explicitPath ?? AccelaConfigImporter.FindLegacyConfigFile();
        if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath))
        {
            throw new FileNotFoundException("ACCELA configuration file was not found.", targetPath);
        }

        string ini = await File.ReadAllTextAsync(targetPath, cancellationToken);
        var imported = AccelaConfigImporter.ImportFromIni(ini);
        await SaveAsync(imported, cancellationToken);
        return imported;
    }

    private void SetCurrent(PotatoSettings newSettings)
    {
        PotatoSettings oldSettings;
        lock (_lock)
        {
            oldSettings = _currentSettings;
            _currentSettings = newSettings;
        }

        SettingsChanged?.Invoke(this, new SettingsChangedEventArgs(oldSettings, newSettings));
    }

    public static string ResolveDefaultSettingsPath()
    {
        // If existing ACCELA.conf exists, directly use it as the primary config
        string? existingAccela = AccelaConfigImporter.FindLegacyConfigFile();
        if (!string.IsNullOrEmpty(existingAccela) && File.Exists(existingAccela))
        {
            return existingAccela;
        }

        string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string configDir = Path.Combine(baseDir, ".config", "Tachibana Labs");

        // Windows fallback
        if (OperatingSystem.IsWindows())
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            configDir = Path.Combine(appData, "Tachibana Labs");
        }

        return Path.Combine(configDir, "ACCELA.conf");
    }
}
