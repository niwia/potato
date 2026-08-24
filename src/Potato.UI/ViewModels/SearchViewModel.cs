using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Potato.Configuration.Services;
using Potato.Domain.ValueObjects;
using Potato.Library.Services;
using Potato.ManifestApi.Client;
using Potato.Pipeline.Models;
using Potato.Queue.Manager;
using Potato.SlsSteam.Paths;
using Potato.SteamMetadata.Models;
using Potato.SteamMetadata.Resolver;

namespace Potato.UI.ViewModels;

public sealed partial class SearchViewModel : ViewModelBase
{
    private readonly ISteamMetadataResolver _metadataResolver;
    private readonly ISlsSteamPathResolver _pathResolver;
    private readonly IDownloadQueueManager _queueManager;
    private readonly IHubcapApiClient _hubcapClient;
    private readonly ILibraryScanner _libraryScanner;
    private readonly ISettingsService _settingsService;
    private CancellationTokenSource? _searchDebounceCts;

    [ObservableProperty]
    private string _searchQuery = "813230";

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private bool _isSearchingLive;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _hasResolvedGame;

    [ObservableProperty]
    private bool _hasSearchResults;

    [ObservableProperty]
    private SteamAppMetadata? _metadata;

    [ObservableProperty]
    private string _selectedBranch = "public";

    [ObservableProperty]
    private string? _selectedLibrary;

    [ObservableProperty]
    private bool _unlockSls = true;

    [ObservableProperty]
    private bool _validateDownloads = true;

    public ObservableCollection<SearchResultItemViewModel> SearchResults { get; } = new();
    public ObservableCollection<string> Branches { get; } = new();
    public ObservableCollection<DepotSelectionItemViewModel> Depots { get; } = new();
    public ObservableCollection<string> AvailableLibraries { get; } = new();

    private HashSet<AppId> _libraryAppIds = new();

    public SearchViewModel(
        ISteamMetadataResolver metadataResolver,
        ISlsSteamPathResolver pathResolver,
        IDownloadQueueManager queueManager,
        IHubcapApiClient hubcapClient,
        ILibraryScanner libraryScanner,
        ISettingsService settingsService)
    {
        _metadataResolver = metadataResolver;
        _pathResolver = pathResolver;
        _queueManager = queueManager;
        _hubcapClient = hubcapClient;
        _libraryScanner = libraryScanner;
        _settingsService = settingsService;
    }

