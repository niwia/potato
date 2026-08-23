using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Potato.Core.Models;

namespace Potato.UI.ViewModels;

public partial class DepotSelectionViewModel : ViewModelBase
{
    [ObservableProperty]
    private uint _appId;

    [ObservableProperty]
    private string _gameName = string.Empty;

    [ObservableProperty]
    private string? _headerUrl;

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private string _statusText = "Fetching depot metadata...";

    [ObservableProperty]
    private ObservableCollection<DepotInfo> _depots = new();

    [ObservableProperty]
    private ObservableCollection<string> _availableLibraries = new();

    [ObservableProperty]
    private string _selectedLibrary = string.Empty;

    public bool WasConfirmed { get; private set; }

    public event Action? RequestClose;

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var d in Depots) d.IsSelected = true;
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var d in Depots) d.IsSelected = false;
    }

    [RelayCommand]
    private void SelectWindowsOnly()
    {
        foreach (var d in Depots)
        {
            d.IsSelected = string.IsNullOrEmpty(d.OsList) || d.OsList.Contains("windows", StringComparison.OrdinalIgnoreCase);
        }
    }

    [RelayCommand]
    private void Confirm()
    {
        WasConfirmed = true;
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        WasConfirmed = false;
        RequestClose?.Invoke();
    }
}
