using Avalonia.Controls;
using Potato.UI.ViewModels;

namespace Potato.UI.Views;

public partial class DepotSelectionWindow : Window
{
    public DepotSelectionWindow()
    {
        InitializeComponent();
    }

    public DepotSelectionWindow(DepotSelectionViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += Close;
    }
}
