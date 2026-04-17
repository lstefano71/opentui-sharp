namespace OpenTui;

/// <summary>Severity level for log messages from the native library.</summary>
public enum LogLevel : byte
{
    /// <summary>Error-level messages.</summary>
    Error = 0,
    /// <summary>Warning-level messages.</summary>
    Warn = 1,
    /// <summary>Informational messages.</summary>
    Info = 2,
    /// <summary>Debug-level messages.</summary>
    Debug = 3,
    /// <summary>Verbose/trace-level messages.</summary>
    Verbose = 4,
    /// <summary>Fatal error messages.</summary>
    Fatal = 5,
}
