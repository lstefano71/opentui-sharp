using System.Text;
using OpenTui.Core.Managed.Unicode;

namespace OpenTui.Core.Managed;

/// <summary>Represents a single visual row after word-wrapping.</summary>
public readonly record struct VirtualLine(
    uint SourceLine,       // Logical line index in the text buffer
    uint SourceColOffset,  // Starting column within that logical line (0 for first wrap segment)
    uint WidthCols);       // Display width of this virtual line in columns

/// <summary>
/// Pure C# implementation of the Zig <c>text-buffer-view.zig</c>.
/// A rectangular viewport into a <see cref="ManagedTextBuffer"/> with scrolling,
/// cursor tracking, text selection, and word-wrapping.
/// </summary>
public sealed class ManagedTextBufferView : IDisposable
{
    private readonly ManagedTextBuffer _originalBuffer;
    private ManagedTextBuffer _buffer;
    private readonly uint _viewId;
    private bool _disposed;

    // ── Viewport ────────────────────────────────────────────────────
    private uint _width;
    private uint _height;
    private uint _scrollTop;
    private uint _scrollLeft;

    // ── Cursor ──────────────────────────────────────────────────────
    private uint _cursorLine;
    private uint _cursorCol;

    // ── Selection ───────────────────────────────────────────────────
    private LogicalCursor? _selectionAnchor;

    // Offset-based selection (used by ManagedEditorView)
    private uint? _selectionStartOffset;
    private uint? _selectionEndOffset;
    private Rgba? _selectionBg;
    private Rgba? _selectionFg;

    // ── Wrapping ────────────────────────────────────────────────────
    private WrapMode _wrapMode = WrapMode.None;

    // Cached wrap data: invalidated when buffer changes or viewport width changes
    private ulong _wrapCacheVersion;
    private uint _wrapCacheWidth;
    private List<WrapLine>? _wrapLineCache;

    // Cached public VirtualLine array (rebuilt when wrap cache changes)
    private VirtualLine[]? _virtualLineArray;
    private ulong _virtualLineArrayVersion;
    private uint _virtualLineArrayWidth;

    #region Construction

    private ManagedTextBufferView(ManagedTextBuffer buffer, uint width, uint height)
    {
        _originalBuffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _buffer = buffer;
        _viewId = buffer.RegisterView();
        _width = width;
        _height = height;
    }

    /// <summary>Creates a new view into the given text buffer with the specified viewport size.</summary>
    public static ManagedTextBufferView Create(ManagedTextBuffer buffer, uint width, uint height)
    {
        return new ManagedTextBufferView(buffer, width, height);
    }

    #endregion

    #region Viewport properties

    /// <summary>Viewport width in columns.</summary>
    public uint Width => _width;

    /// <summary>Viewport height in rows.</summary>
    public uint Height => _height;

    /// <summary>Vertical scroll offset (first visible visual row).</summary>
    public uint ScrollTop => _scrollTop;

    /// <summary>Horizontal scroll offset (alias for <see cref="ScrollLeft"/>, used by EditorView).</summary>
    public uint ViewportX => _scrollLeft;

    /// <summary>Vertical scroll offset (alias for <see cref="ScrollTop"/>, used by EditorView).</summary>
    public uint ViewportY => _scrollTop;

    /// <summary>Horizontal scroll offset (first visible column, used in no-wrap mode).</summary>
    public uint ScrollLeft => _scrollLeft;

    /// <summary>Gets the underlying text buffer.</summary>
    public ManagedTextBuffer Buffer => _buffer;

    /// <summary>Gets the selection background color, if set.</summary>
    public Rgba? SelectionBg => _selectionBg;

    /// <summary>Gets the selection foreground color, if set.</summary>
    public Rgba? SelectionFg => _selectionFg;

    #endregion

    #region Cursor

    /// <summary>Logical cursor position (line and column in buffer content).</summary>
    public LogicalCursor Cursor
    {
        get
        {
            uint offset = _buffer.GetOffset(_cursorLine, _cursorCol);
            return new LogicalCursor(_cursorLine, _cursorCol, offset);
        }
    }

