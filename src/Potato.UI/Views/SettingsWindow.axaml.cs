using Avalonia.Controls;
using Potato.UI.ViewModels;

namespace Potato.UI.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    public SettingsWindow(SettingsViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += Close;
    }
}
