using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Potato.Queue.Manager;

namespace Potato.UI.ViewModels;

public sealed partial class MainViewModel : ViewModelBase
{
    public DashboardViewModel Dashboard { get; }
    public LibraryViewModel Library { get; }
    public SearchViewModel Search { get; }
    public QueueViewModel Queue { get; }
    public SlsToolsViewModel SlsTools { get; }
    public SettingsViewModel Settings { get; }

    [ObservableProperty]
    private string _currentTabName = "Dashboard";

    [ObservableProperty]
    private ViewModelBase _currentView;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SidebarWidth))]
    private bool _isSidebarCollapsed = false;

    public double SidebarWidth => IsSidebarCollapsed ? 64 : 240;

    [ObservableProperty]
    private string _activeDownloadStatus = "Queue Idle";

    [ObservableProperty]
    private int _runningDownloadsBadge = 0;

    [ObservableProperty]
    private int _installedGamesBadge = 0;

    public MainViewModel(
        DashboardViewModel dashboard,
        LibraryViewModel library,
        SearchViewModel search,
        QueueViewModel queue,
        SlsToolsViewModel slsTools,
        SettingsViewModel settings,
        IDownloadQueueManager queueManager)
    {
        Dashboard = dashboard;
        Library = library;
        Search = search;
        Queue = queue;
        SlsTools = slsTools;
        Settings = settings;
        _currentView = dashboard;

        queueManager.QueueSummaryUpdated += (s, e) =>
        {
            RunningDownloadsBadge = e.Summary.RunningCount;
            if (e.Summary.RunningCount > 0)
            {
                ActiveDownloadStatus = $"{e.Summary.RunningCount} Active • {e.Summary.FormattedSpeed}";
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

        // Initialize background data
        _ = Dashboard.RefreshDashboardAsync();
        _ = Library.RefreshLibraryAsync();
        _ = Search.InitializeAsync();
    }

    [RelayCommand]
    public void ToggleSidebar()
    {
        IsSidebarCollapsed = !IsSidebarCollapsed;
    }

    [RelayCommand]
    public void SwitchToDashboard()
    {
        CurrentTabName = "Dashboard";
        CurrentView = Dashboard;
        _ = Dashboard.RefreshDashboardAsync();
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
    public void SwitchToSlsTools()
    {
        CurrentTabName = "SlsTools";
        CurrentView = SlsTools;
        _ = SlsTools.InitializeAsync();
    }

    [RelayCommand]
    public void SwitchToSettings()
    {
        CurrentTabName = "Settings";
        CurrentView = Settings;
    }

    public void Navigate(string tabName)
    {
        switch (tabName.ToLowerInvariant())
        {
            case "dashboard": SwitchToDashboard(); break;
            case "library": SwitchToLibrary(); break;
            case "search": SwitchToSearch(); break;
            case "queue": SwitchToQueue(); break;
            case "slstools": SwitchToSlsTools(); break;
            case "settings": SwitchToSettings(); break;
        }
    }
}