    /// <summary>Visual cursor position relative to the viewport.</summary>
    public VisualCursor VisualCursorPosition
    {
        get
        {
            var (visualRow, visualCol) = LogicalToVisualRaw(_cursorLine, _cursorCol);

            // Relative to viewport
            uint vpRow = visualRow >= _scrollTop ? visualRow - _scrollTop : 0;
            uint vpCol = visualCol >= _scrollLeft ? visualCol - _scrollLeft : 0;

            uint offset = _buffer.GetOffset(_cursorLine, _cursorCol);
            return new VisualCursor(vpRow, vpCol, _cursorLine, _cursorCol, offset);
        }
    }

    #endregion

    #region Selection

    /// <summary>The anchor point of the current selection, or null if no selection.</summary>
    public LogicalCursor? SelectionAnchor => _selectionAnchor;

    /// <summary>Whether a text selection is currently active.</summary>
    public bool HasSelection => _selectionAnchor.HasValue;

    /// <summary>
    /// Begins a selection at the current cursor position.
    /// If a selection is already active, this resets the anchor to the current cursor.
    /// </summary>
    public void StartSelection()
    {
        _selectionAnchor = Cursor;
    }

    /// <summary>Clears the current selection (both anchor-based and offset-based).</summary>
    public void ClearSelection()
    {
        _selectionAnchor = null;
        _selectionStartOffset = null;
        _selectionEndOffset = null;
        _selectionBg = null;
        _selectionFg = null;
    }

    /// <summary>
    /// Gets the ordered (start, end) cursor range of the anchor-based selection.
    /// Start is always &lt;= End in document order.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no selection is active.</exception>
    public (LogicalCursor Start, LogicalCursor End) GetSelectionCursorRange()
    {
        if (!_selectionAnchor.HasValue)
            throw new InvalidOperationException("No selection is active.");

        var anchor = _selectionAnchor.Value;
        var head = Cursor;

        if (anchor.Row < head.Row || (anchor.Row == head.Row && anchor.Col <= head.Col))
            return (anchor, head);

        return (head, anchor);
    }

    /// <summary>
    /// Gets the offset-based selection range, or null if no offset-based selection is active.
    /// Used by <see cref="ManagedEditorView"/>.
    /// </summary>
    public (uint Start, uint End)? GetSelectionRange()
    {
        if (_selectionStartOffset is not { } start || _selectionEndOffset is not { } end)
            return null;

        return start <= end ? (start, end) : (end, start);
    }

    /// <summary>
    /// Sets an offset-based selection range with optional highlight colors.
    /// </summary>
    public void SetSelection(uint startOffset, uint endOffset, Rgba? selBg, Rgba? selFg)
    {
        _selectionStartOffset = startOffset;
        _selectionEndOffset = endOffset;
        _selectionBg = selBg;
        _selectionFg = selFg;
    }

    /// <summary>Clears all selection state (both anchor-based and offset-based).</summary>
    public void ResetSelection()
    {
        _selectionAnchor = null;
        _selectionStartOffset = null;
        _selectionEndOffset = null;
        _selectionBg = null;
        _selectionFg = null;
    }

    /// <summary>Gets the text covered by the current anchor-based selection.</summary>
    /// <exception cref="InvalidOperationException">Thrown when no selection is active.</exception>
    public string GetSelectedText()
    {
        var (start, end) = GetSelectionCursorRange();
        return _buffer.GetTextRange(start.Row, start.Col, end.Row, end.Col);
    }

    #endregion

    #region Wrapping

    /// <summary>Gets or sets the word-wrap mode.</summary>
    public WrapMode WrapMode
    {
        get => _wrapMode;
        set
        {
            if (_wrapMode != value)
            {
                _wrapMode = value;
                InvalidateWrapCache();
            }
        }
    }

    #endregion

    #region Viewport management

