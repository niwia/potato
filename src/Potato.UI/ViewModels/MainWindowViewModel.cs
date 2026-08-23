using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
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

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly SteamApiClient _steamApiClient = new();
    private readonly DownloadJobQueue _jobQueue = new();
    private readonly SettingsManager _settingsManager = new();
    private readonly DatabaseManager _dbManager = new();
    private readonly ImageCacheService _imageCache = new();
    private CancellationTokenSource? _toastCts;

    private static readonly string[] GamingQuotes = new[]
    {
        "The cake is a lie. — Portal",
        "Praise the Sun! — Dark Souls",
        "Wake up, Samurai. We have a city to burn. — Cyberpunk 2077",
        "Protocol 3: Protect the Pilot. — Titanfall 2",
        "War. War never changes. — Fallout",
        "Stay a while and listen. — Diablo II",
        "Nothing is true, everything is permitted. — Assassin's Creed",
        "Hesitation is defeat. — Sekiro"
    };

    // ── Header & Status ──
    [ObservableProperty]
    private string _versionTag = "2.0.0dev";

    [ObservableProperty]
    private string _statusTickerText = "READY • STANDING BY";

    [ObservableProperty]
    private string _systemStatsLine1 = "Hubcap: Online   SLSsteam: Checking...   Steam: Checking...";

    [ObservableProperty]
    private string _systemStatsLine2 = "Steam Updates: Managed   Potato: Up to Date   Library: 0 GB (0 games)";

    [ObservableProperty]
    private string _easterEggQuote = "The cake is a lie. — Portal";

    // ── Toast Notification ──
    [ObservableProperty]
    private ToastMessage _toast = new();

    // ── Search & Quick Deploy ──
    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<StoreSearchResult> _searchResults = new();

    [ObservableProperty]
    private bool _isSearching = false;

    // ── Active Download Hero Card ──
    [ObservableProperty]
    private string _activeGameName = "Ready";

    [ObservableProperty]
    private string _activeStatusMessage = "All systems operational";

    [ObservableProperty]
    private double _activeProgressPercent = 0.0;

    [ObservableProperty]
    private string _activeSpeedText = "0 B/s";

    [ObservableProperty]
    private string _activeEtaText = "--:--";

    [ObservableProperty]
    private bool _hasActiveDownload = false;

    [ObservableProperty]
    private Bitmap? _activeHeroImage;

    // ── Recent Activity & Live Logs ──
    [ObservableProperty]
    private ObservableCollection<RecentActivityItem> _recentActivities = new();

    [ObservableProperty]
    private ObservableCollection<string> _logLines = new();

    // Dialog & Window delegates
    public Func<DepotSelectionViewModel, Task<bool>>? ShowDepotSelectionDialogAsync { get; set; }
    public Func<LibraryViewModel, Task>? ShowLibraryDialogAsync { get; set; }
    public Func<SettingsViewModel, Task>? ShowSettingsDialogAsync { get; set; }
    public Action? RequestMinimize { get; set; }
    public Action? RequestMaximize { get; set; }
    public Action? RequestClose { get; set; }

    public MainWindowViewModel()
    {
        _jobQueue.JobStarted += OnJobStarted;
        _jobQueue.JobProgress += OnJobProgress;
        _jobQueue.JobCompleted += OnJobCompleted;
        _jobQueue.JobFailed += OnJobFailed;
        _jobQueue.LogMessage += AddLog;

        var rand = new Random();
        EasterEggQuote = GamingQuotes[rand.Next(GamingQuotes.Length)];

        AddLog("🥔 Project Potato initialized.");
        AddLog("💻 Modern Linux & Steam Deck Orchestrator.");

        RecentActivities.Add(new RecentActivityItem
        {
            GameName = "Counter-Strike 2",
            AppId = 730,
            Timestamp = DateTime.Now.ToString("HH:mm"),
            MetricsSummary = "Ready • System Verified",
            Details = "ACF: Available • SLSsteam: Ready",
            IsSuccess = true
        });

        RefreshSystemStats();
    }

    public void ShowToast(string message, string icon = "ℹ️", string badgeColor = "#61AFEF", int durationMs = 3500)
    {
        _toastCts?.Cancel();
        _toastCts = new CancellationTokenSource();
        var ct = _toastCts.Token;

        Dispatcher.UIThread.Post(() =>
        {
            Toast.Message = message;
            Toast.Icon = icon;
            Toast.BadgeColor = badgeColor;
            Toast.IsVisible = true;
        });

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(durationMs, ct);
                if (!ct.IsCancellationRequested)
                {
                    Dispatcher.UIThread.Post(() => Toast.IsVisible = false);
                }
            }
            catch (TaskCanceledException) { }
        });
    }

    public void RefreshSystemStats()
    {
        var steamPath = SteamPathResolver.FindSteamInstall(_settingsManager.Current.CustomSteamPath);
        var slsConfig = SlsConfigManager.GetDefaultConfigPath(_settingsManager.Current.CustomSlssteamConfigPath);

        string steamStatus = !string.IsNullOrEmpty(steamPath) ? "Online" : "Offline";
        string slsStatus = File.Exists(slsConfig) ? "Online" : "Config Missing";

        _ = Task.Run(async () =>
        {
            var libs = SteamPathResolver.GetSteamLibraries(_settingsManager.Current.CustomSteamPath);
            var games = await LibraryScanner.ScanLibrariesAsync(libs, slsConfigPath: _settingsManager.Current.CustomSlssteamConfigPath, onlyPotatoManaged: true);
            long totalBytes = games.Sum(g => g.SizeOnDisk);

            Dispatcher.UIThread.Post(() =>
            {
                SystemStatsLine1 = $"Hubcap: Online (Direct)   SLSsteam: {slsStatus}   Steam: {steamStatus}";
                SystemStatsLine2 = $"Steam Updates: Managed   Potato: Up to Date   Library: {SpeedMonitor.FormatBytes(totalBytes)} ({games.Count} deployed games)";
            });
        });
    }

    public void AddLog(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogLines.Add($"[{timestamp}] {message}");
            if (LogLines.Count > 1200) LogLines.RemoveAt(0);
        });
    }

    [RelayCommand]
    private async Task SearchOrFetch()
    {
        var query = SearchQuery.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            AddLog("⚠️ Please enter a Steam App ID or game name.");
            ShowToast("Please enter a game title or App ID", "⚠️", "#F8B195");
            return;
        }

        StatusTickerText = $"QUERYING: {query.ToUpperInvariant()}";

        if (uint.TryParse(query, out var appId))
        {
            await FetchDepotsForApp(appId);
        }
        else
        {
            IsSearching = true;
            AddLog($"🔎 Searching Steam Store for \"{query}\"...");
            SearchResults.Clear();

            try
            {
                var results = await _steamApiClient.SearchStoreAsync(query);
                foreach (var r in results) SearchResults.Add(r);

                if (results.Count > 0)
                {
                    StatusTickerText = $"FOUND {results.Count} MATCHES FOR: {query.ToUpperInvariant()}";
                    AddLog($"✨ Found {results.Count} result(s). Click a game to deploy.");
                    ShowToast($"Found {results.Count} matches for \"{query}\"", "✨", "#61AFEF");
                }
                else
                {
                    StatusTickerText = "NO MATCHES FOUND";
                    AddLog("❌ No matching games found on Steam Store.");
                    ShowToast($"No matching games found for \"{query}\"", "❌", "#E06C75");
                }
            }
            finally
            {
                IsSearching = false;
            }
        }
    }

    [RelayCommand]
    private async Task SelectSearchResult(StoreSearchResult? result)
    {
        if (result == null) return;
        await FetchDepotsForApp(result.AppId);
    }

    private async Task FetchDepotsForApp(uint appId)
    {
        StatusTickerText = $"FETCHING MANIFESTS & DEPOTS FOR APP {appId}...";
        AddLog($"📦 Querying metadata & depots for App ID {appId}...");

        var details = await _steamApiClient.GetAppDetailsAsync(appId);
        string gameName = details?.Name ?? $"App {appId}";
        string? headerUrl = details?.HeaderUrl;

        if (!string.IsNullOrEmpty(headerUrl))
        {
            _ = Task.Run(async () =>
            {
                var bmp = await AsyncBitmapLoader.LoadFromUrlAsync(headerUrl);
                if (bmp != null)
                {
                    Dispatcher.UIThread.Post(() => ActiveHeroImage = bmp);
                }
            });
        }

        var depots = await _steamApiClient.GetDepotsForAppAsync(appId);
        if (depots.Count == 0)
        {
            depots.Add(new DepotInfo
            {
                DepotId = appId + 1,
                Name = $"{gameName} Primary Content",
                OsList = "windows",
                IsSelected = true
            });
        }

        var libs = SteamPathResolver.GetSteamLibraries(_settingsManager.Current.CustomSteamPath);
        var primaryLib = libs.FirstOrDefault() ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "Steam");

        var dialogVm = new DepotSelectionViewModel
        {
            AppId = appId,
            GameName = gameName,
            HeaderUrl = headerUrl,
            Depots = new ObservableCollection<DepotInfo>(depots),
            AvailableLibraries = new ObservableCollection<string>(libs),
            SelectedLibrary = primaryLib,
            IsLoading = false
        };

        if (ShowDepotSelectionDialogAsync != null)
        {
            var confirmed = await ShowDepotSelectionDialogAsync(dialogVm);
            if (confirmed)
            {
                var selectedDepots = dialogVm.Depots.Where(d => d.IsSelected).ToList();
                if (selectedDepots.Count == 0)
                {
                    AddLog("⚠️ No depots selected. Aborted.");
                    ShowToast("No depots selected", "⚠️", "#F8B195");
                    return;
                }

                var taskItem = new DownloadTaskItem
                {
                    AppId = appId,
                    GameName = gameName,
                    LibraryPath = dialogVm.SelectedLibrary,
                    SelectedDepots = selectedDepots
                };

                _jobQueue.Enqueue(taskItem);
                ShowToast($"Deployment queued: {gameName}", "🚀", "#C06C84");
            }
            else
            {
                AddLog("🚫 Depot selection cancelled.");
                StatusTickerText = "READY • STANDING BY";
            }
        }
    }

    [RelayCommand]
    private void CancelDownload()
    {
        _jobQueue.CancelActiveJob();
        StatusTickerText = "DOWNLOAD CANCELLED";
        ShowToast("Download cancelled", "🛑", "#E06C75");
    }

    [RelayCommand]
    private async Task OpenLibrary()
    {
        if (ShowLibraryDialogAsync != null)
        {
            var vm = new LibraryViewModel
            {
                CustomSteamPath = _settingsManager.Current.CustomSteamPath,
                CustomSlsConfigPath = _settingsManager.Current.CustomSlssteamConfigPath,
                OnlyPotatoGames = true
            };
            await ShowLibraryDialogAsync(vm);
            RefreshSystemStats();
        }
    }

    [RelayCommand]
    private async Task OpenSettings()
    {
        if (ShowSettingsDialogAsync != null)
        {
            var vm = new SettingsViewModel(_settingsManager);
            await ShowSettingsDialogAsync(vm);
            RefreshSystemStats();
            ShowToast("Settings updated", "💾", "#98C379");
        }
    }

    [RelayCommand]
    private void ClearLogs()
    {
        LogLines.Clear();
    }

    // ── Window Control Commands ──
    [RelayCommand]
    private void MinimizeWindow() => RequestMinimize?.Invoke();

    [RelayCommand]
    private void MaximizeWindow() => RequestMaximize?.Invoke();

    [RelayCommand]
    private void CloseWindow() => RequestClose?.Invoke();

    // ── Queue Callbacks ──
    private void OnJobStarted(DownloadTaskItem job)
    {
        Dispatcher.UIThread.Post(() =>
        {
            HasActiveDownload = true;
            ActiveGameName = job.GameName;
            ActiveStatusMessage = "Starting download...";
            ActiveProgressPercent = 0.0;
            ActiveSpeedText = "Calculating...";
            ActiveEtaText = "--:--";
            StatusTickerText = $"DOWNLOADING: {job.GameName.ToUpperInvariant()}";
            ShowToast($"Downloading {job.GameName}", "⚡", "#61AFEF");
        });
    }

    private void OnJobProgress(DownloadTaskItem job, DownloadProgress progress)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ActiveProgressPercent = progress.Percent;
            ActiveStatusMessage = progress.StatusMessage;
            ActiveSpeedText = SpeedMonitor.FormatSpeed(progress.SpeedBytesPerSecond);
            ActiveEtaText = progress.Eta != TimeSpan.Zero ? $"{progress.Eta:mm\\:ss}" : "--:--";
            StatusTickerText = $"DOWNLOADING {job.GameName.ToUpperInvariant()} ({progress.Percent:0.0}%) • {ActiveSpeedText}";
        });
    }

    private void OnJobCompleted(DownloadTaskItem job)
    {
        Dispatcher.UIThread.Post(() =>
        {
            HasActiveDownload = false;
            ActiveGameName = $"{job.GameName} (Finished)";
            ActiveStatusMessage = "Integration Complete!";
            ActiveProgressPercent = 100.0;
            ActiveSpeedText = "0 B/s";
            ActiveEtaText = "00:00";
            StatusTickerText = $"DEPLOYED: {job.GameName.ToUpperInvariant()} SUCCESS";
            AddLog($"🎉 '{job.GameName}' is installed and hooked!");
            ShowToast($"🎉 '{job.GameName}' deployed & hooked into Steam!", "🎉", "#98C379", 5000);

            RecentActivities.Insert(0, new RecentActivityItem
            {
                GameName = job.GameName,
                AppId = job.AppId,
                Timestamp = DateTime.Now.ToString("HH:mm"),
                MetricsSummary = "Success • Deployed & Verified",
                Details = "ACF: Created • SLSsteam: Hooked",
                IsSuccess = true
            });

            RefreshSystemStats();
        });
    }

    private void OnJobFailed(DownloadTaskItem job, string reason)
    {
        Dispatcher.UIThread.Post(() =>
        {
            HasActiveDownload = false;
            ActiveStatusMessage = $"Failed: {reason}";
            ActiveSpeedText = "0 B/s";
            StatusTickerText = $"FAILED: {job.GameName.ToUpperInvariant()}";
            AddLog($"❌ Download failed: {reason}");
            ShowToast($"Download failed: {reason}", "❌", "#E06C75", 5000);

            RecentActivities.Insert(0, new RecentActivityItem
            {
                GameName = job.GameName,
                AppId = job.AppId,
                Timestamp = DateTime.Now.ToString("HH:mm"),
                MetricsSummary = $"Failed: {reason}",
                Details = "Download Interrupted",
                IsSuccess = false
            });
        });
    }
}
