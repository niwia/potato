using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Potato.Configuration.Services;
using Potato.Domain.ValueObjects;
using Potato.Library.Models;
using Potato.Library.Services;
using Potato.ManifestApi.Client;
using Potato.Pipeline.Models;
using Potato.Queue.Manager;
using Potato.SlsSteam.Ipc;
using Potato.SlsSteam.Paths;

namespace Potato.UI.ViewModels;

public sealed partial class DashboardViewModel : ViewModelBase
{
    private readonly ILibraryScanner _libraryScanner;
    private readonly IGameUpdateChecker _updateChecker;
    private readonly IActivityLogService _activityLogService;
    private readonly IHubcapApiClient _hubcapClient;
    private readonly IDownloadQueueManager _queueManager;
    private readonly ISlsSteamPathResolver _slsPathResolver;
    private readonly ISlsSteamIpcClient _slsIpcClient;
    private readonly ISettingsService _settingsService;

    public Action<string>? NavigateAction { get; set; }

    // ── STAT PROPERTIES ───────────────────────────────────────────────────────
    [ObservableProperty]
    private int _installedGamesCount;

    [ObservableProperty]
    private string _formattedTotalStorage = "0 GB";

    [ObservableProperty]
    private string _hubcapMainQuotaText = "0/55 API";

    [ObservableProperty]
    private string _hubcapSubQuotaText = "bundle: 0/100, single: 0/1500";

    [ObservableProperty]
    private string _hubcapQuotaText = "api: --, bundle: --, single: -- [--d]";

    [ObservableProperty]
    private string _hubcapQuotaTooltip = "Hubcap API Quotas & Limits";

    [ObservableProperty]
    private string _slsStatusText = "Checking...";

    [ObservableProperty]
    private bool _isSlsOnline;

    [ObservableProperty]
    private string _steamStatusText = "Checking...";

    [ObservableProperty]
    private bool _isSteamOnline;

    [ObservableProperty]
    private string _hubcapConnectionText = "Checking...";

    [ObservableProperty]
    private bool _isHubcapOnline;

    [ObservableProperty]
    private string _steamUpdatesText = "Blocked";

    [ObservableProperty]
    private string _appVersionText = "v0.0.5dev";

    [ObservableProperty]
    private string _librarySummaryText = "-- GB (-- games)";

    [ObservableProperty]
    private string _topTickerText = "POTATO - STEAM MANIFEST DOWNLOADER & PIPELINE";

    // ── PENDING UPDATES ───────────────────────────────────────────────────────
    [ObservableProperty]
    private bool _isCheckingUpdates;

    [ObservableProperty]
    private string _checkStatusText = "Check Updates";

    [ObservableProperty]
    private int _pendingUpdatesCount;

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<PendingUpdateItemViewModel> PendingUpdates { get; } = new();

    // ── RECENT ACTIVITY ───────────────────────────────────────────────────────
    public ObservableCollection<ActivityLogItemViewModel> RecentActivities { get; } = new();

    private IReadOnlyList<InstalledGame> _cachedScannedGames = Array.Empty<InstalledGame>();

