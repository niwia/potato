using CommunityToolkit.Mvvm.ComponentModel;
using Potato.Domain.ValueObjects;
using Potato.Library.Models;

namespace Potato.UI.ViewModels;

public sealed partial class ActivityLogItemViewModel : ObservableObject
{
    public ActivityLogEntry Model { get; }

    public AppId AppId => Model.AppId;
    public string GameName => Model.GameName;
    public ActivityStatus Status => Model.Status;
    public string FormattedTimestamp => Model.FormattedTimestamp;
    public string StatusSummary => Model.StatusSummary;
    public string? Details => Model.Details;
    public string HeaderUrl => $"https://shared.cloudflare.steamstatic.com/store_item_assets/steam/apps/{Model.AppId.Value}/header.jpg";

    public string StatusColor => Status switch
    {
        ActivityStatus.Success => "#10B981", // Green
        ActivityStatus.Running => "#3B82F6", // Blue
        ActivityStatus.Failed => "#EF4444",  // Red
        _ => "#A1A1AA"
    };

    public ActivityLogItemViewModel(ActivityLogEntry model)
    {
        Model = model;
    }
}
