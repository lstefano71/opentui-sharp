using System.Runtime.InteropServices;

namespace OpenTui;

/// <summary>Runtime statistics from a NativeSpanFeed.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct SpanFeedStats(ulong BytesWritten, ulong SpansCommitted, uint Chunks, uint PendingSpans);