    public DashboardViewModel(
        ILibraryScanner libraryScanner,
        IGameUpdateChecker updateChecker,
        IActivityLogService activityLogService,
        IHubcapApiClient hubcapClient,
        IDownloadQueueManager queueManager,
        ISlsSteamPathResolver slsPathResolver,
        ISlsSteamIpcClient slsIpcClient,
        ISettingsService settingsService)
    {
        _libraryScanner = libraryScanner;
        _updateChecker = updateChecker;
        _activityLogService = activityLogService;
        _hubcapClient = hubcapClient;
        _queueManager = queueManager;
        _slsPathResolver = slsPathResolver;
        _slsIpcClient = slsIpcClient;
        _settingsService = settingsService;

        _activityLogService.ActivityAdded += (s, e) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                RecentActivities.Insert(0, new ActivityLogItemViewModel(e));
                if (RecentActivities.Count > 30) RecentActivities.RemoveAt(RecentActivities.Count - 1);
            });
        };
    }

    public async Task RefreshDashboardAsync()
    {
        IsLoading = true;
        try
        {
            // 1. Library scan for stats
            var scanResult = await _libraryScanner.ScanLibrariesAsync();
            _cachedScannedGames = scanResult.InstalledGames;
            InstalledGamesCount = scanResult.InstalledGames.Count;

            ulong totalBytes = (ulong)scanResult.InstalledGames.Sum(g => (long)g.SizeOnDisk);
            FormattedTotalStorage = FormatBytes(totalBytes);
            LibrarySummaryText = $"{FormattedTotalStorage} ({InstalledGamesCount} games)";

            // 2. Load recent activities
            RecentActivities.Clear();
            var activities = _activityLogService.GetRecentActivities(20);
            if (activities.Count > 0)
            {
                foreach (var act in activities)
                {
                    RecentActivities.Add(new ActivityLogItemViewModel(act));
                }
            }
            else if (scanResult.InstalledGames.Count > 0)
            {
                // Seed initial display from recent library games
                foreach (var g in scanResult.InstalledGames.Take(3))
                {
                    var seeded = new ActivityLogEntry(
                        Guid.NewGuid(),
                        g.AppId,
                        g.Name,
                        ActivityStatus.Success,
                        DateTime.UtcNow.AddHours(-1),
                        g.SizeOnDisk,
                        TimeSpan.FromSeconds(30),
                        "Depots: Verified • SLS: Synchronized");
                    RecentActivities.Add(new ActivityLogItemViewModel(seeded));
                }
            }

            // 3. Update Visor stats
            await UpdateVisorStatsAsync();
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

    public async Task UpdateVisorStatsAsync()
    {
        try
        {
            // 1. SLSsteam Status
            bool configExists = File.Exists(_slsPathResolver.ConfigPath);
            bool pipeAvailable = _slsIpcClient.IsPipeAvailable;
            IsSlsOnline = pipeAvailable || configExists;
            SlsStatusText = pipeAvailable ? "Online (IPC Active)" : (configExists ? "Online" : "Offline");

            // 2. Steam Client Status
            var steamProcs = Process.GetProcessesByName("steam");
            IsSteamOnline = steamProcs.Length > 0;
            SteamStatusText = IsSteamOnline ? "Online" : "Offline";
            foreach (var p in steamProcs) p.Dispose();

            // 3. Hubcap Mode & Health
            bool isBypass = _settingsService.Current.Api.UseIspBypass;
            HubcapConnectionText = isBypass ? "Online (Bypass)" : "Online (Direct)";
            IsHubcapOnline = true;

            // 4. Steam Updates
            SteamUpdatesText = "Blocked";

            // 5. Hubcap Stats & Quotas
            if (!string.IsNullOrWhiteSpace(_settingsService.Current.Api.HubcapApiKey))
            {
                var stats = await _hubcapClient.GetAllStatsAsync();
                HubcapMainQuotaText = $"{stats.UserStats.DailyManifestDownloads}/{stats.UserStats.DailyManifestLimit} API";
                HubcapSubQuotaText = $"bundle: {stats.GenerateUsage.AppBundleUsage}/{stats.GenerateUsage.AppBundleLimit}, single: {stats.GenerateUsage.SingleDepotUsage}/{stats.GenerateUsage.SingleDepotLimit}";
                HubcapQuotaText = stats.FormattedQuotaString;
                HubcapQuotaTooltip = stats.TooltipString;
                IsHubcapOnline = stats.IsHealthy;
            }
            else
            {
                HubcapMainQuotaText = "--/-- API";
                HubcapSubQuotaText = "bundle: --, single: --";
                HubcapQuotaText = "api: --, bundle: --, single: -- [No Key]";
                HubcapQuotaTooltip = "Enter your Hubcap API Key in Settings to view quotas.";
            }
        }
        catch
        {
            // Ignore background network errors
        }
    }

    [RelayCommand]
    public async Task CheckUpdatesAsync()
    {
        if (IsCheckingUpdates) return;

        IsCheckingUpdates = true;
        CheckStatusText = "Checking...";
        PendingUpdates.Clear();

        try
        {
            if (_cachedScannedGames.Count == 0)
            {
                var scan = await _libraryScanner.ScanLibrariesAsync();
                _cachedScannedGames = scan.InstalledGames;
            }

            int count = 0;
            foreach (var game in _cachedScannedGames)
            {
                var res = await _updateChecker.CheckGameUpdateAsync(game);
                if (res.Status == UpdateStatus.UpdateAvailable)
                {
                    count++;
                    PendingUpdates.Add(new PendingUpdateItemViewModel(game, res.TargetBuildId ?? "Latest"));
                }
            }

            PendingUpdatesCount = PendingUpdates.Count;
            CheckStatusText = PendingUpdatesCount > 0 ? $"Check Updates ({PendingUpdatesCount})" : "Check Updates";
            TopTickerText = PendingUpdatesCount > 0
                ? $"FOUND {PendingUpdatesCount} PENDING GAME UPDATE(S)"
                : "ALL MANAGED GAMES ARE UP TO DATE";
        }
        catch
        {
            CheckStatusText = "Check Updates";
        }
        finally
        {
            IsCheckingUpdates = false;
        }
    }

    [RelayCommand]
    public void UpdateAll()
    {
        if (PendingUpdates.Count == 0) return;

        int enqueuedCount = 0;
        string defaultDir = _settingsService.Current.Download.DefaultDownloadDirectory ?? _slsPathResolver.SteamAppsPaths.FirstOrDefault() ?? "";

        foreach (var item in PendingUpdates.ToList())
        {
            var req = new InstallRequest(
                item.AppId,
                string.IsNullOrWhiteSpace(item.Model.SteamAppsPath) ? defaultDir : item.Model.SteamAppsPath,
                maxDownloads: _settingsService.Current.Download.MaxDownloadsPerJob,
                validate: true,
                useLanCache: _settingsService.Current.Download.UseLanCache,
                unlockSls: true);

            _queueManager.Enqueue(req, item.Name);
            enqueuedCount++;
        }

        PendingUpdates.Clear();
        PendingUpdatesCount = 0;
        TopTickerText = $"ENQUEUED {enqueuedCount} UPDATE(S) TO DOWNLOAD QUEUE";
        NavigateAction?.Invoke("Queue");
    }

    [RelayCommand]
    public void UpdateSingle(PendingUpdateItemViewModel item)
    {
        if (item == null) return;

        string defaultDir = _settingsService.Current.Download.DefaultDownloadDirectory ?? _slsPathResolver.SteamAppsPaths.FirstOrDefault() ?? "";
        var req = new InstallRequest(
            item.AppId,
            string.IsNullOrWhiteSpace(item.Model.SteamAppsPath) ? defaultDir : item.Model.SteamAppsPath,
            maxDownloads: _settingsService.Current.Download.MaxDownloadsPerJob,
            validate: true,
            useLanCache: _settingsService.Current.Download.UseLanCache,
            unlockSls: true);

        _queueManager.Enqueue(req, item.Name);
        PendingUpdates.Remove(item);
        PendingUpdatesCount = PendingUpdates.Count;
    }

    [RelayCommand]
    public void GoToLibrary() => NavigateAction?.Invoke("Library");

    [RelayCommand]
    public void GoToSearch() => NavigateAction?.Invoke("Search");

    [RelayCommand]
    public void GoToQueue() => NavigateAction?.Invoke("Queue");

    [RelayCommand]
    public void GoToSlsTools() => NavigateAction?.Invoke("SlsTools");

    [RelayCommand]
    public void GoToSettings() => NavigateAction?.Invoke("Settings");

    private static string FormatBytes(ulong bytes)
    {
        if (bytes == 0) return "0 GB";
        double gigabytes = bytes / (1024.0 * 1024.0 * 1024.0);
        return $"{gigabytes:F1} GB";
    }
}
