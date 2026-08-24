namespace Potato.Configuration.Models;

/// <summary>
/// Root application configuration containing all settings sections for Potato.
/// </summary>
public sealed class PotatoSettings
{
    public int SchemaVersion { get; set; } = 1;

    public ApiSettings Api { get; set; } = new();

    public DownloadSettings Download { get; set; } = new();

    public SlsSteamSettings SlsSteam { get; set; } = new();

    public LibrarySettings Library { get; set; } = new();

    public AppearanceSettings Appearance { get; set; } = new();

    public AdvancedSettings Advanced { get; set; } = new();

    /// <summary>
    /// Deep clone helper for atomic edits.
    /// </summary>
    public PotatoSettings Clone()
    {
        return new PotatoSettings
        {
            SchemaVersion = this.SchemaVersion,
            Api = new ApiSettings
            {
                HubcapApiKey = this.Api.HubcapApiKey,
                UseIspBypass = this.Api.UseIspBypass,
                CustomWirecutterUrl = this.Api.CustomWirecutterUrl,
                TimeoutSeconds = this.Api.TimeoutSeconds
            },
            Download = new DownloadSettings
            {
                MaxDownloadsPerJob = this.Download.MaxDownloadsPerJob,
                MaxConcurrentQueueJobs = this.Download.MaxConcurrentQueueJobs,
                UseLanCache = this.Download.UseLanCache,
                ValidateDownloads = this.Download.ValidateDownloads,
                AutoSkipSingleChoice = this.Download.AutoSkipSingleChoice,
                SmartDepotSelection = this.Download.SmartDepotSelection,
                FilterMacOsDepots = this.Download.FilterMacOsDepots,
                FilterSoundtracks = this.Download.FilterSoundtracks,
                LimitToSteamLibraries = this.Download.LimitToSteamLibraries,
                DefaultDownloadDirectory = this.Download.DefaultDownloadDirectory
            },
            SlsSteam = new SlsSteamSettings
            {
                EnableSlsIntegration = this.SlsSteam.EnableSlsIntegration,
                CustomSteamPath = this.SlsSteam.CustomSteamPath,
                CustomSlsConfigPath = this.SlsSteam.CustomSlsConfigPath,
                PromptSteamRestart = this.SlsSteam.PromptSteamRestart,
                AutoInstallGoldberg = this.SlsSteam.AutoInstallGoldberg,
                AutoApplyGoldberg = this.SlsSteam.AutoApplyGoldberg
            },
            Library = new LibrarySettings
            {
                CheckUpdatesOnStartup = this.Library.CheckUpdatesOnStartup,
                UpdateCheckIntervalMinutes = this.Library.UpdateCheckIntervalMinutes,
                LibrarySortOption = this.Library.LibrarySortOption,
                ExcludedFromUpdateAll = new HashSet<uint>(this.Library.ExcludedFromUpdateAll)
            },
            Appearance = new AppearanceSettings
            {
                Theme = this.Appearance.Theme,
                AccentColor = this.Appearance.AccentColor,
                PresetName = this.Appearance.PresetName,
                FontFamily = this.Appearance.FontFamily,
                FontSize = this.Appearance.FontSize,
                NerdMode = this.Appearance.NerdMode
            },
            Advanced = new AdvancedSettings
            {
                LogLevel = this.Advanced.LogLevel,
                EnableRemoteWebUi = this.Advanced.EnableRemoteWebUi,
                WebUiPort = this.Advanced.WebUiPort,
                ManifestCacheRetentionDays = this.Advanced.ManifestCacheRetentionDays,
                WorkshopCellId = this.Advanced.WorkshopCellId,
                WorkshopMaxDownloads = this.Advanced.WorkshopMaxDownloads
            }
        };
    }
}
