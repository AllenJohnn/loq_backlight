using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LoqLighting.App.Models;
using LoqLighting.Backend.Wmi;
using LoqLighting.Core;
using LoqLighting.Core.Effects;

namespace LoqLighting.App.ViewModels;

public partial class MainViewModel : ObservableObject, IAsyncDisposable
{
    private readonly ILightingBackend _backend;
    private readonly LightingEngine _engine;

    [ObservableProperty]
    private string _currentLevelText = "Reading...";

    [ObservableProperty]
    private string _hardwareDetailsText = "Connecting...";

    [ObservableProperty]
    private bool _isBackendConnected;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private EffectItem? _selectedEffect;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    public ObservableCollection<EffectItem> AvailableEffects { get; } = new();

    public MainViewModel(ILightingBackend? backend = null)
    {
        _backend = backend ?? new LenovoWmiBackend();
        _engine = new LightingEngine(_backend);

        _engine.Faulted += OnEngineFaulted;

        AvailableEffects.Add(new EffectItem("Strobe", "Rapid stepped switching between high and low levels.", new StrobeEffect()));
        AvailableEffects.Add(new EffectItem("Strobe with pause", "Stepped strobe pulse bursts separated by a resting pause.", new StrobeWithPauseEffect()));
        AvailableEffects.Add(new EffectItem("Breathing", "4-step stepped illumination cycle across Off, Low, High, and Low levels.", new BreathingEffect()));
        AvailableEffects.Add(new EffectItem("Heartbeat", "Double-pulse stepped rhythm followed by a resting pause.", new HeartbeatEffect()));
        AvailableEffects.Add(new EffectItem("Wave", "Stepped rhythmic sequence cycling across hardware brightness levels.", new WaveEffect()));
        AvailableEffects.Add(new EffectItem("Random flash", "Stepped pseudo-random flashes using hardware brightness levels.", new RandomFlashEffect()));

        SelectedEffect = AvailableEffects[0];
    }

    private void OnEngineFaulted(object? sender, Exception ex)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            IsRunning = false;
            StatusMessage = $"Engine Faulted: {ex.Message}";
        });
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

    [RelayCommand]
    public async Task StartEffectAsync()
    {
        if (SelectedEffect == null) return;

        try
        {
            StatusMessage = $"Running '{SelectedEffect.Name}'";
            var settings = new EffectSettings();
            await _engine.StartAsync(SelectedEffect.Effect, settings);
            IsRunning = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Start Failed: {ex.Message}";
            IsRunning = false;
        }
    }

    [RelayCommand]
    public async Task StopEffectAsync()
    {
        try
        {
            StatusMessage = "Stopping...";
            await _engine.StopAsync();
            IsRunning = false;
            StatusMessage = "Stopped";
            await RefreshCurrentStateAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Stop Failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task TogglePlaybackAsync()
    {
        if (IsRunning)
        {
            await StopEffectAsync();
        }
        else
        {
            await StartEffectAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _engine.Faulted -= OnEngineFaulted;
        await _engine.DisposeAsync();
        await _backend.DisposeAsync();
    }
}