    partial void OnSearchQueryChanged(string value)
    {
        _searchDebounceCts?.Cancel();
        _searchDebounceCts = new CancellationTokenSource();
        var token = _searchDebounceCts.Token;

        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < 2)
        {
            SearchResults.Clear();
            HasSearchResults = false;
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(350, token);
                if (token.IsCancellationRequested) return;

                await ExecuteLiveSearchAsync(value.Trim(), token);
            }
            catch (OperationCanceledException) { }
            catch { }
        }, token);
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        AvailableLibraries.Clear();
        foreach (var p in _pathResolver.SteamAppsPaths)
        {
            if (Directory.Exists(p))
            {
                AvailableLibraries.Add(p);
            }
        }

        if (AvailableLibraries.Count > 0 && SelectedLibrary == null)
        {
            SelectedLibrary = AvailableLibraries[0];
        }

        try
        {
            var scan = await _libraryScanner.ScanLibrariesAsync();
            _libraryAppIds = new HashSet<AppId>(scan.InstalledGames.Select(g => g.AppId));
        }
        catch { }
    }

    private async Task ExecuteLiveSearchAsync(string query, CancellationToken token)
    {
        IsSearchingLive = true;
        try
        {
            var results = await _hubcapClient.SearchGamesAsync(query, limit: 20, token);
            if (token.IsCancellationRequested) return;

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                SearchResults.Clear();
                foreach (var r in results)
                {
                    bool inLib = _libraryAppIds.Contains(r.AppId);
                    SearchResults.Add(new SearchResultItemViewModel(r, inLib));
                }
                HasSearchResults = SearchResults.Count > 0;
            });
        }
        catch
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                SearchResults.Clear();
                HasSearchResults = false;
            });
        }
        finally
        {
            IsSearchingLive = false;
        }
    }

    [RelayCommand]
    public async Task SelectSearchResultAsync(SearchResultItemViewModel item)
    {
        if (item == null) return;
        SearchQuery = item.AppId.ToString();
        HasSearchResults = false;
        await FetchMetadataForAppIdAsync(item.AppId);
    }

    [RelayCommand]
    public async Task FetchMetadataAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery)) return;

        if (uint.TryParse(SearchQuery.Trim(), out uint parsedId))
        {
            await FetchMetadataForAppIdAsync(new AppId(parsedId));
        }
        else
        {
            // If text query, run live search immediately
            await ExecuteLiveSearchAsync(SearchQuery.Trim(), CancellationToken.None);
        }
    }

    private async Task FetchMetadataForAppIdAsync(AppId appId)
    {
        IsSearching = true;
        StatusMessage = $"Resolving Steam metadata for AppID {appId}...";
        HasResolvedGame = false;

        try
        {
            var meta = await _metadataResolver.ResolveAppMetadataAsync(appId);
            if (meta == null)
            {
                StatusMessage = $"Could not resolve metadata for AppID {appId}.";
                return;
            }

            Metadata = meta;
            HasResolvedGame = true;

            // Populate branches
            Branches.Clear();
            if (meta.Branches != null && meta.Branches.Count > 0)
            {
                foreach (var b in meta.Branches.Keys)
                {
                    Branches.Add(b);
                }
            }
            else
            {
                Branches.Add("public");
            }
            SelectedBranch = "public";

            // Populate depots
            Depots.Clear();
            foreach (var (dId, dInfo) in meta.Depots)
            {
                Depots.Add(new DepotSelectionItemViewModel(dId, dInfo));
            }

            ApplySmartSelection();
            StatusMessage = $"Resolved: '{meta.Name}' ({meta.Depots.Count} depots, Build {meta.BuildId ?? "N/A"}).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error resolving metadata: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    public void SelectAllDepots()
    {
        foreach (var d in Depots) d.IsSelected = true;
    }

    [RelayCommand]
    public void DeselectAllDepots()
    {
        foreach (var d in Depots) d.IsSelected = false;
    }

    [RelayCommand]
    public void ApplySmartSelection()
    {
        foreach (var d in Depots)
        {
            string os = (d.OsList ?? "").ToLowerInvariant();
            string name = (d.Name ?? "").ToLowerInvariant();

            // Deselect macOS and Soundtracks if configured
            if (_settingsService.Current.Download.FilterMacOsDepots && (os.Contains("macos") || os.Contains("macosx") || name.Contains("macos")))
            {
                d.IsSelected = false;
                continue;
            }

            if (_settingsService.Current.Download.FilterSoundtracks && (name.Contains("soundtrack") || name.Contains(" ost") || name.Contains("bonus content")))
            {
                d.IsSelected = false;
                continue;
            }

            d.IsSelected = true;
        }
    }

    [RelayCommand]
    public void EnqueueInstall()
    {
        if (Metadata == null) return;

        string targetDir = SelectedLibrary ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "Steam");
        var selectedDepotIds = Depots.Where(d => d.IsSelected).Select(d => d.DepotId).ToList();

        var request = new InstallRequest(
            Metadata.AppId,
            targetDir,
            SelectedBranch,
            selectedDepotIds.Count > 0 ? selectedDepotIds : null,
            maxDownloads: _settingsService.Current.Download.MaxDownloadsPerJob,
            validate: ValidateDownloads,
            useLanCache: _settingsService.Current.Download.UseLanCache,
            unlockSls: UnlockSls);

        var job = _queueManager.Enqueue(request, Metadata.Name);
        StatusMessage = $"Enqueued '{Metadata.Name}' into download queue (Job ID: {job.Id.ToString()[..8]}...).";
    }
}
