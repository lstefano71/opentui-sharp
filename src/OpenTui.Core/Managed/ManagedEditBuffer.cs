using System.Text;
using OpenTui.Core.Managed.Unicode;

namespace OpenTui.Core.Managed;

/// <summary>
/// Pure C# implementation of the Zig <c>edit-buffer.zig</c>.
/// Wraps a <see cref="ManagedTextBuffer"/> and adds editing operations
/// (insert, delete, undo/redo) that store undo checkpoints automatically.
/// </summary>
public sealed class ManagedEditBuffer : IDisposable
{
    private static int _nextIdCounter;
    private readonly ushort _id = (ushort)Interlocked.Increment(ref _nextIdCounter);
    private readonly ManagedTextBuffer _buffer;
    private bool _disposed;
    private uint _cursorLine;
    private uint _cursorCol;

    /// <summary>Fired when the cursor position changes.</summary>
    public event Action? CursorChanged;

    #region Construction

    private ManagedEditBuffer(ManagedTextBuffer buffer)
    {
        _buffer = buffer;
    }

    /// <summary>Creates a new edit buffer with the specified width calculation method.</summary>
    public static ManagedEditBuffer Create(WidthMethod widthMethod = WidthMethod.Wcwidth)
    {
        var buffer = ManagedTextBuffer.Create(widthMethod);
        return new ManagedEditBuffer(buffer);
    }

    /// <summary>Creates a new edit buffer wrapping an existing text buffer.</summary>
    public static ManagedEditBuffer CreateFrom(ManagedTextBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return new ManagedEditBuffer(buffer);
    }

    #endregion

    #region Properties

    /// <summary>The underlying text buffer.</summary>
    public ManagedTextBuffer Buffer => _buffer;

    /// <summary>Number of lines in the buffer.</summary>
    public uint LineCount => _buffer.LineCount;

    /// <summary>Whether an undo operation is available.</summary>
    public bool CanUndo => _buffer.CanUndo;

    /// <summary>Whether a redo operation is available.</summary>
    public bool CanRedo => _buffer.CanRedo;

    /// <summary>Width calculation method in use.</summary>
    public WidthMethod WidthMethod => _buffer.WidthMethod;

    #endregion

    #region Cursor

    /// <summary>Returns the unique identifier of the edit buffer.</summary>
    public ushort GetId() => _id;

    /// <summary>Returns the underlying text buffer.</summary>
    public ManagedTextBuffer GetTextBuffer() => _buffer;

    /// <summary>Returns the current primary cursor position.</summary>
    public (uint Line, uint Col) GetPrimaryCursor() => (_cursorLine, _cursorCol);

