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

        if (vm.LogLines is INotifyCollectionChanged notify)
        {
            notify.CollectionChanged += (_, _) =>
            {
                LogScrollViewer?.ScrollToEnd();
            };
        }

        // Trigger library scan on first load
        Loaded += async (_, _) =>
        {
            await vm.ScanLibrary();
        };
    }
}