using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LoqLighting.App.Models;
using LoqLighting.App.Services;
using LoqLighting.Backend.Wmi;
using LoqLighting.Core;
using LoqLighting.Core.Effects;

namespace LoqLighting.App.ViewModels;

public partial class MainViewModel : ObservableObject, IAsyncDisposable
{
    private readonly ILightingBackend _backend;
    private readonly LightingEngine _engine;
    private readonly ProfileService _profileService;
    private readonly TaskSchedulerService _taskSchedulerService;

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

    // System Startup & Automation Options
    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _autoStartLastProfile;

    // Profiles
    public ObservableCollection<LightingProfile> Profiles { get; } = new();

    [ObservableProperty]
    private LightingProfile? _selectedProfile;

    [ObservableProperty]
    private string _profileNameInput = "";

    // Live Status Panel
    [ObservableProperty]
    private string _engineStateText = "Stopped";

    [ObservableProperty]
    private string _activeEffectName = "None";

    [ObservableProperty]
    private string _elapsedTimeText = "00:00";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLastError))]
    private string? _lastErrorMessage;

    public bool HasLastError => !string.IsNullOrEmpty(LastErrorMessage);

    [ObservableProperty]
    private bool _hasFaultError;

    [ObservableProperty]
    private string? _faultErrorMessage;

    private System.Windows.Threading.DispatcherTimer? _elapsedTimer;
    private DateTime _runStartTime;

    // Controls
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpeedDisplayText))]
    private double _speed = 1.0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTimedDuration))]
    private bool _isInfiniteDuration = true;

    [ObservableProperty]
    private int _runDurationSeconds = 10;

    [ObservableProperty]
    private int _pauseDurationMs = 500;

    [ObservableProperty]
    private Brightness _selectedHighLevel = Brightness.High;

    [ObservableProperty]
    private Brightness _selectedLowLevel = Brightness.Low;

    public string SpeedDisplayText => $"{Speed:F2}x";
    public bool IsTimedDuration => !IsInfiniteDuration;
    public bool IsPauseDurationApplicable => SelectedEffect?.Name.Contains("pause", StringComparison.OrdinalIgnoreCase) == true;

    public IReadOnlyList<Brightness> SupportedBrightnessLevels { get; } =
        [Brightness.Off, Brightness.Low, Brightness.High];

    public ObservableCollection<EffectItem> AvailableEffects { get; } = new();

    private bool _wasRunningBeforeSuspend;

    public MainViewModel(
        ILightingBackend? backend = null,
        ProfileService? profileService = null,
        TaskSchedulerService? taskSchedulerService = null)
    {
        _backend = backend ?? new LenovoWmiBackend();
        _engine = new LightingEngine(_backend);
        _profileService = profileService ?? new ProfileService();
        _taskSchedulerService = taskSchedulerService ?? new TaskSchedulerService();

        _engine.Faulted += OnEngineFaulted;
        Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;

        AvailableEffects.Add(new EffectItem("Strobe", "Rapid stepped switching between high and low levels.", new StrobeEffect()));
        AvailableEffects.Add(new EffectItem("Strobe with pause", "Stepped strobe pulse bursts separated by a resting pause.", new StrobeWithPauseEffect()));
        AvailableEffects.Add(new EffectItem("Breathing", "4-step stepped illumination cycle across Off, Low, High, and Low levels.", new BreathingEffect()));
        AvailableEffects.Add(new EffectItem("Heartbeat", "Double-pulse stepped rhythm followed by a resting pause.", new HeartbeatEffect()));
        AvailableEffects.Add(new EffectItem("Wave", "Stepped rhythmic sequence cycling across hardware brightness levels.", new WaveEffect()));
        AvailableEffects.Add(new EffectItem("Random flash", "Stepped pseudo-random flashes using hardware brightness levels.", new RandomFlashEffect()));

        SelectedEffect = AvailableEffects[0];

        LoadProfiles();
        LoadAppSettings();
    }

    private bool _isLoadingSettings;

    private void LoadAppSettings()
    {
        _isLoadingSettings = true;
        try
        {
            var settings = _profileService.LoadSettings();
            AutoStartLastProfile = settings.AutoStartLastProfile;
            StartWithWindows = _taskSchedulerService.IsTaskScheduled();
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_isLoadingSettings) return;
        _taskSchedulerService.SetStartWithWindows(value, AutoStartLastProfile);
    }

    partial void OnAutoStartLastProfileChanged(bool value)
    {
        if (_isLoadingSettings) return;
        _profileService.SaveSettings(new AppSettings { AutoStartLastProfile = value });
        if (StartWithWindows)
        {
            _taskSchedulerService.SetStartWithWindows(true, value);
        }
    }

    public void LoadProfiles()
    {
        Profiles.Clear();
        var all = _profileService.LoadAllProfiles();
        foreach (var p in all)
        {
            Profiles.Add(p);
        }

        string? lastUsed = _profileService.GetLastUsedProfileName();
        if (!string.IsNullOrEmpty(lastUsed))
        {
            var match = Profiles.FirstOrDefault(p => string.Equals(p.Name, lastUsed, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                SelectedProfile = match;
                ApplyProfile(match);
                return;
            }
        }

        if (Profiles.Count > 0 && SelectedProfile == null)
        {
            SelectedProfile = Profiles[0];
            ApplyProfile(Profiles[0]);
        }
    }

    partial void OnSelectedProfileChanged(LightingProfile? value)
    {
        if (value != null)
        {
            ProfileNameInput = value.Name;
            ApplyProfile(value);
            _profileService.SaveLastUsedProfileName(value.Name);
        }
    }

    public void ApplyProfile(LightingProfile profile)
    {
        var matchedEffect = AvailableEffects.FirstOrDefault(e => string.Equals(e.Name, profile.EffectName, StringComparison.OrdinalIgnoreCase));
        if (matchedEffect != null)
        {
            SelectedEffect = matchedEffect;
        }

        Speed = profile.Speed;
        IsInfiniteDuration = profile.IsInfiniteDuration;
        RunDurationSeconds = profile.RunDurationSeconds;
        PauseDurationMs = profile.PauseDurationMs;
        SelectedHighLevel = profile.HighLevel;
        SelectedLowLevel = profile.LowLevel;
    }

    [RelayCommand]
    public void SaveCurrentProfile()
    {
        string name = string.IsNullOrWhiteSpace(ProfileNameInput)
            ? (SelectedProfile?.Name ?? "Custom Profile")
            : ProfileNameInput.Trim();

        var profile = new LightingProfile
        {
            Name = name,
            EffectName = SelectedEffect?.Name ?? "Strobe",
            Speed = Speed,
            IsInfiniteDuration = IsInfiniteDuration,
            RunDurationSeconds = RunDurationSeconds,
            PauseDurationMs = PauseDurationMs,
            HighLevel = SelectedHighLevel,
            LowLevel = SelectedLowLevel
        };

        _profileService.SaveProfile(profile);

        Profiles.Clear();
        foreach (var p in _profileService.LoadAllProfiles())
        {
            Profiles.Add(p);
        }

        SelectedProfile = Profiles.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        StatusMessage = $"Saved profile '{name}'";
    }

    [RelayCommand]
    public void DeleteCurrentProfile()
    {
        if (SelectedProfile == null) return;

        string name = SelectedProfile.Name;
        _profileService.DeleteProfile(name);

        Profiles.Remove(SelectedProfile);
        SelectedProfile = Profiles.FirstOrDefault();
        if (SelectedProfile != null)
        {
            ApplyProfile(SelectedProfile);
        }
        StatusMessage = $"Deleted profile '{name}'";
    }

    partial void OnSelectedEffectChanged(EffectItem? value)
    {
        OnPropertyChanged(nameof(IsPauseDurationApplicable));
    }

    private void OnEngineFaulted(object? sender, Exception ex)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            _elapsedTimer?.Stop();
            _elapsedTimer = null;
            IsRunning = false;
            EngineStateText = "Faulted";
            ActiveEffectName = "None";
            LastErrorMessage = ex.Message;
            FaultErrorMessage = $"Lighting engine encountered a hardware or communication fault: {ex.Message}";
            HasFaultError = true;
            StatusMessage = "Faulted";
        });
    }

    [RelayCommand]
    public void DismissFaultBanner()
    {
        HasFaultError = false;
        FaultErrorMessage = null;
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
            LastErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    public async Task StartEffectAsync()
    {
        if (SelectedEffect == null) return;

        try
        {
            StatusMessage = $"Running '{SelectedEffect.Name}'";
            var settings = new EffectSettings
            {
                Speed = Speed,
                HighLevel = SelectedHighLevel,
                LowLevel = SelectedLowLevel,
                PauseDuration = TimeSpan.FromMilliseconds(PauseDurationMs),
                RunDuration = IsInfiniteDuration ? null : TimeSpan.FromSeconds(RunDurationSeconds)
            };

            await _engine.StartAsync(SelectedEffect.Effect, settings);
            IsRunning = true;
            EngineStateText = "Running";
            ActiveEffectName = SelectedEffect.Name;
            HasFaultError = false;

            _runStartTime = DateTime.UtcNow;
            ElapsedTimeText = "00:00";
            _elapsedTimer?.Stop();
            _elapsedTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _elapsedTimer.Tick += (s, e) =>
            {
                if (IsRunning)
                {
                    var elapsed = DateTime.UtcNow - _runStartTime;
                    ElapsedTimeText = $"{elapsed:mm\\:ss}";
                }
            };
            _elapsedTimer.Start();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Start Failed: {ex.Message}";
            LastErrorMessage = ex.Message;
            EngineStateText = "Faulted";
            IsRunning = false;
        }
    }

    [RelayCommand]
    public async Task StopEffectAsync()
    {
        try
        {
            StatusMessage = "Stopping...";
            _elapsedTimer?.Stop();
            _elapsedTimer = null;
            await _engine.StopAsync();
            IsRunning = false;
            EngineStateText = "Stopped";
            ActiveEffectName = "None";
            StatusMessage = "Stopped";
            await RefreshCurrentStateAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Stop Failed: {ex.Message}";
            LastErrorMessage = ex.Message;
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

    private async void OnPowerModeChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        try
        {
            if (e.Mode == Microsoft.Win32.PowerModes.Suspend)
            {
                if (IsRunning)
                {
                    _wasRunningBeforeSuspend = true;
                    await StopEffectAsync();
                }
            }
            else if (e.Mode == Microsoft.Win32.PowerModes.Resume)
            {
                await Task.Delay(1000); // Allow hardware/EC to settle after resume
                await RefreshCurrentStateAsync();

                if (_wasRunningBeforeSuspend)
                {
                    _wasRunningBeforeSuspend = false;
                    await StartEffectAsync();
                }
            }
        }
        catch
        {
            // Suppress unhandled errors during power transitions
        }
    }

    public async ValueTask DisposeAsync()
    {
        Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _engine.Faulted -= OnEngineFaulted;
        await _engine.DisposeAsync();
        await _backend.DisposeAsync();
    }
}
