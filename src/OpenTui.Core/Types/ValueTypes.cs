using System.Runtime.InteropServices;

namespace OpenTui.Core;

/// <summary>Logical cursor position in a text buffer (row/col/offset).</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct LogicalCursor(uint Row, uint Col, uint Offset);

/// <summary>Visual cursor position mapping visual → logical coordinates.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct VisualCursor(uint VisualRow, uint VisualCol, uint LogicalRow, uint LogicalCol, uint Offset);

/// <summary>Viewport bounds in columns/rows.</summary>
public readonly record struct ViewportBounds(int X, int Y, int Width, int Height);

/// <summary>Result of measuring text dimensions.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct MeasureResult(uint LineCount, uint WidthColsMax);

/// <summary>
/// Current state of the terminal cursor, as reported by the native renderer.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CursorState
{
    public uint X;
    public uint Y;
    [MarshalAs(UnmanagedType.U1)] public bool Visible;
    public byte Style;
    [MarshalAs(UnmanagedType.U1)] public bool Blinking;
    public float R;
    public float G;
    public float B;
    public float A;

    public readonly CursorStyle CursorStyle => (CursorStyle)Style;
    public readonly Rgba Color => new(R, G, B, A);
}

/// <summary>
/// Options for configuring cursor appearance.
/// Sentinel value 255 means "don't change this property."
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct CursorStyleOptions
{
    public byte Style = 255;
    public byte Blinking = 255;
    public nint ColorPtr;
    public byte Cursor = 255;

    public CursorStyleOptions() { }
}

/// <summary>
/// Line info arrays describing how a text buffer's lines are laid out.
/// </summary>
public sealed class LineInfo
{
    public uint[] LineStartCols { get; init; } = [];
    public uint[] LineWidthCols { get; init; } = [];
    public uint LineWidthColsMax { get; init; }
    public uint[] LineSources { get; init; } = [];
    public uint[] LineWraps { get; init; } = [];
}

/// <summary>A text highlight range with a syntax style and priority.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct Highlight
{
    public uint Start;
    public uint End;
    public uint StyleId;
    public byte Priority;
    public ushort HlRef;
}

/// <summary>
/// A captured span from rendered output, used for testing.
/// </summary>
public readonly record struct CapturedSpan(string Text, Rgba Fg, Rgba Bg, uint Attributes, int Width);

/// <summary>A captured line of rendered output.</summary>
public sealed class CapturedLine
{
    public required CapturedSpan[] Spans { get; init; }
}

/// <summary>A captured rendered frame.</summary>
public sealed class CapturedFrame
{
    public required int Cols { get; init; }
    public required int Rows { get; init; }
    public required (int X, int Y) Cursor { get; init; }
    public required CapturedLine[] Lines { get; init; }
}

/// <summary>
/// Encoded character result from encodeUnicode.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct EncodedChar(byte Width, uint Char);

/// <summary>
/// Terminal capabilities detected from the terminal.
/// </summary>
public sealed class TerminalCapabilities
{
    public bool KittyKeyboard { get; init; }
    public bool KittyGraphics { get; init; }
    public bool Rgb { get; init; }
    public WidthMethod Unicode { get; init; }
    public bool SgrPixels { get; init; }
    public bool ColorSchemeUpdates { get; init; }
    public bool ExplicitWidth { get; init; }
    public bool ScaledText { get; init; }
    public bool Sixel { get; init; }
    public bool FocusTracking { get; init; }
    public bool Sync { get; init; }
    public bool BracketedPaste { get; init; }
    public bool Hyperlinks { get; init; }
    public bool Osc52 { get; init; }
    public bool ExplicitCursorPositioning { get; init; }
    public string TermName { get; init; } = "";
    public string TermVersion { get; init; } = "";
    public bool TermFromXtversion { get; init; }
}

/// <summary>Build options reported by the native library.</summary>
public readonly record struct BuildOptions(bool GpaSafeStats, bool GpaMemoryLimitTracking);

/// <summary>Allocator statistics from the native library.</summary>
public readonly record struct AllocatorStats(
    ulong TotalRequestedBytes,
    ulong ActiveAllocations,
    ulong SmallAllocations,
    ulong LargeAllocations,
    bool RequestedBytesValid);

/// <summary>Growth policy for native span feeds.</summary>
public enum GrowthPolicy : byte
{
    Grow = 0,
    Block = 1,
}

/// <summary>Options for creating a native span feed.</summary>
public sealed class SpanFeedOptions
{
    public uint ChunkSize { get; init; } = 64 * 1024;
    public uint InitialChunks { get; init; } = 2;
    public ulong MaxBytes { get; init; }
    public GrowthPolicy GrowthPolicy { get; init; } = GrowthPolicy.Grow;
    public bool AutoCommitOnFull { get; init; } = true;
    public uint SpanQueueCapacity { get; init; }
}

/// <summary>Statistics from a native span feed.</summary>
public readonly record struct SpanFeedStats(ulong BytesWritten, ulong SpansCommitted, uint Chunks, uint PendingSpans);

/// <summary>Information about a selected option in a Select widget.</summary>
public sealed class SelectOption<T>
{
    public required string Label { get; init; }
    public required T Value { get; init; }
    public string? Description { get; init; }
    public bool Disabled { get; init; }
}
