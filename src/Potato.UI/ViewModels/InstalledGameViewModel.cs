using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Potato.Domain.ValueObjects;
using Potato.Library.Models;

namespace Potato.UI.ViewModels;

public sealed partial class InstalledGameViewModel : ObservableObject
{
    public InstalledGame Model { get; }

    public AppId AppId => Model.AppId;
    public string Name => Model.Name;
    public string InstallDir => Model.InstallDir;
    public string FullGamePath => Model.FullGamePath;
    public string BuildId => Model.BuildId;
    public ulong SizeOnDisk => Model.SizeOnDisk;
    public int DepotsCount => Model.InstalledDepots.Count;
    public string SteamAppsPath => Model.SteamAppsPath;
    public string HeaderUrl => $"https://shared.cloudflare.steamstatic.com/store_item_assets/steam/apps/{Model.AppId.Value}/header.jpg";

    [ObservableProperty]
    private string _updateStatus = "Up to date";

    [ObservableProperty]
    private bool _hasUpdate = false;

    public string FormattedSize => FormatBytes(SizeOnDisk);

    public InstalledGameViewModel(InstalledGame model)
    {
        Model = model;
    }

    [RelayCommand]
    public void OpenGameDirectory()
    {
        try
        {
            if (Directory.Exists(FullGamePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = FullGamePath,
                    UseShellExecute = true
                });
            }
        }
        catch { }
    }

    public static string FormatBytes(ulong bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int idx = 0;
        double dBytes = bytes;
        while (dBytes >= 1024 && idx < suffixes.Length - 1)
        {
            dBytes /= 1024;
            idx++;
        }
        return $"{dBytes:0.##} {suffixes[idx]}";
    }
}
