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
    private uint? _selectionAnchorOffset; // anchor from SetLocalSelection, preserved by UpdateLocalSelection
    private Rgba? _selectionBg;
    private Rgba? _selectionFg;

    // ── Wrapping ────────────────────────────────────────────────────
    private WrapMode _wrapMode = WrapMode.None;
    private uint _wrapWidth; // 0 means "use viewport _width"

    // ── Truncation ──────────────────────────────────────────────────
    private bool _truncate;

    // ── Tab indicators ──────────────────────────────────────────────
    /// <summary>Unicode codepoint used to display tab indicator characters (0 = none).</summary>
    public uint TabIndicatorCodepoint { get; set; }

    /// <summary>Color for tab indicator characters (null = use default fg).</summary>
    public Rgba? TabIndicatorColor { get; set; }

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
            // _cursorCol is a display column; convert to char index for GetOffset
            uint charCol = (uint)_buffer.DisplayColToCharIndex(_cursorLine, _cursorCol);
            uint offset = _buffer.GetOffset(_cursorLine, charCol);
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

            uint offset = _buffer.GetOffset(_cursorLine,
                (uint)_buffer.DisplayColToCharIndex(_cursorLine, _cursorCol));
            return new VisualCursor(vpRow, vpCol, _cursorLine, _cursorCol, offset);
        }
    }

    #endregion

    #region Selection

    /// <summary>The anchor point of the current selection, or null if no selection.</summary>
    public LogicalCursor? SelectionAnchor => _selectionAnchor;

    /// <summary>Whether a text selection is currently active.</summary>
    public bool HasSelection => _selectionAnchor.HasValue
        || (_selectionStartOffset.HasValue && _selectionEndOffset.HasValue
            && _selectionStartOffset != _selectionEndOffset);

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
        _selectionAnchorOffset = null;
        _selectionStartOffset = null;
        _selectionEndOffset = null;
        _selectionBg = null;
        _selectionFg = null;
    }

    /// <summary>Gets the text covered by the current selection (offset-based or anchor-based).</summary>
    /// <exception cref="InvalidOperationException">Thrown when no selection is active.</exception>
    public string GetSelectedText()
    {
        // Prefer offset-based selection (set via SetSelection)
        if (_selectionStartOffset is { } start && _selectionEndOffset is { } end)
            return _buffer.GetTextRangeByOffset(start, end);

        // Fall back to anchor-based selection (set via SetLocalSelection/UpdateSelection)
        if (!_selectionAnchor.HasValue)
            return string.Empty;

        var (cursorStart, cursorEnd) = GetSelectionCursorRange();
        return _buffer.GetTextRange(cursorStart.Row, cursorStart.Col, cursorEnd.Row, cursorEnd.Col);
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

    /// <summary>Sets the wrap width independently of the viewport width. 0 means use viewport width.</summary>
    public void SetWrapWidth(uint width)
    {
        if (_wrapWidth != width)
        {
            _wrapWidth = width;
            InvalidateWrapCache();
        }
    }

    /// <summary>Enables or disables line truncation (when WrapMode is None, truncates lines to viewport width).</summary>
    public void SetTruncate(bool truncate)
    {
        _truncate = truncate;
    }

    /// <summary>Gets whether line truncation is enabled.</summary>
    public bool Truncate => _truncate;

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
        uint lineLen = _buffer.LineWidthAt(_cursorLine);
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
            uint lineLen = _buffer.LineWidthAt(_cursorLine);
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
            uint lineLen = _buffer.LineWidthAt(_cursorLine);
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
                _cursorCol = _buffer.LineWidthAt(_cursorLine);
            }
        }
    }

    /// <summary>Moves the cursor right by the given number of characters.</summary>
    public void MoveCursorRight(uint count = 1)
    {
        for (uint i = 0; i < count; i++)
        {
            uint lineLen = _buffer.LineWidthAt(_cursorLine);
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
        _cursorCol = _buffer.LineWidthAt(_cursorLine);
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
        _cursorCol = _buffer.LineWidthAt(_cursorLine);
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
            _cursorCol = _buffer.LineWidthAt(_cursorLine);
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
        uint lineLen = _buffer.LineWidthAt(_cursorLine);

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
        uint available = totalVisual - _scrollTop;
        return _height > 0 ? Math.Min(available, _height) : available;
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
        // result.Col is a display column; GetOffset expects a char index
        uint charCol = (uint)_buffer.DisplayColToCharIndex(result.Line, result.Col);
        uint offset = _buffer.GetOffset(result.Line, charCol);
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

        // logicalCol is a display column; convert to char index for GetOffset
        uint charCol = (uint)_buffer.DisplayColToCharIndex(logicalLine, logicalCol);
        uint offset = _buffer.GetOffset(logicalLine, charCol);
        return new VisualCursor(vpRow, vpCol, logicalLine, logicalCol, offset);
    }

    #endregion

    #region Measurement

    /// <summary>
    /// Measures text content to fit within the given dimensions.
    /// Temporarily sets viewport to w×h, counts virtual lines, and returns effective dimensions.
    /// Returns true if content overflows (scrollable).
    /// </summary>
    public bool MeasureForDimensions(uint w, uint h, out MeasureResult result)
    {
        // Save current state
        uint savedWidth = _width;
        uint savedHeight = _height;
        uint savedWrapWidth = _wrapWidth;

        // When w=0, use a very large width so lines are not wrapped/truncated
        uint measureWidth = w > 0 ? w : uint.MaxValue / 2;

        // Temporarily set viewport for measurement
        _width = measureWidth;
        _height = h;
        _wrapWidth = 0; // measure uses viewport width directly
        InvalidateWrapCache();

        try
        {
            UpdateVirtualLines();
            var virtualLines = GetVirtualLines();

            uint totalVirtualLines = (uint)virtualLines.Length;
            uint maxWidth = 0;

            for (int i = 0; i < virtualLines.Length; i++)
            {
                uint lineWidth = virtualLines[i].WidthCols;
                if (lineWidth > maxWidth)
                    maxWidth = lineWidth;
            }

            // Report intrinsic content size — don't cap at viewport width
            uint effectiveHeight = Math.Min(totalVirtualLines, h);

            result = new MeasureResult(effectiveHeight, maxWidth);
            return true; // measurement always succeeds
        }
        finally
        {
            // Restore
            _width = savedWidth;
            _height = savedHeight;
            _wrapWidth = savedWrapWidth;
            InvalidateWrapCache();
        }
    }

    #endregion

    #region Line Info

    /// <summary>
    /// Gets virtual line layout information for the current viewport.
    /// Returns line starts, widths, sources, and wrap markers for visible lines only.
    /// </summary>
    public LineInfo GetLineInfo()
    {
        UpdateVirtualLines();
        var virtualLines = GetVirtualLines();

        uint firstVisible = _scrollTop;
        uint visibleCount = GetVisibleLineCount();

        var startCols = new uint[visibleCount];
        var widthCols = new uint[visibleCount];
        var sources = new uint[visibleCount];
        var wraps = new uint[visibleCount];
        uint maxWidth = 0;

        for (uint i = 0; i < visibleCount; i++)
        {
            uint vlineIdx = firstVisible + i;
            if (vlineIdx >= (uint)virtualLines.Length) break;

            ref readonly var vl = ref virtualLines[vlineIdx];
            startCols[i] = vl.SourceColOffset;
            widthCols[i] = vl.WidthCols;
            sources[i] = vl.SourceLine;
            wraps[i] = vl.SourceColOffset > 0 ? 1u : 0u;

            if (vl.WidthCols > maxWidth)
                maxWidth = vl.WidthCols;
        }

        return new LineInfo
        {
            LineStartCols = startCols,
            LineWidthCols = widthCols,
            LineSources = sources,
            LineWraps = wraps,
            LineWidthColsMax = maxWidth,
        };
    }

    /// <summary>
    /// Gets logical (full-document) line layout information.
    /// Unlike <see cref="GetLineInfo"/> which returns viewport-only data,
    /// this returns mapping for all virtual lines in the document.
    /// </summary>
    public LineInfo GetLogicalLineInfo()
    {
        UpdateVirtualLines();
        var virtualLines = GetVirtualLines();

        int count = virtualLines.Length;
        var startCols = new uint[count];
        var widthCols = new uint[count];
        var sources = new uint[count];
        var wraps = new uint[count];
        uint maxWidth = 0;

        for (int i = 0; i < count; i++)
        {
            ref readonly var vl = ref virtualLines[i];
            startCols[i] = vl.SourceColOffset;
            widthCols[i] = vl.WidthCols;
            sources[i] = vl.SourceLine;
            wraps[i] = vl.SourceColOffset > 0 ? 1u : 0u;

            if (vl.WidthCols > maxWidth)
                maxWidth = vl.WidthCols;
        }

        return new LineInfo
        {
            LineStartCols = startCols,
            LineWidthCols = widthCols,
            LineSources = sources,
            LineWraps = wraps,
            LineWidthColsMax = maxWidth,
        };
    }

    #endregion

    #region Text Access

    /// <summary>
    /// Gets the plain text visible in the viewport as a string.
    /// Iterates visible virtual lines and concatenates their text content.
    /// </summary>
    public string GetPlainText(int maxLen = 64 * 1024)
    {
        UpdateVirtualLines();
        var virtualLines = GetVirtualLines();

        uint firstVisible = _scrollTop;
        uint visibleCount = GetVisibleLineCount();

        var sb = new StringBuilder();
        for (uint i = 0; i < visibleCount; i++)
        {
            uint vlineIdx = firstVisible + i;
            if (vlineIdx >= (uint)virtualLines.Length) break;

            ref readonly var vl = ref virtualLines[vlineIdx];
            if (vl.SourceLine >= _buffer.LineCount) break;

            string lineText = _buffer.GetLineText(vl.SourceLine);
            int startCol = (int)vl.SourceColOffset;
            // Determine end char index from the next virtual line's SourceColOffset
            // (WidthCols is display width, not char count)
            int endCol;
            uint nextIdx = vlineIdx + 1;
            if (nextIdx < (uint)virtualLines.Length && virtualLines[nextIdx].SourceLine == vl.SourceLine)
                endCol = (int)virtualLines[nextIdx].SourceColOffset;
            else
                endCol = lineText.Length;
            if (startCol < lineText.Length)
            {
                int len = Math.Min(endCol, lineText.Length) - startCol;
                if (len > 0)
                {
                    sb.Append(lineText.AsSpan(startCol, len));
                }
            }

            // Only insert \n between different source lines (not between wrapped segments)
            if (i < visibleCount - 1)
            {
                uint nextVlineIdx = vlineIdx + 1;
                if (nextVlineIdx < (uint)virtualLines.Length)
                {
                    ref readonly var nextVl = ref virtualLines[nextVlineIdx];
                    if (nextVl.SourceLine != vl.SourceLine)
                        sb.Append('\n');
                }
            }

            if (sb.Length >= maxLen)
            {
                sb.Length = maxLen;
                break;
            }
        }

        return sb.ToString();
    }

    #endregion

    #region Local Selection

    /// <summary>
    /// Sets a local (visual coordinate) selection. Converts screen-space x,y to buffer offsets.
    /// Stores the anchor offset for use by subsequent UpdateLocalSelection calls.
    /// Returns true if the selection was set successfully.
    /// </summary>
    public bool SetLocalSelection(int startX, int startY, int endX, int endY, Rgba? selBg = null, Rgba? selFg = null)
    {
        if (!TryVisualToOffset(startX, startY, out uint anchorOffset) ||
            !TryVisualToOffset(endX, endY, out uint focusOffset))
        {
            _selectionAnchorOffset = null;
            return false;
        }

        _selectionAnchorOffset = anchorOffset;
        uint start = Math.Min(anchorOffset, focusOffset);
        uint end = Math.Max(anchorOffset, focusOffset);
        SetSelection(start, end, selBg, selFg);
        return true;
    }

    /// <summary>
    /// Updates the local (visual coordinate) selection extent.
    /// Preserves the anchor from the initial SetLocalSelection call;
    /// only the focus (end) point is updated. Falls back to SetLocalSelection
    /// when no anchor exists yet.
    /// Returns true if the selection was updated successfully.
    /// </summary>
    public bool UpdateLocalSelection(int startX, int startY, int endX, int endY, Rgba? selBg = null, Rgba? selFg = null)
    {
        if (_selectionAnchorOffset is not { } anchorOffset)
            return SetLocalSelection(startX, startY, endX, endY, selBg, selFg);

        if (!TryVisualToOffset(endX, endY, out uint focusOffset))
            return false;

        uint start = Math.Min(anchorOffset, focusOffset);
        uint end = Math.Max(anchorOffset, focusOffset);

        // When focus is before anchor (backward selection), extend end by 1
        // to include the anchor character (matching Zig reference behavior).
        if (focusOffset < anchorOffset)
        {
            uint textEnd = _buffer.Length;
            end = Math.Min(end + 1, textEnd);
        }

        SetSelection(start, end, selBg, selFg);
        return true;
    }

    /// <summary>Resets the local (visual coordinate) selection.</summary>
    public void ResetLocalSelection()
    {
        _selectionAnchorOffset = null;
        ResetSelection();
    }

    /// <summary>
    /// Updates the end offset of the current selection, keeping the start.
    /// If no selection exists, this is a no-op.
    /// </summary>
    public void UpdateSelection(uint newEnd, Rgba? selBg = null, Rgba? selFg = null)
    {
        if (_selectionStartOffset is null)
            return;

        _selectionEndOffset = newEnd;
        if (selBg.HasValue) _selectionBg = selBg;
        if (selFg.HasValue) _selectionFg = selFg;
    }

    /// <summary>
    /// Gets the current selection info as a packed 64-bit value.
    /// Start in upper 32 bits, end in lower 32 bits.
    /// Returns 0xFFFFFFFF_FFFFFFFF if no selection is active or selection is zero-width.
    /// </summary>
    public ulong GetSelectionInfo()
    {
        var range = GetSelectionRange();
        if (range is null)
            return 0xFFFF_FFFF_FFFF_FFFFUL;

        var (start, end) = range.Value;
        if (start == end)
            return 0xFFFF_FFFF_FFFF_FFFFUL;

        return ((ulong)start << 32) | end;
    }

    /// <summary>
    /// Converts viewport-relative visual coordinates to a display-column-based buffer offset.
    /// </summary>
    private bool TryVisualToOffset(int visualX, int visualY, out uint offset)
    {
        offset = 0;
        if (visualX < 0 || visualY < 0)
            return false;

        var raw = VisualToLogicalRaw((uint)visualY + _scrollTop, (uint)visualX + _scrollLeft);
        offset = _buffer.GetDisplayOffset(raw.Line, raw.Col);
        return true;
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
            && _virtualLineArrayWidth == EffectiveWrapWidth)
        {
            return;
        }

        RebuildVirtualLineArray();
        _virtualLineArrayVersion = version;
        _virtualLineArrayWidth = EffectiveWrapWidth;
    }

    private void RebuildVirtualLineArray()
    {
        if (_wrapMode == WrapMode.None)
        {
            uint lineCount = _buffer.LineCount;
            var result = new VirtualLine[lineCount];
            for (uint i = 0; i < lineCount; i++)
                result[i] = new VirtualLine(i, 0, _buffer.LineWidthAt(i));
            _virtualLineArray = result;
            return;
        }

        EnsureWrapCache();
        var cache = _wrapLineCache!;
        var arr = new VirtualLine[cache.Count];
        for (int i = 0; i < cache.Count; i++)
        {
            var wl = cache[i];
            // ColCount is char count; convert to display width for VirtualLine.WidthCols
            // StartCol is also char count; convert to display column for VirtualLine.SourceColOffset
            string lineText = _buffer.GetLineText(wl.LogicalLine);
            uint sourceDisplayCol = CalculateWidthFromCol(lineText, 0, wl.StartCol);
            uint displayWidth = CalculateWidthFromCol(lineText, wl.StartCol, wl.StartCol + wl.ColCount);
            arr[i] = new VirtualLine(wl.LogicalLine, sourceDisplayCol, displayWidth);
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

    /// <summary>The effective width used for wrapping: _wrapWidth if set, otherwise _width.</summary>
    private uint EffectiveWrapWidth => _wrapWidth > 0 ? _wrapWidth : _width;

    private void InvalidateWrapCache()
    {
        _wrapLineCache = null;
        _virtualLineArray = null;
    }

    private void EnsureWrapCache()
    {
        ulong bufferVersion = _buffer.Version;
        uint wrapWidth = EffectiveWrapWidth;
        if (_wrapLineCache is not null
            && _wrapCacheVersion == bufferVersion
            && _wrapCacheWidth == wrapWidth)
        {
            return;
        }

        RebuildWrapCache();
        _wrapCacheVersion = bufferVersion;
        _wrapCacheWidth = wrapWidth;
    }

    private void RebuildWrapCache()
    {
        uint lineCount = _buffer.LineCount;
        uint wrapWidth = EffectiveWrapWidth;
        _wrapLineCache = new List<WrapLine>((int)lineCount);

        if (wrapWidth == 0)
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
                WrapLineByChar(line, lineText, wrapWidth);
            }
            else // WrapMode.Word
            {
                WrapLineByWord(line, lineText, wrapWidth);
            }
        }
    }

    private void WrapLineByChar(uint logicalLine, string lineText, uint wrapWidth)
    {
        uint col = 0;
        uint displayWidth = TextWidth.CalculateTextWidth(lineText.AsSpan(), _buffer.TabWidth, _buffer.WidthMethod);

        if (displayWidth <= wrapWidth)
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

            if (currentWidth + charWidth > wrapWidth && currentWidth > 0)
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

    private void WrapLineByWord(uint logicalLine, string lineText, uint wrapWidth)
    {
        // Use WrapBreaks to find break opportunities
        byte[] utf8Bytes = Encoding.UTF8.GetBytes(lineText);
        var breaks = new List<WrapBreaks.WrapBreak>();
        WrapBreaks.FindWrapBreaks(utf8Bytes, breaks, _buffer.WidthMethod);

        if (breaks.Count == 0)
        {
            // No break opportunities; fall back to char wrapping
            WrapLineByChar(logicalLine, lineText, wrapWidth);
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

            if (currentWidth + charWidth > wrapWidth && currentWidth > 0)
            {
                // Need to wrap
                if (haveBreak && lastBreakCol > startCol)
                {
                    // Wrap at the last break opportunity.
                    // If the break char itself caused the overflow, don't include it on this line.
                    uint wrapCol = (lastBreakCol < col) ? lastBreakCol + 1 : lastBreakCol;
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
            uint lineLen = _buffer.LineWidthAt(line);
            uint col = Math.Min(visualCol, lineLen);
            return (line, col);
        }

        EnsureWrapCache();
        var cache = _wrapLineCache!;

        if (cache.Count == 0)
            return (0, 0);

        uint idx = Math.Min(visualRow, (uint)(cache.Count - 1));
        var wl = cache[(int)idx];

        // wl.StartCol is a char index; visualCol is a display column offset.
        // Convert wl.StartCol to a display column, then add visualCol.
        uint wlStartDisplayCol = _buffer.CharIndexToDisplayCol(wl.LogicalLine, (int)wl.StartCol);
        uint logicalDisplayCol = wlStartDisplayCol + visualCol;
        uint lineWidth = _buffer.LineWidthAt(wl.LogicalLine);
        logicalDisplayCol = Math.Min(logicalDisplayCol, lineWidth);

        return (wl.LogicalLine, logicalDisplayCol);
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
