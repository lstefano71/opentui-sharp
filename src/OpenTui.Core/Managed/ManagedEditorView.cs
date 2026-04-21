namespace OpenTui.Core.Managed;

/// <summary>
/// Visual cursor with both viewport-relative and document-absolute coordinates.
/// Mirrors the Zig <c>VisualCursor</c> from <c>editor-view.zig</c>.
/// </summary>
public readonly record struct EditorVisualCursor(
    uint VisualRow,    // Viewport-relative row (0 = top of viewport)
    uint VisualCol,    // Viewport-relative column
    uint LogicalRow,   // Document-absolute line
    uint LogicalCol,   // Document-absolute column
    uint Offset);      // Global display-width offset from buffer start

/// <summary>
/// Pure C# implementation of the Zig <c>EditorView</c> (~657 lines in editor-view.zig).
///
/// <para>Wraps a <see cref="ManagedTextBufferView"/> (viewport into text) and references a
/// <see cref="ManagedEditBuffer"/> (editing operations with undo). Provides cursor visibility
/// with scroll margins, sticky visual column for vertical movement, selection handling,
/// placeholder text, and logical↔visual coordinate conversion.</para>
/// </summary>
public sealed class ManagedEditorView : IDisposable
{
    #region Fields

    private readonly ManagedTextBufferView _view;
    private readonly ManagedEditBuffer _editBuffer;
    private float _scrollMargin = 0.15f;   // 15% of viewport height
    private uint? _desiredVisualCol;        // Sticky column for vertical movement
    private bool _selectionFollowCursor;
    private bool _disposed;

    // Placeholder
    private ManagedTextBuffer? _placeholderBuffer;
    private bool _placeholderActive;

    // Selection anchor (offset in buffer when selection started)
    private uint? _selectionAnchorOffset;

    #endregion

    #region Construction

    private ManagedEditorView(ManagedEditBuffer editBuffer, ManagedTextBufferView view)
    {
        _editBuffer = editBuffer;
        _view = view;

        // Listen for cursor changes so we can reset the desired visual column
        // and ensure visibility eagerly.
        _editBuffer.CursorChanged += OnCursorChanged;
    }

    /// <summary>Creates a new editor view for the given edit buffer.</summary>
    public static ManagedEditorView Create(
        ManagedEditBuffer editBuffer,
        uint viewportWidth,
        uint viewportHeight)
    {
        ArgumentNullException.ThrowIfNull(editBuffer);

        var textBuffer = editBuffer.GetTextBuffer();
        var view = ManagedTextBufferView.Create(textBuffer, viewportWidth, viewportHeight);

        return new ManagedEditorView(editBuffer, view);
    }

    #endregion

    #region Properties

