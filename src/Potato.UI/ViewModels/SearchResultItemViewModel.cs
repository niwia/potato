using CommunityToolkit.Mvvm.ComponentModel;
using Potato.Domain.ValueObjects;
using Potato.ManifestApi.Models;

namespace Potato.UI.ViewModels;

public sealed partial class SearchResultItemViewModel : ObservableObject
{
    public HubcapSearchResult Model { get; }

    public AppId AppId => Model.AppId;
    public string Name => Model.Name;
    public string FormattedSize => Model.FormattedSize;
    public string? HeaderImageUrl => Model.HeaderImageUrl;
    public bool ManifestAvailable => Model.ManifestAvailable;
    public string? DenuvoStatus => Model.DenuvoStatus;
    public string? ProtonDbTier => Model.ProtonDbTier;

    [ObservableProperty]
    private bool _isInLibrary;

    public SearchResultItemViewModel(HubcapSearchResult model, bool isInLibrary = false)
    {
        Model = model;
        _isInLibrary = isInLibrary;
    }
}
