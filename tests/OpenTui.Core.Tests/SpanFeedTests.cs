using System.Runtime.InteropServices;
using OpenTui.Core;
using Xunit;

namespace OpenTui.Core.Tests;

/// <summary>
/// C# xunit equivalents of the Zig native-span-feed tests.
/// Exercises the NativeSpanFeed managed wrapper over the native opentui library.
/// Tests that require internal Zig state (stateBuffer, span_ring head/tail),
/// FailingAllocator, or markSpanConsumed for chunk reuse are not portable and are omitted.
/// </summary>
public partial class SpanFeedTests
{
    // Local P/Invoke for use inside unmanaged callbacks where the managed wrapper is inaccessible.
    [LibraryImport("opentui", EntryPoint = "streamDrainSpans")]
    private static partial uint NativeDrainSpans(nint spanFeed, nint outSpans, uint maxSpans);

    // Status codes matching Zig native-span-feed.zig Status struct
    private const int StatusOk = 0;
    private const int ErrNoSpace = -1;
    private const int ErrMaxBytes = -2;
    private const int ErrInvalid = -3;
    private const int ErrBusy = -5;

    // EventId matching Zig native-span-feed.zig EventId enum
    private const uint EventDataAvailable = 7;

    /// <summary>Native layout matching Zig SpanInfo extern struct.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct SpanInfoNative
    {
        public nuint ChunkPtr;
        public uint Offset;
        public uint Len;
        public uint ChunkIndex;
        public uint Reserved;
    }

    private static SpanFeedOptions TestOptions(uint chunkSize, uint initialChunks, bool autoCommit) =>
        new()
        {
            ChunkSize = chunkSize,
            InitialChunks = initialChunks,
            MaxBytes = 0,
            GrowthPolicy = GrowthPolicy.Grow,
            AutoCommitOnFull = autoCommit,
            SpanQueueCapacity = 0,
        };

    private static SpanFeedOptions TestOptionsFull(uint chunkSize, uint initialChunks, ulong maxBytes, bool autoCommit) =>
        new()
        {
            ChunkSize = chunkSize,
            InitialChunks = initialChunks,
            MaxBytes = maxBytes,
            GrowthPolicy = GrowthPolicy.Grow,
            AutoCommitOnFull = autoCommit,
            SpanQueueCapacity = 0,
        };

    private static SpanFeedOptions BlockOptions(uint chunkSize, uint initialChunks, bool autoCommit) =>
        new()
        {
            ChunkSize = chunkSize,
            InitialChunks = initialChunks,
            MaxBytes = 0,
            GrowthPolicy = GrowthPolicy.Block,
            AutoCommitOnFull = autoCommit,
            SpanQueueCapacity = 0,
        };

    /// <summary>
    /// Drains all spans from the feed, returning the total byte count.
    /// Note: markSpanConsumed is not available in the C# API, so chunks are not freed.
    /// </summary>
    private static unsafe ulong DrainAllSpans(NativeSpanFeed feed)
    {
        const int maxSpans = 256;
        SpanInfoNative* buf = stackalloc SpanInfoNative[maxSpans];
        ulong total = 0;
        while (true)
        {
            uint count = feed.DrainSpans((nint)buf, maxSpans);
            if (count == 0) break;
            for (uint i = 0; i < count; i++)
                total += buf[i].Len;
        }
        return total;
    }

    /// <summary>
    /// Drains spans and returns the count of spans drained.
    /// </summary>
    private static unsafe uint DrainSpanCount(NativeSpanFeed feed)
    {
        const int maxSpans = 256;
        SpanInfoNative* buf = stackalloc SpanInfoNative[maxSpans];
        uint totalCount = 0;
        while (true)
        {
            uint count = feed.DrainSpans((nint)buf, maxSpans);
            if (count == 0) break;
            totalCount += count;
        }
        return totalCount;
    }

    /// <summary>Creates a byte array filled with the given value.</summary>
    private static byte[] FilledBytes(byte value, int count)
    {
        byte[] data = new byte[count];
        Array.Fill(data, value);
        return data;
    }

    #region Create / Destroy

    [Fact]
    public void CreateAndDestroyWithOptions()
    {
        using var feed = NativeSpanFeed.Create(TestOptions(1024, 2, true));

        var stats = feed.GetStats();
        Assert.Equal(2u, stats.Chunks);
        Assert.Equal(0UL, stats.BytesWritten);
        Assert.Equal(0UL, stats.SpansCommitted);
    }

    [Fact]
    public void CreateWithDefaultOptions()
    {
        using var feed = NativeSpanFeed.Create();

        var stats = feed.GetStats();
        Assert.True(stats.Chunks >= 1);
    }

    #endregion

    #region Write + Commit basics

    [Fact]
    public void WriteAndCommitProducesSpanWithCorrectByteCount()
    {
        using var feed = NativeSpanFeed.Create(TestOptions(1024, 2, false));

        const string data = "hello world";
        Assert.Equal(StatusOk, feed.Write(data));
        Assert.Equal(StatusOk, feed.Commit());

        var stats = feed.GetStats();
        Assert.Equal((ulong)data.Length, stats.BytesWritten);
        Assert.Equal(1UL, stats.SpansCommitted);

        ulong drained = DrainAllSpans(feed);
        Assert.Equal((ulong)data.Length, drained);
    }

