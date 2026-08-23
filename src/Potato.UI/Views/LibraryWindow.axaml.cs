using Avalonia.Controls;
using Potato.UI.ViewModels;

namespace Potato.UI.Views;

public partial class LibraryWindow : Window
{
    public LibraryWindow()
    {
        InitializeComponent();
    }

    public LibraryWindow(LibraryViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += Close;
        Opened += (_, _) => viewModel.RefreshCommand.Execute(null);
    }
}
