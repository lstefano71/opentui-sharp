using System.Runtime.InteropServices;
using System.Text;
using OpenTui.Native;

namespace OpenTui.Core;

/// <summary>
/// Managed wrapper around the native span feed.
/// Provides a streaming interface for writing structured span data.
/// </summary>
public sealed class NativeSpanFeed : IDisposable
{
    private nint _handle;
    private bool _disposed;

    private NativeSpanFeed(nint handle) => _handle = handle;

    /// <summary>Creates a new NativeSpanFeed with the given options.</summary>
    public static unsafe NativeSpanFeed Create(SpanFeedOptions? options = null)
    {
        nint handle;
        if (options is not null)
        {
            var native = new NativeSpanFeedOptionsStruct
            {
                ChunkSize = options.ChunkSize,
                InitialChunks = options.InitialChunks,
                MaxBytes = options.MaxBytes,
                GrowthPolicy = (byte)options.GrowthPolicy,
                AutoCommitOnFull = options.AutoCommitOnFull ? (byte)1 : (byte)0,
                SpanQueueCapacity = options.SpanQueueCapacity,
            };
            handle = OpenTuiNative.CreateNativeSpanFeed((nint)(&native));
        }
        else
        {
            handle = OpenTuiNative.CreateNativeSpanFeed(nint.Zero);
        }
        if (handle == nint.Zero)
            throw new InvalidOperationException("Failed to create native span feed.");
        return new NativeSpanFeed(handle);
    }

    internal nint Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _handle;
        }
    }

    /// <summary>Attaches the span feed to the processing pipeline.</summary>
    public int Attach() => OpenTuiNative.AttachNativeSpanFeed(Handle);

    /// <summary>Writes data to the stream.</summary>
    public unsafe int Write(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0)
            return 0; // Zig: len==0 returns ok, but null src_ptr returns err_invalid
        fixed (byte* ptr = data)
            return OpenTuiNative.StreamWrite(Handle, (nint)ptr, (ulong)data.Length);
    }

    /// <summary>Writes a UTF-8 string to the stream.</summary>
    public int Write(string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        return Write(bytes);
    }

    /// <summary>Commits pending stream data.</summary>
    public int Commit() => OpenTuiNative.StreamCommit(Handle);

    /// <summary>Drains completed spans from the feed.</summary>
    public uint DrainSpans(nint outSpans, uint maxSpans) =>
        OpenTuiNative.StreamDrainSpans(Handle, outSpans, maxSpans);

    /// <summary>Closes the stream.</summary>
    public int Close() => OpenTuiNative.StreamClose(Handle);

    /// <summary>Reserves space in the stream.</summary>
    public unsafe int Reserve(uint length, out nint reserveInfo)
    {
        // Native StreamReserve writes a ReserveInfo struct that is larger than a single nint.
        // Allocate sufficient stack space to prevent stack corruption.
        byte* buf = stackalloc byte[64];
        new Span<byte>(buf, 64).Clear();
        int result = OpenTuiNative.StreamReserve(Handle, length, (nint)buf);
        reserveInfo = *(nint*)buf;
        return result;
    }

    /// <summary>Commits a previously reserved region.</summary>
    public int CommitReserved(uint length) =>
        OpenTuiNative.StreamCommitReserved(Handle, length);

    /// <summary>Updates the stream processing options.</summary>
    public unsafe int SetOptions(SpanFeedOptions options)
    {
        var native = new NativeSpanFeedOptionsStruct
        {
            ChunkSize = options.ChunkSize,
            InitialChunks = options.InitialChunks,
            MaxBytes = options.MaxBytes,
            GrowthPolicy = (byte)options.GrowthPolicy,
            AutoCommitOnFull = options.AutoCommitOnFull ? (byte)1 : (byte)0,
            SpanQueueCapacity = options.SpanQueueCapacity,
        };
        return OpenTuiNative.StreamSetOptions(Handle, (nint)(&native));
    }

    /// <summary>Gets stream statistics.</summary>
    public unsafe SpanFeedStats GetStats()
    {
        SpanFeedStats stats;
        int result = OpenTuiNative.StreamGetStats(Handle, (nint)(&stats));
        if (result != 0)
            throw new InvalidOperationException($"StreamGetStats failed with code {result}");
        return stats;
    }

    /// <summary>Sets a callback function for stream events.</summary>
    public void SetCallback(nint callback) =>
        OpenTuiNative.StreamSetCallback(Handle, callback);

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            OpenTuiNative.SpanFeedDestroy(_handle);
            _handle = nint.Zero;
        }
    }

    /// <summary>Native layout of span feed options for P/Invoke marshalling.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSpanFeedOptionsStruct
    {
        public uint ChunkSize;
        public uint InitialChunks;
        public ulong MaxBytes;
        public byte GrowthPolicy;
        public byte AutoCommitOnFull;
        public uint SpanQueueCapacity;
    }
}