    [Fact]
    public void WriteWithAutoCommitFillsChunkAndCommitsAutomatically()
    {
        const uint chunkSize = 64;
        using var feed = NativeSpanFeed.Create(TestOptions(chunkSize, 2, true));

        byte[] data = FilledBytes((byte)'A', 64);
        Assert.Equal(StatusOk, feed.Write(data));

        var stats = feed.GetStats();
        Assert.Equal(64UL, stats.BytesWritten);
        Assert.Equal(1UL, stats.SpansCommitted);
    }

    [Fact]
    public void WriteSpanningMultipleChunksWithAutoCommit()
    {
        const uint chunkSize = 64;
        using var feed = NativeSpanFeed.Create(TestOptions(chunkSize, 2, true));

        byte[] data = FilledBytes((byte)'B', 150);
        Assert.Equal(StatusOk, feed.Write(data));

        var stats = feed.GetStats();
        Assert.Equal(150UL, stats.BytesWritten);
        Assert.Equal(2UL, stats.SpansCommitted);

        Assert.Equal(StatusOk, feed.Commit());
        var stats2 = feed.GetStats();
        Assert.Equal(3UL, stats2.SpansCommitted);

        ulong drained = DrainAllSpans(feed);
        Assert.Equal(150UL, drained);
    }

    [Fact]
    public void WriteReturnsNoSpaceWhenAutoCommitDisabledAndDataExceedsChunk()
    {
        const uint chunkSize = 64;
        using var feed = NativeSpanFeed.Create(TestOptions(chunkSize, 2, false));

        byte[] data = FilledBytes((byte)'C', 65);
        Assert.Equal(ErrNoSpace, feed.Write(data));
    }

    [Fact]
    public void WriteExactlyFillsChunkWithoutAutoCommitSucceeds()
    {
        const uint chunkSize = 64;
        using var feed = NativeSpanFeed.Create(TestOptions(chunkSize, 2, false));

        byte[] exact = FilledBytes((byte)'A', 64);
        Assert.Equal(StatusOk, feed.Write(exact));

        var stats = feed.GetStats();
        Assert.Equal(64UL, stats.BytesWritten);

        Assert.Equal(StatusOk, feed.Commit());
        DrainAllSpans(feed);

        Assert.Equal(StatusOk, feed.Write("B"));
        Assert.Equal(StatusOk, feed.Commit());

        var stats2 = feed.GetStats();
        Assert.Equal(65UL, stats2.BytesWritten);
        Assert.Equal(2UL, stats2.SpansCommitted);
    }

    [Fact]
    public unsafe void WrittenDataMatchesDrainedSpanContent()
    {
        const uint chunkSize = 256;
        using var feed = NativeSpanFeed.Create(TestOptions(chunkSize, 2, false));

        const string data = "the quick brown fox jumps over the lazy dog";
        Assert.Equal(StatusOk, feed.Write(data));
        Assert.Equal(StatusOk, feed.Commit());

        SpanInfoNative* buf = stackalloc SpanInfoNative[16];
        uint count = feed.DrainSpans((nint)buf, 16);
        Assert.Equal(1u, count);

        var span = buf[0];
        byte* basePtr = (byte*)span.ChunkPtr;
        var slice = new ReadOnlySpan<byte>(basePtr + span.Offset, (int)span.Len);
        Assert.Equal(data, System.Text.Encoding.UTF8.GetString(slice));
    }

    #endregion

    #region Reserve / CommitReserved

    [Fact]
    public void ReserveAndCommitReservedRoundTrip()
    {
        const uint chunkSize = 256;
        using var feed = NativeSpanFeed.Create(TestOptions(chunkSize, 2, false));

        int result = feed.Reserve(10, out _);
        Assert.Equal(StatusOk, result);

        Assert.Equal(StatusOk, feed.CommitReserved(5));

        var stats = feed.GetStats();
        Assert.Equal(5UL, stats.BytesWritten);
        Assert.Equal(1UL, stats.SpansCommitted);

        ulong drained = DrainAllSpans(feed);
        Assert.Equal(5UL, drained);
    }

    [Fact]
    public void ReserveReturnsBusyIfAlreadyReserved()
    {
        using var feed = NativeSpanFeed.Create(TestOptions(256, 2, false));

        Assert.Equal(StatusOk, feed.Reserve(1, out _));
        Assert.Equal(ErrBusy, feed.Reserve(1, out _));

        Assert.Equal(StatusOk, feed.CommitReserved(0));
    }

    [Fact]
    public void ReserveReturnsBusyIfPendingDataExists()
    {
        using var feed = NativeSpanFeed.Create(TestOptions(256, 2, false));

        Assert.Equal(StatusOk, feed.Write("some data"));
        Assert.Equal(ErrBusy, feed.Reserve(1, out _));
    }

    [Fact]
    public void WriteReturnsBusyWhileReservationIsActive()
    {
        using var feed = NativeSpanFeed.Create(TestOptions(256, 2, false));

        Assert.Equal(StatusOk, feed.Reserve(1, out _));
        Assert.Equal(ErrBusy, feed.Write("data"));

        Assert.Equal(StatusOk, feed.CommitReserved(0));
    }

    [Fact]
    public void ReserveOnClosedStreamReturnsInvalid()
    {
        using var feed = NativeSpanFeed.Create(TestOptions(256, 1, false));

        Assert.Equal(StatusOk, feed.Close());
        Assert.Equal(ErrInvalid, feed.Reserve(1, out _));
    }

