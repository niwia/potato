using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Potato.Configuration.Services;
using Potato.Domain.ValueObjects;
using Potato.Library.Services;
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
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private string _searchQuery = "813230";

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _hasResolvedGame;

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

    public ObservableCollection<string> Branches { get; } = new();
    public ObservableCollection<DepotSelectionItemViewModel> Depots { get; } = new();
    public ObservableCollection<string> AvailableLibraries { get; } = new();

    public SearchViewModel(
        ISteamMetadataResolver metadataResolver,
        ISlsSteamPathResolver pathResolver,
        IDownloadQueueManager queueManager,
        ISettingsService settingsService)
    {
        _metadataResolver = metadataResolver;
        _pathResolver = pathResolver;
        _queueManager = queueManager;
        _settingsService = settingsService;
    }

    [RelayCommand]
    public Task InitializeAsync()
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

        return Task.CompletedTask;
    }

    [RelayCommand]
    public async Task FetchMetadataAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery)) return;

        IsSearching = true;
        StatusMessage = "Resolving Steam metadata...";
        HasResolvedGame = false;

        try
        {
            AppId appId;
            if (uint.TryParse(SearchQuery.Trim(), out uint parsedId))
            {
                appId = new AppId(parsedId);
            }
            else
            {
                StatusMessage = "Please enter a valid numeric AppID for metadata resolution.";
                return;
            }

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
