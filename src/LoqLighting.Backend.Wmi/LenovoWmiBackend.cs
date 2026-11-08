using System.Management;
using LoqLighting.Core;

namespace LoqLighting.Backend.Wmi;

public class LenovoWmiBackend : ILightingBackend
{
    private const string WmiNamespace = @"root\WMI";
    private const string WmiClassName = "LENOVO_LIGHTING_METHOD";
    private const string GetMethodName = "Get_Lighting_Current_Status";
    private const string SetMethodName = "Set_Lighting_Current_Status";

    private const byte RawLow = 2;
    private const byte RawHigh = 3;

    private static readonly HashSet<Brightness> AllowedLevels = new()
    {
        Brightness.Low,
        Brightness.High
    };

    private ManagementObject? _lightingInstance;
    private bool _disposed;

    public IReadOnlySet<Brightness> SupportedLevels => AllowedLevels;

    private ManagementObject GetOrCreateInstance()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_lightingInstance != null)
        {
            return _lightingInstance;
        }

        var scope = new ManagementScope(WmiNamespace);
        scope.Connect();

        using var searcher = new ManagementObjectSearcher(scope, new SelectQuery(WmiClassName));
        using var collection = searcher.Get();

        foreach (ManagementObject instance in collection)
        {
            _lightingInstance = instance;
            return _lightingInstance;
        }

        throw new InvalidOperationException(
            $"WMI class '{WmiClassName}' not found in namespace '{WmiNamespace}'. " +
            "Ensure this device has Lenovo ACPI/lighting drivers installed.");
    }

    public Task<LightingState> ReadAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ct.ThrowIfCancellationRequested();

        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            var instance = GetOrCreateInstance();

            using var inParams = instance.GetMethodParameters(GetMethodName);
            inParams["Lighting_ID"] = (byte)0;

            using var outParams = instance.InvokeMethod(GetMethodName, inParams, null);
            if (outParams == null)
            {
                throw new InvalidOperationException($"WMI method '{GetMethodName}' returned null result.");
            }

            byte rawLevel = Convert.ToByte(outParams["Current_Brightness_Level"]);
            byte stateType = Convert.ToByte(outParams["Current_State_Type"]);

            Brightness level = rawLevel switch
            {
                RawLow => Brightness.Low,
                RawHigh => Brightness.High,
                _ => throw new InvalidOperationException(
                    $"Hardware reported unverified brightness level {rawLevel}. " +
                    "Only levels 2 (Low) and 3 (High) are supported.")
            };

            return new LightingState(level, stateType);
        }, ct);
    }

    public Task WriteAsync(LightingState state, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ct.ThrowIfCancellationRequested();

        if (!SupportedLevels.Contains(state.Level))
        {
            throw new ArgumentOutOfRangeException(
                nameof(state),
                state.Level,
                $"Brightness level '{state.Level}' is not verified or supported. " +
                "Only Low and High are supported. Off remains disabled for safety.");
        }

        byte rawLevel = state.Level switch
        {
            Brightness.Low => RawLow,
            Brightness.High => RawHigh,
            _ => throw new ArgumentOutOfRangeException(
                nameof(state),
                state.Level,
                $"Unexpected brightness level '{state.Level}'.")
        };

        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            var instance = GetOrCreateInstance();

            using var inParams = instance.GetMethodParameters(SetMethodName);
            inParams["Lighting_ID"] = (byte)0;
            inParams["Current_State_Type"] = state.StateType;
            inParams["Current_Brightness_Level"] = rawLevel;

            instance.InvokeMethod(SetMethodName, inParams, null);
        }, ct);
    }

    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _lightingInstance?.Dispose();
            _lightingInstance = null;
            _disposed = true;
        }

        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