    [Fact]
    public void CommitReservedOnClosedStreamReturnsInvalid()
    {
        using var feed = NativeSpanFeed.Create(TestOptions(256, 1, false));

        Assert.Equal(StatusOk, feed.Close());
        Assert.Equal(ErrInvalid, feed.CommitReserved(0));
    }

    [Fact]
    public void CommitReservedWithoutActiveReservationReturnsInvalid()
    {
        using var feed = NativeSpanFeed.Create(TestOptions(256, 1, false));

        Assert.Equal(ErrInvalid, feed.CommitReserved(0));
    }

    [Fact]
    public void ReserveWithMinLenLargerThanChunkReturnsNoSpace()
    {
        using var feed = NativeSpanFeed.Create(TestOptions(64, 1, false));

        Assert.Equal(ErrNoSpace, feed.Reserve(65, out _));
    }

    [Fact]
    public void CommitReservedWithZeroLengthProducesNoSpan()
    {
        using var feed = NativeSpanFeed.Create(TestOptions(256, 1, false));

        Assert.Equal(StatusOk, feed.Reserve(1, out _));
        Assert.Equal(StatusOk, feed.CommitReserved(0));

        var stats = feed.GetStats();
        Assert.Equal(0UL, stats.SpansCommitted);
        Assert.Equal(0UL, stats.BytesWritten);

        Assert.Equal(StatusOk, feed.Write("after"));
        Assert.Equal(StatusOk, feed.Commit());

        var stats2 = feed.GetStats();
        Assert.Equal(1UL, stats2.SpansCommitted);
        Assert.Equal(5UL, stats2.BytesWritten);

        ulong drained = DrainAllSpans(feed);
        Assert.Equal(5UL, drained);
    }

    #endregion

    #region Close

    [Fact]
    public void WriteToClosedStreamReturnsInvalid()
    {
        using var feed = NativeSpanFeed.Create(TestOptions(256, 2, false));

        Assert.Equal(StatusOk, feed.Close());
        Assert.Equal(ErrInvalid, feed.Write("data"));
    }

    [Fact]
    public void DoubleCloseDoesNotError()
    {
        using var feed = NativeSpanFeed.Create(TestOptions(256, 2, false));

        Assert.Equal(StatusOk, feed.Close());
        Assert.Equal(StatusOk, feed.Close());
    }

    [Fact]
    public void CommitOnClosedStreamReturnsInvalid()
    {
        using var feed = NativeSpanFeed.Create(TestOptions(256, 1, false));

        Assert.Equal(StatusOk, feed.Close());
        Assert.Equal(ErrInvalid, feed.Commit());
    }

    [Fact]
    public void CloseWithPendingDataAutoCommits()
    {
        using var feed = NativeSpanFeed.Create(TestOptions(256, 1, false));

        Assert.Equal(StatusOk, feed.Write("pending data"));
        Assert.Equal(StatusOk, feed.Close());

        Assert.Equal(1UL, feed.GetStats().SpansCommitted);

        ulong drained = DrainAllSpans(feed);
        Assert.Equal(12UL, drained);
    }

    [Fact]
    public void CloseWithActiveReservationReturnsBusy()
    {
        using var feed = NativeSpanFeed.Create(TestOptions(256, 1, false));

        Assert.Equal(StatusOk, feed.Reserve(1, out _));
        Assert.Equal(ErrBusy, feed.Close());

        Assert.Equal(StatusOk, feed.CommitReserved(0));
        Assert.Equal(StatusOk, feed.Close());
    }

    [Fact]
    public void DestroyWithoutCloseCommitsPendingData()
    {
        var feed = NativeSpanFeed.Create(TestOptions(256, 1, false));

        Assert.Equal(StatusOk, feed.Write("before destroy"));

        var stats = feed.GetStats();
        Assert.Equal(14UL, stats.BytesWritten);
        Assert.Equal(0UL, stats.SpansCommitted);

        feed.Dispose();
    }

    #endregion

    #region Consecutive writes (no auto_commit)

    [Fact]
    public void ConsecutiveWritesWithoutAutoCommitPreservesAllData()
    {
        const uint chunkSize = 64;
        using var feed = NativeSpanFeed.Create(TestOptions(chunkSize, 2, false));

        byte[] first = FilledBytes((byte)'A', 64);
        Assert.Equal(StatusOk, feed.Write(first));

        var stats = feed.GetStats();
        Assert.Equal(64UL, stats.BytesWritten);

        Assert.Equal(StatusOk, feed.Write("BBBB"));

        stats = feed.GetStats();
        Assert.Equal(68UL, stats.BytesWritten);
        Assert.Equal(1UL, stats.SpansCommitted);
        Assert.Equal(StatusOk, feed.Commit());
        stats = feed.GetStats();
        Assert.Equal(2UL, stats.SpansCommitted);

        ulong drained = DrainAllSpans(feed);
        Assert.Equal(68UL, drained);
    }

