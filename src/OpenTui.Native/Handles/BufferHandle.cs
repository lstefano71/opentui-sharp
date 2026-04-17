namespace OpenTui.Native;

/// <summary>
/// Safe handle wrapping the native opentui Buffer pointer.
/// Released via <c>renderlib_buffer_destroy</c>.
/// </summary>
public sealed class BufferHandle : OpenTuiSafeHandle
{
    /// <inheritdoc />
    protected override bool ReleaseHandle()
    {
        OpenTuiNative.BufferDestroy(handle);
        return true;
    }
}
