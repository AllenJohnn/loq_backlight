using System.ComponentModel;
using System.Windows;
using LoqLighting.App.ViewModels;
using Wpf.Ui.Controls;

namespace LoqLighting.App;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;
    private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.ToolStripMenuItem _startStopMenuItem;
    private bool _isExplicitExit;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        Loaded += async (s, e) =>
        {
            await _viewModel.RefreshCurrentStateAsync();

            bool isAutoStart = Environment.GetCommandLineArgs().Any(a => a.Equals("--autostart", StringComparison.OrdinalIgnoreCase));
            bool isMinimized = Environment.GetCommandLineArgs().Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));

            if (isAutoStart || _viewModel.AutoStartLastProfile)
            {
                await _viewModel.StartEffectAsync();
            }

            if (isAutoStart || isMinimized)
            {
                Hide();
            }
        };

        // Setup System Tray Icon
        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "LOQ Lighting Studio",
            Visible = true
        };

        var contextMenu = new System.Windows.Forms.ContextMenuStrip();

        var openMenuItem = new System.Windows.Forms.ToolStripMenuItem("Open LOQ Lighting Studio");
        openMenuItem.Click += (s, e) => RestoreFromTray();

        _startStopMenuItem = new System.Windows.Forms.ToolStripMenuItem("Start Effect");
        _startStopMenuItem.Click += async (s, e) => await _viewModel.TogglePlaybackAsync();

        var exitMenuItem = new System.Windows.Forms.ToolStripMenuItem("Exit");
        exitMenuItem.Click += async (s, e) => await ExitApplicationAsync();

        contextMenu.Items.Add(openMenuItem);
        contextMenu.Items.Add(_startStopMenuItem);
        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        contextMenu.Items.Add(exitMenuItem);

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (s, e) => RestoreFromTray();

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(_viewModel.IsRunning))
        {
            _startStopMenuItem.Text = _viewModel.IsRunning ? "Stop Effect" : "Start Effect";
        }
    }

    private void RestoreFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExplicitExit)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    private async Task ExitApplicationAsync()
    {
        _isExplicitExit = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();

        try
        {
            await _viewModel.StopEffectAsync();
            await _viewModel.DisposeAsync();
        }
        catch
        {
            // Ignore error during cleanup
        }

        System.Windows.Application.Current.Shutdown();
    }
}
