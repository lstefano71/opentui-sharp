using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using OpenTui.Core.Managed.DataStructures;
using OpenTui.Core.Managed.Unicode;

namespace OpenTui.Core.Managed;

// ─────────────────────────────────────────────────────────────────────────────
// Supporting types
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>A segment of text in the rope. Each segment references bytes in a memory registry.</summary>
public readonly struct Segment : IRopeWeightable, IRopeEmpty<Segment>, IEquatable<Segment>
{
    /// <summary>Memory registry ID containing the bytes.</summary>
    public readonly byte MemId;

    /// <summary>Start offset in the memory buffer.</summary>
    public readonly uint ByteStart;

    /// <summary>End offset (exclusive) in the memory buffer.</summary>
    public readonly uint ByteEnd;

    /// <summary>Display width in columns.</summary>
    public readonly ushort Width;

    /// <summary>Flags. <c>ASCII_ONLY = 0x01</c>.</summary>
    public readonly byte Flags;

    /// <summary>Whether this segment represents a line break.</summary>
    public readonly bool IsNewline;

    private const byte AsciiOnlyFlag = 0x01;

    public Segment(byte memId, uint byteStart, uint byteEnd, ushort width, byte flags, bool isNewline)
    {
        MemId = memId;
        ByteStart = byteStart;
        ByteEnd = byteEnd;
        Width = width;
        Flags = flags;
        IsNewline = isNewline;
    }

    /// <summary>IRopeWeightable — weight is display width.</summary>
    public uint Weight => Width;

    /// <summary>IRopeEmpty sentinel.</summary>
    public static Segment Empty() => default;

    public bool IsEmpty => ByteStart == ByteEnd && !IsNewline;
    public bool IsAsciiOnly => (Flags & AsciiOnlyFlag) != 0;

    /// <summary>Creates a newline segment (zero-width, not referencing any bytes).</summary>
    public static Segment NewLine() => new(0, 0, 0, 0, 0, isNewline: true);

    public bool Equals(Segment other) =>
        MemId == other.MemId && ByteStart == other.ByteStart && ByteEnd == other.ByteEnd &&
        Width == other.Width && Flags == other.Flags && IsNewline == other.IsNewline;

    public override bool Equals(object? obj) => obj is Segment s && Equals(s);
    public override int GetHashCode() => HashCode.Combine(MemId, ByteStart, ByteEnd, Width, Flags, IsNewline);
}

/// <summary>Information about a line produced during a walk.</summary>
public readonly record struct ManagedLineInfo(uint LineIndex, uint ByteOffset, uint Width, uint SegmentCount);

/// <summary>A style span within a line (from syntax highlighting).</summary>
public readonly record struct StyleSpan(uint Start, uint End, uint StyleId);

/// <summary>A styled text chunk for <see cref="ManagedTextBuffer.SetStyledText"/>.</summary>
public readonly record struct StyledChunk(
    ReadOnlyMemory<byte> Text,
    Rgba? Fg,
    Rgba? Bg,
    uint Attributes,
    string? LinkUrl);

// ─────────────────────────────────────────────────────────────────────────────
// ManagedTextBuffer
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Pure C# reimplementation of the Zig text buffer (<c>text-buffer.zig</c> +
/// <c>text-buffer-segment.zig</c>).
/// <para>
/// Stores text as a <see cref="Rope{T}"/> of <see cref="Segment"/> values where
/// each segment references bytes in a <see cref="ManagedMemRegistry"/>. Lines are
/// separated by newline sentinel segments.
/// </para>
/// </summary>
public sealed class ManagedTextBuffer : IDisposable
{
    // ── Static initialization ────────────────────────────────────────
    private static bool _ropeEmptyRegistered;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EnsureRopeEmptyRegistered()
    {
        if (_ropeEmptyRegistered) return;
        RopeEmpty<Segment>.Register();
        _ropeEmptyRegistered = true;
    }

    // ── Core state ───────────────────────────────────────────────────
    private Rope<Segment> _rope;
    private readonly ManagedMemRegistry _memRegistry;
    private readonly ManagedGraphemePool _graphemePool;
    private readonly ManagedLinkPool _linkPool;
    private ManagedSyntaxStyle? _syntaxStyle;
    private WidthMethod _widthMethod;
    private byte _tabWidth = 2;
    private bool _disposed;

    // ── View dirty tracking ──────────────────────────────────────────
    private readonly List<bool> _viewDirtyFlags = [];
    private readonly List<uint> _freeViewIds = [];
    private uint _nextViewId;
    private ulong _contentEpoch;

    // ── Default styling ──────────────────────────────────────────────
    public Rgba? DefaultFg { get; set; }
    public Rgba? DefaultBg { get; set; }
    public uint? DefaultAttributes { get; set; }

    // ── Highlight / style span caches (per-line) ─────────────────────
    private readonly List<List<Highlight>?> _lineHighlights = [];
    private readonly List<List<StyleSpan>?> _lineSpans = [];
    private uint _highlightBatchDepth;
    private readonly HashSet<int> _dirtySpanLines = [];

    // ── Construction ─────────────────────────────────────────────────

    private ManagedTextBuffer(WidthMethod widthMethod)
    {
        _widthMethod = widthMethod;
        _memRegistry = new ManagedMemRegistry();
        _graphemePool = new ManagedGraphemePool();
        _linkPool = new ManagedLinkPool();
        _rope = Rope<Segment>.Create();
    }

    /// <summary>Creates a new managed text buffer.</summary>
    public static ManagedTextBuffer Create(WidthMethod widthMethod = WidthMethod.Unicode)
    {
        EnsureRopeEmptyRegistered();
        return new ManagedTextBuffer(widthMethod);
    }

