using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
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

        // Window Controls
        vm.RequestMinimize = () => WindowState = WindowState.Minimized;
        vm.RequestMaximize = () =>
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        };
        vm.RequestClose = () => Close();

        if (vm.LogLines is INotifyCollectionChanged notify)
        {
            notify.CollectionChanged += (_, _) =>
            {
                LogScrollViewer?.ScrollToEnd();
            };
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        // Allow dragging window from top bar
        var point = e.GetCurrentPoint(this);
        if (point.Position.Y <= 40 && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }
}