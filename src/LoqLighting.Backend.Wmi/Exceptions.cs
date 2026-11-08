namespace LoqLighting.Backend.Wmi;

/// <summary>
/// Base exception for Lenovo WMI lighting backend errors.
/// </summary>
public class LenovoWmiException : Exception
{
    public LenovoWmiException(string message) : base(message) { }
    public LenovoWmiException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when WMI access to root\WMI or LENOVO_LIGHTING_METHOD is denied due to lack of elevation.
/// </summary>
public class LenovoWmiAccessDeniedException : LenovoWmiException
{
    public LenovoWmiAccessDeniedException(string message) : base(message) { }
    public LenovoWmiAccessDeniedException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when the LENOVO_LIGHTING_METHOD WMI class or instance is not found.
/// </summary>
public class LenovoWmiClassNotFoundException : LenovoWmiException
{
    public LenovoWmiClassNotFoundException(string message) : base(message) { }
    public LenovoWmiClassNotFoundException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when the WMI service or ACPI connection is lost or disconnected.
/// </summary>
public class LenovoWmiDisconnectedException : LenovoWmiException
{
    public LenovoWmiDisconnectedException(string message) : base(message) { }
    public LenovoWmiDisconnectedException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when a WMI invocation fails with an unexpected WMI or COM error.
/// </summary>
public class LenovoWmiOperationException : LenovoWmiException
{
    public LenovoWmiOperationException(string message) : base(message) { }
    public LenovoWmiOperationException(string message, Exception innerException) : base(message, innerException) { }
}
