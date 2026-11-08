using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LoqLighting.Backend.Wmi;
using LoqLighting.Core;

namespace LoqLighting.App.ViewModels;

public partial class MainViewModel : ObservableObject, IAsyncDisposable
{
    private readonly ILightingBackend _backend;

    [ObservableProperty]
    private string _currentLevelText = "Reading...";

    [ObservableProperty]
    private string _hardwareDetailsText = "Connecting...";

    [ObservableProperty]
    private bool _isBackendConnected;

    public MainViewModel(ILightingBackend? backend = null)
    {
        _backend = backend ?? new LenovoWmiBackend();
    }

    [RelayCommand]
    public async Task RefreshCurrentStateAsync()
    {
        try
        {
            var state = await _backend.ReadAsync();
            CurrentLevelText = state.Level.ToString();
            HardwareDetailsText = $"Hardware status: Raw level {state.RawLevel}, StateType {state.StateType}";
            IsBackendConnected = true;
        }
        catch (Exception ex)
        {
            CurrentLevelText = "Unknown / Error";
            HardwareDetailsText = $"Error reading status: {ex.Message}";
            IsBackendConnected = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _backend.DisposeAsync();
    }
}
