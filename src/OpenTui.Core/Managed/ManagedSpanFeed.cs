using System.Buffers;
using System.Runtime.CompilerServices;

namespace OpenTui.Core.Managed;

/// <summary>Event types emitted by <see cref="ManagedSpanFeed"/>.</summary>
public enum SpanFeedEvent : uint
{
    /// <summary>A new chunk was allocated.</summary>
    ChunkAdded = 2,
    /// <summary>The stream was closed.</summary>
    Closed = 5,
    /// <summary>An error occurred.</summary>
    Error = 6,
    /// <summary>One or more committed spans are ready to drain.</summary>
    DataAvailable = 7,
    /// <summary>The state buffer (per-chunk refcounts) was (re)allocated.</summary>
    StateBuffer = 8,
}

/// <summary>Describes a committed span inside a chunk.</summary>
public readonly record struct SpanInfo(int ChunkIndex, int Offset, int Length);

/// <summary>Result of a <see cref="ManagedSpanFeed.Reserve"/> call.</summary>
public readonly record struct ReserveResult(Memory<byte> Buffer);

/// <summary>Callback signature for span feed events.</summary>
/// <param name="eventId">The event type.</param>
/// <param name="arg0">Event-specific argument (e.g. chunk index or span count).</param>
/// <param name="arg1">Event-specific second argument (e.g. chunk size).</param>
public delegate void SpanFeedCallback(SpanFeedEvent eventId, int arg0, long arg1);

/// <summary>
/// Pure C# implementation of the Zig <c>native-span-feed</c> streaming chunked byte-buffer.
/// Provides a ring buffer of committed spans, chunk lifecycle management, and an event system.
/// Thread-safe: no. Callers must synchronize externally if needed.
/// </summary>
public sealed class ManagedSpanFeed : IDisposable
{
    #region Status codes

    /// <summary>Operation succeeded.</summary>
    public const int Ok = 0;
    /// <summary>No space available in the span ring or current chunk.</summary>
    public const int ErrNoSpace = -1;
    /// <summary>Maximum byte limit would be exceeded.</summary>
    public const int ErrMaxBytes = -2;
    /// <summary>Invalid state (e.g. stream closed or no chunks).</summary>
    public const int ErrInvalid = -3;
    /// <summary>Memory allocation failed.</summary>
    public const int ErrOutOfMemory = -4;
    /// <summary>Operation conflicts with an active reserve.</summary>
    public const int ErrBusy = -5;

    #endregion

    #region Constants

    private const uint DefaultChunkSize = 64 * 1024;
    private const uint DefaultSpanQueueCapacity = 4096;
    private const uint NotifyThreshold = 1;

    #endregion

    #region Stream state

    private byte[][] _chunks;
    private int _chunkCount;

    private int _currentChunkIndex;
    private int _writeOffset;

    private int _pendingChunkIndex;
    private int _pendingOffset;
    private int _pendingLen;

    private bool _reservedActive;
    private int _reservedChunkIndex;
    private int _reservedOffset;
    private int _reservedLen;

    private bool _attached;
    private bool _closed;
    private bool _disposed;

    private SpanFeedCallback? _callback;

    // Span ring buffer
    private SpanInfo[] _ringBuffer;
    private readonly uint _ringCapacity;
    private uint _ringHead;
    private uint _ringTail;

    // Per-chunk refcount buffer
    private byte[] _stateBuffer;
    private int _stateCapacity;

    // Options (runtime-mutable subset)
    private readonly uint _chunkSize;
    private ulong _maxBytes;
    private GrowthPolicy _growthPolicy;
    private bool _autoCommitOnFull;

    // Statistics
    private ulong _bytesWritten;
    private ulong _spansCommitted;

    #endregion

    #region Construction

