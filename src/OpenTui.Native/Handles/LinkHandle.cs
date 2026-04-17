namespace OpenTui.Native;

/// <summary>
/// Safe handle wrapping the native opentui Link pointer.
/// Released via <c>renderlib_link_destroy</c>.
/// </summary>
public sealed class LinkHandle : OpenTuiSafeHandle
{
    /// <inheritdoc />
    protected override bool ReleaseHandle()
    {
        OpenTuiNative.LinkDestroy(handle);
        return true;
    }
}
