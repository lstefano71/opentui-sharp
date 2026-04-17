namespace OpenTui.Native;

/// <summary>
/// Safe handle wrapping the native opentui EditorView pointer.
/// Released via <c>renderlib_editor_view_destroy</c>.
/// </summary>
public sealed class EditorViewHandle : OpenTuiSafeHandle
{
    /// <inheritdoc />
    protected override bool ReleaseHandle()
    {
        OpenTuiNative.EditorViewDestroy(handle);
        return true;
    }
}
