using CommunityToolkit.Mvvm.ComponentModel;

namespace Potato.UI.Models;

public partial class ToastMessage : ObservableObject
{
    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private string _icon = "ℹ️";

    [ObservableProperty]
    private string _badgeColor = "#61AFEF";

    [ObservableProperty]
    private bool _isVisible = false;
}
