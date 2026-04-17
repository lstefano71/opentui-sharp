namespace OpenTui.Native;

/// <summary>
/// Safe handle wrapping the native opentui TextBufferView pointer.
/// Released via <c>renderlib_text_buffer_view_destroy</c>.
/// </summary>
public sealed class TextBufferViewHandle : OpenTuiSafeHandle
{
    /// <inheritdoc />
    protected override bool ReleaseHandle()
    {
        OpenTuiNative.TextBufferViewDestroy(handle);
        return true;
    }
}
