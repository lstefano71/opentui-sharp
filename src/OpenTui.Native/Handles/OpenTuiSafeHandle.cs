using System.Runtime.InteropServices;

namespace OpenTui.Native;

/// <summary>
/// Base class for all OpenTUI native opaque-pointer handles.
/// Ensures deterministic release via the C ABI destroy function.
/// </summary>
public abstract class OpenTuiSafeHandle : SafeHandle
{
    /// <inheritdoc />
    protected OpenTuiSafeHandle() : base(nint.Zero, ownsHandle: true) { }

    /// <inheritdoc />
    public override bool IsInvalid => handle == nint.Zero;

    /// <summary>Sets the underlying handle value. For use by wrapper constructors.</summary>
    internal void SetHandleValue(nint value) => SetHandle(value);
}
