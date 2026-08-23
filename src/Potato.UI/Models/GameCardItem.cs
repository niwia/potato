using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Potato.UI.Models;

public partial class GameCardItem : ObservableObject
{
    public uint AppId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string FormattedSize { get; init; } = "Unknown Size";
    public string? InstallDir { get; init; }
    public string LibraryPath { get; init; } = string.Empty;

    [ObservableProperty]
    private Bitmap? _headerImage;

    [ObservableProperty]
    private bool _isSlssteamHooked;

    [ObservableProperty]
    private string _statusBadge = "Installed";
}
