using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Potato.Configuration.Models;
using Potato.Configuration.Services;

namespace Potato.UI.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private string? _hubcapApiKey;

    [ObservableProperty]
    private bool _useIspBypass;

    [ObservableProperty]
    private string? _customWirecutterUrl;

    [ObservableProperty]
    private int _maxDownloadsPerJob;

    [ObservableProperty]
    private int _maxConcurrentQueueJobs;

    [ObservableProperty]
    private bool _useLanCache;

    [ObservableProperty]
    private bool _validateDownloads;

    [ObservableProperty]
    private bool _filterSoundtracks;

    [ObservableProperty]
    private bool _filterMacOsDepots;

    [ObservableProperty]
    private bool _limitToSteamLibraries;

    [ObservableProperty]
    private string? _defaultDownloadDirectory;

    [ObservableProperty]
    private bool _enableSlsIntegration;

    [ObservableProperty]
    private string? _customSteamPath;

    [ObservableProperty]
    private string? _customSlsConfigPath;

    [ObservableProperty]
    private bool _checkUpdatesOnStartup;

    [ObservableProperty]
    private string _theme = "Dark";

    [ObservableProperty]
    private string _accentColor = "#7C4DFF";

    [ObservableProperty]
    private bool _nerdMode;

    [ObservableProperty]
    private string? _statusMessage;

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        LoadFromCurrentSettings();
    }

    public void LoadFromCurrentSettings()
    {
        var s = _settingsService.Current;
        HubcapApiKey = s.Api.HubcapApiKey;
        UseIspBypass = s.Api.UseIspBypass;
        CustomWirecutterUrl = s.Api.CustomWirecutterUrl;
        MaxDownloadsPerJob = s.Download.MaxDownloadsPerJob;
        MaxConcurrentQueueJobs = s.Download.MaxConcurrentQueueJobs;
        UseLanCache = s.Download.UseLanCache;
        ValidateDownloads = s.Download.ValidateDownloads;
        FilterSoundtracks = s.Download.FilterSoundtracks;
        FilterMacOsDepots = s.Download.FilterMacOsDepots;
        LimitToSteamLibraries = s.Download.LimitToSteamLibraries;
        DefaultDownloadDirectory = s.Download.DefaultDownloadDirectory;
        EnableSlsIntegration = s.SlsSteam.EnableSlsIntegration;
        CustomSteamPath = s.SlsSteam.CustomSteamPath;
        CustomSlsConfigPath = s.SlsSteam.CustomSlsConfigPath;
        CheckUpdatesOnStartup = s.Library.CheckUpdatesOnStartup;
        Theme = s.Appearance.Theme;
        AccentColor = s.Appearance.AccentColor;
        NerdMode = s.Appearance.NerdMode;
    }

    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        try
        {
            await _settingsService.UpdateAsync(s =>
            {
                s.Api.HubcapApiKey = HubcapApiKey?.Trim();
                s.Api.UseIspBypass = UseIspBypass;
                s.Api.CustomWirecutterUrl = CustomWirecutterUrl?.Trim();
                s.Download.MaxDownloadsPerJob = MaxDownloadsPerJob;
                s.Download.MaxConcurrentQueueJobs = MaxConcurrentQueueJobs;
                s.Download.UseLanCache = UseLanCache;
                s.Download.ValidateDownloads = ValidateDownloads;
                s.Download.FilterSoundtracks = FilterSoundtracks;
                s.Download.FilterMacOsDepots = FilterMacOsDepots;
                s.Download.LimitToSteamLibraries = LimitToSteamLibraries;
                s.Download.DefaultDownloadDirectory = DefaultDownloadDirectory?.Trim();
                s.SlsSteam.EnableSlsIntegration = EnableSlsIntegration;
                s.SlsSteam.CustomSteamPath = CustomSteamPath?.Trim();
                s.SlsSteam.CustomSlsConfigPath = CustomSlsConfigPath?.Trim();
                s.Library.CheckUpdatesOnStartup = CheckUpdatesOnStartup;
                s.Appearance.Theme = Theme;
                s.Appearance.AccentColor = AccentColor;
                s.Appearance.NerdMode = NerdMode;
            });

            StatusMessage = "Settings saved successfully to settings.json.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving settings: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task ImportFromAccelaAsync()
    {
        try
        {
            var imported = await _settingsService.ImportFromLegacyConfigAsync();
            LoadFromCurrentSettings();
            StatusMessage = "Successfully imported legacy settings from ACCELA.conf!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task ResetDefaultsAsync()
    {
        try
        {
            await _settingsService.ResetToDefaultsAsync();
            LoadFromCurrentSettings();
            StatusMessage = "Settings reset to defaults.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error resetting settings: {ex.Message}";
        }
    }
}