    /// <summary>Resizes the viewport.</summary>
    public void Resize(uint width, uint height)
    {
        bool widthChanged = _width != width;
        _width = width;
        _height = height;

        if (widthChanged)
            InvalidateWrapCache();

        // Ensure cursor is still visible after resize
        ClampScroll();
    }

    /// <summary>Resizes the viewport (alias for <see cref="Resize"/>).</summary>
    public void SetViewportSize(uint width, uint height) => Resize(width, height);

    /// <summary>Sets the scroll position and viewport size in one call.</summary>
    public void SetViewport(uint x, uint y, uint width, uint height)
    {
        _scrollLeft = x;
        _scrollTop = y;

        bool widthChanged = _width != width;
        _width = width;
        _height = height;

        if (widthChanged)
            InvalidateWrapCache();

        ClampScroll();
    }

    /// <summary>Sets the scroll position directly.</summary>
    public void ScrollTo(uint top, uint left)
    {
        _scrollTop = top;
        _scrollLeft = left;
        ClampScroll();
    }

    /// <summary>Scrolls by a delta in rows and columns.</summary>
    public void ScrollBy(int deltaRows, int deltaCols)
    {
        _scrollTop = AddClampUint(_scrollTop, deltaRows);
        _scrollLeft = AddClampUint(_scrollLeft, deltaCols);
        ClampScroll();
    }

    /// <summary>Adjusts scroll position so the cursor is visible within the viewport.</summary>
    public void ScrollToCursor()
    {
        var (visualRow, visualCol) = LogicalToVisualRaw(_cursorLine, _cursorCol);

        // Vertical: ensure cursor row is within [scrollTop, scrollTop + height)
        if (visualRow < _scrollTop)
        {
            _scrollTop = visualRow;
        }
        else if (visualRow >= _scrollTop + _height)
        {
            _scrollTop = visualRow - _height + 1;
        }

        // Horizontal (only relevant in no-wrap mode)
        if (_wrapMode == WrapMode.None)
        {
            if (visualCol < _scrollLeft)
            {
                _scrollLeft = visualCol;
            }
            else if (visualCol >= _scrollLeft + _width)
            {
                _scrollLeft = visualCol - _width + 1;
            }
        }
        else
        {
            _scrollLeft = 0;
        }
    }

    #endregion

    #region Cursor movement

    /// <summary>Sets the cursor to the given logical line and column.</summary>
    public void SetCursor(uint line, uint col)
    {
        uint lineCount = _buffer.LineCount;
        _cursorLine = Math.Min(line, lineCount > 0 ? lineCount - 1 : 0);
        uint lineLen = _buffer.GetLineLength(_cursorLine);
        _cursorCol = Math.Min(col, lineLen);
    }

    /// <summary>Moves the cursor up by the given number of lines.</summary>
    public void MoveCursorUp(uint count = 1)
    {
        if (_cursorLine == 0) return;

        if (_wrapMode != WrapMode.None)
        {
            // In wrap mode, move up by visual lines
            var (visualRow, visualCol) = LogicalToVisualRaw(_cursorLine, _cursorCol);
            if (visualRow >= count)
            {
                var logical = VisualToLogicalRaw(visualRow - count, visualCol);
                _cursorLine = logical.Line;
                _cursorCol = logical.Col;
            }
            else
            {
                _cursorLine = 0;
                _cursorCol = 0;
            }
        }
        else
        {
            uint savedCol = _cursorCol;
            _cursorLine = _cursorLine >= count ? _cursorLine - count : 0;
            uint lineLen = _buffer.GetLineLength(_cursorLine);
            _cursorCol = Math.Min(savedCol, lineLen);
        }
    }