    [Fact]
    public unsafe void WriteExactlyFillsChunkThenWriteMoreNoAutoCommit()
    {
        const uint chunkSize = 32;
        using var feed = NativeSpanFeed.Create(TestOptions(chunkSize, 2, false));

        byte[] fill = FilledBytes((byte)'X', 32);
        Assert.Equal(StatusOk, feed.Write(fill));

        Assert.Equal(StatusOk, feed.Write("Y"));
        Assert.Equal(StatusOk, feed.Commit());

        var stats = feed.GetStats();
        Assert.Equal(33UL, stats.BytesWritten);
        Assert.Equal(2UL, stats.SpansCommitted);

        SpanInfoNative* buf = stackalloc SpanInfoNative[16];
        uint count = feed.DrainSpans((nint)buf, 16);
        Assert.Equal(2u, count);

        // Verify first span is 32 bytes of 'X'
        byte* base1 = (byte*)buf[0].ChunkPtr;
        Assert.Equal(32u, buf[0].Len);
        Assert.Equal((byte)'X', base1[buf[0].Offset]);
        Assert.Equal((byte)'X', base1[buf[0].Offset + 31]);

        // Verify second span is "Y"
        byte* base2 = (byte*)buf[1].ChunkPtr;
        var slice2 = new ReadOnlySpan<byte>(base2 + buf[1].Offset, (int)buf[1].Len);
        Assert.Equal("Y", System.Text.Encoding.UTF8.GetString(slice2));
    }

    [Fact]
    public void MultipleChunkTransitionsWithoutAutoCommit()
    {
        const uint chunkSize = 16;
        using var feed = NativeSpanFeed.Create(TestOptions(chunkSize, 1, false));

        Assert.Equal(StatusOk, feed.Write("AAAAAAAAAAAAAAAA")); // 16 bytes
        Assert.Equal(StatusOk, feed.Write("BBBBBBBBBBBBBBBB")); // 16 bytes
        Assert.Equal(StatusOk, feed.Write("CCCCCCCC"));         // 8 bytes

        var stats = feed.GetStats();
        Assert.Equal(40UL, stats.BytesWritten);
        Assert.Equal(2UL, stats.SpansCommitted);
        Assert.Equal(StatusOk, feed.Commit());
        stats = feed.GetStats();
        Assert.Equal(3UL, stats.SpansCommitted);

        ulong drained = DrainAllSpans(feed);
        Assert.Equal(40UL, drained);
    }

    #endregion

    #region Chunk reuse and growth

    [Fact]
    public void CommitAfterSmallWriteShouldAllowReuseOfRemainingChunkSpace()
    {
        const uint chunkSize = 256;
        using var feed = NativeSpanFeed.Create(TestOptions(chunkSize, 1, false));

        Assert.Equal(StatusOk, feed.Write("0123456789"));
        Assert.Equal(StatusOk, feed.Commit());

        var stats = feed.GetStats();
        Assert.Equal(10UL, stats.BytesWritten);
        Assert.Equal(1UL, stats.SpansCommitted);
        Assert.Equal(1u, stats.Chunks);

        // Drain (but can't markSpanConsumed — chunk won't be freed)
        // Still, the write offset within the same chunk should allow more writes.
        DrainAllSpans(feed);

        Assert.Equal(StatusOk, feed.Write("abcdefghij"));
        Assert.Equal(StatusOk, feed.Commit());

        stats = feed.GetStats();
        Assert.Equal(20UL, stats.BytesWritten);
        Assert.Equal(2UL, stats.SpansCommitted);
        Assert.Equal(1u, stats.Chunks);
    }

    [Fact]
    public void RepeatedSmallWriteCommitShouldNotForceChunkGrowth()
    {
        const uint chunkSize = 1024;
        using var feed = NativeSpanFeed.Create(TestOptions(chunkSize, 1, false));

        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(StatusOk, feed.Write("12345678"));
            Assert.Equal(StatusOk, feed.Commit());
        }

        var stats = feed.GetStats();
        Assert.Equal(32UL, stats.BytesWritten);
        Assert.Equal(4UL, stats.SpansCommitted);
        Assert.Equal(1u, stats.Chunks);

