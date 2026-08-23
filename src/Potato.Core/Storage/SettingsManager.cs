using System.Text.Json;
using Potato.Core.Models;

namespace Potato.Core.Storage;

public class SettingsManager
{
    private readonly string _settingsPath;
    private AppSettings _settings;

    public AppSettings Current => _settings;

    public SettingsManager(string? configDir = null)
    {
        var baseDir = configDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "Potato");
        Directory.CreateDirectory(baseDir);
        _settingsPath = Path.Combine(baseDir, "settings.json");
        _settings = LoadSettings();
    }

    private AppSettings LoadSettings()
    {
        if (File.Exists(_settingsPath))
        {
            try
            {
                var json = File.ReadAllText(_settingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null) return loaded;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
            }
        }

        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        _settings = settings;
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving settings: {ex.Message}");
        }
    }
}