    /// <summary>Creates a new <see cref="ManagedSpanFeed"/> with the given options.</summary>
    public ManagedSpanFeed(SpanFeedOptions? options = null)
    {
        var opts = NormalizeOptions(options);

        _chunkSize = opts.ChunkSize;
        _maxBytes = opts.MaxBytes;
        _growthPolicy = opts.GrowthPolicy;
        _autoCommitOnFull = opts.AutoCommitOnFull;

        _ringCapacity = opts.SpanQueueCapacity;
        _ringBuffer = new SpanInfo[_ringCapacity];

        int initialChunks = (int)opts.InitialChunks;
        _chunks = new byte[Math.Max(initialChunks, 4)][];
        _stateBuffer = new byte[_chunks.Length];
        _stateCapacity = _stateBuffer.Length;

        for (int i = 0; i < initialChunks; i++)
        {
            AddChunkCore();
        }
    }

    #endregion

    #region Public API

    /// <summary>Attaches the consumer. Emits initial events if a callback is set.</summary>
    public int Attach()
    {
        if (_closed) return ErrInvalid;

        bool notify = false;
        _attached = true;

        if (_callback is not null)
        {
            EmitStateBuffer();

            for (int i = 0; i < _chunkCount; i++)
            {
                EmitChunkAdded(i, (int)_chunkSize);
            }

            uint queued = RingCount;
            if (queued > 0)
                notify = true;

            Finish(notify, queued);
        }

        return Ok;
    }

    /// <summary>Sets (or clears) the event callback. Re-emits current state if attached.</summary>
    public void SetCallback(SpanFeedCallback? callback)
    {
        _callback = callback;
        if (callback is null || !_attached) return;

        EmitStateBuffer();

        for (int i = 0; i < _chunkCount; i++)
        {
            EmitChunkAdded(i, (int)_chunkSize);
        }

        uint queued = RingCount;
        if (queued > 0)
        {
            EmitDataAvailable(queued);
        }
    }

    /// <summary>Writes data into the stream. Auto-commits when a chunk fills up (if enabled).</summary>
    public int Write(ReadOnlySpan<byte> data)
    {
        if (_closed) return ErrInvalid;
        if (data.Length == 0) return Ok;
        if (_reservedActive) return ErrBusy;

        bool notify = false;
        int remaining = data.Length;
        int srcIndex = 0;
        int chunkLen = (int)_chunkSize;

        while (remaining > 0)
        {
            int available = chunkLen - _writeOffset;
            if (available == 0)
            {
                if (_pendingLen > 0)
                {
                    int rc = CommitLocked(ref notify);
                    if (rc != Ok) { Finish(notify, 0); return rc; }
                }
                int ec = EnsureWritableChunkLocked();
                if (ec != Ok) { Finish(notify, 0); return ec; }
                available = chunkLen;
            }

            if (remaining > available && !_autoCommitOnFull)
            {
                Finish(notify, 0);
                return ErrNoSpace;
            }

            int toWrite = Math.Min(remaining, available);
            if (_pendingLen == 0)
            {
                _pendingChunkIndex = _currentChunkIndex;
                _pendingOffset = _writeOffset;
            }

            data.Slice(srcIndex, toWrite).CopyTo(
                _chunks[_currentChunkIndex].AsSpan(_writeOffset, toWrite));

            _writeOffset += toWrite;
            _pendingLen += toWrite;
            _bytesWritten += (ulong)toWrite;
            srcIndex += toWrite;
            remaining -= toWrite;

            if (_writeOffset == chunkLen && _autoCommitOnFull)
            {
                int rc = CommitLocked(ref notify);
                if (rc != Ok) { Finish(notify, 0); return rc; }
                if (remaining > 0)
                {
                    int ec = EnsureWritableChunkLocked();
                    if (ec != Ok) { Finish(notify, 0); return ec; }
                }
            }
        }

        Finish(notify, 0);
        return Ok;
    }

    /// <summary>Commits the current pending region as a span.</summary>
    public int Commit()
    {
        if (_closed) return ErrInvalid;
        if (_reservedActive) return ErrBusy;

        bool notify = false;
        int rc = CommitLocked(ref notify);
        Finish(notify, 0);
        return rc;
    }

