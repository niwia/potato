using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Potato.Library.Models;
using Potato.Library.Services;
using Potato.Queue.Manager;
using Potato.SlsSteam.Ipc;
using Potato.SlsSteam.Paths;

namespace Potato.UI.ViewModels;

public sealed partial class DashboardViewModel : ViewModelBase
{
    private readonly ILibraryScanner _libraryScanner;
    private readonly IDownloadQueueManager _queueManager;
    private readonly ISlsSteamPathResolver _slsPathResolver;
    private readonly ISlsSteamIpcClient _slsIpcClient;

    public Action<string>? NavigateAction { get; set; }

    [ObservableProperty]
    private int _installedGamesCount;

    [ObservableProperty]
    private string _formattedTotalStorage = "0 GB";

    [ObservableProperty]
    private int _activeDownloadsCount;

    [ObservableProperty]
    private string _downloadSpeedText = "0 KB/s";

    [ObservableProperty]
    private int _queuedJobsCount;

    [ObservableProperty]
    private string _slsSteamStatus = "Not Connected";

    [ObservableProperty]
    private bool _isSlsAvailable;

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<InstalledGameViewModel> RecentGames { get; } = new();

    public DashboardViewModel(
        ILibraryScanner libraryScanner,
        IDownloadQueueManager queueManager,
        ISlsSteamPathResolver slsPathResolver,
        ISlsSteamIpcClient slsIpcClient)
    {
        _libraryScanner = libraryScanner;
        _queueManager = queueManager;
        _slsPathResolver = slsPathResolver;
        _slsIpcClient = slsIpcClient;

        _queueManager.QueueSummaryUpdated += (s, e) =>
        {
            ActiveDownloadsCount = e.Summary.RunningCount;
            QueuedJobsCount = e.Summary.QueuedCount;
            DownloadSpeedText = e.Summary.FormattedSpeed;
        };

        UpdateSlsStatus();
    }

    public async Task RefreshDashboardAsync()
    {
        IsLoading = true;
        try
        {
            var scanResult = await _libraryScanner.ScanLibrariesAsync();
            InstalledGamesCount = scanResult.InstalledGames.Count;

            ulong totalBytes = (ulong)scanResult.InstalledGames.Sum(g => (long)g.SizeOnDisk);
            FormattedTotalStorage = FormatBytes(totalBytes);

            RecentGames.Clear();
            foreach (var g in scanResult.InstalledGames.Take(8))
            {
                RecentGames.Add(new InstalledGameViewModel(g));
            }

            UpdateSlsStatus();
        }
        catch
        {
            // Ignore background refresh errors
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdateSlsStatus()
    {
        bool configExists = File.Exists(_slsPathResolver.ConfigPath);
        bool pipeAvailable = _slsIpcClient.IsPipeAvailable;

        IsSlsAvailable = configExists;
        if (pipeAvailable)
        {
            SlsSteamStatus = "Connected (IPC Active)";
        }
        else if (configExists)
        {
            SlsSteamStatus = "Config Available (Idle)";
        }
        else
        {
            SlsSteamStatus = "Not Detected";
        }
    }

    [RelayCommand]
    public void GoToLibrary() => NavigateAction?.Invoke("Library");

    [RelayCommand]
    public void GoToSearch() => NavigateAction?.Invoke("Search");

    [RelayCommand]
    public void GoToQueue() => NavigateAction?.Invoke("Queue");

    [RelayCommand]
    public void GoToSlsTools() => NavigateAction?.Invoke("SlsTools");

    private static string FormatBytes(ulong bytes)
    {
        if (bytes == 0) return "0 GB";
        double gigabytes = bytes / (1024.0 * 1024.0 * 1024.0);
        return $"{gigabytes:F1} GB";
    }
}
