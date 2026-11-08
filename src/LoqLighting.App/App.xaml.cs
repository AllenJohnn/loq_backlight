using System.Windows;
using LoqLighting.Backend.Wmi;
using LoqLighting.Core;

namespace LoqLighting.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private static ILightingBackend? _cleanupBackend;
    private static LightingState? _initialSnapshot;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        try
        {
            _cleanupBackend = new LenovoWmiBackend();
            _initialSnapshot = await _cleanupBackend.ReadAsync();
        }
        catch
        {
            // If backend cannot connect at startup, continue
        }
    }

    private static void OnProcessExit(object? sender, EventArgs e)
    {
        RestoreSnapshotSync();
    }

    private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        RestoreSnapshotSync();
    }

    private static void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        RestoreSnapshotSync();
    }

    private static void RestoreSnapshotSync()
    {
        if (_cleanupBackend != null && _initialSnapshot.HasValue)
        {
            try
            {
                _cleanupBackend.RestoreAsync(_initialSnapshot.Value, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch
            {
                // Suppress errors during emergency exit
            }
        }
    }
}