        ulong drained = DrainAllSpans(feed);
        Assert.Equal(32UL, drained);
    }

    [Fact]
    public void MemoryGrowthUnderPressureAllocatesNewChunks()
    {
        using var feed = NativeSpanFeed.Create(TestOptions(64, 1, true));

        Assert.Equal(1u, feed.GetStats().Chunks);

        for (int i = 0; i < 10; i++)
        {
            byte[] data = FilledBytes((byte)i, 64);
            Assert.Equal(StatusOk, feed.Write(data));
        }

        var stats = feed.GetStats();
        Assert.Equal(640UL, stats.BytesWritten);
        Assert.True(stats.Chunks >= 10);

        ulong drained = DrainAllSpans(feed);
        Assert.Equal(640UL, drained);
    }

    #endregion

    #region max_bytes

    [Fact]
    public void MaxBytesReturnsMaxBytesWhenLimitIsReached()
    {
        using var feed = NativeSpanFeed.Create(TestOptionsFull(32, 2, 64, false));

        Assert.Equal(2u, feed.GetStats().Chunks);

        byte[] fill1 = FilledBytes((byte)'A', 32);
        Assert.Equal(StatusOk, feed.Write(fill1));
        Assert.Equal(StatusOk, feed.Commit());

        byte[] fill2 = FilledBytes((byte)'B', 32);
        Assert.Equal(StatusOk, feed.Write(fill2));
        Assert.Equal(StatusOk, feed.Commit());

        Assert.Equal(ErrMaxBytes, feed.Write("C"));
    }

    [Fact]
    public void AutoCommitWithMaxBytesShouldHandleWriteSpanningChunkBoundary()
    {
        using var feed = NativeSpanFeed.Create(TestOptionsFull(32, 2, 64, true));

        byte[] data = FilledBytes((byte)'X', 64);
        Assert.Equal(StatusOk, feed.Write(data));

        Assert.Equal(64UL, feed.GetStats().BytesWritten);
        Assert.True(feed.GetStats().SpansCommitted >= 1);

        DrainAllSpans(feed);
    }

    [Fact]
    public void WriteErrorMidLoopPreservesAlreadyCommittedSpans()
    {
        using var feed = NativeSpanFeed.Create(TestOptionsFull(32, 2, 64, true));

        byte[] data = FilledBytes((byte)'Z', 96);
        int result = feed.Write(data);
        Assert.Equal(ErrMaxBytes, result);

        var stats = feed.GetStats();
        Assert.Equal(2UL, stats.SpansCommitted);

        ulong drained = DrainAllSpans(feed);
        Assert.Equal(64UL, drained);
    }

    #endregion

    #region growth_policy=block

    [Fact]
    public void GrowthPolicyBlockPreventsNewChunkAllocation()
    {
        const uint chunkSize = 64;
        using var feed = NativeSpanFeed.Create(BlockOptions(chunkSize, 2, false));

        Assert.Equal(2u, feed.GetStats().Chunks);

        byte[] fill1 = FilledBytes((byte)'A', 64);
        Assert.Equal(StatusOk, feed.Write(fill1));
        Assert.Equal(StatusOk, feed.Commit());

        byte[] fill2 = FilledBytes((byte)'B', 64);
        Assert.Equal(StatusOk, feed.Write(fill2));
        Assert.Equal(StatusOk, feed.Commit());

        Assert.Equal(ErrNoSpace, feed.Write("C"));
        Assert.Equal(2u, feed.GetStats().Chunks);
    }

    [Fact]
    public void GrowthPolicyBlockWithAutoCommitReturnsNoSpaceWhenPoolExhausted()
    {
        const uint chunkSize = 32;
        using var feed = NativeSpanFeed.Create(BlockOptions(chunkSize, 2, true));

        byte[] data = FilledBytes((byte)'X', 64);
        Assert.Equal(StatusOk, feed.Write(data));

        Assert.Equal(ErrNoSpace, feed.Write("Y"));
        Assert.Equal(2u, feed.GetStats().Chunks);
    }

    #endregion

    #region Span ring overflow

    [Fact]
    public void SpanRingOverflowReturnsNoSpace()
    {
        const uint chunkSize = 4096;
        using var feed = NativeSpanFeed.Create(TestOptions(chunkSize, 1, false));

        for (uint i = 0; i < 4096; i++)
        {
            Assert.Equal(StatusOk, feed.Write("x"));
            Assert.Equal(StatusOk, feed.Commit());
        }

        Assert.Equal(4096u, feed.GetStats().PendingSpans);

        Assert.Equal(StatusOk, feed.Write("y"));
        Assert.Equal(ErrNoSpace, feed.Commit());
    }

    [Fact]
    public void SpanQueueCapacityZeroDefaultsTo4096()
    {
        var opts = TestOptions(4096, 1, false);
        using var feed = NativeSpanFeed.Create(opts);

        for (uint i = 0; i < 4096; i++)
        {
            Assert.Equal(StatusOk, feed.Write("x"));
            Assert.Equal(StatusOk, feed.Commit());
        }

        Assert.Equal(4096u, feed.GetStats().PendingSpans);

        Assert.Equal(StatusOk, feed.Write("y"));
        Assert.Equal(ErrNoSpace, feed.Commit());
    }

    [Fact]
    public void CustomSpanQueueCapacityIsRespectedOverflow()
    {
        var opts = new SpanFeedOptions
        {
            ChunkSize = 4096,
            InitialChunks = 1,
            GrowthPolicy = GrowthPolicy.Grow,
            AutoCommitOnFull = false,
            SpanQueueCapacity = 8,
        };
        using var feed = NativeSpanFeed.Create(opts);

        for (uint i = 0; i < 8; i++)
        {
            Assert.Equal(StatusOk, feed.Write("x"));
            Assert.Equal(StatusOk, feed.Commit());
        }

        Assert.Equal(8u, feed.GetStats().PendingSpans);

        Assert.Equal(StatusOk, feed.Write("y"));
        Assert.Equal(ErrNoSpace, feed.Commit());
    }

    #endregion

    #region Empty / no-op

    [Fact]
    public void EmptyWriteIsNoOp()
    {
        using var feed = NativeSpanFeed.Create(TestOptions(256, 1, false));

        // Zig: streamWrite with len==0 returns Status.ok (no-op)
        Assert.Equal(StatusOk, feed.Write(""));
        Assert.Equal(0UL, feed.GetStats().BytesWritten);
    }

    [Fact]
    public void CommitWithNoPendingDataIsNoOp()
    {
        using var feed = NativeSpanFeed.Create(TestOptions(256, 1, false));

        Assert.Equal(StatusOk, feed.Commit());
        Assert.Equal(0UL, feed.GetStats().SpansCommitted);
    }

    [Fact]
    public unsafe void DrainWithNoSpansReturnsZero()
    {
        using var feed = NativeSpanFeed.Create(TestOptions(256, 1, false));

        SpanInfoNative* buf = stackalloc SpanInfoNative[16];
        uint count = feed.DrainSpans((nint)buf, 16);
        Assert.Equal(0u, count);
    }

    #endregion

    #region SetOptions

    [Fact]
    public void SetOptionsOnClosedStreamReturnsInvalid()
    {
        using var feed = NativeSpanFeed.Create(TestOptions(256, 1, false));

        Assert.Equal(StatusOk, feed.Close());
        Assert.Equal(ErrInvalid, feed.SetOptions(TestOptions(128, 1, true)));
    }

    [Fact]
    public void SetOptionsIgnoresChunkSizeImmutableAfterCreation()
    {
        using var feed = NativeSpanFeed.Create(TestOptions(64, 1, true));

        byte[] fill1 = FilledBytes((byte)'A', 64);
        Assert.Equal(StatusOk, feed.Write(fill1));

        Assert.Equal(StatusOk, feed.SetOptions(TestOptions(128, 1, true)));

        byte[] fill2 = FilledBytes((byte)'B', 64);
        Assert.Equal(StatusOk, feed.Write(fill2));

        Assert.Equal(StatusOk, feed.Commit());

        var stats = feed.GetStats();
        Assert.Equal(128UL, stats.BytesWritten);

        ulong drained = DrainAllSpans(feed);
        Assert.Equal(128UL, drained);
    }

    [Fact]
    public void SetOptionsEnablesAutoCommitMidStream()
    {
        using var feed = NativeSpanFeed.Create(TestOptions(64, 2, false));

        byte[] first = FilledBytes((byte)'A', 32);
        Assert.Equal(StatusOk, feed.Write(first));
        Assert.Equal(0UL, feed.GetStats().SpansCommitted);
        Assert.Equal(StatusOk, feed.Commit());
        Assert.Equal(1UL, feed.GetStats().SpansCommitted);

        DrainAllSpans(feed);
        Assert.Equal(StatusOk, feed.SetOptions(TestOptions(64, 2, true)));
        byte[] second = FilledBytes((byte)'B', 64);
        Assert.Equal(StatusOk, feed.Write(second));
        Assert.Equal(2UL, feed.GetStats().SpansCommitted);

        DrainAllSpans(feed);
    }

    #endregion

    #region Auto-commit: exact multiples

    [Fact]
    public void WriteExactlyChunkSizeTimesNWithAutoCommitCommitsAllNoDanglingPending()
    {
        const uint chunkSize = 64;
        const int n = 5;
        const int total = (int)(chunkSize * n);
        using var feed = NativeSpanFeed.Create(TestOptions(chunkSize, 2, true));

        byte[] data = FilledBytes((byte)'E', total);
        Assert.Equal(StatusOk, feed.Write(data));

        var stats = feed.GetStats();
        Assert.Equal((ulong)n, stats.SpansCommitted);
        Assert.Equal((ulong)total, stats.BytesWritten);

        Assert.Equal(StatusOk, feed.Commit());
        Assert.Equal((ulong)n, feed.GetStats().SpansCommitted);

        ulong drained = DrainAllSpans(feed);
        Assert.Equal((ulong)total, drained);
    }

    #endregion

    #region bytes_written consistency

    [Fact]
    public void BytesWrittenMatchesTotalDrainedAcrossAllOperations()
    {
        using var feed = NativeSpanFeed.Create(TestOptions(64, 1, true));

        Assert.Equal(StatusOk, feed.Write("short"));
        Assert.Equal(StatusOk, feed.Commit());
        Assert.Equal(StatusOk, feed.Write(FilledBytes((byte)'M', 64)));
        Assert.Equal(StatusOk, feed.Write(FilledBytes((byte)'L', 200)));

        Assert.Equal(StatusOk, feed.Commit());

        var stats = feed.GetStats();
        ulong drained = DrainAllSpans(feed);

        Assert.Equal(stats.BytesWritten, drained);
    }

    #endregion

    #region Callback tests

    [ThreadStatic]
    private static int s_dataAvailableCount;

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static void CountingCallback(nuint streamPtr, uint eventId, nuint param1, ulong param2)
    {
        if (eventId == EventDataAvailable)
            s_dataAvailableCount++;
    }

    [Fact]
    public unsafe void WriteReturningNoSpaceEmitsDataAvailableExactlyOnce()
    {
        s_dataAvailableCount = 0;
        using var feed = NativeSpanFeed.Create(TestOptions(64, 2, false));

        feed.SetCallback((nint)(delegate* unmanaged[Cdecl]<nuint, uint, nuint, ulong, void>)&CountingCallback);
        feed.Attach();
        s_dataAvailableCount = 0;

        byte[] first = FilledBytes((byte)'A', 64);
        Assert.Equal(StatusOk, feed.Write(first));
        byte[] overflow = FilledBytes((byte)'B', 65);
        int result = feed.Write(overflow);
        Assert.Equal(ErrNoSpace, result);
        Assert.Equal(1, s_dataAvailableCount);
    }

    #endregion

    #region Data integrity

    [Fact]
    public unsafe void DataIntegrityAcrossManyChunksWithAutoCommit()
    {
        const uint chunkSize = 64;
        using var feed = NativeSpanFeed.Create(TestOptions(chunkSize, 1, true));

        byte[] source = new byte[1024];
        for (int i = 0; i < source.Length; i++)
            source[i] = (byte)(i % 256);

        Assert.Equal(StatusOk, feed.Write(source));
        Assert.Equal(StatusOk, feed.Commit());

        byte[] received = new byte[1024];
        int offset = 0;

        const int maxSpans = 256;
        SpanInfoNative* buf = stackalloc SpanInfoNative[maxSpans];
        while (true)
        {
            uint count = feed.DrainSpans((nint)buf, maxSpans);
            if (count == 0) break;
            for (uint i = 0; i < count; i++)
            {
                var span = buf[i];
                byte* basePtr = (byte*)span.ChunkPtr;
                new ReadOnlySpan<byte>(basePtr + span.Offset, (int)span.Len)
                    .CopyTo(received.AsSpan(offset));
                offset += (int)span.Len;
            }
        }

        Assert.Equal(1024, offset);
        Assert.Equal(source, received);
    }

    #endregion

    #region Pending data survives failed commit (ring full)

    [Fact]
    public void PendingDataSurvivesFailedCommitRingFull()
    {
        const uint chunkSize = 4096;
        using var feed = NativeSpanFeed.Create(TestOptions(chunkSize, 1, false));

        for (uint i = 0; i < 4096; i++)
        {
            Assert.Equal(StatusOk, feed.Write("x"));
            Assert.Equal(StatusOk, feed.Commit());
        }

        Assert.Equal(StatusOk, feed.Write("important"));
        Assert.Equal(ErrNoSpace, feed.Commit());

        // Drain the ring to free space
        DrainAllSpans(feed);

        // Now the pending commit should succeed
        Assert.Equal(StatusOk, feed.Commit());

        var stats = feed.GetStats();
        Assert.Equal(4096UL + 9, stats.BytesWritten);
        Assert.Equal(4097UL, stats.SpansCommitted);
    }

    #endregion

    #region Large span_queue_capacity

    [Fact]
    public void LargeSpanQueueCapacityWorks()
    {
        var opts = new SpanFeedOptions
        {
            ChunkSize = 4096,
            InitialChunks = 1,
            GrowthPolicy = GrowthPolicy.Grow,
            AutoCommitOnFull = false,
            SpanQueueCapacity = 8192,
        };
        using var feed = NativeSpanFeed.Create(opts);

        for (int i = 0; i < 5000; i++)
        {
            Assert.Equal(StatusOk, feed.Write("x"));
            Assert.Equal(StatusOk, feed.Commit());
        }

        Assert.Equal(5000u, feed.GetStats().PendingSpans);

        DrainAllSpans(feed);
        Assert.Equal(0u, feed.GetStats().PendingSpans);
    }

    #endregion

    #region max_bytes allows reuse after draining

    [Fact(Skip = "Chunk reuse after draining requires markSpanConsumed which is not exposed in the C# API")]
    public void MaxBytesAllowsReuseAfterDraining()
    {
        using var feed = NativeSpanFeed.Create(TestOptionsFull(32, 2, 64, false));

        byte[] fill1 = FilledBytes((byte)'A', 32);
        Assert.Equal(StatusOk, feed.Write(fill1));
        Assert.Equal(StatusOk, feed.Commit());
        byte[] fill2 = FilledBytes((byte)'B', 32);
        Assert.Equal(StatusOk, feed.Write(fill2));
        Assert.Equal(StatusOk, feed.Commit());

        // Drain (without markSpanConsumed, chunks are not freed for reuse,
        // but the native implementation may still recycle via drainSpans).
        DrainAllSpans(feed);

        byte[] fill3 = FilledBytes((byte)'C', 32);
        Assert.Equal(StatusOk, feed.Write(fill3));
        Assert.Equal(StatusOk, feed.Commit());

        Assert.Equal(96UL, feed.GetStats().BytesWritten);
        Assert.Equal(2u, feed.GetStats().Chunks);
    }

    #endregion

    #region auto_commit with max_bytes consumer keeps up

    [Fact(Skip = "Chunk reuse after draining requires markSpanConsumed which is not exposed in the C# API")]
    public void AutoCommitWithMaxBytesWorksWhenConsumerKeepsUp()
    {
        using var feed = NativeSpanFeed.Create(TestOptionsFull(32, 2, 64, true));

        byte[] fill1 = FilledBytes((byte)'A', 32);
        Assert.Equal(StatusOk, feed.Write(fill1));
        DrainAllSpans(feed);

        byte[] fill2 = FilledBytes((byte)'B', 32);
        Assert.Equal(StatusOk, feed.Write(fill2));
        DrainAllSpans(feed);

        byte[] fill3 = FilledBytes((byte)'C', 32);
        Assert.Equal(StatusOk, feed.Write(fill3));

        Assert.Equal(96UL, feed.GetStats().BytesWritten);
        Assert.Equal(2u, feed.GetStats().Chunks);

        DrainAllSpans(feed);
    }

    #endregion

    #region growth_policy=block allows reuse after draining

    [Fact(Skip = "Chunk reuse after draining requires markSpanConsumed which is not exposed in the C# API")]
    public void GrowthPolicyBlockAllowsReuseAfterDraining()
    {
        const uint chunkSize = 64;
        using var feed = NativeSpanFeed.Create(BlockOptions(chunkSize, 2, false));

        Assert.Equal(StatusOk, feed.Write(FilledBytes((byte)'A', 64)));
        Assert.Equal(StatusOk, feed.Commit());
        Assert.Equal(StatusOk, feed.Write(FilledBytes((byte)'B', 64)));
        Assert.Equal(StatusOk, feed.Commit());

        DrainAllSpans(feed);
        Assert.Equal(StatusOk, feed.Write(FilledBytes((byte)'C', 64)));
        Assert.Equal(StatusOk, feed.Commit());

        Assert.Equal(192UL, feed.GetStats().BytesWritten);
        Assert.Equal(2u, feed.GetStats().Chunks);
    }

    #endregion

    #region Span ring recovers after draining

    [Fact]
    public void SpanRingRecoversAfterDraining()
    {
        const uint chunkSize = 4096;
        using var feed = NativeSpanFeed.Create(TestOptions(chunkSize, 1, false));

        for (uint i = 0; i < 4096; i++)
        {
            Assert.Equal(StatusOk, feed.Write("x"));
            Assert.Equal(StatusOk, feed.Commit());
        }

        DrainAllSpans(feed);
        Assert.Equal(0u, feed.GetStats().PendingSpans);

        Assert.Equal(StatusOk, feed.Write("z"));
        Assert.Equal(StatusOk, feed.Commit());
        Assert.Equal(1u, feed.GetStats().PendingSpans);

        DrainAllSpans(feed);
    }

    #endregion

    #region Custom span_queue_capacity full cycle

    [Fact]
    public void CustomSpanQueueCapacityRecoverAfterDrain()
    {
        var opts = new SpanFeedOptions
        {
            ChunkSize = 4096,
            InitialChunks = 1,
            GrowthPolicy = GrowthPolicy.Grow,
            AutoCommitOnFull = false,
            SpanQueueCapacity = 8,
        };
        using var feed = NativeSpanFeed.Create(opts);

        for (uint i = 0; i < 8; i++)
        {
            Assert.Equal(StatusOk, feed.Write("x"));
            Assert.Equal(StatusOk, feed.Commit());
        }
        Assert.Equal(8u, feed.GetStats().PendingSpans);

        Assert.Equal(StatusOk, feed.Write("y"));
        Assert.Equal(ErrNoSpace, feed.Commit());

        DrainAllSpans(feed);
        Assert.Equal(0u, feed.GetStats().PendingSpans);

        Assert.Equal(StatusOk, feed.Write("z"));
        Assert.Equal(StatusOk, feed.Commit());
        Assert.Equal(1u, feed.GetStats().PendingSpans);
    }

    #endregion

    #region hasPendingSpans (via PendingSpans stat)

    [Fact]
    public void PendingSpansReflectsStateCorrectly()
    {
        using var feed = NativeSpanFeed.Create(TestOptions(256, 1, false));

        Assert.Equal(0u, feed.GetStats().PendingSpans);

        Assert.Equal(StatusOk, feed.Write("data"));
        Assert.Equal(StatusOk, feed.Commit());
        Assert.True(feed.GetStats().PendingSpans > 0);

        DrainAllSpans(feed);
        Assert.Equal(0u, feed.GetStats().PendingSpans);
    }

    #endregion

    #region Synchronous drain during write (callback)

    [ThreadStatic]
    private static nint s_drainTarget;
    [ThreadStatic]
    private static ulong s_drainTotal;

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static unsafe void DrainingCallback(nuint streamPtr, uint eventId, nuint param1, ulong param2)
    {
        if (eventId != EventDataAvailable) return;
        if (s_drainTarget == nint.Zero) return;

        SpanInfoNative* buf = stackalloc SpanInfoNative[64];
        while (true)
        {
            uint count = NativeDrainSpans(s_drainTarget, (nint)buf, 64);
            if (count == 0) break;
            for (uint i = 0; i < count; i++)
                s_drainTotal += buf[i].Len;
        }
    }

    [Fact]
    public unsafe void SynchronousDrainDuringWriteDoesNotCorruptState()
    {
        s_drainTarget = nint.Zero;
        s_drainTotal = 0;

        const uint chunkSize = 64;
        using var feed = NativeSpanFeed.Create(TestOptions(chunkSize, 2, true));

        feed.SetCallback((nint)(delegate* unmanaged[Cdecl]<nuint, uint, nuint, ulong, void>)&DrainingCallback);
        feed.Attach();
        s_drainTarget = feed.Handle;
        s_drainTotal = 0;

        byte[] data = FilledBytes((byte)'D', 256);
        Assert.Equal(StatusOk, feed.Write(data));

        Assert.Equal(StatusOk, feed.Commit());

        // Also drain any remaining spans manually
        const int maxSpans = 64;
        SpanInfoNative* finalBuf = stackalloc SpanInfoNative[maxSpans];
        while (true)
        {
            uint count = feed.DrainSpans((nint)finalBuf, maxSpans);
            if (count == 0) break;
            for (uint i = 0; i < count; i++)
                s_drainTotal += finalBuf[i].Len;
        }

        Assert.Equal(256UL, s_drainTotal);
        Assert.Equal(256UL, feed.GetStats().BytesWritten);

        s_drainTarget = nint.Zero;
    }

    #endregion
}
