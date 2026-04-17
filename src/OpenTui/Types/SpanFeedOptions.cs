namespace OpenTui;

/// <summary>Configuration options for NativeSpanFeed.</summary>
public sealed record SpanFeedOptions
{
    /// <summary>Size of each chunk in bytes.</summary>
    public uint ChunkSize { get; init; } = 64 * 1024;

    /// <summary>Number of chunks to pre-allocate.</summary>
    public uint InitialChunks { get; init; } = 2;

    /// <summary>Maximum total bytes allowed (0 = unlimited).</summary>
    public ulong MaxBytes { get; init; } = 0;

    /// <summary>Policy when capacity is exhausted.</summary>
    public GrowthPolicy GrowthPolicy { get; init; } = GrowthPolicy.Grow;

    /// <summary>Whether to auto-commit spans when a chunk is full.</summary>
    public bool AutoCommitOnFull { get; init; } = true;

    /// <summary>Capacity of the span queue (0 = default).</summary>
    public uint SpanQueueCapacity { get; init; } = 0;
}
