using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Potato.Library.Services;

namespace Potato.UI.ViewModels;

public sealed partial class LibraryViewModel : ViewModelBase
{
    private readonly ILibraryScanner _scanner;
    private readonly IGameUpdateChecker _updateChecker;
    private readonly IGameUninstallService _uninstaller;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private string _totalSizeFormatted = "0 B";

    [ObservableProperty]
    private int _gamesCount;

    public ObservableCollection<InstalledGameViewModel> Games { get; } = new();

    public LibraryViewModel(
        ILibraryScanner scanner,
        IGameUpdateChecker updateChecker,
        IGameUninstallService uninstaller)
    {
        _scanner = scanner;
        _updateChecker = updateChecker;
        _uninstaller = uninstaller;
    }

    [RelayCommand]
    public async Task RefreshLibraryAsync()
    {
        IsLoading = true;
        StatusMessage = "Scanning Steam libraries...";

        try
        {
            var result = await _scanner.ScanLibrariesAsync();
            Games.Clear();

            foreach (var g in result.InstalledGames)
            {
                Games.Add(new InstalledGameViewModel(g));
            }

            GamesCount = result.TotalGames;
            TotalSizeFormatted = InstalledGameViewModel.FormatBytes(result.TotalSizeBytes);
            StatusMessage = $"Discovered {result.TotalGames} game(s) across {result.ScannedLibraries.Count} library directory(ies) ({result.Elapsed.TotalMilliseconds:N0} ms).";
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
        if (Games.Count == 0) return;

        IsLoading = true;
        StatusMessage = "Checking upstream updates for installed games...";

        try
        {
            int updates = 0;
            foreach (var g in Games)
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
                Games.Remove(game);
                GamesCount = Games.Count;
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
