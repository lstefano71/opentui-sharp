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
/// Current state of the terminal cursor, as reported by the native renderer after a frame.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CursorState
{
    /// <summary>
    /// Stores the x.
    /// </summary>
    public uint X;
    /// <summary>
    /// Stores the y.
    /// </summary>
    public uint Y;
    /// <summary>
    /// Stores the visible.
    /// </summary>
    [MarshalAs(UnmanagedType.U1)] public bool Visible;
    /// <summary>
    /// Stores the style.
    /// </summary>
    public byte Style;
    /// <summary>
    /// Stores the blinking.
    /// </summary>
    [MarshalAs(UnmanagedType.U1)] public bool Blinking;
    /// <summary>
    /// Stores the r.
    /// </summary>
    public float R;
    /// <summary>
    /// Stores the g.
    /// </summary>
    public float G;
    /// <summary>
    /// Stores the b.
    /// </summary>
    public float B;
    /// <summary>
    /// Stores the a.
    /// </summary>
    public float A;

    /// <summary>Gets the current cursor style.</summary>
    public readonly CursorStyle CursorStyle => (CursorStyle)Style;
    /// <summary>Gets the current cursor color.</summary>
    public readonly Rgba Color => new(R, G, B, A);
}

/// <summary>
/// Options for configuring cursor appearance.
/// Sentinel value 255 means "don't change this property."
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct CursorStyleOptions
{
    /// <summary>
    /// Stores the style.
    /// </summary>
    public byte Style = 255;
    /// <summary>
    /// Stores the blinking.
    /// </summary>
    public byte Blinking = 255;
    /// <summary>
    /// Stores the color ptr.
    /// </summary>
    public nint ColorPtr;
    /// <summary>
    /// Stores the cursor.
    /// </summary>
    public byte Cursor = 255;

    /// <summary>
    /// Initializes a new instance of the CursorStyleOptions class.
    /// </summary>
    public CursorStyleOptions() { }
}

/// <summary>
/// Line-layout metadata returned by text-buffer and editor-view APIs, including wrapped line starts,
/// widths, sources, and wrap markers.
/// </summary>
public sealed class LineInfo
{
    /// <summary>
    /// Gets or sets the line start cols.
    /// </summary>
    public uint[] LineStartCols { get; init; } = [];
    /// <summary>
    /// Gets or sets the line width cols.
    /// </summary>
    public uint[] LineWidthCols { get; init; } = [];
    /// <summary>
    /// Gets or sets the line width cols max.
    /// </summary>
    public uint LineWidthColsMax { get; init; }
    /// <summary>
    /// Gets or sets the line sources.
    /// </summary>
    public uint[] LineSources { get; init; } = [];
    /// <summary>
    /// Gets or sets the line wraps.
    /// </summary>
    public uint[] LineWraps { get; init; } = [];
}

/// <summary>A text highlight range with a syntax style and priority.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct Highlight
{
    /// <summary>
    /// Stores the start.
    /// </summary>
    public uint Start;
    /// <summary>
    /// Stores the end.
    /// </summary>
    public uint End;
    /// <summary>
    /// Stores the style id.
    /// </summary>
    public uint StyleId;
    /// <summary>
    /// Stores the priority.
    /// </summary>
    public byte Priority;
    /// <summary>
    /// Stores the hl ref.
    /// </summary>
    public ushort HlRef;
}

/// <summary>
/// A captured span from rendered output, used for testing.
/// </summary>
public readonly record struct CapturedSpan(string Text, Rgba Fg, Rgba Bg, uint Attributes, int Width);

/// <summary>A captured line of rendered output.</summary>
public sealed class CapturedLine
{
    /// <summary>
    /// Gets or sets the spans.
    /// </summary>
    public required CapturedSpan[] Spans { get; init; }
}

/// <summary>A captured rendered frame.</summary>
public sealed class CapturedFrame
{
    /// <summary>
    /// Gets or sets the cols.
    /// </summary>
    public required int Cols { get; init; }
    /// <summary>
    /// Gets or sets the rows.
    /// </summary>
    public required int Rows { get; init; }
    /// <summary>
    /// Gets or sets the cursor.
    /// </summary>
    public required (int X, int Y) Cursor { get; init; }
    /// <summary>
    /// Gets or sets the lines.
    /// </summary>
    public required CapturedLine[] Lines { get; init; }
}

/// <summary>
/// Encoded character result from encodeUnicode.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct EncodedChar(byte Width, uint Char);