    /// <summary>Moves the cursor down by the given number of lines.</summary>
    public void MoveCursorDown(uint count = 1)
    {
        uint lastLine = _buffer.LineCount > 0 ? _buffer.LineCount - 1 : 0;
        if (_cursorLine >= lastLine) return;

        if (_wrapMode != WrapMode.None)
        {
            var (visualRow, visualCol) = LogicalToVisualRaw(_cursorLine, _cursorCol);
            uint totalVisual = GetTotalVisualLines();
            uint targetVisualRow = Math.Min(visualRow + count, totalVisual > 0 ? totalVisual - 1 : 0);
            var logical = VisualToLogicalRaw(targetVisualRow, visualCol);
            _cursorLine = logical.Line;
            _cursorCol = logical.Col;
        }
        else
        {
            uint savedCol = _cursorCol;
            _cursorLine = Math.Min(_cursorLine + count, lastLine);
            uint lineLen = _buffer.GetLineLength(_cursorLine);
            _cursorCol = Math.Min(savedCol, lineLen);
        }
    }

    /// <summary>Moves the cursor left by the given number of characters.</summary>
    public void MoveCursorLeft(uint count = 1)
    {
        for (uint i = 0; i < count; i++)
        {
            if (_cursorCol > 0)
            {
                _cursorCol--;
            }
            else if (_cursorLine > 0)
            {
                // Wrap to end of previous line
                _cursorLine--;
                _cursorCol = _buffer.GetLineLength(_cursorLine);
            }
        }
    }

    /// <summary>Moves the cursor right by the given number of characters.</summary>
    public void MoveCursorRight(uint count = 1)
    {
        for (uint i = 0; i < count; i++)
        {
            uint lineLen = _buffer.GetLineLength(_cursorLine);
            if (_cursorCol < lineLen)
            {
                _cursorCol++;
            }
            else if (_cursorLine < _buffer.LineCount - 1)
            {
                // Wrap to start of next line
                _cursorLine++;
                _cursorCol = 0;
            }
        }
    }

    /// <summary>Moves the cursor to the start of the current line.</summary>
    public void MoveCursorToLineStart()
    {
        _cursorCol = 0;
    }

    /// <summary>Moves the cursor to the end of the current line.</summary>
    public void MoveCursorToLineEnd()
    {
        _cursorCol = _buffer.GetLineLength(_cursorLine);
    }

    /// <summary>Moves the cursor to the very beginning of the buffer.</summary>
    public void MoveCursorToStart()
    {
        _cursorLine = 0;
        _cursorCol = 0;
    }

    /// <summary>Moves the cursor to the very end of the buffer.</summary>
    public void MoveCursorToEnd()
    {
        uint lineCount = _buffer.LineCount;
        _cursorLine = lineCount > 0 ? lineCount - 1 : 0;
        _cursorCol = _buffer.GetLineLength(_cursorLine);
    }

    /// <summary>Moves the cursor to the previous word boundary.</summary>
    public void MoveCursorWordLeft()
    {
        if (_cursorCol == 0 && _cursorLine == 0)
            return;

        // If at line start, move to end of previous line
        if (_cursorCol == 0)
        {
            _cursorLine--;
            _cursorCol = _buffer.GetLineLength(_cursorLine);
            return;
        }

        string lineText = _buffer.GetLineText(_cursorLine);
        int pos = (int)Math.Min(_cursorCol, (uint)lineText.Length);

        // Skip non-word characters backwards
        while (pos > 0 && !IsWordChar(lineText, pos - 1))
            pos--;

        // Skip word characters backwards
        while (pos > 0 && IsWordChar(lineText, pos - 1))
            pos--;

        _cursorCol = (uint)pos;
    }

    /// <summary>Moves the cursor to the next word boundary.</summary>
    public void MoveCursorWordRight()
    {
        uint lineCount = _buffer.LineCount;
        uint lastLine = lineCount > 0 ? lineCount - 1 : 0;
        uint lineLen = _buffer.GetLineLength(_cursorLine);

        if (_cursorCol >= lineLen && _cursorLine >= lastLine)
            return;

        // If at line end, move to start of next line
        if (_cursorCol >= lineLen)
        {
            _cursorLine++;
            _cursorCol = 0;
            return;
        }

        string lineText = _buffer.GetLineText(_cursorLine);
        int pos = (int)Math.Min(_cursorCol, (uint)lineText.Length);

        // Skip word characters forward
        while (pos < lineText.Length && IsWordChar(lineText, pos))
            pos++;

        // Skip non-word characters forward
        while (pos < lineText.Length && !IsWordChar(lineText, pos))
            pos++;

        _cursorCol = (uint)pos;
    }

