using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Potato.Core.Models;
using Potato.Core.Services;
using Potato.Core.Steam;
using Potato.Core.Storage;
using Potato.Core.Slssteam;
using Potato.Downloader;
using Potato.UI.Helpers;
using Potato.UI.Models;

namespace Potato.UI.ViewModels;

public partial class LibraryViewModel : ViewModelBase
{
    private readonly ImageCacheService _imageCache = new();

    [ObservableProperty]
    private ObservableCollection<GameCardItem> _allGames = new();

    [ObservableProperty]
    private ObservableCollection<GameCardItem> _filteredGames = new();

    [ObservableProperty]
    private string _searchFilter = string.Empty;

    [ObservableProperty]
    private bool _onlyPotatoGames = true;

    [ObservableProperty]
    private bool _isScanning = false;

    [ObservableProperty]
    private string _statsSummary = "0 Games";

    [ObservableProperty]
    private string? _customSteamPath;

    [ObservableProperty]
    private string? _customSlsConfigPath;

    public event Action? RequestClose;

    [RelayCommand]
    public async Task Refresh()
    {
        IsScanning = true;
        AllGames.Clear();
        FilteredGames.Clear();

        try
        {
            var libs = SteamPathResolver.GetSteamLibraries(CustomSteamPath);
            var games = await LibraryScanner.ScanLibrariesAsync(
                libs,
                slsConfigPath: CustomSlsConfigPath,
                onlyPotatoManaged: OnlyPotatoGames
            );

            long totalBytes = 0;

            foreach (var g in games)
            {
                totalBytes += g.SizeOnDisk;
                var card = new GameCardItem
                {
                    AppId = g.AppId,
                    Name = g.Name,
                    FormattedSize = SpeedMonitor.FormatBytes(g.SizeOnDisk),
                    InstallDir = g.InstallDir,
                    LibraryPath = g.LibraryPath,
                    IsSlssteamHooked = g.IsSlssteamManaged,
                    StatusBadge = g.IsSlssteamManaged ? "SLSsteam Active" : "Installed"
                };

                // Asynchronously load thumbnail image
                _ = Task.Run(async () =>
                {
                    var cdnUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{g.AppId}/header.jpg";
                    var localImg = await _imageCache.EnsureImageCachedAsync(g.AppId, cdnUrl);
                    if (localImg != null && File.Exists(localImg))
                    {
                        var bmp = new Bitmap(localImg);
                        Dispatcher.UIThread.Post(() => card.HeaderImage = bmp);
                    }
                    else
                    {
                        var bmp = await AsyncBitmapLoader.LoadFromUrlAsync(cdnUrl);
                        if (bmp != null)
                        {
                            Dispatcher.UIThread.Post(() => card.HeaderImage = bmp);
                        }
                    }
                });

                AllGames.Add(card);
            }

            ApplyFilter();
            StatsSummary = $"{AllGames.Count} Games ({SpeedMonitor.FormatBytes(totalBytes)})";
        }
        catch (Exception ex)
        {
            StatsSummary = $"Error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    partial void OnSearchFilterChanged(string value) => ApplyFilter();
    partial void OnOnlyPotatoGamesChanged(bool value) => _ = Refresh();

    private void ApplyFilter()
    {
        FilteredGames.Clear();
        var filter = SearchFilter?.Trim().ToLowerInvariant() ?? "";

        foreach (var card in AllGames)
        {
            if (string.IsNullOrEmpty(filter) ||
                card.Name.ToLowerInvariant().Contains(filter) ||
                card.AppId.ToString().Contains(filter))
            {
                FilteredGames.Add(card);
            }
        }
    }

    [RelayCommand]
    private void OpenFolder(GameCardItem? card)
    {
        if (card == null) return;
        var path = AcfManager.GetGameDirectory(card.LibraryPath, card.AppId, card.Name, card.InstallDir);
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
    private void ToggleSlssteamHook(GameCardItem? card)
    {
        if (card == null) return;

        var slsConfigPath = SlsConfigManager.GetDefaultConfigPath(CustomSlsConfigPath);
        if (card.IsSlssteamHooked)
        {
            SlsConfigManager.RemoveAdditionalApp(slsConfigPath, card.AppId);
            card.IsSlssteamHooked = false;
            card.StatusBadge = "Installed";
        }
        else
        {
            SlsConfigManager.AddAdditionalApp(slsConfigPath, card.AppId, card.Name);
            card.IsSlssteamHooked = true;
            card.StatusBadge = "SLSsteam Active";
        }
    }

    [RelayCommand]
    private void LaunchGame(GameCardItem? card)
    {
        if (card == null) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "xdg-open",
                Arguments = $"steam://rungameid/{card.AppId}",
                UseShellExecute = true
            });
        }
        catch { }
    }

    [RelayCommand]
    private void Close()
    {
        RequestClose?.Invoke();
    }
}
