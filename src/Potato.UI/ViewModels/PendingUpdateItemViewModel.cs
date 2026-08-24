using CommunityToolkit.Mvvm.ComponentModel;
using Potato.Domain.ValueObjects;
using Potato.Library.Models;

namespace Potato.UI.ViewModels;

public sealed partial class PendingUpdateItemViewModel : ObservableObject
{
    public InstalledGame Model { get; }

    public AppId AppId => Model.AppId;
    public string Name => Model.Name;
    public string CurrentBuildId => Model.BuildId;
    public string TargetBuildId { get; }
    public int DepotsCount => Model.InstalledDepots.Count;
    public string HeaderUrl => $"https://shared.cloudflare.steamstatic.com/store_item_assets/steam/apps/{Model.AppId.Value}/header.jpg";

    public string BuildDiffSummary => $"Build {CurrentBuildId} → {TargetBuildId}";

    public PendingUpdateItemViewModel(InstalledGame model, string targetBuildId)
    {
        Model = model;
        TargetBuildId = !string.IsNullOrWhiteSpace(targetBuildId) ? targetBuildId : "Latest";
    }
}