    #endregion

    #region Queries

    /// <summary>Gets the number of visual lines visible in the current viewport.</summary>
    public uint GetVisibleLineCount()
    {
        uint totalVisual = GetTotalVisualLines();
        if (totalVisual <= _scrollTop)
            return 0;
        return Math.Min(totalVisual - _scrollTop, _height);
    }

    /// <summary>
    /// Gets the total number of visual lines, accounting for wrapping.
    /// Without wrapping, this equals the buffer's line count.
    /// </summary>
    public uint GetTotalVisualLines()
    {
        if (_wrapMode == WrapMode.None)
            return _buffer.LineCount;

        EnsureWrapCache();
        return (uint)(_wrapLineCache?.Count ?? 0);
    }

    /// <summary>Gets the total virtual line count (alias for <see cref="GetTotalVisualLines"/>).</summary>
    public uint GetVirtualLineCount() => GetTotalVisualLines();

    /// <summary>Whether the underlying buffer has been modified since the last <see cref="ClearDirty"/> call.</summary>
    public bool IsDirty => _originalBuffer.IsViewDirty(_viewId);

    /// <summary>Clears the dirty flag for this view.</summary>
    public void ClearDirty() => _originalBuffer.ClearViewDirty(_viewId);

    #endregion

    #region Coordinate conversion

    /// <summary>
    /// Converts a visual (row, col) position within the document to a logical (line, col) position.
    /// The visual row accounts for wrapping; the visual col accounts for scroll offset.
    /// </summary>
    public LogicalCursor VisualToLogical(uint visualRow, uint visualCol)
    {
        var result = VisualToLogicalRaw(visualRow + _scrollTop, visualCol + _scrollLeft);
        uint offset = _buffer.GetOffset(result.Line, result.Col);
        return new LogicalCursor(result.Line, result.Col, offset);
    }

    /// <summary>
    /// Converts a logical (line, col) position to a visual (row, col) position relative to the viewport.
    /// </summary>
    public VisualCursor LogicalToVisual(uint logicalLine, uint logicalCol)
    {
        var (visualRow, visualCol) = LogicalToVisualRaw(logicalLine, logicalCol);

        uint vpRow = visualRow >= _scrollTop ? visualRow - _scrollTop : 0;
        uint vpCol = visualCol >= _scrollLeft ? visualCol - _scrollLeft : 0;

        uint offset = _buffer.GetOffset(logicalLine, logicalCol);
        return new VisualCursor(vpRow, vpCol, logicalLine, logicalCol, offset);
    }

    #endregion