    /// <summary>
    /// Reserves writable space for zero-copy writes.
    /// The caller writes directly into the returned <see cref="Memory{T}"/>
    /// and then calls <see cref="CommitReserved"/> with the actual bytes written.
    /// </summary>
    public int Reserve(uint minLen, out ReserveResult result)
    {
        result = default;
        if (_closed) return ErrInvalid;
        if (_reservedActive) return ErrBusy;
        if (_pendingLen != 0) return ErrBusy;

        int ec = EnsureWritableChunkLocked();
        if (ec != Ok) return ec;

        int available = (int)_chunkSize - _writeOffset;
        if (available < (int)minLen) return ErrNoSpace;

        _reservedActive = true;
        _reservedChunkIndex = _currentChunkIndex;
        _reservedOffset = _writeOffset;
        _reservedLen = available;

        result = new ReserveResult(
            _chunks[_currentChunkIndex].AsMemory(_writeOffset, available));
        return Ok;
    }

    /// <summary>Commits <paramref name="length"/> bytes of a previously reserved region.</summary>
    public int CommitReserved(uint length)
    {
        if (_closed) return ErrInvalid;
        if (!_reservedActive) return ErrInvalid;
        if ((int)length > _reservedLen) return ErrNoSpace;

        bool notify = false;

        _pendingChunkIndex = _reservedChunkIndex;
        _pendingOffset = _reservedOffset;
        _pendingLen = (int)length;
        _writeOffset = _reservedOffset + (int)length;
        _reservedActive = false;
        _reservedLen = 0;

        _bytesWritten += length;

        int rc = CommitLocked(ref notify);
        Finish(notify, 0);
        return rc;
    }

    /// <summary>
    /// Drains committed spans from the ring buffer into <paramref name="output"/>.
    /// Returns the number of spans written.
    /// </summary>
    public int DrainSpans(Span<SpanInfo> output)
    {
        if (output.Length == 0) return 0;

        uint available = RingCount;
        if (available == 0) return 0;

        uint toRead = Math.Min(available, (uint)output.Length);
        for (uint i = 0; i < toRead; i++)
        {
            uint index = (_ringHead + i) % _ringCapacity;
            output[(int)i] = _ringBuffer[index];
        }
        _ringHead += toRead;

        // Update pending_spans stat
        return (int)toRead;
    }

    /// <summary>Closes the stream. Commits any pending data and emits Closed event.</summary>
    public int Close()
    {
        if (_closed) return Ok;
        if (_reservedActive) return ErrBusy;

        bool notify = false;
        if (_pendingLen > 0)
        {
            int rc = CommitLocked(ref notify);
            if (rc != Ok) { Finish(notify, 0); return rc; }
        }

        _closed = true;
        _attached = false;
        Finish(notify, 0);
        EmitClosed();
        return Ok;
    }

    /// <summary>Returns current statistics.</summary>
    public SpanFeedStats GetStats()
    {
        return new SpanFeedStats(
            _bytesWritten,
            _spansCommitted,
            (uint)_chunkCount,
            RingCount);
    }

    /// <summary>Updates runtime-safe options (max_bytes, growth_policy, auto_commit_on_full).</summary>
    public int SetOptions(SpanFeedOptions options)
    {
        if (_closed) return ErrInvalid;
        _maxBytes = options.MaxBytes;
        _growthPolicy = options.GrowthPolicy;
        _autoCommitOnFull = options.AutoCommitOnFull;
        return Ok;
    }

    /// <summary>Returns <c>true</c> if committed spans are waiting to be drained.</summary>
    public bool HasPendingSpans() => RingCount > 0;

    /// <summary>Returns the per-chunk refcount state buffer.</summary>
    public ReadOnlySpan<byte> StateBuffer => _stateBuffer.AsSpan(0, _stateCapacity);

    /// <summary>Decrements the refcount for the given chunk index.</summary>
    public void MarkChunkFree(int chunkIndex)
    {
        if (chunkIndex < _stateCapacity && _stateBuffer[chunkIndex] > 0)
        {
            _stateBuffer[chunkIndex]--;
        }
    }

