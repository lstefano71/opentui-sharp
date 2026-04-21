using System.Text;
using OpenTui.Core.Managed;

namespace OpenTui.Core;

/// <summary>
/// Managed wrapper around the span feed.
/// Delegates to <see cref="ManagedSpanFeed"/>.
/// </summary>
public sealed class NativeSpanFeed : IDisposable
{
    internal ManagedSpanFeed _managed;
    private bool _disposed;

    private NativeSpanFeed(ManagedSpanFeed managed) => _managed = managed;

    /// <summary>Creates a new NativeSpanFeed with the given options.</summary>
    public static NativeSpanFeed Create(SpanFeedOptions? options = null)
    {
        return new NativeSpanFeed(new ManagedSpanFeed(options));
    }

    /// <summary>Attaches the span feed to the processing pipeline.</summary>
    public int Attach()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _managed.Attach();
    }

    /// <summary>Writes data to the stream.</summary>
    public int Write(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _managed.Write(data);
    }

    /// <summary>Writes a UTF-8 string to the stream.</summary>
    public int Write(string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _managed.Write(Encoding.UTF8.GetBytes(text));
    }

    /// <summary>Commits pending stream data.</summary>
    public int Commit()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _managed.Commit();
    }

    /// <summary>Drains completed spans from the feed (legacy nint overload).</summary>
    public uint DrainSpans(nint outSpans, uint maxSpans)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Span<SpanInfo> output = stackalloc SpanInfo[(int)Math.Min(maxSpans, 256)];
        int drained = _managed.DrainSpans(output);
        return (uint)drained;
    }

    /// <summary>Drains completed spans into a managed span buffer.</summary>
    public int DrainSpans(Span<SpanInfo> output)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _managed.DrainSpans(output);
    }

    /// <summary>Returns the raw bytes for a previously drained span.</summary>
    public ReadOnlySpan<byte> GetSpanData(SpanInfo span)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _managed.GetSpanData(span);
    }

    /// <summary>Marks a span as consumed, releasing its chunk refcount.</summary>
    public void MarkSpanConsumed(SpanInfo span)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _managed.MarkSpanConsumed(span);
    }

    /// <summary>Closes the stream.</summary>
    public int Close()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _managed.Close();
    }

    /// <summary>Reserves space in the stream.</summary>
    public int Reserve(uint length, out nint reserveInfo)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        int result = _managed.Reserve(length, out _);
        reserveInfo = nint.Zero;
        return result;
    }

    /// <summary>Commits a previously reserved region.</summary>
    public int CommitReserved(uint length)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _managed.CommitReserved(length);
    }

    /// <summary>Updates the stream processing options.</summary>
    public int SetOptions(SpanFeedOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _managed.SetOptions(options);
    }

    /// <summary>Gets stream statistics.</summary>
    public SpanFeedStats GetStats()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _managed.GetStats();
    }

    /// <summary>Sets a callback function for stream events.</summary>
    public void SetCallback(nint callback)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        // Native callback pointer not supported in managed mode
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _managed?.Dispose();
            _managed = null!;
        }
    }
}
