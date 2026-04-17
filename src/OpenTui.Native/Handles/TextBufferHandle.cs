namespace OpenTui.Native;

/// <summary>
/// Safe handle wrapping the native opentui TextBuffer pointer.
/// Released via <c>renderlib_text_buffer_destroy</c>.
/// </summary>
public sealed class TextBufferHandle : OpenTuiSafeHandle
{
    /// <inheritdoc />
    protected override bool ReleaseHandle()
    {
        OpenTuiNative.TextBufferDestroy(handle);
        return true;
    }
}