    // ── IDisposable ──────────────────────────────────────────────────

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _rope.Clear();
        _memRegistry.Clear();
        _syntaxStyle?.Dispose();
        ClearHighlights();
    }

    // ── Properties ───────────────────────────────────────────────────

    /// <summary>Total display width in columns (sum of all segment weights).</summary>
    public uint Length
    {
        get
        {
            ThrowIfDisposed();
            return _rope.TotalWeight;
        }
    }

    /// <summary>Total bytes across all segments (newline segments count as 1 byte each).</summary>
    public uint ByteSize
    {
        get
        {
            ThrowIfDisposed();
            uint total = 0;
            _rope.Walk((seg, _) =>
            {
                total += seg.IsNewline ? 1u : (seg.ByteEnd - seg.ByteStart);
                return true;
            });
            return total;
        }
    }

    /// <summary>Number of lines. Always ≥ 1 even if the buffer is empty.</summary>
    public uint LineCount
    {
        get
        {
            ThrowIfDisposed();
            uint newlines = 0;
            _rope.Walk((seg, _) =>
            {
                if (seg.IsNewline) newlines++;
                return true;
            });
            return newlines + 1;
        }
    }

    /// <summary>Width of a specific line in columns.</summary>
    public uint LineWidthAt(uint row)
    {
        ThrowIfDisposed();
        uint currentLine = 0;
        uint lineWidth = 0;
        _rope.Walk((seg, _) =>
        {
            if (seg.IsNewline)
            {
                if (currentLine == row) return false; // done
                currentLine++;
                lineWidth = 0;
                return true;
            }
            if (currentLine == row)
                lineWidth += seg.Width;
            return currentLine <= row;
        });
        return lineWidth;
    }

    /// <summary>Maximum line width across all lines.</summary>
    public uint MaxLineWidth
    {
        get
        {
            ThrowIfDisposed();
            uint maxWidth = 0;
            uint currentWidth = 0;
            _rope.Walk((seg, _) =>
            {
                if (seg.IsNewline)
                {
                    if (currentWidth > maxWidth) maxWidth = currentWidth;
                    currentWidth = 0;
                }
                else
                {
                    currentWidth += seg.Width;
                }
                return true;
            });
            if (currentWidth > maxWidth) maxWidth = currentWidth;
            return maxWidth;
        }
    }

    /// <summary>Measure display width of arbitrary UTF-8 text using this buffer's width method.</summary>
    public uint MeasureText(ReadOnlySpan<byte> utf8Text)
    {
        ThrowIfDisposed();
        if (utf8Text.IsEmpty) return 0;
        bool ascii = TextWidth.IsAsciiOnly(utf8Text);
        return TextWidth.CalculateTextWidth(utf8Text, _tabWidth, ascii, _widthMethod);
    }

    /// <summary>Monotonic change counter. Incremented on every mutation.</summary>
    public ulong ContentEpoch
    {
        get
        {
            ThrowIfDisposed();
            return _contentEpoch;
        }
    }

    /// <summary>Gets or sets the tab display width.</summary>
    public byte TabWidth
    {
        get => _tabWidth;
        set
        {
            ThrowIfDisposed();
            _tabWidth = value;
        }
    }

    /// <summary>Width calculation method in use.</summary>
    public WidthMethod WidthMethod
    {
        get
        {
            ThrowIfDisposed();
            return _widthMethod;
        }
    }

    /// <summary>Access to the underlying memory registry.</summary>
    public ManagedMemRegistry MemRegistry
    {
        get
        {
            ThrowIfDisposed();
            return _memRegistry;
        }
    }

    /// <summary>Sets or clears the syntax style used for style span resolution.</summary>
    public ManagedSyntaxStyle? SyntaxStyle
    {
        get => _syntaxStyle;
        set
        {
            ThrowIfDisposed();
            _syntaxStyle = value;
        }
    }

    // ── Text content ─────────────────────────────────────────────────

    /// <summary>Sets the text buffer content, replacing any existing content.</summary>
    public void SetText(ReadOnlySpan<byte> utf8Text)
    {
        ThrowIfDisposed();
        ClearInternal();
        if (utf8Text.IsEmpty) return;
        AppendInternal(utf8Text);
    }

    /// <summary>Sets the text buffer content from a string.</summary>
    public void SetText(string text)
    {
        ThrowIfDisposed();
        ClearInternal();
        if (string.IsNullOrEmpty(text)) return;

        int maxBytes = Encoding.UTF8.GetMaxByteCount(text.Length);
        byte[]? rented = null;
        Span<byte> buf = maxBytes <= 4096
            ? stackalloc byte[maxBytes]
            : (rented = System.Buffers.ArrayPool<byte>.Shared.Rent(maxBytes));
        try
        {
            int written = Encoding.UTF8.GetBytes(text, buf);
            AppendInternal(buf[..written]);
        }
        finally
        {
            if (rented is not null)
                System.Buffers.ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>Appends UTF-8 text to the end of the buffer.</summary>
    public void Append(ReadOnlySpan<byte> utf8Text)
    {
        ThrowIfDisposed();
        if (utf8Text.IsEmpty) return;
        AppendInternal(utf8Text);
    }

    /// <summary>Appends text to the end of the buffer.</summary>
    public void Append(string text)
    {
        ThrowIfDisposed();
        if (string.IsNullOrEmpty(text)) return;

        int maxBytes = Encoding.UTF8.GetMaxByteCount(text.Length);
        byte[]? rented = null;
        Span<byte> buf = maxBytes <= 4096
            ? stackalloc byte[maxBytes]
            : (rented = System.Buffers.ArrayPool<byte>.Shared.Rent(maxBytes));
        try
        {
            int written = Encoding.UTF8.GetBytes(text, buf);
            AppendInternal(buf[..written]);
        }
        finally
        {
            if (rented is not null)
                System.Buffers.ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// Sets styled text content. Each chunk can have different fg/bg/attrs.
    /// Text geometry is stored in the segments; per-chunk styling is stored in the highlight system.
    /// </summary>
    public void SetStyledText(ReadOnlySpan<StyledChunk> chunks)
    {
        ThrowIfDisposed();
        ClearInternal();
        if (chunks.IsEmpty) return;

        var segments = new List<Segment>();
        uint lineIndex = 0;
        uint colOffset = 0;

        for (int ci = 0; ci < chunks.Length; ci++)
        {
            ref readonly StyledChunk chunk = ref chunks[ci];
            ReadOnlySpan<byte> text = chunk.Text.Span;
            if (text.IsEmpty) continue;

            // Register bytes into memory registry
            byte[] copy = text.ToArray();
            ushort memId = _memRegistry.Register(copy);

            // Resolve link
            uint linkId = 0;
            if (chunk.LinkUrl is not null)
                linkId = _linkPool.Alloc(chunk.LinkUrl);

            // Build segments for this chunk
            int pos = 0;
            while (pos < text.Length)
            {
                int lineEnd = FindLineBreak(text, pos);
                if (lineEnd > pos)
                {
                    ReadOnlySpan<byte> lineSlice = text[pos..lineEnd];
                    bool ascii = TextWidth.IsAsciiOnly(lineSlice);
                    ushort w = (ushort)TextWidth.CalculateTextWidth(lineSlice, _tabWidth, ascii, _widthMethod);
                    byte flags = ascii ? (byte)0x01 : (byte)0;
                    segments.Add(new Segment((byte)memId, (uint)pos, (uint)lineEnd, w, flags, false));

                    // Record highlight for this segment's range
                    AddChunkHighlight(lineIndex, colOffset, colOffset + w, chunk.Fg, chunk.Bg, chunk.Attributes, linkId);
                    colOffset += w;
                }

                if (lineEnd < text.Length)
                {
                    int nlBytes = text[lineEnd] == (byte)'\r' && lineEnd + 1 < text.Length && text[lineEnd + 1] == (byte)'\n' ? 2 : 1;
                    segments.Add(Segment.NewLine());
                    lineIndex++;
                    colOffset = 0;
                    pos = lineEnd + nlBytes;
                }
                else
                {
                    pos = lineEnd;
                }
            }
        }

        _rope.SetSegments(segments.ToArray());
        _contentEpoch++;
        MarkViewsDirty();
    }

    /// <summary>Full reset — clears everything including memory registry.</summary>
    public void Reset()
    {
        ThrowIfDisposed();
        _rope.Clear();
        _memRegistry.Clear();
        ClearHighlights();
        ClearStyleSpans();
        _contentEpoch++;
        MarkViewsDirty();
    }

    /// <summary>Clear text content but preserve registry/arena.</summary>
    public void Clear()
    {
        ThrowIfDisposed();
        ClearInternal();
    }

    // ── View management ──────────────────────────────────────────────

    /// <summary>Registers a new view and returns its ID.</summary>
    public uint RegisterView()
    {
        ThrowIfDisposed();
        if (_freeViewIds.Count > 0)
        {
            uint recycled = _freeViewIds[^1];
            _freeViewIds.RemoveAt(_freeViewIds.Count - 1);
            if (recycled < (uint)_viewDirtyFlags.Count)
                _viewDirtyFlags[(int)recycled] = true;
            return recycled;
        }

        uint id = _nextViewId++;
        _viewDirtyFlags.Add(true);
        return id;
    }

    /// <summary>Unregisters a view, allowing its ID to be recycled.</summary>
    public void UnregisterView(uint viewId)
    {
        ThrowIfDisposed();
        if (viewId >= (uint)_viewDirtyFlags.Count) return;
        _viewDirtyFlags[(int)viewId] = false;
        _freeViewIds.Add(viewId);
    }

    /// <summary>Whether the given view has been dirtied since it was last cleared.</summary>
    public bool IsViewDirty(uint viewId)
    {
        ThrowIfDisposed();
        return viewId < (uint)_viewDirtyFlags.Count && _viewDirtyFlags[(int)viewId];
    }

    /// <summary>Clears the dirty flag for the given view.</summary>
    public void ClearViewDirty(uint viewId)
    {
        ThrowIfDisposed();
        if (viewId < (uint)_viewDirtyFlags.Count)
            _viewDirtyFlags[(int)viewId] = false;
    }

    /// <summary>Marks all registered views as dirty.</summary>
    public void MarkViewsDirty()
    {
        for (int i = 0; i < _viewDirtyFlags.Count; i++)
            _viewDirtyFlags[i] = true;
    }

    // ── Highlights ───────────────────────────────────────────────────

    /// <summary>Sets the highlights for a specific line, replacing any existing ones.</summary>
    public void SetHighlights(uint lineIdx, ReadOnlySpan<Highlight> highlights)
    {
        ThrowIfDisposed();
        EnsureHighlightCapacity(lineIdx);

        var list = _lineHighlights[(int)lineIdx];
        if (list is null)
        {
            list = new List<Highlight>(highlights.Length);
            _lineHighlights[(int)lineIdx] = list;
        }
        else
        {
            list.Clear();
        }

        for (int i = 0; i < highlights.Length; i++)
            list.Add(highlights[i]);

        if (_highlightBatchDepth == 0)
            MarkViewsDirty();
    }

    /// <summary>Adds a single highlight to a specific line, appending to existing highlights.</summary>
    public void AddHighlight(uint lineIdx, Highlight highlight)
    {
        ThrowIfDisposed();
        EnsureHighlightCapacity(lineIdx);

        var list = _lineHighlights[(int)lineIdx];
        if (list is null)
        {
            list = new List<Highlight>();
            _lineHighlights[(int)lineIdx] = list;
        }

        list.Add(highlight);

        if (_highlightBatchDepth == 0)
            MarkViewsDirty();
    }

    /// <summary>
    /// Adds a highlight by character range. The highlight's <see cref="Highlight.Start"/> and
    /// <see cref="Highlight.End"/> are character offsets in the entire buffer. The method converts
    /// these to line-relative highlights on each affected line.
    /// </summary>
    public void AddHighlightByCharRange(Highlight highlight)
    {
        ThrowIfDisposed();
        uint startOffset = highlight.Start;
        uint endOffset = highlight.End;
        if (startOffset >= endOffset) return;

        uint lineCount = LineCount;
        uint charPos = 0;

        for (uint line = 0; line < lineCount; line++)
        {
            uint lineLen = GetLineLength(line);
            uint lineStart = charPos;
            uint lineEnd = charPos + lineLen;

            if (startOffset < lineEnd + 1 && endOffset > lineStart)
            {
                uint hlStart = startOffset > lineStart ? startOffset - lineStart : 0;
                uint hlEnd = endOffset < lineEnd ? endOffset - lineStart : lineLen;

                if (hlStart < hlEnd)
                {
                    var lineHl = new Highlight
                    {
                        Start = hlStart,
                        End = hlEnd,
                        StyleId = highlight.StyleId,
                        Priority = highlight.Priority,
                        HlRef = highlight.HlRef,
                    };
                    AddHighlight(line, lineHl);
                }
            }

            charPos = lineEnd + 1; // +1 for newline
            if (charPos > endOffset) break;
        }
    }

    /// <summary>Removes all highlights matching the given reference ID from all lines.</summary>
    public void RemoveHighlight(ushort hlRef)
    {
        ThrowIfDisposed();
        bool changed = false;
        for (int i = 0; i < _lineHighlights.Count; i++)
        {
            var list = _lineHighlights[i];
            if (list is null || list.Count == 0) continue;

            for (int j = list.Count - 1; j >= 0; j--)
            {
                if (list[j].HlRef == hlRef)
                {
                    list.RemoveAt(j);
                    changed = true;
                }
            }
        }

        if (changed && _highlightBatchDepth == 0)
            MarkViewsDirty();
    }

    /// <summary>Clears all highlights for a specific line.</summary>
    public void ClearLineHighlights(uint lineIdx)
    {
        ThrowIfDisposed();
        if (lineIdx >= (uint)_lineHighlights.Count) return;
        var list = _lineHighlights[(int)lineIdx];
        if (list is null || list.Count == 0) return;
        list.Clear();

        if (_highlightBatchDepth == 0)
            MarkViewsDirty();
    }

    /// <summary>Gets the highlights for a specific line as an array.</summary>
    public Highlight[] GetLineHighlights(uint lineIdx)
    {
        ThrowIfDisposed();
        if (lineIdx >= (uint)_lineHighlights.Count) return [];
        var list = _lineHighlights[(int)lineIdx];
        if (list is null || list.Count == 0) return [];
        return list.ToArray();
    }

    /// <summary>Gets the total number of highlights across all lines.</summary>
    public uint HighlightCount
    {
        get
        {
            ThrowIfDisposed();
            uint count = 0;
            for (int i = 0; i < _lineHighlights.Count; i++)
            {
                var list = _lineHighlights[i];
                if (list is not null) count += (uint)list.Count;
            }
            return count;
        }
    }

    /// <summary>Gets the highlights for a specific line.</summary>
    public ReadOnlySpan<Highlight> GetHighlights(uint lineIdx)
    {
        ThrowIfDisposed();
        if (lineIdx >= (uint)_lineHighlights.Count) return [];
        var list = _lineHighlights[(int)lineIdx];
        if (list is null || list.Count == 0) return [];
        return System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list);
    }

    /// <summary>Clears all highlights from all lines.</summary>
    public void ClearHighlights()
    {
        for (int i = 0; i < _lineHighlights.Count; i++)
            _lineHighlights[i]?.Clear();
        _lineHighlights.Clear();
    }

    /// <summary>
    /// Begin a highlight batch. While active, view dirty notifications are deferred.
    /// </summary>
    public void BeginHighlightBatch()
    {
        ThrowIfDisposed();
        _highlightBatchDepth++;
    }

    /// <summary>
    /// End a highlight batch. If this was the outermost batch, marks all views dirty.
    /// </summary>
    public void EndHighlightBatch()
    {
        ThrowIfDisposed();
        if (_highlightBatchDepth == 0) return;
        _highlightBatchDepth--;
        if (_highlightBatchDepth == 0)
        {
            if (_dirtySpanLines.Count > 0)
                _dirtySpanLines.Clear();
            MarkViewsDirty();
        }
    }

    // ── Style spans ──────────────────────────────────────────────────

    /// <summary>Sets the style spans for a specific line.</summary>
    public void SetStyleSpans(uint lineIdx, ReadOnlySpan<StyleSpan> spans)
    {
        ThrowIfDisposed();
        EnsureStyleSpanCapacity(lineIdx);

        var list = _lineSpans[(int)lineIdx];
        if (list is null)
        {
            list = new List<StyleSpan>(spans.Length);
            _lineSpans[(int)lineIdx] = list;
        }
        else
        {
            list.Clear();
        }

        for (int i = 0; i < spans.Length; i++)
            list.Add(spans[i]);

        if (_highlightBatchDepth > 0)
            _dirtySpanLines.Add((int)lineIdx);
        else
            MarkViewsDirty();
    }

    /// <summary>Gets the style spans for a specific line.</summary>
    public ReadOnlySpan<StyleSpan> GetStyleSpans(uint lineIdx)
    {
        ThrowIfDisposed();
        if (lineIdx >= (uint)_lineSpans.Count) return [];
        var list = _lineSpans[(int)lineIdx];
        if (list is null || list.Count == 0) return [];
        return System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list);
    }

    // ── Line / segment iteration ─────────────────────────────────────

    /// <summary>
    /// Walks all lines, calling <paramref name="lineCallback"/> for each line with its metadata.
    /// </summary>
    public void WalkLines(Action<uint, ManagedLineInfo> lineCallback)
    {
        ThrowIfDisposed();

        uint lineIndex = 0;
        uint byteOffset = 0;
        uint lineWidth = 0;
        uint segCount = 0;

        _rope.Walk((seg, _) =>
        {
            if (seg.IsNewline)
            {
                lineCallback(lineIndex, new ManagedLineInfo(lineIndex, byteOffset, lineWidth, segCount));
                byteOffset += 1; // newline byte
                lineIndex++;
                lineWidth = 0;
                segCount = 0;
            }
            else if (!seg.IsEmpty)
            {
                lineWidth += seg.Width;
                byteOffset += seg.ByteEnd - seg.ByteStart;
                segCount++;
            }
            return true;
        });

        // Emit the final line (always present even when empty)
        lineCallback(lineIndex, new ManagedLineInfo(lineIndex, byteOffset, lineWidth, segCount));
    }

    /// <summary>
    /// Walks all segments, calling <paramref name="segmentCallback"/> for each non-newline
    /// segment and <paramref name="lineEndCallback"/> at each line boundary.
    /// </summary>
    public void WalkLinesAndSegments(
        Action<uint, Segment, uint> segmentCallback,
        Action<ManagedLineInfo> lineEndCallback)
    {
        ThrowIfDisposed();

        uint lineIndex = 0;
        uint byteOffset = 0;
        uint lineWidth = 0;
        uint segCount = 0;
        uint colOffset = 0;

        _rope.Walk((seg, _) =>
        {
            if (seg.IsNewline)
            {
                lineEndCallback(new ManagedLineInfo(lineIndex, byteOffset, lineWidth, segCount));
                byteOffset += 1;
                lineIndex++;
                lineWidth = 0;
                segCount = 0;
                colOffset = 0;
            }
            else if (!seg.IsEmpty)
            {
                segmentCallback(lineIndex, seg, colOffset);
                lineWidth += seg.Width;
                colOffset += seg.Width;
                byteOffset += seg.ByteEnd - seg.ByteStart;
                segCount++;
            }
            return true;
        });

        // Emit the final line
        lineEndCallback(new ManagedLineInfo(lineIndex, byteOffset, lineWidth, segCount));
    }

    /// <summary>Gets the raw bytes referenced by a segment.</summary>
    public ReadOnlyMemory<byte> GetSegmentBytes(in Segment segment)
    {
        ThrowIfDisposed();
        if (segment.IsNewline || segment.IsEmpty)
            return ReadOnlyMemory<byte>.Empty;

        var buf = _memRegistry.Get(segment.MemId);
        if (buf is null) return ReadOnlyMemory<byte>.Empty;
        return buf.Value.Slice((int)segment.ByteStart, (int)(segment.ByteEnd - segment.ByteStart));
    }

    // ── Internal helpers ─────────────────────────────────────────────

    private void ClearInternal()
    {
        _rope.Clear();
        ClearHighlights();
        ClearStyleSpans();
        _contentEpoch++;
        MarkViewsDirty();
    }

    private void ClearStyleSpans()
    {
        for (int i = 0; i < _lineSpans.Count; i++)
            _lineSpans[i]?.Clear();
        _lineSpans.Clear();
        _dirtySpanLines.Clear();
    }

    /// <summary>Core append — parses UTF-8 text into segments and appends to the rope.</summary>
    private void AppendInternal(ReadOnlySpan<byte> utf8Text)
    {
        // Register the text block in the memory registry
        byte[] textCopy = utf8Text.ToArray();
        ushort memId = _memRegistry.Register(textCopy);

        var segments = TextToSegments(utf8Text, (byte)memId);
        if (segments.Length > 0)
        {
            // Append each segment to the rope
            _rope.SetSegments(ConcatExistingAndNew(segments));
        }

        _contentEpoch++;
        MarkViewsDirty();
    }

    /// <summary>Concatenates existing rope segments with new segments.</summary>
    private Segment[] ConcatExistingAndNew(ReadOnlySpan<Segment> newSegments)
    {
        uint existingCount = _rope.Count;
        if (existingCount == 0)
        {
            return newSegments.ToArray();
        }

        var existing = _rope.ToArray();
        var combined = new Segment[existing.Length + newSegments.Length];
        existing.CopyTo(combined, 0);
        newSegments.CopyTo(combined.AsSpan(existing.Length));
        return combined;
    }

    /// <summary>
    /// Core parsing logic: converts UTF-8 text into rope segments.
    /// Scans for line breaks (LF, CR, CRLF), creates text segments for each line
    /// and newline segments for each break.
    /// </summary>
    private Segment[] TextToSegments(ReadOnlySpan<byte> utf8Text, byte memId)
    {
        // Count line breaks to size the output
        var segments = new List<Segment>();
        int pos = 0;

        while (pos < utf8Text.Length)
        {
            int lineEnd = FindLineBreak(utf8Text, pos);

            if (lineEnd > pos)
            {
                // Text segment for this line fragment
                ReadOnlySpan<byte> lineSlice = utf8Text[pos..lineEnd];
                bool ascii = IsAsciiOnlyFast(lineSlice);
                ushort width = (ushort)TextWidth.CalculateTextWidth(lineSlice, _tabWidth, ascii, _widthMethod);
                byte flags = ascii ? (byte)0x01 : (byte)0;
                segments.Add(new Segment(memId, (uint)pos, (uint)lineEnd, width, flags, false));
            }

            if (lineEnd < utf8Text.Length)
            {
                byte b = utf8Text[lineEnd];
                if (b == (byte)'\r' && lineEnd + 1 < utf8Text.Length && utf8Text[lineEnd + 1] == (byte)'\n')
                {
                    // CRLF
                    segments.Add(Segment.NewLine());
                    pos = lineEnd + 2;
                }
                else
                {
                    // LF or CR alone
                    segments.Add(Segment.NewLine());
                    pos = lineEnd + 1;
                }
            }
            else
            {
                break;
            }
        }

        return segments.ToArray();
    }

    /// <summary>
    /// Fast scan for the next line break (\n or \r) starting at <paramref name="offset"/>.
    /// Uses SIMD (SSE2) 16-byte-at-a-time fast path when available.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FindLineBreak(ReadOnlySpan<byte> data, int offset)
    {
        int pos = offset;

        if (Sse2.IsSupported)
        {
            Vector128<byte> newlineVec = Vector128.Create((byte)'\n');
            Vector128<byte> crVec = Vector128.Create((byte)'\r');

            while (pos + Vector128<byte>.Count <= data.Length)
            {
                Vector128<byte> chunk = Vector128.Create(data.Slice(pos, Vector128<byte>.Count));
                Vector128<byte> cmpLf = Sse2.CompareEqual(chunk, newlineVec);
                Vector128<byte> cmpCr = Sse2.CompareEqual(chunk, crVec);
                int mask = Sse2.MoveMask(Sse2.Or(cmpLf, cmpCr));
                if (mask != 0)
                {
                    return pos + BitOperations.TrailingZeroCount((uint)mask);
                }
                pos += Vector128<byte>.Count;
            }
        }

        // Scalar tail
        for (int i = pos; i < data.Length; i++)
        {
            byte b = data[i];
            if (b == (byte)'\n' || b == (byte)'\r')
                return i;
        }

        return data.Length;
    }

    /// <summary>
    /// Fast ASCII-only check. Uses SIMD when available.
    /// Checks all bytes are in printable ASCII range [32, 126] or tab (9).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAsciiOnlyFast(ReadOnlySpan<byte> text)
    {
        return TextWidth.IsAsciiOnly(text);
    }

    /// <summary>
    /// Adds a highlight for a styled chunk range.
    /// </summary>
    private void AddChunkHighlight(uint lineIdx, uint start, uint end, Rgba? fg, Rgba? bg, uint attrs, uint linkId)
    {
        if (start == end && fg is null && bg is null && attrs == 0 && linkId == 0)
            return;

        // Combine attrs with link ID in upper bits
        uint combinedAttrs = attrs;
        if (linkId != 0)
            combinedAttrs = ManagedLinkPool.AttributesWithLink(attrs, linkId);

        // We need a style ID for the highlight. If we have a syntax style, register;
        // otherwise use a default style id of 0.
        uint styleId = 0;
        if (_syntaxStyle is not null && (fg is not null || bg is not null || attrs != 0))
        {
            string synName = $"_chunk_{lineIdx}_{start}";
            styleId = _syntaxStyle.Register(synName, fg, bg, (TextAttributes)(attrs & 0xFF));
        }

        EnsureHighlightCapacity(lineIdx);
        var list = _lineHighlights[(int)lineIdx];
        if (list is null)
        {
            list = new List<Highlight>();
            _lineHighlights[(int)lineIdx] = list;
        }

        list.Add(new Highlight
        {
            Start = start,
            End = end,
            StyleId = styleId,
            Priority = 0,
            HlRef = 0,
        });
    }

    private void EnsureHighlightCapacity(uint lineIdx)
    {
        while ((uint)_lineHighlights.Count <= lineIdx)
            _lineHighlights.Add(null);
    }

    private void EnsureStyleSpanCapacity(uint lineIdx)
    {
        while ((uint)_lineSpans.Count <= lineIdx)
            _lineSpans.Add(null);
    }

    // ── Text extraction helpers ─────────────────────────────────────

    /// <summary>Alias for <see cref="ContentEpoch"/>. Monotonically-increasing version counter.</summary>
    public ulong Version => ContentEpoch;

    /// <summary>Gets the full text content of the buffer as a string.</summary>
    public string GetText()
    {
        ThrowIfDisposed();
        var sb = new StringBuilder();
        _rope.Walk((seg, _) =>
        {
            if (seg.IsNewline)
            {
                sb.Append('\n');
            }
            else if (!seg.IsEmpty)
            {
                var bytes = GetSegmentBytes(in seg);
                if (!bytes.IsEmpty)
                    sb.Append(Encoding.UTF8.GetString(bytes.Span));
            }
            return true;
        });
        return sb.ToString();
    }

    /// <summary>Gets the text of a specific line (without the trailing newline).</summary>
    public string GetLineText(uint line)
    {
        ThrowIfDisposed();
        uint currentLine = 0;
        var sb = new StringBuilder();
        _rope.Walk((seg, _) =>
        {
            if (seg.IsNewline)
            {
                if (currentLine == line) return false; // done
                currentLine++;
                return true;
            }
            if (currentLine == line && !seg.IsEmpty)
            {
                var bytes = GetSegmentBytes(in seg);
                if (!bytes.IsEmpty)
                    sb.Append(Encoding.UTF8.GetString(bytes.Span));
            }
            return currentLine <= line;
        });
        return sb.ToString();
    }

    /// <summary>Gets the character length of a specific line (without newline).</summary>
    public uint GetLineLength(uint line)
    {
        ThrowIfDisposed();
        string lineText = GetLineText(line);
        return (uint)lineText.Length;
    }

    /// <summary>
    /// Gets the character offset one past the end of the text (total character positions).
    /// </summary>
    public uint GetTextEndOffset()
    {
        ThrowIfDisposed();
        uint lc = LineCount;
        if (lc == 0) return 0;
        uint lastLine = lc - 1;
        return GetOffset(lastLine, GetLineLength(lastLine));
    }

    /// <summary>Gets a range of text by line/column coordinates.</summary>
    public string GetTextRange(uint startLine, uint startCol, uint endLine, uint endCol)
    {
        ThrowIfDisposed();
        if (startLine > endLine || (startLine == endLine && startCol >= endCol))
            return string.Empty;

        var sb = new StringBuilder();
        uint currentLine = 0;
        uint currentCol = 0;

        _rope.Walk((seg, _) =>
        {
            if (currentLine > endLine) return false;

            if (seg.IsNewline)
            {
                if (currentLine >= startLine && currentLine < endLine)
                    sb.Append('\n');
                currentLine++;
                currentCol = 0;
                return currentLine <= endLine;
            }

            if (seg.IsEmpty) return true;

            if (currentLine >= startLine && currentLine <= endLine)
            {
                var bytes = GetSegmentBytes(in seg);
                if (!bytes.IsEmpty)
                {
                    string text = Encoding.UTF8.GetString(bytes.Span);
                    uint segEnd = currentCol + (uint)text.Length;

                    // Determine the portion of this segment that falls in our range
                    uint rangeStart = currentLine == startLine ? startCol : 0;
                    uint rangeEnd = currentLine == endLine ? endCol : segEnd;

                    if (segEnd > rangeStart && currentCol < rangeEnd)
                    {
                        int sliceStart = (int)Math.Max(rangeStart, currentCol) - (int)currentCol;
                        int sliceEnd = (int)Math.Min(rangeEnd, segEnd) - (int)currentCol;
                        if (sliceStart < sliceEnd && sliceStart < text.Length)
                        {
                            sb.Append(text, sliceStart, Math.Min(sliceEnd, text.Length) - sliceStart);
                        }
                    }

                    currentCol = segEnd;
                }
            }
            else
            {
                // Still need to track column for lines we're skipping
                if (!seg.IsEmpty)
                {
                    var bytes = GetSegmentBytes(in seg);
                    if (!bytes.IsEmpty)
                        currentCol += (uint)Encoding.UTF8.GetString(bytes.Span).Length;
                }
            }

            return true;
        });

        return sb.ToString();
    }

    /// <summary>
    /// Gets the character offset for a (line, col) position.
    /// The offset counts characters from the beginning of the buffer.
    /// </summary>
    public uint GetOffset(uint line, uint col)
    {
        ThrowIfDisposed();
        uint offset = 0;
        uint currentLine = 0;
        uint currentCol = 0;

        _rope.Walk((seg, _) =>
        {
            if (currentLine > line) return false;

            if (seg.IsNewline)
            {
                if (currentLine == line)
                {
                    // Don't add offset here — post-walk code handles it.
                    return false;
                }
                offset += currentCol + 1; // +1 for the newline character
                currentLine++;
                currentCol = 0;
                return true;
            }

            if (!seg.IsEmpty)
            {
                var bytes = GetSegmentBytes(in seg);
                if (!bytes.IsEmpty)
                {
                    uint charCount = (uint)Encoding.UTF8.GetString(bytes.Span).Length;
                    currentCol += charCount;
                }
            }
            return true;
        });

        // If we never hit a newline after the target line, we're on the last line
        if (currentLine == line)
            offset += Math.Min(col, currentCol);

        return offset;
    }

    /// <summary>
    /// Converts a character offset to a (line, col) position.
    /// Walks lines until the offset is consumed.
    /// </summary>
    public (uint Line, uint Col) OffsetToLineCol(uint offset)
    {
        ThrowIfDisposed();
        uint remaining = offset;
        uint lineCount = LineCount;

        for (uint i = 0; i < lineCount; i++)
        {
            uint lineLen = GetLineLength(i);
            if (remaining <= lineLen)
                return (i, remaining);
            remaining -= lineLen + 1; // +1 for the newline
        }

        // Past end — clamp to last line
        uint lastLine = lineCount > 0 ? lineCount - 1 : 0;
        return (lastLine, lineCount > 0 ? GetLineLength(lastLine) : 0);
    }

    /// <summary>
    /// Gets text between two character offsets (not line/col).
    /// Converts offsets to line/col positions and delegates to <see cref="GetTextRange(uint,uint,uint,uint)"/>.
    /// </summary>
    public string GetTextRangeByOffset(uint startOffset, uint endOffset)
    {
        ThrowIfDisposed();
        if (startOffset >= endOffset) return string.Empty;

        // Convert start offset to line/col
        uint lineCount = LineCount;
        uint remaining = startOffset;
        uint startLine = 0, startCol = 0;
        bool foundStart = false;

        for (uint i = 0; i < lineCount; i++)
        {
            uint lineLen = GetLineLength(i);
            if (remaining <= lineLen)
            {
                startLine = i;
                startCol = remaining;
                foundStart = true;
                break;
            }
            remaining -= lineLen + 1; // +1 for newline
        }
        if (!foundStart) return string.Empty;

        // Convert end offset to line/col
        remaining = endOffset;
        uint endLine = 0, endCol = 0;
        bool foundEnd = false;

        for (uint i = 0; i < lineCount; i++)
        {
            uint lineLen = GetLineLength(i);
            if (remaining <= lineLen)
            {
                endLine = i;
                endCol = remaining;
                foundEnd = true;
                break;
            }
            remaining -= lineLen + 1;
        }
        if (!foundEnd)
        {
            endLine = lineCount > 0 ? lineCount - 1 : 0;
            endCol = lineCount > 0 ? GetLineLength(endLine) : 0;
        }

        return GetTextRange(startLine, startCol, endLine, endCol);
    }

    /// <summary>Reads a file and sets its content. Returns false if the file does not exist.</summary>
    public bool LoadFile(string path)
    {
        ThrowIfDisposed();
        if (!File.Exists(path)) return false;
        var bytes = File.ReadAllBytes(path);
        SetText(bytes);
        return true;
    }

    // ── Editing operations ───────────────────────────────────────────

    /// <summary>Inserts text at the given line and column position.</summary>
    public void InsertText(uint line, uint col, string text)
    {
        ThrowIfDisposed();
        if (string.IsNullOrEmpty(text)) return;

        // Convert text to UTF-8 and register in memory
        byte[] utf8Bytes = Encoding.UTF8.GetBytes(text);
        ushort memId = _memRegistry.Register(utf8Bytes);

        // Parse into segments
        var newSegments = TextToSegments(utf8Bytes, (byte)memId);
        if (newSegments.Length == 0) return;

        // Find the rope index where we need to insert
        var result = FindRopeIndex(line, col);

        if (result.SplitSegment.HasValue && result.SplitCharOffset > 0)
        {
            // Need to split the segment at the column position
            var seg = result.SplitSegment.Value;
            var (leftSeg, rightSeg) = SplitSegmentAtChar(seg, result.SplitCharOffset);

            // Delete the original segment and insert: left + new + right
            _rope.DeleteRange(result.SplitRopeIndex, result.SplitRopeIndex + 1);

            var allSegments = new List<Segment>();
            if (!leftSeg.IsEmpty) allSegments.Add(leftSeg);
            allSegments.AddRange(newSegments);
            if (!rightSeg.IsEmpty) allSegments.Add(rightSeg);

            _rope.InsertSlice(result.SplitRopeIndex, allSegments.ToArray());
        }
        else
        {
            // Insert at boundary (beginning of segment or empty line)
            _rope.InsertSlice(result.InsertIndex, newSegments);
        }

        _contentEpoch++;
        MarkViewsDirty();
    }

    /// <summary>Deletes a range of text from (startLine, startCol) to (endLine, endCol).</summary>
    public void DeleteRange(uint startLine, uint startCol, uint endLine, uint endCol)
    {
        ThrowIfDisposed();
        if (startLine > endLine || (startLine == endLine && startCol >= endCol))
            return;

        // Rebuild the entire rope content minus the deleted range
        // TODO: Optimize with direct rope manipulation instead of full rebuild
        string fullText = GetText();
        if (string.IsNullOrEmpty(fullText)) return;

        // Convert (line,col) to flat character offsets
        uint startOffset = GetOffset(startLine, startCol);
        uint endOffset = GetOffset(endLine, endCol);

        if (startOffset >= endOffset || startOffset >= (uint)fullText.Length)
            return;

        endOffset = Math.Min(endOffset, (uint)fullText.Length);

        string newText = string.Concat(fullText.AsSpan(0, (int)startOffset), fullText.AsSpan((int)endOffset));

        // Replace buffer content
        _rope.Clear();
        _memRegistry.Clear();
        ClearHighlights();
        ClearStyleSpans();

        if (newText.Length > 0)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(newText);
            ushort newMemId = _memRegistry.Register(utf8);
            var segments = TextToSegments(utf8, (byte)newMemId);
            if (segments.Length > 0)
                _rope.SetSegments(segments);
        }

        _contentEpoch++;
        MarkViewsDirty();
    }

    // ── Undo / Redo ──────────────────────────────────────────────────

    /// <summary>Whether an undo operation is available.</summary>
    public bool CanUndo => _rope.CanUndo;

    /// <summary>Whether a redo operation is available.</summary>
    public bool CanRedo => _rope.CanRedo;

    /// <summary>Stores the current state as an undo checkpoint.</summary>
    public void StoreUndo(string meta = "")
    {
        ThrowIfDisposed();
        _rope.StoreUndo(meta);
    }

    /// <summary>Undoes the last change. Returns the metadata, or null if nothing to undo.</summary>
    public string? Undo()
    {
        ThrowIfDisposed();
        var result = _rope.Undo();
        if (result is not null)
        {
            _contentEpoch++;
            MarkViewsDirty();
        }
        return result;
    }

    /// <summary>Redoes the last undone change. Returns the metadata, or null if nothing to redo.</summary>
    public string? Redo()
    {
        ThrowIfDisposed();
        var result = _rope.Redo();
        if (result is not null)
        {
            _contentEpoch++;
            MarkViewsDirty();
        }
        return result;
    }

    /// <summary>Clears all undo/redo history.</summary>
    public void ClearHistory()
    {
        ThrowIfDisposed();
        _rope.ClearHistory();
    }

    // ── Private editing helpers ──────────────────────────────────────

    /// <summary>Result of a rope index search for insert/split operations.</summary>
    private readonly record struct RopeIndexResult(
        uint InsertIndex, Segment? SplitSegment, uint SplitCharOffset, uint SplitRopeIndex);

    /// <summary>
    /// Finds the rope index for a (line, col) position.
    /// If the position falls inside a segment, returns info about the segment to split.
    /// </summary>
    private RopeIndexResult FindRopeIndex(uint targetLine, uint targetCol)
    {
        Segment? foundSplitSegment = null;
        uint foundSplitCharOffset = 0;
        uint foundSplitRopeIndex = 0;

        uint currentLine = 0;
        uint currentCol = 0;
        uint ropeIdx = 0;
        uint insertIdx = 0;
        bool found = false;

        _rope.Walk((seg, idx) =>
        {
            if (found) return false;

            if (currentLine == targetLine)
            {
                if (seg.IsNewline)
                {
                    insertIdx = ropeIdx;
                    found = true;
                    return false;
                }

                if (!seg.IsEmpty)
                {
                    var bytes = GetSegmentBytes(in seg);
                    uint charCount = bytes.IsEmpty ? 0 : (uint)Encoding.UTF8.GetString(bytes.Span).Length;

                    if (targetCol >= currentCol && targetCol < currentCol + charCount)
                    {
                        foundSplitSegment = seg;
                        foundSplitCharOffset = targetCol - currentCol;
                        foundSplitRopeIndex = ropeIdx;
                        insertIdx = ropeIdx;
                        found = true;
                        return false;
                    }
                    else if (targetCol == currentCol + charCount)
                    {
                        insertIdx = ropeIdx + 1;
                        found = true;
                        return false;
                    }

                    currentCol += charCount;
                }
            }
            else if (seg.IsNewline)
            {
                currentLine++;
                currentCol = 0;
            }
            else if (!seg.IsEmpty)
            {
                var bytes = GetSegmentBytes(in seg);
                if (!bytes.IsEmpty)
                    currentCol += (uint)Encoding.UTF8.GetString(bytes.Span).Length;
            }

            ropeIdx++;
            return true;
        });

        if (!found)
            insertIdx = _rope.Count;

        return new RopeIndexResult(insertIdx, foundSplitSegment, foundSplitCharOffset, foundSplitRopeIndex);
    }

    /// <summary>Splits a segment at a character offset within it.</summary>
    private (Segment Left, Segment Right) SplitSegmentAtChar(Segment seg, uint charOffset)
    {
        if (seg.IsNewline || seg.IsEmpty)
            return (seg, Segment.Empty());

        var bytes = GetSegmentBytes(in seg);
        if (bytes.IsEmpty)
            return (seg, Segment.Empty());

        // Find the byte offset corresponding to the character offset
        var span = bytes.Span;
        int bytePos = 0;
        uint chars = 0;
        while (bytePos < span.Length && chars < charOffset)
        {
            var (_, consumed) = TextWidth.DecodeUtf8(span, bytePos);
            bytePos += consumed;
            chars++;
        }

        if (bytePos == 0)
            return (Segment.Empty(), seg);
        if (bytePos >= span.Length)
            return (seg, Segment.Empty());

        uint splitBytePos = seg.ByteStart + (uint)bytePos;

        ReadOnlySpan<byte> leftBytes = span[..bytePos];
        ReadOnlySpan<byte> rightBytes = span[bytePos..];

        bool leftAscii = TextWidth.IsAsciiOnly(leftBytes);
        bool rightAscii = TextWidth.IsAsciiOnly(rightBytes);
        ushort leftWidth = (ushort)TextWidth.CalculateTextWidth(leftBytes, _tabWidth, leftAscii, _widthMethod);
        ushort rightWidth = (ushort)TextWidth.CalculateTextWidth(rightBytes, _tabWidth, rightAscii, _widthMethod);

        var left = new Segment(seg.MemId, seg.ByteStart, splitBytePos, leftWidth,
            leftAscii ? (byte)0x01 : (byte)0, false);
        var right = new Segment(seg.MemId, splitBytePos, seg.ByteEnd, rightWidth,
            rightAscii ? (byte)0x01 : (byte)0, false);

        return (left, right);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
