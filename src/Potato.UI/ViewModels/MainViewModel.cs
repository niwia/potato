using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Potato.Queue.Manager;

namespace Potato.UI.ViewModels;

public sealed partial class MainViewModel : ViewModelBase
{
    public LibraryViewModel Library { get; }
    public SearchViewModel Search { get; }
    public QueueViewModel Queue { get; }
    public SettingsViewModel Settings { get; }

    [ObservableProperty]
    private string _currentTabName = "Library";

    [ObservableProperty]
    private ViewModelBase _currentView;

    [ObservableProperty]
    private string _activeDownloadStatus = "Queue Idle";

    public MainViewModel(
        LibraryViewModel library,
        SearchViewModel search,
        QueueViewModel queue,
        SettingsViewModel settings,
        IDownloadQueueManager queueManager)
    {
        Library = library;
        Search = search;
        Queue = queue;
        Settings = settings;
        _currentView = library;

        queueManager.QueueSummaryUpdated += (s, e) =>
        {
            if (e.Summary.RunningCount > 0)
            {
                ActiveDownloadStatus = $"Downloading ({e.Summary.RunningCount} active) • {e.Summary.FormattedSpeed}";
            }
            else if (e.Summary.QueuedCount > 0)
            {
                ActiveDownloadStatus = $"{e.Summary.QueuedCount} job(s) queued";
            }
            else
            {
                ActiveDownloadStatus = "Queue Idle";
            }
        };

        // Initial library scan
        _ = Library.RefreshLibraryAsync();
        _ = Search.InitializeAsync();
    }

    [RelayCommand]
    public void SwitchToLibrary()
    {
        CurrentTabName = "Library";
        CurrentView = Library;
    }

    [RelayCommand]
    public void SwitchToSearch()
    {
        CurrentTabName = "Search";
        CurrentView = Search;
    }

    [RelayCommand]
    public void SwitchToQueue()
    {
        CurrentTabName = "Queue";
        CurrentView = Queue;
    }

    [RelayCommand]
    public void SwitchToSettings()
    {
        CurrentTabName = "Settings";
        CurrentView = Settings;
    }
}
