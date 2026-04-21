using OpenTui.Core.Managed;

namespace OpenTui.Core;

/// <summary>
/// Managed wrapper around <see cref="ManagedEditBuffer"/>, providing text editing,
/// cursor movement, word boundaries, undo/redo, and text range queries.
/// </summary>
public sealed class EditBuffer : IDisposable
{
    internal readonly ManagedEditBuffer _managed;
    private bool _disposed;

    private EditBuffer(ManagedEditBuffer managed)
    {
        _managed = managed;
    }

    /// <summary>Creates a new edit buffer with the specified width method.</summary>
    public static EditBuffer Create(byte widthMethod = 0)
    {
        var managed = ManagedEditBuffer.Create((WidthMethod)widthMethod);
        return new EditBuffer(managed);
    }

    #region Text Operations

    /// <summary>Sets the entire text content of the edit buffer.</summary>
    public void SetText(string text) => _managed.SetText(text);

    /// <summary>Sets the text content from a registered memory buffer.</summary>
    public void SetTextFromMem(byte memId) =>
        throw new NotSupportedException("SetTextFromMem requires native handle; use SetText instead.");

    /// <summary>Inserts text at the current cursor position.</summary>
    public void InsertText(string text) => _managed.InsertText(text);

    /// <summary>Inserts a character at the cursor position.</summary>
    public void InsertChar(string ch) => _managed.InsertChar(ch);

    /// <summary>Replaces the current text content.</summary>
    public void ReplaceText(string text) => _managed.ReplaceText(text);

    /// <summary>Replaces the current text content from a registered memory buffer.</summary>
    public void ReplaceTextFromMem(byte memId) =>
        throw new NotSupportedException("ReplaceTextFromMem requires native handle; use ReplaceText instead.");

    /// <summary>Deletes the character at the cursor position (forward delete).</summary>
    public void DeleteChar() => _managed.DeleteForward();

    /// <summary>Deletes the character before the cursor position (backspace).</summary>
    public void DeleteCharBackward() => _managed.DeleteBackward();

    /// <summary>Deletes a range of text specified by row/column coordinates.</summary>
    public void DeleteRange(uint startRow, uint startCol, uint endRow, uint endCol) =>
        _managed.DeleteRange(startRow, startCol, endRow, endCol);

    /// <summary>Deletes the current line.</summary>
    public void DeleteLine() => _managed.DeleteLine();

    /// <summary>Inserts a newline at the cursor position.</summary>
    public void NewLine() => _managed.NewLine();

    /// <summary>Clears all content from the edit buffer.</summary>
    public void Clear() => _managed.Clear();

    /// <summary>Clears the entire undo/redo history.</summary>
    public void ClearHistory() => _managed.ClearHistory();

    #endregion

    #region Cursor

    /// <summary>Gets the current cursor position as a logical row/col/offset.</summary>
    public LogicalCursor GetCursorPosition()
    {
        var (line, col) = _managed.GetPrimaryCursor();
        uint offset = _managed.Buffer.GetOffset(line, col);
        return new LogicalCursor(line, col, offset);
    }

    /// <summary>Sets the cursor to the specified row and column.</summary>
    public void SetCursor(uint row, uint col) => _managed.SetCursor(row, col);

    /// <summary>Sets the cursor to the specified line and column.</summary>
    public void SetCursorToLineCol(uint line, uint col) => _managed.SetCursor(line, col);

    /// <summary>Sets the cursor position by character offset.</summary>
    public void SetCursorByOffset(uint offset) => _managed.SetCursorByOffset(offset);

    #endregion

    #region Movement

    /// <summary>Moves the cursor one position to the left.</summary>
    public void MoveCursorLeft() => _managed.MoveLeft();

    /// <summary>Moves the cursor one position to the right.</summary>
    public void MoveCursorRight() => _managed.MoveRight();

    /// <summary>Moves the cursor one line up.</summary>
    public void MoveCursorUp() => _managed.MoveCursorUp();

    /// <summary>Moves the cursor one line down.</summary>
    public void MoveCursorDown() => _managed.MoveCursorDown();

    /// <summary>Moves the cursor to the specified line.</summary>
    public void GotoLine(uint line) => _managed.GotoLine(line);

    #endregion

    #region Word Boundaries

