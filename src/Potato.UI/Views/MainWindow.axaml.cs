using System.Collections.Specialized;
using Avalonia.Controls;
using Potato.UI.ViewModels;

namespace Potato.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        if (DataContext is MainWindowViewModel vm)
        {
            SetupDialogHandlers(vm);
        }

        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel newVm)
            {
                SetupDialogHandlers(newVm);
            }
        };
    }

    private void SetupDialogHandlers(MainWindowViewModel vm)
    {
        vm.ShowDepotSelectionDialogAsync = async (dialogVm) =>
        {
            var dialog = new DepotSelectionWindow(dialogVm);
            await dialog.ShowDialog(this);
            return dialogVm.WasConfirmed;
        };

        vm.ShowLibraryDialogAsync = async (dialogVm) =>
        {
            var dialog = new LibraryWindow(dialogVm);
            await dialog.ShowDialog(this);
        };

        vm.ShowSettingsDialogAsync = async (dialogVm) =>
        {
            var dialog = new SettingsWindow(dialogVm);
            await dialog.ShowDialog(this);
        };

        if (vm.LogLines is INotifyCollectionChanged notify)
        {
            notify.CollectionChanged += (_, _) =>
            {
                LogScrollViewer?.ScrollToEnd();
            };
        }
    }
}