namespace OpenTui.Native;

/// <summary>
/// Safe handle wrapping the native opentui NativeSpanFeed pointer.
/// Released via <c>renderlib_span_feed_destroy</c>.
/// </summary>
public sealed class SpanFeedHandle : OpenTuiSafeHandle
{
    /// <inheritdoc />
    protected override bool ReleaseHandle()
    {
        OpenTuiNative.SpanFeedDestroy(handle);
        return true;
    }
}
