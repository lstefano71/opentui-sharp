namespace OpenTui.Native;

/// <summary>
/// Safe handle wrapping the native opentui HitGrid pointer.
/// Released via <c>renderlib_hit_grid_destroy</c>.
/// </summary>
public sealed class HitGridHandle : OpenTuiSafeHandle
{
    /// <inheritdoc />
    protected override bool ReleaseHandle()
    {
        OpenTuiNative.HitGridDestroy(handle);
        return true;
    }
}
