using System.Globalization;
using Potato.Configuration.Models;

namespace Potato.Configuration.Migration;

/// <summary>
/// Scans for and imports settings from legacy ACCELA configuration files (ACCELA.conf).
/// </summary>
public static class AccelaConfigImporter
{
    /// <summary>
    /// Searches common platform locations for existing ACCELA.conf files.
    /// </summary>
    public static string? FindLegacyConfigFile()
    {
        string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        string[] candidatePaths =
        {
            Path.Combine(userHome, ".config", "Tachibana Labs", "ACCELA.conf"),
            Path.Combine(userHome, ".config", "ACCELA", "ACCELA.conf"),
            Path.Combine(appData, "Tachibana Labs", "ACCELA.conf"),
            Path.Combine(appData, "ACCELA", "ACCELA.conf"),
        };

        foreach (string candidate in candidatePaths)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Parses an ACCELA.conf file and migrates its configuration keys into a new PotatoSettings instance.
    /// </summary>
    public static PotatoSettings ImportFromIni(string iniContent)
    {
        var settings = new PotatoSettings();
        var sections = ParseIni(iniContent);

        if (sections.TryGetValue("General", out var general))
        {
            // API
            if (general.TryGetValue("morrenus_api_key", out string? key) && !string.IsNullOrWhiteSpace(key))
            {
                settings.Api.HubcapApiKey = key.Trim();
            }
            if (general.TryGetValue("isp_bypass_hubcap", out string? ispStr))
            {
                settings.Api.UseIspBypass = ParseBool(ispStr, true);
            }
            if (general.TryGetValue("wirecutter_url", out string? wirecutter) && !string.IsNullOrWhiteSpace(wirecutter))
            {
                settings.Api.CustomWirecutterUrl = wirecutter.Trim();
            }

            // Downloader
            if (general.TryGetValue("max_downloads", out string? maxDlStr) && int.TryParse(maxDlStr, out int maxDl))
            {
                settings.Download.MaxDownloadsPerJob = Math.Clamp(maxDl, 1, 30);
            }
            if (general.TryGetValue("use_lancache", out string? lcStr) || general.TryGetValue("download_lan_cache", out lcStr))
            {
                settings.Download.UseLanCache = ParseBool(lcStr, true);
            }
            if (general.TryGetValue("auto_skip_single_choice", out string? autoSkipStr))
            {
                settings.Download.AutoSkipSingleChoice = ParseBool(autoSkipStr, true);
            }
            if (general.TryGetValue("smart_depot_selection", out string? smartDepotStr))
            {
                settings.Download.SmartDepotSelection = ParseBool(smartDepotStr, true);
            }
            if (general.TryGetValue("filter_soundtracks", out string? filterOstStr))
            {
                settings.Download.FilterSoundtracks = ParseBool(filterOstStr, true);
            }
            if (general.TryGetValue("hide_macos_depots", out string? hideMacStr))
            {
                settings.Download.FilterMacOsDepots = ParseBool(hideMacStr, true);
            }
            if (general.TryGetValue("library_mode", out string? libModeStr))
            {
                settings.Download.LimitToSteamLibraries = ParseBool(libModeStr, true);
            }
            if (general.TryGetValue("default_download_directory", out string? dlDir) && !string.IsNullOrWhiteSpace(dlDir))
            {
                settings.Download.DefaultDownloadDirectory = dlDir.Trim();
            }

            // SLSsteam
            if (general.TryGetValue("sls_config_management", out string? slsStr))
            {
                settings.SlsSteam.EnableSlsIntegration = ParseBool(slsStr, true);
            }
            if (general.TryGetValue("prompt_steam_restart", out string? promptRestartStr))
            {
                settings.SlsSteam.PromptSteamRestart = ParseBool(promptRestartStr, false);
            }
            if (general.TryGetValue("auto_install_goldberg", out string? autoInstallGoldbergStr))
            {
                settings.SlsSteam.AutoInstallGoldberg = ParseBool(autoInstallGoldbergStr, true);
            }
            if (general.TryGetValue("auto_apply_goldberg", out string? autoApplyGoldbergStr))
            {
                settings.SlsSteam.AutoApplyGoldberg = ParseBool(autoApplyGoldbergStr, false);
            }

            // Library
            if (general.TryGetValue("check_updates_on_boot", out string? checkBootStr))
            {
                settings.Library.CheckUpdatesOnStartup = ParseBool(checkBootStr, true);
            }
            if (general.TryGetValue("update_check_interval_minutes", out string? intervalStr) && int.TryParse(intervalStr, out int interval))
            {
                settings.Library.UpdateCheckIntervalMinutes = Math.Max(0, interval);
            }
            if (general.TryGetValue("library_sort_option", out string? sortOpt) && !string.IsNullOrWhiteSpace(sortOpt))
            {
                settings.Library.LibrarySortOption = sortOpt.Trim();
            }

            // Appearance
            if (general.TryGetValue("accent_color", out string? accent) || general.TryGetValue("user_accent_color", out accent))
            {
                if (!string.IsNullOrWhiteSpace(accent)) settings.Appearance.AccentColor = accent.Trim();
            }
            if (general.TryGetValue("dark_mode", out string? darkModeStr))
            {
                settings.Appearance.Theme = ParseBool(darkModeStr, true) ? "Dark" : "Light";
            }
            if (general.TryGetValue("material_preset", out string? preset) && !string.IsNullOrWhiteSpace(preset))
            {
                settings.Appearance.PresetName = preset.Trim();
            }
            if (general.TryGetValue("font", out string? font) && !string.IsNullOrWhiteSpace(font))
            {
                settings.Appearance.FontFamily = font.Trim();
            }
            if (general.TryGetValue("font-size", out string? fontSizeStr) && int.TryParse(fontSizeStr, out int fontSize))
            {
                settings.Appearance.FontSize = Math.Clamp(fontSize, 8, 24);
            }
            if (general.TryGetValue("nerd_mode", out string? nerdStr))
            {
                settings.Appearance.NerdMode = ParseBool(nerdStr, false);
            }

            // Advanced
            if (general.TryGetValue("log_filter_level", out string? logLevel) && !string.IsNullOrWhiteSpace(logLevel))
            {
                settings.Advanced.LogLevel = logLevel.Trim();
            }
            if (general.TryGetValue("enable_remote_web_ui", out string? webUiStr))
            {
                settings.Advanced.EnableRemoteWebUi = ParseBool(webUiStr, false);
            }
            if (general.TryGetValue("web_ui_port", out string? webPortStr) && int.TryParse(webPortStr, out int webPort))
            {
                settings.Advanced.WebUiPort = Math.Clamp(webPort, 1024, 65535);
            }
            if (general.TryGetValue("workshop_cell_id", out string? cellId) && !string.IsNullOrWhiteSpace(cellId))
            {
                settings.Advanced.WorkshopCellId = cellId.Trim();
            }
            if (general.TryGetValue("workshop_max_downloads", out string? wsDlStr) && int.TryParse(wsDlStr, out int wsDl))
            {
                settings.Advanced.WorkshopMaxDownloads = Math.Clamp(wsDl, 1, 30);
            }
        }

        // Excluded from update all
        if (sections.TryGetValue("exclude_from_update_all", out var excludedSection))
        {
            foreach (var kvp in excludedSection)
            {
                if (uint.TryParse(kvp.Key, out uint excludedAppId) && ParseBool(kvp.Value, false))
                {
                    settings.Library.ExcludedFromUpdateAll.Add(excludedAppId);
                }
            }
        }

        return settings;
    }

    private static bool ParseBool(string? value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value)) return defaultValue;
        string v = value.Trim().ToLowerInvariant();
        return v is "true" or "1" or "yes" or "on";
    }

    private static Dictionary<string, Dictionary<string, string>> ParseIni(string content)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        string currentSection = "General";
        result[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var reader = new StringReader(content);
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith(";"))
            {
                continue;
            }

            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                currentSection = trimmed[1..^1].Trim();
                if (!result.ContainsKey(currentSection))
                {
                    result[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
                continue;
            }

            int eqIdx = trimmed.IndexOf('=');
            if (eqIdx > 0)
            {
                string key = trimmed[..eqIdx].Trim();
                string val = trimmed[(eqIdx + 1)..].Trim();
                result[currentSection][key] = val;
            }
        }

        return result;
    }
}
