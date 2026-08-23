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

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly SteamApiClient _steamApiClient = new();
    private readonly DownloadJobQueue _jobQueue = new();
    private readonly SettingsManager _settingsManager = new();
    private readonly DatabaseManager _dbManager = new();
    private readonly ImageCacheService _imageCache = new();

    // Navigation & UI State
    [ObservableProperty]
    private int _selectedTabIndex = 0;

    [ObservableProperty]
    private string _steamStatusText = "Detecting Steam...";

    [ObservableProperty]
    private string _slssteamStatusText = "Detecting SLSsteam...";

    // ── Search & Deploy ──
    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<StoreSearchResult> _searchResults = new();

    [ObservableProperty]
    private StoreSearchResult? _selectedSearchResult;

    [ObservableProperty]
    private bool _isSearching = false;

    // ── Active Download Hero Card ──
    [ObservableProperty]
    private string _activeGameName = "Ready";

    [ObservableProperty]
    private string _activeStatusMessage = "No active downloads";

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

    // ── Live Logs ──
    [ObservableProperty]
    private ObservableCollection<string> _logLines = new();

    // ── Library Tab (Game Cards Grid) ──
    [ObservableProperty]
    private ObservableCollection<GameCardItem> _allLibraryCards = new();

    [ObservableProperty]
    private ObservableCollection<GameCardItem> _filteredLibraryCards = new();

    [ObservableProperty]
    private string _libraryFilter = string.Empty;

    [ObservableProperty]
    private string _librarySortBy = "Name (A-Z)";

    [ObservableProperty]
    private bool _isScanningLibrary = false;

    [ObservableProperty]
    private string _libraryStatsSummary = "0 Games";

    // ── Manifest & Direct Tools Tab ──
    [ObservableProperty]
    private string _directAppId = string.Empty;

    [ObservableProperty]
    private string _directDepotId = string.Empty;

    [ObservableProperty]
    private string _directManifestId = string.Empty;

    // ── Settings Tab ──
    [ObservableProperty]
    private string? _settingsSteamPath;

    [ObservableProperty]
    private string? _settingsSlssteamPath;

    [ObservableProperty]
    private string? _settingsDepotDownloaderPath;

    [ObservableProperty]
    private string? _settingsHubcapApiKey;

    [ObservableProperty]
    private bool _settingsSlssteamEnabled = true;

    [ObservableProperty]
    private bool _settingsAutoAcf = true;

    // Dialog delegate
    public Func<DepotSelectionViewModel, Task<bool>>? ShowDepotSelectionDialogAsync { get; set; }

    public MainWindowViewModel()
    {
        _jobQueue.JobStarted += OnJobStarted;
        _jobQueue.JobProgress += OnJobProgress;
        _jobQueue.JobCompleted += OnJobCompleted;
        _jobQueue.JobFailed += OnJobFailed;
        _jobQueue.LogMessage += AddLog;

        AddLog("🥔 Project Potato v2.0 initialized.");
        AddLog("💻 High-Performance Native Linux & Steam Deck Engine.");

        LoadInitialSettings();
        DetectSystemEnvironment();
    }

    private void LoadInitialSettings()
    {
        var s = _settingsManager.Current;
        SettingsSteamPath = s.CustomSteamPath;
        SettingsSlssteamPath = s.CustomSlssteamConfigPath;
        SettingsDepotDownloaderPath = s.CustomDepotDownloaderPath;
        SettingsHubcapApiKey = s.HubcapApiKey;
        SettingsSlssteamEnabled = s.SlssteamModeEnabled;
        SettingsAutoAcf = s.AutoGenerateAcf;
    }

    private void DetectSystemEnvironment()
    {
        var steamPath = SteamPathResolver.FindSteamInstall(SettingsSteamPath);
        if (!string.IsNullOrEmpty(steamPath))
        {
            SteamStatusText = $"Steam: {Path.GetFileName(steamPath)}";
            AddLog($"🎮 Steam detected: {steamPath}");
        }
        else
        {
            SteamStatusText = "Steam: Not Found";
            AddLog("⚠️ No standard Steam install detected. Please configure in Settings.");
        }

        var slsConfig = SlsConfigManager.GetDefaultConfigPath(SettingsSlssteamPath);
        if (File.Exists(slsConfig))
        {
            SlssteamStatusText = "SLSsteam: Ready";
            AddLog($"🟢 SLSsteam config: {slsConfig}");
        }
        else
        {
            SlssteamStatusText = "SLSsteam: Config Missing";
            AddLog("ℹ️ SLSsteam config not found at standard path.");
        }

        var ddPath = DepotDownloaderService.LocateDepotDownloader(SettingsDepotDownloaderPath);
        if (!string.IsNullOrEmpty(ddPath))
        {
            AddLog($"🚀 DepotDownloader located: {ddPath}");
        }
        else
        {
            AddLog("⚠️ DepotDownloader binary not found. Set path in Settings.");
        }
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

    // ── Search & Deploy Logic ──

    [RelayCommand]
    private async Task SearchOrFetch()
    {
        var query = SearchQuery.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            AddLog("⚠️ Please enter a Steam App ID or game name.");
            return;
        }

        if (uint.TryParse(query, out var appId))
        {
            // Numerical App ID -> Directly trigger depot fetch
            await FetchDepotsForApp(appId);
        }
        else
        {
            // Name search -> Query Steam Store
            IsSearching = true;
            AddLog($"🔎 Searching Steam Store for \"{query}\"...");
            SearchResults.Clear();

            try
            {
                var results = await _steamApiClient.SearchStoreAsync(query);
                foreach (var r in results) SearchResults.Add(r);

                if (results.Count > 0)
                {
                    AddLog($"✨ Found {results.Count} result(s). Click a game to deploy.");
                }
                else
                {
                    AddLog("❌ No matching games found on Steam Store.");
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
        AddLog($"📦 Querying metadata & depots for App ID {appId}...");

        var details = await _steamApiClient.GetAppDetailsAsync(appId);
        string gameName = details?.Name ?? $"App {appId}";
        string? headerUrl = details?.HeaderUrl;

        // Async load image into Hero Card
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

        var libs = SteamPathResolver.GetSteamLibraries(SettingsSteamPath);
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
                SelectedTabIndex = 0; // Switch to Deploy / Dashboard tab
            }
            else
            {
                AddLog("🚫 Depot selection cancelled.");
            }
        }
    }

    [RelayCommand]
    private void CancelDownload()
    {
        _jobQueue.CancelActiveJob();
    }

    // ── Direct Manifest Download ──
    [RelayCommand]
    private void StartDirectManifestDownload()
    {
        if (!uint.TryParse(DirectAppId.Trim(), out var appId) ||
            !uint.TryParse(DirectDepotId.Trim(), out var depotId))
        {
            AddLog("⚠️ Please provide valid App ID and Depot ID.");
            return;
        }

        ulong.TryParse(DirectManifestId.Trim(), out var manifestId);

        var libs = SteamPathResolver.GetSteamLibraries(SettingsSteamPath);
        var primaryLib = libs.FirstOrDefault() ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "Steam");

        var taskItem = new DownloadTaskItem
        {
            AppId = appId,
            GameName = $"App {appId} (Depot {depotId})",
            LibraryPath = primaryLib,
            SelectedDepots = new List<DepotInfo>
            {
                new DepotInfo
                {
                    DepotId = depotId,
                    ManifestId = manifestId,
                    Name = $"Depot {depotId}",
                    OsList = "windows",
                    IsSelected = true
                }
            }
        };

        _jobQueue.Enqueue(taskItem);
        SelectedTabIndex = 0;
    }

    // ── Library Tab (Game Cards Grid) ──
    [RelayCommand]
    public async Task ScanLibrary()
    {
        IsScanningLibrary = true;
        AddLog("📚 Scanning installed Steam libraries...");
        AllLibraryCards.Clear();

        try
        {
            var libs = SteamPathResolver.GetSteamLibraries(SettingsSteamPath);
            var games = await LibraryScanner.ScanLibrariesAsync(libs);

            var slsConfigPath = SlsConfigManager.GetDefaultConfigPath(SettingsSlssteamPath);
            var slsApps = SlsConfigManager.GetAdditionalApps(slsConfigPath);

            long totalBytes = 0;

            foreach (var g in games)
            {
                totalBytes += g.SizeOnDisk;
                var isManaged = slsApps.Contains(g.AppId);
                var formattedSize = SpeedMonitor.FormatBytes(g.SizeOnDisk);

                var card = new GameCardItem
                {
                    AppId = g.AppId,
                    Name = g.Name,
                    FormattedSize = formattedSize,
                    InstallDir = g.InstallDir,
                    LibraryPath = g.LibraryPath,
                    IsSlssteamHooked = isManaged,
                    StatusBadge = isManaged ? "SLSsteam Active" : "Installed"
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

                AllLibraryCards.Add(card);
            }

            ApplyLibraryFilter();
            LibraryStatsSummary = $"{AllLibraryCards.Count} Games ({SpeedMonitor.FormatBytes(totalBytes)})";
            AddLog($"📚 Found {AllLibraryCards.Count} installed game(s) total ({SpeedMonitor.FormatBytes(totalBytes)}).");
        }
        catch (Exception ex)
        {
            AddLog($"❌ Library scan failed: {ex.Message}");
        }
        finally
        {
            IsScanningLibrary = false;
        }
    }

    partial void OnLibraryFilterChanged(string value)
    {
        ApplyLibraryFilter();
    }

    partial void OnLibrarySortByChanged(string value)
    {
        ApplyLibraryFilter();
    }

    private void ApplyLibraryFilter()
    {
        FilteredLibraryCards.Clear();
        var filter = LibraryFilter?.Trim().ToLowerInvariant() ?? "";

        IEnumerable<GameCardItem> query = AllLibraryCards;

        if (!string.IsNullOrEmpty(filter))
        {
            query = query.Where(g => g.Name.ToLowerInvariant().Contains(filter) || g.AppId.ToString().Contains(filter));
        }

        // Apply Sorting
        query = LibrarySortBy switch
        {
            "Name (Z-A)" => query.OrderByDescending(g => g.Name),
            "SLSsteam Hooked First" => query.OrderByDescending(g => g.IsSlssteamHooked).ThenBy(g => g.Name),
            _ => query.OrderBy(g => g.Name)
        };

        foreach (var card in query)
        {
            FilteredLibraryCards.Add(card);
        }
    }

    [RelayCommand]
    private void OpenGameFolder(GameCardItem? card)
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
            AddLog($"📂 Opened game folder: {path}");
        }
        else
        {
            AddLog($"⚠️ Directory does not exist: {path}");
        }
    }

    [RelayCommand]
    private void ToggleSlssteamHook(GameCardItem? card)
    {
        if (card == null) return;

        var slsConfigPath = SlsConfigManager.GetDefaultConfigPath(SettingsSlssteamPath);
        if (card.IsSlssteamHooked)
        {
            SlsConfigManager.RemoveAdditionalApp(slsConfigPath, card.AppId);
            card.IsSlssteamHooked = false;
            card.StatusBadge = "Installed";
            AddLog($"⚡ Removed App {card.AppId} ({card.Name}) from SLSsteam.");
        }
        else
        {
            SlsConfigManager.AddAdditionalApp(slsConfigPath, card.AppId, card.Name);
            card.IsSlssteamHooked = true;
            card.StatusBadge = "SLSsteam Active";
            AddLog($"⚡ Hooked App {card.AppId} ({card.Name}) into SLSsteam.");
        }
    }

    [RelayCommand]
    private void LaunchGameViaSteam(GameCardItem? card)
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
            AddLog($"🚀 Launch command sent to Steam for {card.Name} (App ID {card.AppId})");
        }
        catch (Exception ex)
        {
            AddLog($"❌ Failed to launch game: {ex.Message}");
        }
    }

    // ── Settings Save ──
    [RelayCommand]
    private void SaveSettings()
    {
        var s = new AppSettings
        {
            CustomSteamPath = SettingsSteamPath,
            CustomSlssteamConfigPath = SettingsSlssteamPath,
            CustomDepotDownloaderPath = SettingsDepotDownloaderPath,
            HubcapApiKey = SettingsHubcapApiKey,
            SlssteamModeEnabled = SettingsSlssteamEnabled,
            AutoGenerateAcf = SettingsAutoAcf
        };

        _settingsManager.Save(s);
        AddLog("💾 Settings saved successfully.");
        DetectSystemEnvironment();
    }

    [RelayCommand]
    private void ClearLogs()
    {
        LogLines.Clear();
    }

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
        });
    }

    private void OnJobCompleted(DownloadTaskItem job)
    {
        Dispatcher.UIThread.Post(() =>
        {
            HasActiveDownload = false;
            ActiveGameName = $"{job.GameName} (Finished)";
            ActiveStatusMessage = "Setup & Integration Complete!";
            ActiveProgressPercent = 100.0;
            ActiveSpeedText = "0 B/s";
            ActiveEtaText = "00:00";
            AddLog($"🎉 '{job.GameName}' is installed and hooked!");
            _ = ScanLibrary();
        });
    }

    private void OnJobFailed(DownloadTaskItem job, string reason)
    {
        Dispatcher.UIThread.Post(() =>
        {
            HasActiveDownload = false;
            ActiveStatusMessage = $"Failed: {reason}";
            ActiveSpeedText = "0 B/s";
            AddLog($"❌ Download failed: {reason}");
        });
    }
}