    /// <summary>Gets the previous word boundary cursor position.</summary>
    public LogicalCursor GetPrevWordBoundary()
    {
        var (line, col) = _managed.GetPrevWordBoundary();
        uint offset = _managed.Buffer.GetOffset(line, col);
        return new LogicalCursor(line, col, offset);
    }

    /// <summary>Gets the next word boundary cursor position.</summary>
    public LogicalCursor GetNextWordBoundary()
    {
        var (line, col) = _managed.GetNextWordBoundary();
        uint offset = _managed.Buffer.GetOffset(line, col);
        return new LogicalCursor(line, col, offset);
    }

    /// <summary>Gets the end-of-line cursor position.</summary>
    public LogicalCursor GetEOL()
    {
        var (line, _) = _managed.GetPrimaryCursor();
        uint lineWidth = _managed.GetLineWidth(line);
        uint offset = _managed.Buffer.GetOffset(line, lineWidth);
        return new LogicalCursor(line, lineWidth, offset);
    }

    #endregion

    #region Undo / Redo

    /// <summary>Undoes the last edit operation. Returns the undo description, or empty if unavailable.</summary>
    public string Undo() => _managed.Undo();

    /// <summary>Redoes the last undone operation. Returns the redo description, or empty if unavailable.</summary>
    public string Redo() => _managed.Redo();

    /// <summary>Gets whether an undo operation is available.</summary>
    public bool CanUndo() => _managed.CanUndo;

    /// <summary>Gets whether a redo operation is available.</summary>
    public bool CanRedo() => _managed.CanRedo;

    #endregion

    #region Text Access

    /// <summary>Gets the entire text content of the buffer.</summary>
    public string GetText() => _managed.GetText();

    /// <summary>Gets the underlying managed text buffer.</summary>
    internal ManagedTextBuffer GetManagedTextBuffer() => _managed.Buffer;

    /// <summary>Gets the unique identifier of the edit buffer.</summary>
    public ushort GetId() => _managed.GetId();

    #endregion

    #region Default Styling

    /// <summary>Sets the default foreground color for unstyled text in the edit buffer.</summary>
    public void SetForeground(Rgba? fg) => _managed.Buffer.DefaultFg = fg;

    /// <summary>Sets the default background color for unstyled text in the edit buffer.</summary>
    public void SetBackground(Rgba? bg) => _managed.Buffer.DefaultBg = bg;

    /// <summary>Sets the default text attributes for unstyled text in the edit buffer.</summary>
    public void SetAttributes(TextAttributes attrs) =>
        _managed.Buffer.DefaultAttributes = (uint)attrs;

    #endregion

    #region Position Conversion

    /// <summary>Converts a character offset to a logical cursor position.</summary>
    public bool OffsetToPosition(uint offset, out LogicalCursor cursor)
    {
        var buf = _managed.Buffer;
        uint lineCount = buf.LineCount;
        uint remaining = offset;
        for (uint i = 0; i < lineCount; i++)
        {
            uint lineLen = buf.GetLineLength(i);
            if (remaining <= lineLen)
            {
                cursor = new LogicalCursor(i, remaining, offset);
                return true;
            }
            remaining -= lineLen + 1; // +1 for newline
        }
        cursor = default;
        return false;
    }

    /// <summary>Converts a row/column position to a character offset.</summary>
    public uint PositionToOffset(uint row, uint col) =>
        _managed.Buffer.GetOffset(row, col);

    /// <summary>Gets the byte offset of the start of the specified line.</summary>
    public uint GetLineStartOffset(uint line) =>
        _managed.Buffer.GetOffset(line, 0);

    /// <summary>Gets a range of text by character offsets.</summary>
    public string GetTextRange(uint start, uint end) =>
        _managed.GetTextRange(start, end);

    /// <summary>Gets a range of text by row/column coordinates.</summary>
    public string GetTextRangeByCoords(uint startRow, uint startCol, uint endRow, uint endCol) =>
        _managed.GetTextRangeByCoords(startRow, startCol, endRow, endCol);

    #endregion

    #region Debug

    /// <summary>Dumps the internal rope structure for debugging (no-op in managed mode).</summary>
    public void DebugLogRope() { }

    #endregion

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _managed.Dispose();
        }
    }

    #endregion
}
