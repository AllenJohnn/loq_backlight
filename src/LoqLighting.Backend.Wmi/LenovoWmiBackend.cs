using System.Management;
using System.Runtime.InteropServices;
using LoqLighting.Core;

namespace LoqLighting.Backend.Wmi;

public class LenovoWmiBackend : ILightingBackend
{
    private const string WmiNamespace = @"root\WMI";
    private const string WmiClassName = "LENOVO_LIGHTING_METHOD";
    private const string GetMethodName = "Get_Lighting_Current_Status";
    private const string SetMethodName = "Set_Lighting_Current_Status";

    private const byte RawOff = 1;
    private const byte RawLow = 2;
    private const byte RawHigh = 3;

    private static readonly HashSet<Brightness> AllowedLevels = new()
    {
        Brightness.Low,
        Brightness.High
    };

    private readonly object _syncRoot = new();
    private ManagementObject? _lightingInstance;
    private bool _disposed;

    public IReadOnlySet<Brightness> SupportedLevels => AllowedLevels;

    private ManagementObject GetOrCreateInstance()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_syncRoot)
        {
            if (_lightingInstance != null)
            {
                return _lightingInstance;
            }

            try
            {
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                using var searcher = new ManagementObjectSearcher(scope, new SelectQuery(WmiClassName));
                using var collection = searcher.Get();

                foreach (ManagementObject instance in collection)
                {
                    _lightingInstance = instance;
                    return _lightingInstance;
                }

                throw new LenovoWmiClassNotFoundException(
                    $"WMI class '{WmiClassName}' instance not found in namespace '{WmiNamespace}'. " +
                    "Ensure this device is a supported Lenovo LOQ laptop and Lenovo ACPI lighting drivers are active.");
            }
            catch (Exception ex) when (ex is not LenovoWmiException)
            {
                throw TranslateException(ex, "Connect to Lenovo lighting WMI");
            }
        }
    }

    public Task<LightingState> ReadAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ct.ThrowIfCancellationRequested();

        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var instance = GetOrCreateInstance();

                using var inParams = instance.GetMethodParameters(GetMethodName);
                inParams["Lighting_ID"] = (byte)0;

                using var outParams = instance.InvokeMethod(GetMethodName, inParams, null);
                if (outParams == null)
                {
                    throw new LenovoWmiOperationException($"WMI method '{GetMethodName}' returned a null result.");
                }

                byte rawLevel = Convert.ToByte(outParams["Current_Brightness_Level"]);
                byte stateType = Convert.ToByte(outParams["Current_State_Type"]);

                Brightness level = rawLevel switch
                {
                    RawOff => Brightness.Off,
                    RawLow => Brightness.Low,
                    RawHigh => Brightness.High,
                    _ => Brightness.Off // Raw values other than 1, 2, 3 return Off-unknown with real RawLevel
                };

                return new LightingState(level, stateType, rawLevel);
            }
            catch (Exception ex) when (ex is not LenovoWmiException)
            {
                ResetInstanceOnDisconnect(ex);
                throw TranslateException(ex, "Read backlight state");
            }
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
                $"Brightness level '{state.Level}' is not verified or supported for writing. " +
                "Only Low (2) and High (3) are supported for WriteAsync. Off remains disabled for WriteAsync safety.");
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

            try
            {
                var instance = GetOrCreateInstance();

                using var inParams = instance.GetMethodParameters(SetMethodName);
                inParams["Lighting_ID"] = (byte)0;
                inParams["Current_State_Type"] = state.StateType;
                inParams["Current_Brightness_Level"] = rawLevel;

                instance.InvokeMethod(SetMethodName, inParams, null);
            }
            catch (Exception ex) when (ex is not LenovoWmiException)
            {
                ResetInstanceOnDisconnect(ex);
                throw TranslateException(ex, $"Write backlight state (Level: {state.Level}, StateType: {state.StateType})");
            }
        }, ct);
    }

    private void ResetInstanceOnDisconnect(Exception ex)
    {
        lock (_syncRoot)
        {
            _lightingInstance?.Dispose();
            _lightingInstance = null;
        }
    }

    private static LenovoWmiException TranslateException(Exception ex, string operation)
    {
        if (ex is UnauthorizedAccessException)
        {
            return new LenovoWmiAccessDeniedException(
                $"Access denied during '{operation}'. Administrator privileges are required to access root\\WMI LENOVO_LIGHTING_METHOD. Run the application as administrator.",
                ex);
        }

        if (ex is ManagementException mgmtEx)
        {
            return mgmtEx.ErrorCode switch
            {
                ManagementStatus.AccessDenied => new LenovoWmiAccessDeniedException(
                    $"Access denied during '{operation}'. Administrator privileges are required to access root\\WMI LENOVO_LIGHTING_METHOD. Run the application as administrator.",
                    mgmtEx),

                ManagementStatus.NotFound or ManagementStatus.InvalidClass => new LenovoWmiClassNotFoundException(
                    $"WMI class '{WmiClassName}' was not found in namespace '{WmiNamespace}' during '{operation}'. Ensure this device is a supported Lenovo LOQ laptop with Lenovo ACPI drivers.",
                    mgmtEx),

                ManagementStatus.TransportFailure or ManagementStatus.ShuttingDown or ManagementStatus.CallCanceled =>
                    new LenovoWmiDisconnectedException(
                        $"WMI connection was lost or disconnected during '{operation}'.",
                        mgmtEx),

                _ => new LenovoWmiOperationException(
                    $"WMI error (code: {mgmtEx.ErrorCode}) during '{operation}': {mgmtEx.Message}",
                    mgmtEx)
            };
        }

        if (ex is COMException comEx)
        {
            uint hr = (uint)comEx.ErrorCode;
            if (hr == 0x80070005 || hr == 0x80041003) // E_ACCESSDENIED / WBEM_E_ACCESS_DENIED
            {
                return new LenovoWmiAccessDeniedException(
                    $"Access denied during '{operation}' (HRESULT 0x{hr:X8}). Administrator privileges are required to access root\\WMI LENOVO_LIGHTING_METHOD. Run the application as administrator.",
                    comEx);
            }

            if (hr == 0x800706BA || hr == 0x80010108) // RPC_S_SERVER_UNAVAILABLE / RPC_E_DISCONNECTED
            {
                return new LenovoWmiDisconnectedException(
                    $"WMI service disconnected or RPC server unavailable during '{operation}' (HRESULT 0x{hr:X8}).",
                    comEx);
            }

            return new LenovoWmiOperationException(
                $"COM error (0x{hr:X8}) during '{operation}': {comEx.Message}",
                comEx);
        }

        return new LenovoWmiOperationException(
            $"Unexpected error during '{operation}': {ex.Message}",
            ex);
    }

    public ValueTask DisposeAsync()
    {
        lock (_syncRoot)
        {
            if (!_disposed)
            {
                _lightingInstance?.Dispose();
                _lightingInstance = null;
                _disposed = true;
            }
        }

        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
