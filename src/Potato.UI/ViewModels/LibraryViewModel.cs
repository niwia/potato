using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Potato.Configuration.Services;
using Potato.Domain.ValueObjects;
using Potato.Library.Services;
using Potato.Pipeline.Models;
using Potato.Queue.Manager;
using Potato.SlsSteam.Config;
using Potato.SlsSteam.Ipc;

namespace Potato.UI.ViewModels;

public sealed partial class LibraryViewModel : ViewModelBase
{
    private readonly ILibraryScanner _scanner;
    private readonly IGameUpdateChecker _updateChecker;
    private readonly IGameUninstallService _uninstaller;
    private readonly ISlsConfigManager _slsConfigManager;
    private readonly ISlsSteamIpcClient _slsIpcClient;
    private readonly IDownloadQueueManager _queueManager;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private string _totalSizeFormatted = "0 B";

    [ObservableProperty]
    private int _gamesCount;

    [ObservableProperty]
    private string _filterQuery = "";

    [ObservableProperty]
    private bool _isGridView = true;

    [ObservableProperty]
    private InstalledGameViewModel? _selectedGame;

    [ObservableProperty]
    private bool _isGameDetailsOpen;

    private readonly List<InstalledGameViewModel> _allGames = new();

    public ObservableCollection<InstalledGameViewModel> Games { get; } = new();

    public LibraryViewModel(
        ILibraryScanner scanner,
        IGameUpdateChecker updateChecker,
        IGameUninstallService uninstaller,
        ISlsConfigManager slsConfigManager,
        ISlsSteamIpcClient slsIpcClient,
        IDownloadQueueManager queueManager,
        ISettingsService settingsService)
    {
        _scanner = scanner;
        _updateChecker = updateChecker;
        _uninstaller = uninstaller;
        _slsConfigManager = slsConfigManager;
        _slsIpcClient = slsIpcClient;
        _queueManager = queueManager;
        _settingsService = settingsService;
    }

    partial void OnFilterQueryChanged(string value)
    {
        ApplyFilter();
    }

    [RelayCommand]
    public void ToggleViewMode()
    {
        IsGridView = !IsGridView;
    }

    [RelayCommand]
    public void OpenGameDetails(InstalledGameViewModel? game)
    {
        if (game == null) return;
        SelectedGame = game;
        IsGameDetailsOpen = true;
    }

    [RelayCommand]
    public void CloseGameDetails()
    {
        IsGameDetailsOpen = false;
        SelectedGame = null;
    }

    [RelayCommand]
    public async Task UnlockSelectedGameSlsAsync()
    {
        if (SelectedGame == null) return;

        try
        {
            await _slsConfigManager.AddAdditionalAppAsync(SelectedGame.AppId, SelectedGame.Name);
            if (_slsIpcClient.IsPipeAvailable)
            {
                await _slsIpcClient.SendCommandAsync("reload");
            }
            StatusMessage = $"Unlocked '{SelectedGame.Name}' (AppID: {SelectedGame.AppId}) in SLSsteam!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to unlock in SLS: {ex.Message}";
        }
    }

    [RelayCommand]
    public void UpdateSelectedGame()
    {
        if (SelectedGame == null) return;

        string defaultDir = _settingsService.Current.Download.DefaultDownloadDirectory ?? SelectedGame.Model.SteamAppsPath;
        var req = new InstallRequest(
            SelectedGame.AppId,
            string.IsNullOrWhiteSpace(SelectedGame.Model.SteamAppsPath) ? defaultDir : SelectedGame.Model.SteamAppsPath,
            maxDownloads: _settingsService.Current.Download.MaxDownloadsPerJob,
            validate: true,
            useLanCache: _settingsService.Current.Download.UseLanCache,
            unlockSls: true);

        _queueManager.Enqueue(req, SelectedGame.Name);
        StatusMessage = $"Enqueued '{SelectedGame.Name}' for update/verification in download queue.";
    }

    private void ApplyFilter()
    {
        string q = FilterQuery.Trim().ToLowerInvariant();
        Games.Clear();

        var matches = string.IsNullOrWhiteSpace(q)
            ? _allGames
            : _allGames.Where(g => g.Name.ToLowerInvariant().Contains(q) || g.AppId.ToString().Contains(q)).ToList();

        foreach (var g in matches)
        {
            Games.Add(g);
        }

        GamesCount = Games.Count;
    }

    [RelayCommand]
    public async Task RefreshLibraryAsync()
    {
        IsLoading = true;
        StatusMessage = "Scanning Steam libraries...";

        try
        {
            var result = await _scanner.ScanLibrariesAsync();
            _allGames.Clear();

            foreach (var g in result.InstalledGames)
            {
                _allGames.Add(new InstalledGameViewModel(g));
            }

            TotalSizeFormatted = InstalledGameViewModel.FormatBytes(result.TotalSizeBytes);
            ApplyFilter();
            StatusMessage = $"Discovered {result.TotalGames} game(s) across {result.ScannedLibraries.Count} library directory(ies).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error scanning library: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task CheckUpdatesAsync()
    {
        if (_allGames.Count == 0) return;

        IsLoading = true;
        StatusMessage = "Checking upstream updates for installed games...";

        try
        {
            int updates = 0;
            foreach (var g in _allGames)
            {
                var check = await _updateChecker.CheckGameUpdateAsync(g.Model, "public");
                if (check.Status == Potato.Library.Models.UpdateStatus.UpdateAvailable)
                {
                    g.HasUpdate = true;
                    g.UpdateStatus = $"Update Available ({check.Reason})";
                    updates++;
                }
                else if (check.Status == Potato.Library.Models.UpdateStatus.UpToDate)
                {
                    g.HasUpdate = false;
                    g.UpdateStatus = "Up to date";
                }
                else
                {
                    g.HasUpdate = false;
                    g.UpdateStatus = check.Reason ?? "Unknown";
                }
            }

            StatusMessage = updates > 0 ? $"{updates} game(s) have updates available upstream." : "All installed games are up to date!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error checking updates: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task UninstallGameAsync(InstalledGameViewModel? game)
    {
        if (game == null) return;

        IsLoading = true;
        StatusMessage = $"Uninstalling {game.Name}...";

        try
        {
            bool success = await _uninstaller.UninstallGameAsync(game.Model);
            if (success)
            {
                _allGames.Remove(game);
                if (SelectedGame == game)
                {
                    CloseGameDetails();
                }
                ApplyFilter();
                StatusMessage = $"Successfully uninstalled '{game.Name}'.";
            }
            else
            {
                StatusMessage = $"Failed to uninstall '{game.Name}'.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error uninstalling game: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
