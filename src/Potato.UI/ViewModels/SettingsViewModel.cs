using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Potato.Core.Models;
using Potato.Core.Storage;

namespace Potato.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly SettingsManager _settingsManager;

    [ObservableProperty]
    private string? _customSteamPath;

    [ObservableProperty]
    private string? _customSlssteamConfigPath;

    [ObservableProperty]
    private bool _slssteamModeEnabled;

    [ObservableProperty]
    private bool _autoGenerateAcf;

    [ObservableProperty]
    private string? _hubcapApiKey;

    [ObservableProperty]
    private string? _customDepotDownloaderPath;

    public event Action? RequestClose;

    public SettingsViewModel(SettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
        var s = settingsManager.Current;

        _customSteamPath = s.CustomSteamPath;
        _customSlssteamConfigPath = s.CustomSlssteamConfigPath;
        _slssteamModeEnabled = s.SlssteamModeEnabled;
        _autoGenerateAcf = s.AutoGenerateAcf;
        _hubcapApiKey = s.HubcapApiKey;
        _customDepotDownloaderPath = s.CustomDepotDownloaderPath;
    }

    [RelayCommand]
    private void Save()
    {
        var s = new AppSettings
        {
            CustomSteamPath = CustomSteamPath,
            CustomSlssteamConfigPath = CustomSlssteamConfigPath,
            SlssteamModeEnabled = SlssteamModeEnabled,
            AutoGenerateAcf = AutoGenerateAcf,
            HubcapApiKey = HubcapApiKey,
            CustomDepotDownloaderPath = CustomDepotDownloaderPath
        };

        _settingsManager.Save(s);
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke();
    }
}