    /// <summary>Decrements the refcount for the chunk referenced by <paramref name="span"/>.</summary>
    public void MarkSpanConsumed(SpanInfo span) => MarkChunkFree(span.ChunkIndex);

    /// <summary>Returns the actual data for a committed span.</summary>
    public ReadOnlySpan<byte> GetSpanData(SpanInfo span)
    {
        return _chunks[span.ChunkIndex].AsSpan(span.Offset, span.Length);
    }

    /// <summary>Returns the actual data for a committed span as a <see cref="ReadOnlyMemory{T}"/>.</summary>
    public ReadOnlyMemory<byte> GetSpanDataMemory(SpanInfo span)
    {
        return _chunks[span.ChunkIndex].AsMemory(span.Offset, span.Length);
    }

    #endregion

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (!_closed)
        {
            _closed = true;
            _attached = false;
        }

        // Return rented chunks to the pool
        for (int i = 0; i < _chunkCount; i++)
        {
            if (_chunks[i] is not null)
            {
                ArrayPool<byte>.Shared.Return(_chunks[i]);
                _chunks[i] = null!;
            }
        }
        _chunkCount = 0;
    }

    #endregion

    #region Internal: Ring buffer

    private uint RingCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _ringTail - _ringHead;
    }

    /// <summary>Pushes a span onto the ring, sets <paramref name="notify"/> if threshold crossed.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int RingPush(SpanInfo span, ref bool notify)
    {
        uint queued = _ringTail - _ringHead;
        if (queued >= _ringCapacity) return ErrNoSpace;

        uint index = _ringTail % _ringCapacity;
        _ringBuffer[index] = span;
        _ringTail++;

        uint newCount = queued + 1;
        if (_attached && _callback is not null)
        {
            if (queued < NotifyThreshold && newCount >= NotifyThreshold)
            {
                notify = true;
            }
        }

        return Ok;
    }

    #endregion

    #region Internal: Commit / Reserve / Chunk management

    private int CommitLocked(ref bool notify)
    {
        if (_pendingLen == 0) return Ok;

        var info = new SpanInfo(_pendingChunkIndex, _pendingOffset, _pendingLen);

        int rc = RingPush(info, ref notify);
        if (rc != Ok) return rc;

        // Increment chunk refcount (saturating at 254 to avoid overflow)
        if (_pendingChunkIndex < _stateCapacity)
        {
            ref byte refCount = ref _stateBuffer[_pendingChunkIndex];
            if (refCount < 255)
                refCount++;
            // Avoid refcount saturation: if it hits 255, force chunk to appear full
            if (refCount == 255)
                _writeOffset = (int)_chunkSize;
        }

        _spansCommitted++;
        _pendingLen = 0;
        _pendingOffset = _writeOffset;
        _pendingChunkIndex = _currentChunkIndex;
        return Ok;
    }

    private int EnsureWritableChunkLocked()
    {
        int total = _chunkCount;
        if (total == 0) return ErrInvalid;

        // Scan existing chunks for one with refcount 0
        int index = _currentChunkIndex % total;
        for (int attempts = 0; attempts < total; attempts++)
        {
            if (IsChunkFree(index))
            {
                _currentChunkIndex = index;
                _writeOffset = 0;
                _pendingChunkIndex = index;
                _pendingOffset = 0;
                _pendingLen = 0;
                return Ok;
            }
            index = (index + 1) % total;
        }

        // All chunks in use
        if (_growthPolicy == GrowthPolicy.Block) return ErrNoSpace;

        int rc = AddChunkLocked();
        if (rc != Ok) return rc;

        int newTotal = _chunkCount;
        if (newTotal == 0) return ErrInvalid;

        _currentChunkIndex = newTotal - 1;
        _writeOffset = 0;
        _pendingChunkIndex = _currentChunkIndex;
        _pendingOffset = 0;
        _pendingLen = 0;
        return Ok;
    }

    private int AddChunkLocked()
    {
        ulong allocated = (ulong)_chunkCount * _chunkSize;
        if (_maxBytes != 0 && allocated + _chunkSize > _maxBytes)
            return ErrMaxBytes;

        int newIndex = _chunkCount;

        // Grow state buffer if needed
        EnsureStateCapacity(newIndex + 1);

        // Grow chunks array if needed
        if (newIndex >= _chunks.Length)
        {
            int newLen = _chunks.Length * 2;
            Array.Resize(ref _chunks, newLen);
        }

        byte[] chunk = ArrayPool<byte>.Shared.Rent((int)_chunkSize);
        _chunks[newIndex] = chunk;
        _chunkCount++;

        if (_attached && _callback is not null)
        {
            EmitChunkAdded(newIndex, (int)_chunkSize);
        }

        return Ok;
    }

    /// <summary>Adds a chunk during construction (no events emitted).</summary>
    private void AddChunkCore()
    {
        int newIndex = _chunkCount;

        if (newIndex >= _chunks.Length)
        {
            int newLen = _chunks.Length * 2;
            Array.Resize(ref _chunks, newLen);
        }

        EnsureStateCapacity(newIndex + 1);

        byte[] chunk = ArrayPool<byte>.Shared.Rent((int)_chunkSize);
        _chunks[newIndex] = chunk;
        _chunkCount++;
    }

    private void EnsureStateCapacity(int required)
    {
        if (required <= _stateCapacity) return;

        int newCapacity = _stateCapacity == 0 ? 1 : _stateCapacity;
        while (newCapacity < required) newCapacity *= 2;

        byte[] newBuffer = new byte[newCapacity];
        if (_stateCapacity > 0)
        {
            _stateBuffer.AsSpan(0, _stateCapacity).CopyTo(newBuffer);
        }
        _stateBuffer = newBuffer;
        _stateCapacity = newCapacity;

        if (_attached && _callback is not null)
        {
            EmitStateBuffer();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsChunkFree(int index)
    {
        if (index >= _stateCapacity) return true;
        return _stateBuffer[index] == 0;
    }

    #endregion

    #region Internal: Events

    private void Finish(bool notify, uint queuedOverride)
    {
        if (!notify || _callback is null) return;

        uint queued = queuedOverride != 0 ? queuedOverride : RingCount;
        if (queued > 0)
            EmitDataAvailable(queued);
    }

    private void EmitChunkAdded(int chunkIndex, int chunkSize)
    {
        _callback?.Invoke(SpanFeedEvent.ChunkAdded, chunkIndex, chunkSize);
    }

    private void EmitDataAvailable(uint count)
    {
        _callback?.Invoke(SpanFeedEvent.DataAvailable, (int)count, 0);
    }

    private void EmitStateBuffer()
    {
        _callback?.Invoke(SpanFeedEvent.StateBuffer, _stateCapacity, 0);
    }

    private void EmitClosed()
    {
        _callback?.Invoke(SpanFeedEvent.Closed, 0, 0);
    }

    #endregion

    #region Helpers

    private static SpanFeedOptions NormalizeOptions(SpanFeedOptions? opts)
    {
        uint chunkSize = opts?.ChunkSize ?? 0;
        if (chunkSize == 0) chunkSize = DefaultChunkSize;

        uint initialChunks = opts?.InitialChunks ?? 0;
        if (initialChunks == 0) initialChunks = 1;

        uint spanQueueCapacity = opts?.SpanQueueCapacity ?? 0;
        if (spanQueueCapacity == 0) spanQueueCapacity = DefaultSpanQueueCapacity;

        return new SpanFeedOptions
        {
            ChunkSize = chunkSize,
            InitialChunks = initialChunks,
            MaxBytes = opts?.MaxBytes ?? 0,
            GrowthPolicy = opts?.GrowthPolicy ?? GrowthPolicy.Grow,
            AutoCommitOnFull = opts?.AutoCommitOnFull ?? true,
            SpanQueueCapacity = spanQueueCapacity,
        };
    }

    #endregion
}
