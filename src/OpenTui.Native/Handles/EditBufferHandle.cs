namespace OpenTui.Native;

/// <summary>
/// Safe handle wrapping the native opentui EditBuffer pointer.
/// Released via <c>renderlib_edit_buffer_destroy</c>.
/// </summary>
public sealed class EditBufferHandle : OpenTuiSafeHandle
{
    /// <inheritdoc />
    protected override bool ReleaseHandle()
    {
        OpenTuiNative.EditBufferDestroy(handle);
        return true;
    }
}