    /// <summary>Gets the underlying text buffer view.</summary>
    public ManagedTextBufferView View
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _view;
        }
    }

    /// <summary>Gets the underlying edit buffer.</summary>
    public ManagedEditBuffer EditBuffer
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _editBuffer;
        }
    }

    /// <summary>
    /// Gets or sets the scroll margin as a fraction of viewport height (0.0–0.5).
    /// The cursor will stay at least this many proportional lines from the top/bottom edges.
    /// </summary>
    public float ScrollMargin
    {
        get => _scrollMargin;
        set => _scrollMargin = Math.Clamp(value, 0f, 0.5f);
    }

    /// <summary>
    /// When true, the viewport follows the cursor even during selection.
    /// When false, scroll position is only adjusted when there is no active selection.
    /// </summary>
    public bool SelectionFollowCursor
    {
        get => _selectionFollowCursor;
        set => _selectionFollowCursor = value;
    }

    /// <summary>Gets the viewport width in columns.</summary>
    public uint ViewportWidth => _view.Width;

    /// <summary>Gets the viewport height in rows.</summary>
    public uint ViewportHeight => _view.Height;

    /// <summary>Returns true when the main buffer has no content and a placeholder is set.</summary>
    public bool IsPlaceholderActive => _placeholderActive;

    /// <summary>Gets the placeholder buffer, if one has been set.</summary>
    public ManagedTextBuffer? PlaceholderBuffer => _placeholderBuffer;

    /// <summary>Whether undo is available.</summary>
    public bool CanUndo => _editBuffer.CanUndo;

    /// <summary>Whether redo is available.</summary>
    public bool CanRedo => _editBuffer.CanRedo;

    /// <summary>Gets or sets the line wrap mode.</summary>
    public WrapMode WrapMode
    {
        get => _view.WrapMode;
        set => _view.WrapMode = value;
    }

    #endregion

    #region Viewport

    /// <summary>Resizes the viewport and clamps scroll offset / cursor visibility.</summary>
    public void Resize(uint width, uint height)
    {
        _view.SetViewportSize(width, height);

        // After resize, clamp offset so it doesn't exceed total lines
        var totalLines = _view.GetVirtualLineCount();
        var maxOffset = totalLines > height ? totalLines - height : 0u;
        var offset = GetScrollOffset();

        var offsetX = _view.ViewportX;
        if (_view.WrapMode == WrapMode.None)
        {
            var maxLineWidth = _editBuffer.GetMaxLineWidth();
            var maxOffsetX = maxLineWidth > width ? maxLineWidth - width : 0u;
            if (offsetX > maxOffsetX)
                offsetX = maxOffsetX;
        }

        if (offset > maxOffset || offsetX != _view.ViewportX)
        {
            _view.SetViewport(offsetX, Math.Min(offset, maxOffset), width, height);
        }

        // Ensure cursor stays visible after resize
        var (line, col) = GetLogicalCursor();
        var vcursor = LogicalToVisualCursor(line, col);
        EnsureCursorVisibleCore(vcursor.VisualRow);
    }

    /// <summary>Sets the scroll offset (viewport Y) in virtual rows.</summary>
    public void SetScrollOffset(uint row) =>
        _view.SetViewport(_view.ViewportX, row, _view.Width, _view.Height);

    /// <summary>Gets the current scroll offset (viewport Y) in virtual rows.</summary>
    public uint GetScrollOffset() => _view.ViewportY;

    #endregion

    #region Cursor

    /// <summary>
    /// Gets the visual cursor position, viewport-relative, after ensuring visibility.
    /// </summary>
    public EditorVisualCursor GetVisualCursor()
    {
        UpdateBeforeRender();

        var (line, col) = _editBuffer.GetPrimaryCursor();
        var abs = LogicalToVisualCursor(line, col);

        // Convert absolute to viewport-relative
        var vpY = _view.ViewportY;
        var vpX = _view.ViewportX;

        var relativeRow = abs.VisualRow >= vpY ? abs.VisualRow - vpY : 0u;
        var relativeCol = _view.WrapMode == WrapMode.None
            ? (abs.VisualCol >= vpX ? abs.VisualCol - vpX : 0u)
            : abs.VisualCol;

        return new EditorVisualCursor(relativeRow, relativeCol, abs.LogicalRow, abs.LogicalCol, abs.Offset);
    }

    /// <summary>Gets the logical (document-absolute) cursor position.</summary>
    public (uint Line, uint Col) GetLogicalCursor() => _editBuffer.GetPrimaryCursor();

    /// <summary>Sets the cursor to a logical position.</summary>
    public void SetCursor(uint line, uint col)
    {
        _editBuffer.SetCursor(line, col);
    }

    #endregion

    #region Cursor Movement (visual-aware)

    /// <summary>Moves the cursor up by visual rows, preserving the sticky column.</summary>
    public void MoveUp(uint count = 1)
    {
        for (uint i = 0; i < count; i++)
            MoveUpVisualOnce();
    }

    /// <summary>Moves the cursor down by visual rows, preserving the sticky column.</summary>
    public void MoveDown(uint count = 1)
    {
        for (uint i = 0; i < count; i++)
            MoveDownVisualOnce();
    }

    /// <summary>Moves the cursor left by the given number of positions.</summary>
    public void MoveLeft(uint count = 1)
    {
        for (uint i = 0; i < count; i++)
            _editBuffer.MoveLeft();
    }

    /// <summary>Moves the cursor right by the given number of positions.</summary>
    public void MoveRight(uint count = 1)
    {
        for (uint i = 0; i < count; i++)
            _editBuffer.MoveRight();
    }

    /// <summary>Moves the cursor to the start of the current visual line.</summary>
    public void MoveToLineStart()
    {
        var sol = GetVisualSOL();
        _editBuffer.SetCursor(sol.LogicalRow, sol.LogicalCol);
    }

    /// <summary>Moves the cursor to the end of the current visual line.</summary>
    public void MoveToLineEnd()
    {
        var eol = GetVisualEOL();
        _editBuffer.SetCursor(eol.LogicalRow, eol.LogicalCol);
    }

    /// <summary>Moves the cursor to the beginning of the document.</summary>
    public void MoveToDocumentStart() => _editBuffer.SetCursor(0, 0);

    /// <summary>Moves the cursor to the end of the document.</summary>
    public void MoveToDocumentEnd()
    {
        var lineCount = _editBuffer.GetLineCount();
        if (lineCount == 0)
        {
            _editBuffer.SetCursor(0, 0);
            return;
        }
        var lastLine = lineCount - 1;
        var lastCol = _editBuffer.GetLineWidth(lastLine);
        _editBuffer.SetCursor(lastLine, lastCol);
    }

    /// <summary>Moves the cursor to the previous word boundary.</summary>
    public void MoveWordLeft()
    {
        var boundary = _editBuffer.GetPrevWordBoundary();
        _editBuffer.SetCursor(boundary.Line, boundary.Col);
    }

    /// <summary>Moves the cursor to the next word boundary.</summary>
    public void MoveWordRight()
    {
        var boundary = _editBuffer.GetNextWordBoundary();
        _editBuffer.SetCursor(boundary.Line, boundary.Col);
    }

    /// <summary>Moves the cursor up by one viewport page.</summary>
    public void MovePageUp()
    {
        var height = ViewportHeight;
        if (height == 0) return;
        MoveUp(height > 1 ? height - 1 : 1);
    }

    /// <summary>Moves the cursor down by one viewport page.</summary>
    public void MovePageDown()
    {
        var height = ViewportHeight;
        if (height == 0) return;
        MoveDown(height > 1 ? height - 1 : 1);
    }

    #endregion

    #region Editing (delegates to EditBuffer)

    /// <summary>Inserts text at the current cursor position.</summary>
    public void InsertText(string text) => _editBuffer.InsertText(text);

    /// <summary>Inserts a newline at the current cursor position.</summary>
    public void InsertNewline() => _editBuffer.InsertText("\n");

    /// <summary>Deletes the character after the cursor (Delete key).</summary>
    public void DeleteForward() => _editBuffer.DeleteForward();

    /// <summary>Deletes the character before the cursor (Backspace).</summary>
    public void DeleteBackward() => _editBuffer.DeleteBackward();

    /// <summary>Deletes the current line.</summary>
    public void DeleteLine() => _editBuffer.DeleteLine();

    /// <summary>Deletes from cursor to the previous word boundary.</summary>
    public void DeleteWordLeft() => _editBuffer.DeleteWordLeft();

    /// <summary>Deletes from cursor to the next word boundary.</summary>
    public void DeleteWordRight() => _editBuffer.DeleteWordRight();

    #endregion

    #region Selection

    /// <summary>
    /// Begins a selection at the current cursor position.
    /// Subsequent cursor movements can extend the selection via <see cref="ExtendSelection"/>.
    /// </summary>
    public void StartSelection()
    {
        var (line, col) = _editBuffer.GetPrimaryCursor();
        _selectionAnchorOffset = _editBuffer.CoordsToOffset(line, col);
    }

    /// <summary>
    /// Extends the selection from the anchor to the current cursor position.
    /// Call <see cref="StartSelection"/> first to set the anchor.
    /// </summary>
    public void ExtendSelection()
    {
        if (_selectionAnchorOffset is not { } anchor) return;

        var (line, col) = _editBuffer.GetPrimaryCursor();
        var cursorOffset = _editBuffer.CoordsToOffset(line, col);
        if (cursorOffset is not { } end) return;

        var start = Math.Min(anchor, end);
        var stop = Math.Max(anchor, end);
        _view.SetSelection(start, stop, selBg: null, selFg: null);
    }

    /// <summary>Clears any active selection.</summary>
    public void ClearSelection()
    {
        _selectionAnchorOffset = null;
        _view.ResetSelection();
    }

    /// <summary>Selects all text in the buffer.</summary>
    public void SelectAll()
    {
        var totalLength = _editBuffer.GetTotalLength();
        if (totalLength == 0) return;

        _selectionAnchorOffset = 0;
        _view.SetSelection(0, totalLength, selBg: null, selFg: null);
    }

    /// <summary>Selects the word under the cursor.</summary>
    public void SelectWord()
    {
        var prev = _editBuffer.GetPrevWordBoundary();
        var next = _editBuffer.GetNextWordBoundary();

        var startOffset = _editBuffer.CoordsToOffset(prev.Line, prev.Col);
        var endOffset = _editBuffer.CoordsToOffset(next.Line, next.Col);
        if (startOffset is null || endOffset is null) return;

        _selectionAnchorOffset = startOffset.Value;
        _view.SetSelection(startOffset.Value, endOffset.Value, selBg: null, selFg: null);
    }

    /// <summary>Selects the current line.</summary>
    public void SelectLine()
    {
        var (line, _) = _editBuffer.GetPrimaryCursor();
        var lineStart = _editBuffer.CoordsToOffset(line, 0);
        if (lineStart is null) return;

        var lineWidth = _editBuffer.GetLineWidth(line);
        var lineEnd = _editBuffer.CoordsToOffset(line, lineWidth);
        if (lineEnd is null) return;

        // Include the newline character if there is a next line
        var lineCount = _editBuffer.GetLineCount();
        uint endOffset;
        if (line + 1 < lineCount)
        {
            var nextLineStart = _editBuffer.CoordsToOffset(line + 1, 0);
            endOffset = nextLineStart ?? lineEnd.Value;
        }
        else
        {
            endOffset = lineEnd.Value;
        }

        _selectionAnchorOffset = lineStart.Value;
        _view.SetSelection(lineStart.Value, endOffset, selBg: null, selFg: null);
    }

    /// <summary>Gets the currently selected text, or an empty string if nothing is selected.</summary>
    public string GetSelectedText()
    {
        var range = _view.GetSelectionRange();
        if (range is null) return string.Empty;

        var (start, end) = range.Value;
        return _editBuffer.GetTextRange(start, end);
    }

    /// <summary>Deletes the currently selected text and clears the selection.</summary>
    public void DeleteSelection()
    {
        var range = _view.GetSelectionRange();
        if (range is null) return;

        var (start, end) = range.Value;

        var startCoords = _editBuffer.OffsetToCoords(start);
        var endCoords = _editBuffer.OffsetToCoords(end);
        if (startCoords is null || endCoords is null) return;

        _editBuffer.DeleteRange(startCoords.Value.Line, startCoords.Value.Col,
                                endCoords.Value.Line, endCoords.Value.Col);
        // Move cursor to start of deleted range
        _editBuffer.SetCursor(startCoords.Value.Line, startCoords.Value.Col);
        ClearSelection();
    }

    #endregion

    #region Clipboard

    /// <summary>Cuts the selected text (returns it and deletes it).</summary>
    public string Cut()
    {
        var text = GetSelectedText();
        if (text.Length > 0)
            DeleteSelection();
        return text;
    }

    /// <summary>Copies the selected text (returns it without deleting).</summary>
    public string Copy() => GetSelectedText();

    /// <summary>Pastes text at the current cursor, replacing any selection.</summary>
    public void Paste(string text)
    {
        if (_view.GetSelectionRange() is not null)
            DeleteSelection();
        InsertText(text);
    }

    #endregion

    #region Undo/Redo

    /// <summary>Undoes the last edit operation.</summary>
    public void Undo()
    {
        _editBuffer.Undo();
        UpdateBeforeRender();
    }

    /// <summary>Redoes the last undone edit operation.</summary>
    public void Redo()
    {
        _editBuffer.Redo();
        UpdateBeforeRender();
    }

    #endregion

    #region Coordinate Conversion

    /// <summary>
    /// Converts logical (document-absolute) coordinates to absolute visual coordinates.
    /// Accounts for line wrapping by finding which virtual line contains the position.
    /// </summary>
    public EditorVisualCursor LogicalToVisualCursor(uint logicalLine, uint logicalCol)
    {
        // Clamp to valid buffer ranges
        var lineCount = _editBuffer.GetLineCount();
        var clampedRow = lineCount > 0 ? Math.Min(logicalLine, lineCount - 1) : 0u;
        var lineWidth = _editBuffer.GetLineWidth(clampedRow);
        var clampedCol = Math.Min(logicalCol, lineWidth);

        var visualRowIdx = _view.FindVisualLineIndex(clampedRow, clampedCol);

        var vlines = _view.GetVirtualLines();
        if (vlines.Length == 0 || visualRowIdx >= (uint)vlines.Length)
        {
            var fallbackOffset = _editBuffer.CoordsToOffset(clampedRow, clampedCol) ?? 0;
            return new EditorVisualCursor(0, 0, clampedRow, clampedCol, fallbackOffset);
        }

        ref readonly var vline = ref vlines[(int)visualRowIdx];
        var vlineStartCol = vline.SourceColOffset;

        var visualCol = clampedCol >= vlineStartCol ? clampedCol - vlineStartCol : 0u;
        var offset = _editBuffer.CoordsToOffset(clampedRow, clampedCol) ?? 0;

        return new EditorVisualCursor(visualRowIdx, visualCol, clampedRow, clampedCol, offset);
    }

    /// <summary>
    /// Converts absolute visual coordinates to a logical cursor position.
    /// Returns null if the visual row is beyond the virtual line count.
    /// </summary>
    public (uint Line, uint Col)? VisualToLogicalCursor(uint visualRow, uint visualCol)
    {
        _view.UpdateVirtualLines();

        var vlines = _view.GetVirtualLines();
        if (visualRow >= (uint)vlines.Length) return null;

        ref readonly var vline = ref vlines[(int)visualRow];
        var clampedVisualCol = Math.Min(visualCol, vline.WidthCols);
        var logicalCol = vline.SourceColOffset + clampedVisualCol;
        var logicalRow = vline.SourceLine;

        return (logicalRow, logicalCol);
    }

    #endregion

    #region Placeholder

    /// <summary>Sets the placeholder text shown when the buffer is empty.</summary>
    public void SetPlaceholder(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            ClearPlaceholder();
            return;
        }

        _placeholderBuffer ??= ManagedTextBuffer.Create(_editBuffer.WidthMethod);
        _placeholderBuffer.SetText(text);
        UpdatePlaceholderVisibility();
    }

    /// <summary>Clears the placeholder text.</summary>
    public void ClearPlaceholder()
    {
        if (_placeholderActive)
        {
            _view.SwitchToOriginalBuffer();
            _placeholderActive = false;
        }

        if (_placeholderBuffer is { } buf)
        {
            buf.Dispose();
            _placeholderBuffer = null;
        }
    }

    #endregion

    #region Scroll

    /// <summary>
    /// Ensures the cursor is visible within the viewport by adjusting scroll offsets.
    /// This is the public API — internally calls <see cref="EnsureCursorVisibleCore"/>.
    /// </summary>
    public void EnsureCursorVisible()
    {
        var (line, col) = GetLogicalCursor();
        var vcursor = LogicalToVisualCursor(line, col);
        EnsureCursorVisibleCore(vcursor.VisualRow);
    }

    /// <summary>Scrolls the viewport by the given number of rows (positive = down).</summary>
    public void ScrollBy(int deltaRows)
    {
        var current = GetScrollOffset();
        var newOffset = (int)current + deltaRows;
        SetScrollOffset(newOffset < 0 ? 0 : (uint)newOffset);
    }

    #endregion

    #region Visual Line Helpers (SOL / EOL)

    /// <summary>
    /// Gets the start of the current visual line (SOL).
    /// For wrapped lines this is the start column of the current wrap segment.
    /// </summary>
    internal EditorVisualCursor GetVisualSOL()
    {
        var (line, col) = _editBuffer.GetPrimaryCursor();
        var vcursor = LogicalToVisualCursor(line, col);

        _view.UpdateVirtualLines();
        var vlines = _view.GetVirtualLines();

        if (vcursor.VisualRow >= (uint)vlines.Length)
        {
            var fallbackOffset = _editBuffer.CoordsToOffset(line, 0) ?? 0;
            return new EditorVisualCursor(vcursor.VisualRow, 0, line, 0, fallbackOffset);
        }

        ref readonly var vline = ref vlines[(int)vcursor.VisualRow];
        var logicalCol = vline.SourceColOffset;
        var logicalRow = vline.SourceLine;
        var offset = _editBuffer.CoordsToOffset(logicalRow, logicalCol) ?? 0;

        return new EditorVisualCursor(vcursor.VisualRow, 0, logicalRow, logicalCol, offset);
    }

    /// <summary>
    /// Gets the end of the current visual line (EOL).
    /// For wrapped lines, returns one position before the wrap boundary to stay on
    /// the current visual line.
    /// </summary>
    internal EditorVisualCursor GetVisualEOL()
    {
        var (line, col) = _editBuffer.GetPrimaryCursor();
        var vcursor = LogicalToVisualCursor(line, col);

        _view.UpdateVirtualLines();
        var vlines = _view.GetVirtualLines();

        if (vcursor.VisualRow >= (uint)vlines.Length)
        {
            // Fallback: end of current logical line
            var eolCol = _editBuffer.GetLineWidth(line);
            return LogicalToVisualCursor(line, eolCol);
        }

        ref readonly var vline = ref vlines[(int)vcursor.VisualRow];
        var logicalRow = vline.SourceLine;
        uint logicalCol;

        if (vcursor.VisualRow + 1 < (uint)vlines.Length)
        {
            ref readonly var nextVline = ref vlines[(int)(vcursor.VisualRow + 1)];
            if (nextVline.SourceLine == vline.SourceLine)
            {
                // Next visual line is a continuation — stay before the wrap boundary
                logicalCol = vline.WidthCols > 0
                    ? vline.SourceColOffset + vline.WidthCols - 1
                    : vline.SourceColOffset;
            }
            else
            {
                // Different logical line — use full line width
                logicalCol = _editBuffer.GetLineWidth(logicalRow);
            }
        }
        else
        {
            // Last visual line
            logicalCol = _editBuffer.GetLineWidth(logicalRow);
        }

        return LogicalToVisualCursor(logicalRow, logicalCol);
    }

    #endregion

    #region Internal Helpers

    /// <summary>
    /// Called when the edit buffer's cursor changes.
    /// Resets the sticky column and ensures cursor visibility.
    /// </summary>
    private void OnCursorChanged()
    {
        _desiredVisualCol = null;
        UpdatePlaceholderVisibility();

        var hasSelection = _view.GetSelectionRange() is not null;
        if (!hasSelection || _selectionFollowCursor)
        {
            var (line, col) = _editBuffer.GetPrimaryCursor();
            var vcursor = LogicalToVisualCursor(line, col);
            EnsureCursorVisibleCore(vcursor.VisualRow);
        }
    }

    /// <summary>
    /// Ensures the given absolute visual row is visible in the viewport,
    /// adjusting both Y and X offsets as needed.
    /// </summary>
    private void EnsureCursorVisibleCore(uint cursorVisualRow)
    {
        var vpY = _view.ViewportY;
        var vpX = _view.ViewportX;
        var vpWidth = _view.Width;
        var vpHeight = _view.Height;

        if (vpHeight == 0 || vpWidth == 0) return;

        // Compute row margin (clamped so it doesn't exceed half the viewport)
        var rawMarginRows = Math.Max(1u, (uint)(vpHeight * _scrollMargin));
        var maxMarginRows = vpHeight > 1 ? (vpHeight - 1) / 2 : 0u;
        var marginRows = Math.Min(rawMarginRows, maxMarginRows);

        // Compute column margin
        var rawMarginCols = Math.Max(1u, (uint)(vpWidth * _scrollMargin));
        var maxMarginCols = vpWidth > 1 ? (vpWidth - 1) / 2 : 0u;
        var marginCols = Math.Min(rawMarginCols, maxMarginCols);

        var totalLines = _view.GetVirtualLineCount();
        var maxOffsetY = totalLines > vpHeight ? totalLines - vpHeight : 0u;

        var newOffsetY = vpY;
        var newOffsetX = vpX;

        // Vertical scrolling
        if (cursorVisualRow < vpY + marginRows)
        {
            newOffsetY = cursorVisualRow >= marginRows
                ? cursorVisualRow - marginRows
                : 0;
        }
        else if (cursorVisualRow >= vpY + vpHeight - marginRows)
        {
            var desired = cursorVisualRow + marginRows - vpHeight + 1;
            newOffsetY = Math.Min(desired, maxOffsetY);
        }

        // Horizontal scrolling (only when wrapping is off)
        if (_view.WrapMode == WrapMode.None)
        {
            var (_, cursorCol) = _editBuffer.GetPrimaryCursor();

            if (cursorCol < vpX + marginCols)
            {
                newOffsetX = cursorCol >= marginCols ? cursorCol - marginCols : 0;
            }
            else if (cursorCol >= vpX + vpWidth - marginCols)
            {
                newOffsetX = cursorCol + marginCols - vpWidth + 1;
            }
        }

        if (newOffsetY != vpY || newOffsetX != vpX)
        {
            _view.SetViewport(newOffsetX, newOffsetY, vpWidth, vpHeight);
        }
    }

    /// <summary>Moves the cursor up one visual row with sticky column support.</summary>
    private void MoveUpVisualOnce()
    {
        var (line, col) = _editBuffer.GetPrimaryCursor();
        var vcursor = LogicalToVisualCursor(line, col);

        if (vcursor.VisualRow == 0) return;

        var targetVisualRow = vcursor.VisualRow - 1;

        _desiredVisualCol ??= vcursor.VisualCol;
        var desiredCol = _desiredVisualCol.Value;

        var newLogical = VisualToLogicalCursor(targetVisualRow, desiredCol);
        if (newLogical is not null)
        {
            var (newLine, newCol) = newLogical.Value;
            _editBuffer.SetCursor(newLine, newCol);
            EnsureCursorVisibleCore(targetVisualRow);

            // Restore sticky column after cursor-change event resets it
            _desiredVisualCol = desiredCol;
        }
    }

    /// <summary>Moves the cursor down one visual row with sticky column support.</summary>
    private void MoveDownVisualOnce()
    {
        var (line, col) = _editBuffer.GetPrimaryCursor();
        var vcursor = LogicalToVisualCursor(line, col);

        _view.UpdateVirtualLines();
        var vlines = _view.GetVirtualLines();

        if (vcursor.VisualRow + 1 >= (uint)vlines.Length) return;

        var targetVisualRow = vcursor.VisualRow + 1;

        _desiredVisualCol ??= vcursor.VisualCol;
        var desiredCol = _desiredVisualCol.Value;

        var newLogical = VisualToLogicalCursor(targetVisualRow, desiredCol);
        if (newLogical is not null)
        {
            var (newLine, newCol) = newLogical.Value;
            _editBuffer.SetCursor(newLine, newCol);
            EnsureCursorVisibleCore(targetVisualRow);

            // Restore sticky column after cursor-change event resets it
            _desiredVisualCol = desiredCol;
        }
    }

    /// <summary>
    /// Updates placeholder visibility and ensures cursor is visible before rendering.
    /// </summary>
    private void UpdateBeforeRender()
    {
        UpdatePlaceholderVisibility();

        var hasSelection = _view.GetSelectionRange() is not null;
        if (!hasSelection || _selectionFollowCursor)
        {
            var (line, col) = _editBuffer.GetPrimaryCursor();
            var vcursor = LogicalToVisualCursor(line, col);
            EnsureCursorVisibleCore(vcursor.VisualRow);
        }
    }

    /// <summary>
    /// Determines whether the placeholder should be shown or hidden and switches
    /// the text buffer view accordingly.
    /// </summary>
    private void UpdatePlaceholderVisibility()
    {
        var shouldShow = _editBuffer.GetTotalLength() == 0 && _placeholderBuffer is not null;

        if (shouldShow && !_placeholderActive)
        {
            _view.SwitchToBuffer(_placeholderBuffer!);
            _placeholderActive = true;
        }
        else if (!shouldShow && _placeholderActive)
        {
            _view.SwitchToOriginalBuffer();
            _placeholderActive = false;
        }
    }

    #endregion

    #region Dispose

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _editBuffer.CursorChanged -= OnCursorChanged;

        if (_placeholderActive)
        {
            _view.SwitchToOriginalBuffer();
            _placeholderActive = false;
        }

        _placeholderBuffer?.Dispose();
        _placeholderBuffer = null;

        _view.Dispose();
    }

    #endregion
}
