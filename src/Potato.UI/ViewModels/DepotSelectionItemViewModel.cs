using CommunityToolkit.Mvvm.ComponentModel;
using Potato.Domain.ValueObjects;
using Potato.SteamMetadata.Models;

namespace Potato.UI.ViewModels;

public sealed partial class DepotSelectionItemViewModel : ObservableObject
{
    public DepotId DepotId { get; }
    public string Name { get; }
    public string? Size { get; }
    public ManifestGid? ManifestGid { get; }
    public string? OsList { get; }

    [ObservableProperty]
    private bool _isSelected = true;

    public DepotSelectionItemViewModel(DepotId depotId, SteamDepotInfo info)
    {
        DepotId = depotId;
        Name = info.Name ?? $"Depot {depotId}";
        Size = info.Size;
        ManifestGid = info.ManifestGid;
        OsList = info.OsList;
    }
}
