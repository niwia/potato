using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Potato.Domain.ValueObjects;
using Potato.SlsSteam.Config;
using Potato.SlsSteam.Ipc;
using Potato.SlsSteam.Paths;

namespace Potato.UI.ViewModels;

public sealed partial class SlsItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _key;

    [ObservableProperty]
    private string _value;

    [ObservableProperty]
    private string _detail;

    public SlsItemViewModel(string key, string value, string detail = "")
    {
        _key = key;
        _value = value;
        _detail = detail;
    }
}

public sealed partial class SlsToolsViewModel : ViewModelBase
{
    private readonly ISlsConfigManager _configManager;
    private readonly ISlsSteamPathResolver _pathResolver;
    private readonly ISlsSteamIpcClient _ipcClient;

    [ObservableProperty]
    private string _configPath = "";

    [ObservableProperty]
    private bool _configExists;

    [ObservableProperty]
    private bool _isPipeActive;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private string _newAppIdText = "";

    [ObservableProperty]
    private string _newAppNameText = "";

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<SlsItemViewModel> AdditionalApps { get; } = new();
    public ObservableCollection<SlsItemViewModel> AppTokens { get; } = new();

    public SlsToolsViewModel(
        ISlsConfigManager configManager,
        ISlsSteamPathResolver pathResolver,
        ISlsSteamIpcClient ipcClient)
    {
        _configManager = configManager;
        _pathResolver = pathResolver;
        _ipcClient = ipcClient;

        ConfigPath = _pathResolver.ConfigPath;
    }

    public async Task InitializeAsync()
    {
        await ReloadConfigAsync();
    }

    [RelayCommand]
    public async Task ReloadConfigAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading SLSsteam configuration...";

        try
        {
            ConfigPath = _pathResolver.ConfigPath;
            ConfigExists = File.Exists(ConfigPath);
            IsPipeActive = _ipcClient.IsPipeAvailable;

            if (!ConfigExists)
            {
                StatusMessage = $"Config file not found at: {ConfigPath}";
                return;
            }

            var model = await _configManager.LoadAsync();
            AdditionalApps.Clear();
            foreach (var kvp in model.AdditionalApps)
            {
                AdditionalApps.Add(new SlsItemViewModel(kvp.Key.ToString(), kvp.Value));
            }

            AppTokens.Clear();
            foreach (var kvp in model.AppTokens)
            {
                AppTokens.Add(new SlsItemViewModel(kvp.Key.ToString(), kvp.Value?.ToString() ?? ""));
            }

            StatusMessage = $"Loaded {AdditionalApps.Count} AdditionalApps and {AppTokens.Count} AppTokens.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading config: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task AddAppAsync()
    {
        if (string.IsNullOrWhiteSpace(NewAppIdText) || !AppId.TryParse(NewAppIdText.Trim(), out var appId))
        {
            StatusMessage = "Invalid AppID format.";
            return;
        }

        string gameName = string.IsNullOrWhiteSpace(NewAppNameText) ? $"App {appId}" : NewAppNameText.Trim();

        try
        {
            await _configManager.AddAdditionalAppAsync(appId, gameName);
            NewAppIdText = "";
            NewAppNameText = "";
            StatusMessage = $"Successfully added {gameName} ({appId}) to AdditionalApps.";
            await ReloadConfigAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to add app: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task RemoveAppAsync(SlsItemViewModel? item)
    {
        if (item == null || !AppId.TryParse(item.Key, out var appId)) return;

        try
        {
            await _configManager.RemoveAdditionalAppAsync(appId);
            StatusMessage = $"Removed {item.Value} ({appId}) from AdditionalApps.";
            await ReloadConfigAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to remove app: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task HealConfigAsync()
    {
        if (!File.Exists(ConfigPath))
        {
            StatusMessage = "Config file does not exist to heal.";
            return;
        }

        try
        {
            string yaml = await File.ReadAllTextAsync(ConfigPath);
            var model = SlsConfigHealer.ParseAndHeal(yaml);
            await _configManager.SaveAsync(model, ConfigPath);
            StatusMessage = "Config YAML successfully validated, healed, and saved in-place.";
            await ReloadConfigAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error healing config: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task SendReloadPipeAsync()
    {
        try
        {
            if (!_ipcClient.IsPipeAvailable)
            {
                StatusMessage = "SLSsteam IPC pipe is not active (Steam or SLSsteam might not be running).";
                return;
            }

            var response = await _ipcClient.SendCommandAsync("reload");
            StatusMessage = $"IPC Reload Command sent successfully. Response: {response}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"IPC Error: {ex.Message}";
        }
    }

    [RelayCommand]
    public void OpenConfigFile()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = $"\"{ConfigPath}\"",
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to open file: {ex.Message}";
        }
    }
}