    /// <summary>Returns the current cursor position as a <see cref="LogicalCursor"/>.</summary>
    public LogicalCursor GetCursorPosition()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        uint offset = CoordsToOffset(_cursorLine, _cursorCol) ?? 0;
        return new LogicalCursor(_cursorLine, _cursorCol, offset);
    }

    /// <summary>Sets the cursor position, clamping to valid range.</summary>
    public void SetCursor(uint line, uint col)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        uint lineCount = _buffer.LineCount;
        if (lineCount == 0)
        {
            _cursorLine = 0;
            _cursorCol = 0;
        }
        else
        {
            _cursorLine = Math.Min(line, lineCount - 1);
            _cursorCol = Math.Min(col, _buffer.GetLineLength(_cursorLine));
        }
        CursorChanged?.Invoke();
    }

    /// <summary>Moves cursor one character left (wrapping to end of prev line if at col 0).</summary>
    public void MoveLeft()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_cursorCol > 0)
        {
            _cursorCol--;
            CursorChanged?.Invoke();
        }
        else if (_cursorLine > 0)
        {
            _cursorLine--;
            _cursorCol = _buffer.GetLineLength(_cursorLine);
            CursorChanged?.Invoke();
        }
    }

    /// <summary>Moves cursor one character right (wrapping to start of next line if at end).</summary>
    public void MoveRight()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        uint lineLen = _buffer.GetLineLength(_cursorLine);
        if (_cursorCol < lineLen)
        {
            _cursorCol++;
            CursorChanged?.Invoke();
        }
        else if (_cursorLine + 1 < _buffer.LineCount)
        {
            _cursorLine++;
            _cursorCol = 0;
            CursorChanged?.Invoke();
        }
    }

    /// <summary>Moves cursor one logical line up, maintaining column where possible.</summary>
    public void MoveCursorUp()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_cursorLine == 0)
        {
            if (_cursorCol != 0)
            {
                _cursorCol = 0;
                CursorChanged?.Invoke();
            }
        }
        else
        {
            _cursorLine--;
            _cursorCol = Math.Min(_cursorCol, _buffer.GetLineLength(_cursorLine));
            CursorChanged?.Invoke();
        }
    }

    /// <summary>Moves cursor one logical line down, maintaining column where possible.</summary>
    public void MoveCursorDown()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        uint lineCount = _buffer.LineCount;
        if (_cursorLine + 1 >= lineCount)
        {
            uint lineLen = _buffer.GetLineLength(_cursorLine);
            if (_cursorCol != lineLen)
            {
                _cursorCol = lineLen;
                CursorChanged?.Invoke();
            }
        }
        else
        {
            _cursorLine++;
            _cursorCol = Math.Min(_cursorCol, _buffer.GetLineLength(_cursorLine));
            CursorChanged?.Invoke();
        }
    }

    /// <summary>Sets the cursor position by character offset.</summary>
    public void SetCursorByOffset(uint offset)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var coords = OffsetToCoords(offset);
        if (coords is not null)
        {
            var (line, col) = coords.Value;
            SetCursor(line, col);
        }
    }

    /// <summary>Moves the cursor to the start of the specified line, clamping to the last line.</summary>
    public void GotoLine(uint line)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        uint lineCount = _buffer.LineCount;
        uint targetLine = lineCount == 0 ? 0 : Math.Min(line, lineCount - 1);
        SetCursor(targetLine, 0);
    }

    #endregion

    #region Line queries

    /// <summary>Returns the number of lines in the buffer.</summary>
    public uint GetLineCount() => _buffer.LineCount;

    /// <summary>Returns the display width of a specific line.</summary>
    public uint GetLineWidth(uint line) => _buffer.GetLineLength(line);

    /// <summary>Returns the maximum display width across all lines.</summary>
    public uint GetMaxLineWidth() => _buffer.MaxLineWidth;

    #endregion

    #region Word boundaries

    /// <summary>Scans backward from cursor to the previous word boundary.</summary>
    public (uint Line, uint Col) GetPrevWordBoundary()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        uint line = _cursorLine;
        uint col = _cursorCol;

        // If at start of line, wrap to end of previous line
        if (col == 0)
        {
            if (line == 0) return (0, 0);
            line--;
            col = _buffer.GetLineLength(line);
            if (col == 0) return (line, 0);
        }

        string lineText = _buffer.GetLineText(line);
        int pos = (int)Math.Min(col, (uint)lineText.Length);

        // Skip whitespace/other backward
        WordClass? startClass = null;
        while (pos > 0)
        {
            int prevPos = pos;
            if (Rune.DecodeLastFromUtf16(lineText.AsSpan(0, prevPos), out var rune, out int consumed)
                != System.Buffers.OperationStatus.Done)
            {
                consumed = 1;
                rune = Rune.ReplacementChar;
            }
            var cls = WordBoundary.ClassifyWord(rune);

            if (startClass is null)
            {
                startClass = cls;
            }
            else if (cls != startClass.Value)
            {
                break;
            }

            pos -= consumed;
        }

        return (line, (uint)pos);
    }

    /// <summary>Scans forward from cursor to the next word boundary.</summary>
    public (uint Line, uint Col) GetNextWordBoundary()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        uint line = _cursorLine;
        uint col = _cursorCol;

        string lineText = _buffer.GetLineText(line);
        int pos = (int)Math.Min(col, (uint)lineText.Length);

        // If at end of line, wrap to start of next line
        if (pos >= lineText.Length)
        {
            if (line + 1 < _buffer.LineCount)
            {
                return (line + 1, 0);
            }
            return (line, col);
        }

        // Skip current word class forward
        WordClass? startClass = null;
        while (pos < lineText.Length)
        {
            if (Rune.DecodeFromUtf16(lineText.AsSpan(pos), out var rune, out int consumed)
                != System.Buffers.OperationStatus.Done)
            {
                consumed = 1;
                rune = Rune.ReplacementChar;
            }
            var cls = WordBoundary.ClassifyWord(rune);

            if (startClass is null)
            {
                startClass = cls;
            }
            else if (cls != startClass.Value)
            {
                break;
            }

            pos += consumed;
        }

        return (line, (uint)pos);
    }

    #endregion

    #region Offset conversion

    /// <summary>Converts a character offset to a <see cref="LogicalCursor"/> position.</summary>
    public bool OffsetToPosition(uint offset, out LogicalCursor cursor)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var coords = OffsetToCoords(offset);
        if (coords is null)
        {
            cursor = default;
            return false;
        }
        var (line, col) = coords.Value;
        cursor = new LogicalCursor(line, col, offset);
        return true;
    }

    /// <summary>Converts a row/column position to a character offset.</summary>
    public uint PositionToOffset(uint row, uint col)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return CoordsToOffset(row, col) ?? 0;
    }

    /// <summary>Gets the byte offset of the start of the specified line.</summary>
    public uint GetLineStartOffset(uint line)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        uint result = 0;
        for (uint i = 0; i < line && i < _buffer.LineCount; i++)
        {
            string lineText = _buffer.GetLineText(i);
            result += (uint)Encoding.UTF8.GetByteCount(lineText) + 1; // +1 for newline
        }
        return result;
    }

    /// <summary>Converts line/col to a linear character offset.</summary>
    public uint? CoordsToOffset(uint line, uint col)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        uint lineCount = _buffer.LineCount;
        if (line >= lineCount) return null;

        uint offset = 0;
        for (uint i = 0; i < line; i++)
        {
            offset += _buffer.GetLineLength(i) + 1; // +1 for newline
        }
        offset += Math.Min(col, _buffer.GetLineLength(line));
        return offset;
    }

    /// <summary>Converts linear offset back to line/col.</summary>
    public (uint Line, uint Col)? OffsetToCoords(uint offset)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        uint lineCount = _buffer.LineCount;
        uint remaining = offset;

        for (uint i = 0; i < lineCount; i++)
        {
            uint lineLen = _buffer.GetLineLength(i);
            uint lineLenWithNewline = lineLen + 1; // +1 for newline separator
            if (remaining <= lineLen)
            {
                return (i, remaining);
            }
            if (i + 1 >= lineCount)
            {
                // Last line — clamp to end
                return (i, lineLen);
            }
            remaining -= lineLenWithNewline;
        }

        return lineCount == 0 ? (0u, 0u) : null;
    }

    /// <summary>Extracts text between two linear offsets.</summary>
    public string GetTextRange(uint startOffset, uint endOffset)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (startOffset >= endOffset) return string.Empty;

        var startCoords = OffsetToCoords(startOffset);
        var endCoords = OffsetToCoords(endOffset);

        if (startCoords is null || endCoords is null) return string.Empty;

        var (startLine, startCol) = startCoords.Value;
        var (endLine, endCol) = endCoords.Value;

        return _buffer.GetTextRange(startLine, startCol, endLine, endCol);
    }

    /// <summary>Gets a range of text by row/column coordinates.</summary>
    public string GetTextRangeByCoords(uint startRow, uint startCol, uint endRow, uint endCol)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _buffer.GetTextRange(startRow, startCol, endRow, endCol);
    }

    /// <summary>Returns the total character count across all lines (including newlines).</summary>
    public uint GetTotalLength()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        uint lineCount = _buffer.LineCount;
        if (lineCount == 0) return 0;

        uint total = 0;
        for (uint i = 0; i < lineCount; i++)
        {
            total += _buffer.GetLineLength(i);
        }
        // Add newlines between lines
        total += lineCount - 1;
        return total;
    }

    #endregion

    #region Text insertion

    /// <summary>
    /// Inserts text at the given line and column.
    /// Stores an undo checkpoint before the mutation.
    /// </summary>
    public void InsertText(uint line, uint col, string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrEmpty(text)) return;

        _buffer.StoreUndo("insert");
        _buffer.InsertText(line, col, text);
    }

    /// <summary>
    /// Inserts UTF-8 encoded text at the given line and column.
    /// Stores an undo checkpoint before the mutation.
    /// </summary>
    public void InsertText(uint line, uint col, ReadOnlySpan<byte> utf8Text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (utf8Text.IsEmpty) return;

        string text = Encoding.UTF8.GetString(utf8Text);
        InsertText(line, col, text);
    }

    /// <summary>
    /// Inserts a newline at the given line and column, splitting the line.
    /// Stores an undo checkpoint before the mutation.
    /// </summary>
    public void InsertNewline(uint line, uint col)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _buffer.StoreUndo("newline");
        _buffer.InsertText(line, col, "\n");
    }

    #endregion

    #region Text deletion

    /// <summary>
    /// Deletes a range of text from (startLine, startCol) to (endLine, endCol).
    /// Stores an undo checkpoint before the mutation.
    /// </summary>
    public void DeleteRange(uint startLine, uint startCol, uint endLine, uint endCol)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _buffer.StoreUndo("delete");
        _buffer.DeleteRange(startLine, startCol, endLine, endCol);
    }

    /// <summary>
    /// Deletes the character at the given cursor position (forward delete).
    /// Stores an undo checkpoint before the mutation.
    /// </summary>
    public void DeleteChar(uint line, uint col)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        uint lineLen = _buffer.GetLineLength(line);
        if (col >= lineLen)
        {
            // At end of line: merge with next line
            if (line + 1 < _buffer.LineCount)
            {
                _buffer.StoreUndo("delete");
                _buffer.DeleteRange(line, col, line + 1, 0);
            }
            return;
        }

        // Delete one character at the cursor position
        string lineText = _buffer.GetLineText(line);
        int charIndex = (int)Math.Min(col, (uint)lineText.Length);
        if (charIndex >= lineText.Length) return;

        // Determine the end of the character (could be multi-codepoint)
        int consumed;
        if (Rune.DecodeFromUtf16(lineText.AsSpan(charIndex), out _, out consumed)
            != System.Buffers.OperationStatus.Done)
        {
            consumed = 1;
        }

        _buffer.StoreUndo("delete");
        _buffer.DeleteRange(line, col, line, col + (uint)consumed);
    }

    /// <summary>
    /// Deletes the character before the given cursor position (backspace).
    /// Stores an undo checkpoint before the mutation.
    /// </summary>
    public void DeleteCharBefore(uint line, uint col)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (col > 0)
        {
            // Delete the character before the cursor
            string lineText = _buffer.GetLineText(line);
            int charIndex = (int)Math.Min(col, (uint)lineText.Length);

            // Find the start of the previous character
            int prevStart = charIndex - 1;
            if (prevStart >= 0 && char.IsLowSurrogate(lineText[prevStart]) && prevStart > 0)
                prevStart--; // Skip back past high surrogate

            _buffer.StoreUndo("backspace");
            _buffer.DeleteRange(line, (uint)prevStart, line, col);
        }
        else if (line > 0)
        {
            // At start of line: merge with previous line
            uint prevLineLen = _buffer.GetLineLength(line - 1);
            _buffer.StoreUndo("backspace");
            _buffer.DeleteRange(line - 1, prevLineLen, line, 0);
        }
    }

    /// <summary>
    /// Replaces a range of text with new text.
    /// Stores an undo checkpoint before the mutation.
    /// </summary>
    public void ReplaceRange(uint startLine, uint startCol, uint endLine, uint endCol, string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _buffer.StoreUndo("replace");
        _buffer.DeleteRange(startLine, startCol, endLine, endCol);
        if (!string.IsNullOrEmpty(text))
        {
            _buffer.InsertText(startLine, startCol, text);
        }
    }

    #endregion

    #region Cursor-relative editing

    /// <summary>Inserts text at the current cursor position, advancing cursor past inserted text.</summary>
    public void InsertText(string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrEmpty(text)) return;

        _buffer.StoreUndo("insert");
        _buffer.InsertText(_cursorLine, _cursorCol, text);

        // Advance cursor past inserted text
        int lastNewline = text.LastIndexOf('\n');
        if (lastNewline < 0)
        {
            _cursorCol += (uint)text.Length;
        }
        else
        {
            uint newlineCount = 0;
            foreach (char ch in text)
            {
                if (ch == '\n') newlineCount++;
            }
            _cursorLine += newlineCount;
            _cursorCol = (uint)(text.Length - lastNewline - 1);
        }
        CursorChanged?.Invoke();
    }

    /// <summary>Inserts a character at the cursor position. Alias for <see cref="InsertText(string)"/>.</summary>
    public void InsertChar(string ch) => InsertText(ch);

    /// <summary>Inserts a newline at the current cursor position.</summary>
    public void NewLine() => InsertText("\n");

    /// <summary>Replaces the entire buffer content with the specified text.</summary>
    public void ReplaceText(string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _buffer.StoreUndo("replace");
        _buffer.Clear();
        if (!string.IsNullOrEmpty(text))
            _buffer.SetText(text);
        _cursorLine = 0;
        _cursorCol = 0;
        CursorChanged?.Invoke();
    }

    /// <summary>Sets the entire text content, replacing any existing content.</summary>
    public void SetText(string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _buffer.Clear();
        if (!string.IsNullOrEmpty(text))
            _buffer.SetText(text);
        _cursorLine = 0;
        _cursorCol = 0;
        CursorChanged?.Invoke();
    }

    /// <summary>Clears all content from the edit buffer and resets the cursor.</summary>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _buffer.Clear();
        _cursorLine = 0;
        _cursorCol = 0;
        CursorChanged?.Invoke();
    }

    /// <summary>Deletes the character after the cursor (Delete key).</summary>
    public void DeleteForward()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        DeleteChar(_cursorLine, _cursorCol);
    }

    /// <summary>Deletes the character before the cursor (Backspace), moves cursor back.</summary>
    public void DeleteBackward()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        uint prevLine = _cursorLine;
        uint prevCol = _cursorCol;

        if (_cursorCol > 0)
        {
            // Move cursor back by one character
            string lineText = _buffer.GetLineText(_cursorLine);
            int charIndex = (int)Math.Min(_cursorCol, (uint)lineText.Length);
            int prevStart = charIndex - 1;
            if (prevStart >= 0 && char.IsLowSurrogate(lineText[prevStart]) && prevStart > 0)
                prevStart--;

            _cursorCol = (uint)prevStart;
        }
        else if (_cursorLine > 0)
        {
            _cursorLine--;
            _cursorCol = _buffer.GetLineLength(_cursorLine);
        }
        else
        {
            return; // Nothing to delete
        }

        DeleteCharBefore(prevLine, prevCol);
        CursorChanged?.Invoke();
    }

    /// <summary>Deletes the current line.</summary>
    public void DeleteLine()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        uint lineCount = _buffer.LineCount;
        if (lineCount == 0) return;

        _buffer.StoreUndo("delete-line");

        if (lineCount == 1)
        {
            // Only line — clear it
            uint lineLen = _buffer.GetLineLength(0);
            if (lineLen > 0)
                _buffer.DeleteRange(0, 0, 0, lineLen);
            _cursorLine = 0;
            _cursorCol = 0;
        }
        else if (_cursorLine + 1 < lineCount)
        {
            // Not the last line — delete line including its trailing newline
            _buffer.DeleteRange(_cursorLine, 0, _cursorLine + 1, 0);
            // Cursor stays on same line number, clamp col
            _cursorCol = Math.Min(_cursorCol, _buffer.GetLineLength(_cursorLine));
        }
        else
        {
            // Last line — delete the newline before it and the line itself
            uint lineLen = _buffer.GetLineLength(_cursorLine);
            _buffer.DeleteRange(_cursorLine - 1, _buffer.GetLineLength(_cursorLine - 1), _cursorLine, lineLen);
            _cursorLine--;
            _cursorCol = Math.Min(_cursorCol, _buffer.GetLineLength(_cursorLine));
        }
        CursorChanged?.Invoke();
    }

    /// <summary>Deletes from cursor to previous word boundary.</summary>
    public void DeleteWordLeft()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var (boundLine, boundCol) = GetPrevWordBoundary();
        if (boundLine == _cursorLine && boundCol == _cursorCol) return;

        _buffer.StoreUndo("delete-word-left");
        _buffer.DeleteRange(boundLine, boundCol, _cursorLine, _cursorCol);
        _cursorLine = boundLine;
        _cursorCol = boundCol;
        CursorChanged?.Invoke();
    }

    /// <summary>Deletes from cursor to next word boundary.</summary>
    public void DeleteWordRight()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var (boundLine, boundCol) = GetNextWordBoundary();
        if (boundLine == _cursorLine && boundCol == _cursorCol) return;

        _buffer.StoreUndo("delete-word-right");
        _buffer.DeleteRange(_cursorLine, _cursorCol, boundLine, boundCol);
        // Cursor stays at current position
        CursorChanged?.Invoke();
    }

    #endregion

    #region Clipboard operations

    /// <summary>
    /// Cuts the selected text from the view, removing it from the buffer and returning it.
    /// </summary>
    public string Cut(ManagedTextBufferView view)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(view);

        if (!view.HasSelection)
            return string.Empty;

        string text = view.GetSelectedText();
        var (start, end) = view.GetSelectionCursorRange();

        _buffer.StoreUndo("cut");
        _buffer.DeleteRange(start.Row, start.Col, end.Row, end.Col);

        view.ClearSelection();
        view.SetCursor(start.Row, start.Col);
        return text;
    }

    /// <summary>
    /// Copies the selected text from the view without modifying the buffer.
    /// </summary>
    public string Copy(ManagedTextBufferView view)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(view);

        if (!view.HasSelection)
            return string.Empty;

        return view.GetSelectedText();
    }

    /// <summary>
    /// Pastes text at the cursor position (or replaces the current selection).
    /// Stores an undo checkpoint before the mutation.
    /// </summary>
    public void Paste(ManagedTextBufferView view, string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(view);
        if (string.IsNullOrEmpty(text)) return;

        _buffer.StoreUndo("paste");

        if (view.HasSelection)
        {
            var (start, end) = view.GetSelectionCursorRange();
            _buffer.DeleteRange(start.Row, start.Col, end.Row, end.Col);
            _buffer.InsertText(start.Row, start.Col, text);
            view.ClearSelection();

            // Position cursor at the end of inserted text
            PositionCursorAfterInsert(view, start.Row, start.Col, text);
        }
        else
        {
            var cursor = view.Cursor;
            _buffer.InsertText(cursor.Row, cursor.Col, text);

            // Position cursor at the end of inserted text
            PositionCursorAfterInsert(view, cursor.Row, cursor.Col, text);
        }
    }

    #endregion

    #region Undo / Redo

    /// <summary>Undoes the last edit operation. Returns the undo description, or empty if unavailable.</summary>
    public string Undo()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _buffer.Undo() ?? string.Empty;
    }

    /// <summary>Redoes the last undone operation. Returns the redo description, or empty if unavailable.</summary>
    public string Redo()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _buffer.Redo() ?? string.Empty;
    }

    /// <summary>
    /// Manually stores an undo checkpoint. Useful for grouping multiple operations
    /// into a single undoable unit.
    /// </summary>
    public void StoreUndoCheckpoint(string meta = "")
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _buffer.StoreUndo(meta);
    }

    /// <summary>Clears all undo/redo history.</summary>
    public void ClearHistory()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _buffer.ClearHistory();
    }

    #endregion

    #region Content access

    /// <summary>Gets the entire text content of the buffer.</summary>
    public string GetText()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _buffer.GetText();
    }

    /// <summary>Gets the text of a specific line (without line ending).</summary>
    public string GetLineText(uint line)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _buffer.GetLineText(line);
    }

    #endregion

    #region Dispose

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _buffer.Dispose();
        }
    }

    #endregion

    #region Private helpers

    /// <summary>
    /// Positions the cursor after inserted text, accounting for newlines within the text.
    /// </summary>
    private static void PositionCursorAfterInsert(ManagedTextBufferView view, uint startLine, uint startCol, string text)
    {
        // Count newlines in the inserted text
        int lastNewline = text.LastIndexOf('\n');
        if (lastNewline < 0)
        {
            // No newlines: cursor moves right by text length
            view.SetCursor(startLine, startCol + (uint)text.Length);
        }
        else
        {
            // Has newlines: count them and position after the last one
            uint newlineCount = 0;
            foreach (char ch in text)
            {
                if (ch == '\n') newlineCount++;
            }
            uint colAfterLastNewline = (uint)(text.Length - lastNewline - 1);
            view.SetCursor(startLine + newlineCount, colAfterLastNewline);
        }
    }

    #endregion
}
