namespace OpenTui.Native;

/// <summary>
/// Safe handle wrapping the native opentui Link store pointer.
/// Link store is internally managed by the renderer — there is no separate destroy function.
/// This handle is non-owning and does not release the underlying resource.
/// </summary>
public sealed class LinkHandle : OpenTuiSafeHandle
{
    /// <inheritdoc />
    protected override bool ReleaseHandle()
    {
        // Link store is owned by the renderer and destroyed when the renderer is destroyed.
        return true;
    }
}