/// <summary>
/// Terminal feature detection result returned by renderer capability queries.
/// </summary>
public sealed class TerminalCapabilities
{
    /// <summary>
    /// Gets or sets the kitty keyboard.
    /// </summary>
    public bool KittyKeyboard { get; init; }
    /// <summary>
    /// Gets or sets the kitty graphics.
    /// </summary>
    public bool KittyGraphics { get; init; }
    /// <summary>
    /// Gets or sets the rgb.
    /// </summary>
    public bool Rgb { get; init; }
    /// <summary>
    /// Gets or sets the unicode.
    /// </summary>
    public WidthMethod Unicode { get; init; }
    /// <summary>
    /// Gets or sets the sgr pixels.
    /// </summary>
    public bool SgrPixels { get; init; }
    /// <summary>
    /// Gets or sets the color scheme updates.
    /// </summary>
    public bool ColorSchemeUpdates { get; init; }
    /// <summary>
    /// Gets or sets the explicit width.
    /// </summary>
    public bool ExplicitWidth { get; init; }
    /// <summary>
    /// Gets or sets the scaled text.
    /// </summary>
    public bool ScaledText { get; init; }
    /// <summary>
    /// Gets or sets the sixel.
    /// </summary>
    public bool Sixel { get; init; }
    /// <summary>
    /// Gets or sets the focus tracking.
    /// </summary>
    public bool FocusTracking { get; init; }
    /// <summary>
    /// Gets or sets the sync.
    /// </summary>
    public bool Sync { get; init; }
    /// <summary>
    /// Gets or sets the bracketed paste.
    /// </summary>
    public bool BracketedPaste { get; init; }
    /// <summary>
    /// Gets or sets the hyperlinks.
    /// </summary>
    public bool Hyperlinks { get; init; }
    /// <summary>
    /// Gets or sets the osc 52.
    /// </summary>
    public bool Osc52 { get; init; }
    /// <summary>
    /// Gets or sets the explicit cursor positioning.
    /// </summary>
    public bool ExplicitCursorPositioning { get; init; }
    /// <summary>
    /// Gets or sets the term name.
    /// </summary>
    public string TermName { get; init; } = "";
    /// <summary>
    /// Gets or sets the term version.
    /// </summary>
    public string TermVersion { get; init; } = "";
    /// <summary>
    /// Gets or sets the term from xtversion.
    /// </summary>
    public bool TermFromXtversion { get; init; }
}

/// <summary>Options used when querying terminal colors through OSC palette requests.</summary>
public sealed class GetPaletteOptions
{
    /// <summary>
    /// Gets or sets the timeout.
    /// </summary>
    public int Timeout { get; init; } = 1200;
    /// <summary>
    /// Gets or sets the size.
    /// </summary>
    public int Size { get; init; } = 16;
}

/// <summary>
/// Terminal palette colors and special color slots returned by OSC palette queries.
/// </summary>
public sealed class TerminalColors
{
    /// <summary>
    /// Gets or sets the palette.
    /// </summary>
    public string?[] Palette { get; init; } = [];
    /// <summary>
    /// Gets or sets the default foreground.
    /// </summary>
    public string? DefaultForeground { get; init; }
    /// <summary>
    /// Gets or sets the default background.
    /// </summary>
    public string? DefaultBackground { get; init; }
    /// <summary>
    /// Gets or sets the cursor color.
    /// </summary>
    public string? CursorColor { get; init; }
    /// <summary>
    /// Gets or sets the mouse foreground.
    /// </summary>
    public string? MouseForeground { get; init; }
    /// <summary>
    /// Gets or sets the mouse background.
    /// </summary>
    public string? MouseBackground { get; init; }
    /// <summary>
    /// Gets or sets the tek foreground.
    /// </summary>
    public string? TekForeground { get; init; }
    /// <summary>
    /// Gets or sets the tek background.
    /// </summary>
    public string? TekBackground { get; init; }
    /// <summary>
    /// Gets or sets the highlight background.
    /// </summary>
    public string? HighlightBackground { get; init; }
    /// <summary>
    /// Gets or sets the highlight foreground.
    /// </summary>
    public string? HighlightForeground { get; init; }
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
    /// <summary>
    /// Represents the Grow option.
    /// </summary>
    Grow = 0,
    /// <summary>
    /// Represents the Block option.
    /// </summary>
    Block = 1,
}

/// <summary>Options for creating a native span feed.</summary>
public sealed class SpanFeedOptions
{
    /// <summary>
    /// Gets or sets the chunk size.
    /// </summary>
    public uint ChunkSize { get; init; } = 64 * 1024;
    /// <summary>
    /// Gets or sets the initial chunks.
    /// </summary>
    public uint InitialChunks { get; init; } = 2;
    /// <summary>
    /// Gets or sets the max bytes.
    /// </summary>
    public ulong MaxBytes { get; init; }
    /// <summary>
    /// Gets or sets the growth policy.
    /// </summary>
    public GrowthPolicy GrowthPolicy { get; init; } = GrowthPolicy.Grow;
    /// <summary>
    /// Gets or sets the auto commit on full.
    /// </summary>
    public bool AutoCommitOnFull { get; init; } = true;
    /// <summary>
    /// Gets or sets the span queue capacity.
    /// </summary>
    public uint SpanQueueCapacity { get; init; }
}

/// <summary>Statistics from a native span feed.</summary>
public readonly record struct SpanFeedStats(ulong BytesWritten, ulong SpansCommitted, uint Chunks, uint PendingSpans);

/// <summary>Information about a selected option in a Select widget.</summary>
public sealed class SelectOption<T>
{
    /// <summary>
    /// Gets or sets the label.
    /// </summary>
    public required string Label { get; init; }
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public required T Value { get; init; }
    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string? Description { get; init; }
    /// <summary>
    /// Gets or sets the disabled.
    /// </summary>
    public bool Disabled { get; init; }
}
