namespace OpenTui.Native;

/// <summary>
/// Safe handle wrapping the native opentui Renderer pointer.
/// Released via <c>renderlib_destroy</c>.
/// </summary>
public sealed class RendererHandle : OpenTuiSafeHandle
{
    /// <inheritdoc />
    protected override bool ReleaseHandle()
    {
        OpenTuiNative.RendererDestroy(handle);
        return true;
    }
}
