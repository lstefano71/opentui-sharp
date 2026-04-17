namespace OpenTui.Native;

/// <summary>
/// Safe handle wrapping the native opentui SyntaxStyle pointer.
/// Released via <c>renderlib_syntax_style_destroy</c>.
/// </summary>
public sealed class SyntaxStyleHandle : OpenTuiSafeHandle
{
    /// <inheritdoc />
    protected override bool ReleaseHandle()
    {
        OpenTuiNative.SyntaxStyleDestroy(handle);
        return true;
    }
}