    #region Dispose

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _buffer = _originalBuffer; // restore original before cleanup
            _originalBuffer.UnregisterView(_viewId);
            _wrapLineCache = null;
            _virtualLineArray = null;
        }
    }

    #endregion

    #region Virtual lines

    /// <summary>
    /// Returns the computed virtual line array. Each virtual line represents
    /// one visual row after word-wrapping.
    /// </summary>
    public VirtualLine[] GetVirtualLines()
    {
        EnsureVirtualLineArray();
        return _virtualLineArray!;
    }

    /// <summary>Recomputes virtual lines if the buffer or viewport has changed.</summary>
    public void UpdateVirtualLines()
    {
        EnsureVirtualLineArray();
    }

    /// <summary>
    /// Given logical coordinates, finds which virtual line index they map to.
    /// </summary>
    public uint FindVisualLineIndex(uint logicalLine, uint logicalCol)
    {
        if (_wrapMode == WrapMode.None)
            return logicalLine;

        EnsureWrapCache();
        var cache = _wrapLineCache!;

        for (int i = 0; i < cache.Count; i++)
        {
            var wl = cache[i];
            if (wl.LogicalLine != logicalLine) continue;

            if (logicalCol >= wl.StartCol && logicalCol < wl.StartCol + wl.ColCount)
                return (uint)i;

            // Last wrap line for this logical line
            if (i + 1 >= cache.Count || cache[i + 1].LogicalLine != logicalLine)
                return (uint)i;
        }

        return cache.Count > 0 ? (uint)(cache.Count - 1) : 0;
    }

    private void EnsureVirtualLineArray()
    {
        ulong version = _buffer.Version;
        if (_virtualLineArray is not null
            && _virtualLineArrayVersion == version
            && _virtualLineArrayWidth == _width)
        {
            return;
        }

        RebuildVirtualLineArray();
        _virtualLineArrayVersion = version;
        _virtualLineArrayWidth = _width;
    }

    private void RebuildVirtualLineArray()
    {
        if (_wrapMode == WrapMode.None)
        {
            uint lineCount = _buffer.LineCount;
            var result = new VirtualLine[lineCount];
            for (uint i = 0; i < lineCount; i++)
                result[i] = new VirtualLine(i, 0, _buffer.GetLineLength(i));
            _virtualLineArray = result;
            return;
        }

        EnsureWrapCache();
        var cache = _wrapLineCache!;
        var arr = new VirtualLine[cache.Count];
        for (int i = 0; i < cache.Count; i++)
        {
            var wl = cache[i];
            arr[i] = new VirtualLine(wl.LogicalLine, wl.StartCol, wl.ColCount);
        }
        _virtualLineArray = arr;
    }

    #endregion

    #region Buffer switching

    /// <summary>Switches the view to display a different buffer (e.g. placeholder text).</summary>
    public void SwitchToBuffer(ManagedTextBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        _buffer = buffer;
        InvalidateWrapCache();
        _virtualLineArray = null;
    }

    /// <summary>Restores the view to the original buffer it was created with.</summary>
    public void SwitchToOriginalBuffer()
    {
        if (_buffer != _originalBuffer)
        {
            _buffer = _originalBuffer;
            InvalidateWrapCache();
            _virtualLineArray = null;
        }
    }

    #endregion

    #region Wrap internals

    /// <summary>Represents one visual line (possibly a wrapped portion of a logical line).</summary>
    private readonly record struct WrapLine(uint LogicalLine, uint StartCol, uint ColCount);

    private void InvalidateWrapCache()
    {
        _wrapLineCache = null;
        _virtualLineArray = null;
    }

    private void EnsureWrapCache()
    {
        ulong bufferVersion = _buffer.Version;
        if (_wrapLineCache is not null
            && _wrapCacheVersion == bufferVersion
            && _wrapCacheWidth == _width)
        {
            return;
        }

        RebuildWrapCache();
        _wrapCacheVersion = bufferVersion;
        _wrapCacheWidth = _width;
    }

    private void RebuildWrapCache()
    {
        uint lineCount = _buffer.LineCount;
        _wrapLineCache = new List<WrapLine>((int)lineCount);

        if (_width == 0)
        {
            // Degenerate case: zero-width viewport → one visual line per logical line
            for (uint i = 0; i < lineCount; i++)
                _wrapLineCache.Add(new WrapLine(i, 0, 0));
            return;
        }

        for (uint line = 0; line < lineCount; line++)
        {
            string lineText = _buffer.GetLineText(line);
            if (lineText.Length == 0)
            {
                _wrapLineCache.Add(new WrapLine(line, 0, 0));
                continue;
            }

            if (_wrapMode == WrapMode.Char)
            {
                WrapLineByChar(line, lineText);
            }
            else // WrapMode.Word
            {
                WrapLineByWord(line, lineText);
            }
        }
    }

    private void WrapLineByChar(uint logicalLine, string lineText)
    {
        uint col = 0;
        uint displayWidth = TextWidth.CalculateTextWidth(lineText.AsSpan(), _buffer.TabWidth, _buffer.WidthMethod);

        if (displayWidth <= _width)
        {
            _wrapLineCache!.Add(new WrapLine(logicalLine, 0, (uint)lineText.Length));
            return;
        }

        // Walk through characters, accumulating display width
        int charIdx = 0;
        uint startCol = 0;
        uint currentWidth = 0;

        while (charIdx < lineText.Length)
        {
            Rune rune;
            int consumed;
            if (Rune.DecodeFromUtf16(lineText.AsSpan(charIdx), out rune, out consumed)
                != System.Buffers.OperationStatus.Done)
            {
                rune = Rune.ReplacementChar;
                consumed = 1;
            }

            uint charWidth = TextWidth.CharWidth(rune, _buffer.TabWidth);

            if (currentWidth + charWidth > _width && currentWidth > 0)
            {
                // Wrap here
                _wrapLineCache!.Add(new WrapLine(logicalLine, startCol, col - startCol));
                startCol = col;
                currentWidth = 0;
            }

            currentWidth += charWidth;
            col += (uint)consumed;
            charIdx += consumed;
        }

        // Remainder
        if (col > startCol)
        {
            _wrapLineCache!.Add(new WrapLine(logicalLine, startCol, col - startCol));
        }
    }

    private void WrapLineByWord(uint logicalLine, string lineText)
    {
        // Use WrapBreaks to find break opportunities
        byte[] utf8Bytes = Encoding.UTF8.GetBytes(lineText);
        var breaks = new List<WrapBreaks.WrapBreak>();
        WrapBreaks.FindWrapBreaks(utf8Bytes, breaks, _buffer.WidthMethod);

        if (breaks.Count == 0)
        {
            // No break opportunities; fall back to char wrapping
            WrapLineByChar(logicalLine, lineText);
            return;
        }

        // Walk through characters, tracking accumulated display width
        // and use break opportunities to decide where to wrap
        uint startCol = 0;
        uint lastBreakCol = 0;
        bool haveBreak = false;
        uint currentWidth = 0;
        int breakIdx = 0;
        int charIdx = 0;
        uint col = 0;

        while (charIdx < lineText.Length)
        {
            // Check if current char position is a break opportunity
            // Break offsets are in char positions from WrapBreaks
            while (breakIdx < breaks.Count && breaks[breakIdx].CharOffset <= col)
            {
                lastBreakCol = breaks[breakIdx].CharOffset;
                haveBreak = true;
                breakIdx++;
            }

            Rune rune;
            int consumed;
            if (Rune.DecodeFromUtf16(lineText.AsSpan(charIdx), out rune, out consumed)
                != System.Buffers.OperationStatus.Done)
            {
                rune = Rune.ReplacementChar;
                consumed = 1;
            }

            uint charWidth = TextWidth.CharWidth(rune, _buffer.TabWidth);

            if (currentWidth + charWidth > _width && currentWidth > 0)
            {
                // Need to wrap
                if (haveBreak && lastBreakCol > startCol)
                {
                    // Wrap at the last break opportunity
                    uint wrapCol = lastBreakCol + 1; // after the break character
                    _wrapLineCache!.Add(new WrapLine(logicalLine, startCol, wrapCol - startCol));
                    startCol = wrapCol;

                    // Recalculate width from the new start
                    // TODO: Optimize by not rescanning — track incrementally
                    currentWidth = CalculateWidthFromCol(lineText, startCol, col + (uint)consumed);
                }
                else
                {
                    // No break opportunity found — hard wrap at char boundary
                    _wrapLineCache!.Add(new WrapLine(logicalLine, startCol, col - startCol));
                    startCol = col;
                    currentWidth = charWidth;
                }

                haveBreak = false;
                col += (uint)consumed;
                charIdx += consumed;
                continue;
            }

            currentWidth += charWidth;
            col += (uint)consumed;
            charIdx += consumed;
        }

        // Remainder
        if (col > startCol)
        {
            _wrapLineCache!.Add(new WrapLine(logicalLine, startCol, col - startCol));
        }
    }

    private uint CalculateWidthFromCol(string lineText, uint fromCol, uint toCol)
    {
        if (fromCol >= toCol || fromCol >= (uint)lineText.Length)
            return 0;

        int start = (int)fromCol;
        int end = (int)Math.Min(toCol, (uint)lineText.Length);
        return TextWidth.CalculateTextWidth(lineText.AsSpan(start, end - start), _buffer.TabWidth, _buffer.WidthMethod);
    }

    #endregion

    #region Private helpers

    /// <summary>
    /// Converts logical (line, col) to absolute visual (row, col) without viewport offset.
    /// </summary>
    private (uint VisualRow, uint VisualCol) LogicalToVisualRaw(uint logicalLine, uint logicalCol)
    {
        if (_wrapMode == WrapMode.None)
        {
            return (logicalLine, logicalCol);
        }

        EnsureWrapCache();
        var cache = _wrapLineCache!;

        // Find the visual row corresponding to this logical line and column
        for (int i = 0; i < cache.Count; i++)
        {
            var wl = cache[i];
            if (wl.LogicalLine != logicalLine) continue;

            // Check if this wrap line contains the column
            if (logicalCol >= wl.StartCol && logicalCol < wl.StartCol + wl.ColCount)
            {
                return ((uint)i, logicalCol - wl.StartCol);
            }

            // If this is the last wrap line for this logical line and the col is at or past the end
            if (i + 1 >= cache.Count || cache[i + 1].LogicalLine != logicalLine)
            {
                // Column is at or past the end of the last wrap line for this logical line
                return ((uint)i, logicalCol - wl.StartCol);
            }
        }

        // Fallback: beyond end of document
        uint lastRow = cache.Count > 0 ? (uint)(cache.Count - 1) : 0;
        return (lastRow, logicalCol);
    }

    /// <summary>
    /// Converts absolute visual (row, col) to logical (line, col) without viewport offset.
    /// </summary>
    private (uint Line, uint Col) VisualToLogicalRaw(uint visualRow, uint visualCol)
    {
        if (_wrapMode == WrapMode.None)
        {
            uint lineCount = _buffer.LineCount;
            uint line = Math.Min(visualRow, lineCount > 0 ? lineCount - 1 : 0);
            uint lineLen = _buffer.GetLineLength(line);
            uint col = Math.Min(visualCol, lineLen);
            return (line, col);
        }

        EnsureWrapCache();
        var cache = _wrapLineCache!;

        if (cache.Count == 0)
            return (0, 0);

        uint idx = Math.Min(visualRow, (uint)(cache.Count - 1));
        var wl = cache[(int)idx];

        uint logicalCol = wl.StartCol + visualCol;
        uint lineLen2 = _buffer.GetLineLength(wl.LogicalLine);
        logicalCol = Math.Min(logicalCol, lineLen2);

        return (wl.LogicalLine, logicalCol);
    }

    private void ClampScroll()
    {
        uint totalVisual = GetTotalVisualLines();
        if (totalVisual > 0 && _scrollTop >= totalVisual)
            _scrollTop = totalVisual - 1;

        // In wrap mode, horizontal scroll is always 0
        if (_wrapMode != WrapMode.None)
            _scrollLeft = 0;
    }

    private static uint AddClampUint(uint value, int delta)
    {
        long result = (long)value + delta;
        if (result < 0) return 0;
        if (result > uint.MaxValue) return uint.MaxValue;
        return (uint)result;
    }

    private static bool IsWordChar(string text, int index)
    {
        if ((uint)index >= (uint)text.Length) return false;

        if (Rune.DecodeFromUtf16(text.AsSpan(index), out Rune rune, out _)
            != System.Buffers.OperationStatus.Done)
        {
            return false;
        }

        return WordBoundary.IsWordCodepoint(rune);
    }

    #endregion
}
