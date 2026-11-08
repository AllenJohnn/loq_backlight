using System.Windows;
using LoqLighting.App.ViewModels;
using Wpf.Ui.Controls;

namespace LoqLighting.App;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        Loaded += async (s, e) => await _viewModel.RefreshCurrentStateAsync();
    }
}
