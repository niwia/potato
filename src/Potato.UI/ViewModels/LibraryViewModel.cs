using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Potato.Core.Models;
using Potato.Core.Steam;
using Potato.Core.Slssteam;

namespace Potato.UI.ViewModels;

public partial class LibraryViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<SteamApp> _installedGames = new();

    [ObservableProperty]
    private SteamApp? _selectedGame;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _statusText = "Ready";

    public event Action? RequestClose;

    [RelayCommand]
    public async Task Refresh()
    {
        IsScanning = true;
        StatusText = "Scanning Steam libraries...";
        InstalledGames.Clear();

        try
        {
            var libs = SteamPathResolver.GetSteamLibraries();
            var games = await LibraryScanner.ScanLibrariesAsync(libs);

            var slsConfigPath = SlsConfigManager.GetDefaultConfigPath();
            var slsApps = SlsConfigManager.GetAdditionalApps(slsConfigPath);

            foreach (var g in games)
            {
                var isManaged = slsApps.Contains(g.AppId);
                InstalledGames.Add(g with { IsSlssteamManaged = isManaged });
            }

            StatusText = $"Found {InstalledGames.Count} game(s).";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private void OpenFolder()
    {
        if (SelectedGame == null) return;
        var path = AcfManager.GetGameDirectory(SelectedGame.LibraryPath, SelectedGame.AppId, SelectedGame.Name, SelectedGame.InstallDir);
        if (Directory.Exists(path))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "xdg-open",
                Arguments = $"\"{path}\"",
                UseShellExecute = true
            });
        }
    }

    [RelayCommand]
    private void ToggleSlssteam()
    {
        if (SelectedGame == null) return;

        var slsConfigPath = SlsConfigManager.GetDefaultConfigPath();
        if (SelectedGame.IsSlssteamManaged)
        {
            SlsConfigManager.RemoveAdditionalApp(slsConfigPath, SelectedGame.AppId);
            StatusText = $"Removed App {SelectedGame.AppId} from SLSsteam config.";
        }
        else
        {
            SlsConfigManager.AddAdditionalApp(slsConfigPath, SelectedGame.AppId, SelectedGame.Name);
            StatusText = $"Added App {SelectedGame.AppId} to SLSsteam config.";
        }

        _ = Refresh();
    }

    [RelayCommand]
    private void Close()
    {
        RequestClose?.Invoke();
    }
}
