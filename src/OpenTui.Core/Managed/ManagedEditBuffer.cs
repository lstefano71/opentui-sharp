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

    /// <summary>Sets the cursor position (in display columns), clamping to valid range.</summary>
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
            _cursorCol = Math.Min(col, _buffer.LineWidthAt(_cursorLine));
        }
        CursorChanged?.Invoke();
    }

    /// <summary>Moves cursor one visible character left (wrapping to end of prev line if at col 0).</summary>
    public void MoveLeft()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_cursorCol > 0)
        {
            string lineText = _buffer.GetLineText(_cursorLine);
            byte tabWidth = _buffer.TabWidth;
            // Walk backwards from cursor display col to find the start of the previous visible char
            uint prevCol = FindPrevVisibleCharCol(lineText, _cursorCol, tabWidth);
            _cursorCol = prevCol;
            CursorChanged?.Invoke();
        }
        else if (_cursorLine > 0)
        {
            _cursorLine--;
            _cursorCol = _buffer.LineWidthAt(_cursorLine);
            CursorChanged?.Invoke();
        }
    }

    /// <summary>Moves cursor one visible character right (wrapping to start of next line if at end).</summary>
    public void MoveRight()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        uint lineDisplayWidth = _buffer.LineWidthAt(_cursorLine);
        if (_cursorCol < lineDisplayWidth)
        {
            string lineText = _buffer.GetLineText(_cursorLine);
            byte tabWidth = _buffer.TabWidth;
            // Walk forward from cursor display col to find the end of the next visible char
            uint nextCol = FindNextVisibleCharCol(lineText, _cursorCol, tabWidth);
            _cursorCol = nextCol;
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
            _cursorCol = Math.Min(_cursorCol, _buffer.LineWidthAt(_cursorLine));
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
            uint lineWidth = _buffer.LineWidthAt(_cursorLine);
            if (_cursorCol != lineWidth)
            {
                _cursorCol = lineWidth;
                CursorChanged?.Invoke();
            }
        }
        else
        {
            _cursorLine++;
            _cursorCol = Math.Min(_cursorCol, _buffer.LineWidthAt(_cursorLine));
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

    /// <summary>Moves the cursor to the end of the last line in the buffer.</summary>
    public void GotoBufferEnd()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        uint lineCount = _buffer.LineCount;
        if (lineCount == 0)
        {
            SetCursor(0, 0);
        }
        else
        {
            uint lastLine = lineCount - 1;
            uint endCol = _buffer.LineWidthAt(lastLine);
            SetCursor(lastLine, endCol);
        }
    }

    #endregion

    #region Line queries

    /// <summary>Returns the number of lines in the buffer.</summary>
    public uint GetLineCount() => _buffer.LineCount;

    /// <summary>Returns the display width of a specific line.</summary>
    public uint GetLineWidth(uint line) => _buffer.LineWidthAt(line);

    /// <summary>Returns the maximum display width across all lines.</summary>
    public uint GetMaxLineWidth() => _buffer.MaxLineWidth;

    #endregion

    #region Word boundaries

    /// <summary>Scans backward from cursor to the previous word boundary (display column).</summary>
    public (uint Line, uint Col) GetPrevWordBoundary()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        uint line = _cursorLine;
        uint col = _cursorCol;

        if (line == 0 && col == 0) return (0, 0);

        // If at start of line, wrap to end of previous line
        if (col == 0)
        {
            if (line == 0) return (0, 0);
            line--;
            return (line, _buffer.LineWidthAt(line));
        }

        string lineText = _buffer.GetLineText(line);
        byte tabWidth = _buffer.TabWidth;

        // Find all break boundaries in this line, return the last one before cursor
        uint? lastBoundary = null;
        FindBreakBoundaries(lineText, tabWidth, (boundaryCol) =>
        {
            if (boundaryCol < col)
                lastBoundary = boundaryCol;
            return boundaryCol < col; // stop scanning once we pass cursor
        });

        if (lastBoundary.HasValue)
            return (line, lastBoundary.Value);

        // No break found before cursor — go to previous line end
        if (line > 0)
            return (line - 1, _buffer.LineWidthAt(line - 1));

        return (0, 0);
    }

    /// <summary>Scans forward from cursor to the next word boundary (display column).</summary>
    public (uint Line, uint Col) GetNextWordBoundary()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        uint line = _cursorLine;
        uint col = _cursorCol;

        uint lineDisplayWidth = _buffer.LineWidthAt(line);

        // If at/past end of line, wrap to start of next line
        if (col >= lineDisplayWidth)
        {
            if (line + 1 < _buffer.LineCount)
                return (line + 1, 0);
            return (line, col);
        }

        string lineText = _buffer.GetLineText(line);
        byte tabWidth = _buffer.TabWidth;

        // Find first break boundary strictly after cursor
        uint? firstBoundaryAfterCursor = null;
        FindBreakBoundaries(lineText, tabWidth, (boundaryCol) =>
        {
            if (boundaryCol > col)
            {
                firstBoundaryAfterCursor = boundaryCol;
                return false; // stop
            }
            return true; // continue
        });

        if (firstBoundaryAfterCursor.HasValue)
            return (line, firstBoundaryAfterCursor.Value);

        // No break found — go to next line
        if (line + 1 < _buffer.LineCount)
            return (line + 1, 0);
        return (line, lineDisplayWidth);
    }

    /// <summary>
    /// Walks through line text and calls the callback with each break boundary's display column.
    /// A boundary is positioned AFTER the break character: breakCol + breakCharWidth.
    /// Breaks occur at whitespace, punctuation, and CJK↔ASCII transitions.
    /// The callback returns true to continue scanning, false to stop.
    /// </summary>
    private static void FindBreakBoundaries(string lineText, byte tabWidth, Func<uint, bool> callback)
    {
        uint displayCol = 0;
        WordClass prevClass = WordClass.Other;
        bool havePrev = false;
        uint prevDisplayCol = 0;
        uint prevCharWidth = 0;

        int pos = 0;
        while (pos < lineText.Length)
        {
            if (Rune.DecodeFromUtf16(lineText.AsSpan(pos), out var rune, out int consumed)
                != System.Buffers.OperationStatus.Done)
            {
                consumed = 1;
                rune = Rune.ReplacementChar;
            }

            uint charWidth = TextWidth.CharWidth(rune, tabWidth);
            var currentClass = WordBoundary.ClassifyWord(rune);

            // CJK↔ASCII transition → break at PREVIOUS character
            if (havePrev && IsCjkAsciiTransition(prevClass, currentClass))
            {
                uint boundary = prevDisplayCol + prevCharWidth;
                if (!callback(boundary)) return;
            }

            // Check if this character is a break character
            bool isBreak;
            int cp = rune.Value;
            if (cp < 0x80)
                isBreak = IsAsciiWrapBreak((byte)cp);
            else
                isBreak = IsUnicodeWrapBreak(cp);

            if (isBreak)
            {
                uint boundary = displayCol + charWidth;
                if (!callback(boundary)) return;
            }

            havePrev = true;
            prevClass = currentClass;
            prevDisplayCol = displayCol;
            prevCharWidth = charWidth;
            displayCol += charWidth;
            pos += consumed;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static bool IsAsciiWrapBreak(byte b) => b switch
    {
        (byte)' ' or (byte)'\t' => true,
        (byte)'-' => true,
        (byte)'/' or (byte)'\\' => true,
        (byte)'.' or (byte)',' or (byte)';' or (byte)':' or (byte)'!' or (byte)'?' => true,
        (byte)'(' or (byte)')' or (byte)'[' or (byte)']' or (byte)'{' or (byte)'}' => true,
        _ => false,
    };

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static bool IsUnicodeWrapBreak(int cp) => cp switch
    {
        0x00A0 => true, 0x1680 => true,
        >= 0x2000 and <= 0x200A => true,
        0x202F => true, 0x205F => true, 0x3000 => true, 0x200B => true, 0x00AD => true,
        0x2010 => true, 0x3001 => true, 0x3002 => true, 0xFF01 => true, 0xFF1F => true,
        _ => false,
    };

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static bool IsCjkAsciiTransition(WordClass prev, WordClass curr)
        => (prev == WordClass.CjkWord && curr == WordClass.AsciiWord)
        || (prev == WordClass.AsciiWord && curr == WordClass.CjkWord);

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static bool IsWordCodepoint(Rune rune)
    {
        var cls = WordBoundary.ClassifyWord(rune);
        return cls == WordClass.AsciiWord || cls == WordClass.CjkWord;
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

    /// <summary>Converts line/col (display columns) to a linear display-width offset.</summary>
    public uint? CoordsToOffset(uint line, uint col)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        uint lineCount = _buffer.LineCount;
        if (line >= lineCount) return null;

        uint offset = 0;
        for (uint i = 0; i < line; i++)
        {
            offset += _buffer.LineWidthAt(i) + 1; // +1 for newline
        }
        offset += Math.Min(col, _buffer.LineWidthAt(line));
        return offset;
    }

    /// <summary>Converts linear display-width offset back to line/col (display columns).</summary>
    public (uint Line, uint Col)? OffsetToCoords(uint offset)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        uint lineCount = _buffer.LineCount;
        uint remaining = offset;

        for (uint i = 0; i < lineCount; i++)
        {
            uint lineWidth = _buffer.LineWidthAt(i);
            if (remaining <= lineWidth)
            {
                return (i, remaining);
            }
            if (i + 1 >= lineCount)
            {
                return (i, lineWidth);
            }
            remaining -= lineWidth + 1; // +1 for newline
        }

        return lineCount == 0 ? (0u, 0u) : null;
    }

    /// <summary>Extracts text between two linear display-width offsets.</summary>
    public string GetTextRange(uint startOffset, uint endOffset)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (startOffset >= endOffset) return string.Empty;

        var startCoords = OffsetToCoords(startOffset);
        var endCoords = OffsetToCoords(endOffset);

        if (startCoords is null || endCoords is null) return string.Empty;

        var (startLine, startDisplayCol) = startCoords.Value;
        var (endLine, endDisplayCol) = endCoords.Value;

        // Convert display columns to char indices for the underlying text buffer
        uint startCharCol = (uint)_buffer.DisplayColToCharIndex(startLine, startDisplayCol);
        uint endCharCol = (uint)_buffer.DisplayColToCharIndex(endLine, endDisplayCol, roundUp: true);

        return _buffer.GetTextRange(startLine, startCharCol, endLine, endCharCol);
    }

    /// <summary>Gets a range of text by row/column coordinates (display columns).</summary>
    public string GetTextRangeByCoords(uint startRow, uint startCol, uint endRow, uint endCol)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        // Convert display columns to char indices
        uint startCharCol = (uint)_buffer.DisplayColToCharIndex(startRow, startCol);
        uint endCharCol = (uint)_buffer.DisplayColToCharIndex(endRow, endCol, roundUp: true);
        return _buffer.GetTextRange(startRow, startCharCol, endRow, endCharCol);
    }

    /// <summary>Returns the total display width across all lines (including 1 per newline).</summary>
    public uint GetTotalLength()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        uint lineCount = _buffer.LineCount;
        if (lineCount == 0) return 0;

        uint total = 0;
        for (uint i = 0; i < lineCount; i++)
        {
            total += _buffer.LineWidthAt(i);
        }
        total += lineCount - 1; // newlines
        return total;
    }

    #endregion

    #region Text insertion

    /// <summary>
    /// Inserts text at the given line and column (display column).
    /// Stores an undo checkpoint before the mutation.
    /// </summary>
    public void InsertText(uint line, uint col, string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrEmpty(text)) return;

        uint charCol = (uint)_buffer.DisplayColToCharIndex(line, col);
        _buffer.StoreUndo("edit");
        _buffer.InsertText(line, charCol, text);
    }

    /// <summary>
    /// Inserts UTF-8 encoded text at the given line and column (display column).
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
    /// Inserts a newline at the given line and column (display column), splitting the line.
    /// Stores an undo checkpoint before the mutation.
    /// </summary>
    public void InsertNewline(uint line, uint col)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        uint charCol = (uint)_buffer.DisplayColToCharIndex(line, col);
        _buffer.StoreUndo("edit");
        _buffer.InsertText(line, charCol, "\n");
    }

    #endregion

    #region Text deletion

    /// <summary>
    /// Deletes a range of text from (startLine, startCol) to (endLine, endCol).
    /// Columns are display columns, converted to char indices before deletion.
    /// Stores an undo checkpoint before the mutation.
    /// </summary>
    public void DeleteRange(uint startLine, uint startCol, uint endLine, uint endCol)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        uint startCharCol = (uint)_buffer.DisplayColToCharIndex(startLine, startCol);
        uint endCharCol = (uint)_buffer.DisplayColToCharIndex(endLine, endCol, roundUp: true);
        _buffer.StoreUndo("edit");
        _buffer.DeleteRange(startLine, startCharCol, endLine, endCharCol);
    }

    /// <summary>
    /// Deletes the character at the given cursor position (display column, forward delete).
    /// Stores an undo checkpoint before the mutation.
    /// </summary>
    public void DeleteChar(uint line, uint col)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        uint lineDisplayWidth = _buffer.LineWidthAt(line);
        if (col >= lineDisplayWidth)
        {
            // At end of line: merge with next line
            if (line + 1 < _buffer.LineCount)
            {
                uint charCol = (uint)_buffer.DisplayColToCharIndex(line, col);
                _buffer.StoreUndo("edit");
                _buffer.DeleteRange(line, charCol, line + 1, 0);
            }
            return;
        }

        // Delete one character at the cursor position (display col → char index)
        string lineText = _buffer.GetLineText(line);
        byte tabWidth = _buffer.TabWidth;
        int charIndex = ManagedTextBuffer.DisplayColToCharIndex(lineText, col, tabWidth);
        if (charIndex >= lineText.Length) return;

        // Determine the end of the character (could be multi-codepoint)
        int consumed;
        if (Rune.DecodeFromUtf16(lineText.AsSpan(charIndex), out _, out consumed)
            != System.Buffers.OperationStatus.Done)
        {
            consumed = 1;
        }

        _buffer.StoreUndo("edit");
        _buffer.DeleteRange(line, (uint)charIndex, line, (uint)(charIndex + consumed));
    }

    /// <summary>
    /// Deletes the character before the given cursor position (display column, backspace).
    /// Stores an undo checkpoint before the mutation.
    /// </summary>
    public void DeleteCharBefore(uint line, uint col)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (col > 0)
        {
            // Convert display col to char index
            string lineText = _buffer.GetLineText(line);
            byte tabWidth = _buffer.TabWidth;
            int charIndex = ManagedTextBuffer.DisplayColToCharIndex(lineText, col, tabWidth);

            int deleteStart, deleteEnd;

            if (_buffer.WidthMethod == WidthMethod.Unicode)
            {
                // Unicode mode: delete entire grapheme cluster (emoji sequences, ZWJ, etc.)
                var clusterStarts = System.Globalization.StringInfo.ParseCombiningCharacters(lineText);

                int clusterIdx = -1;
                for (int i = 0; i < clusterStarts.Length; i++)
                {
                    if (clusterStarts[i] >= charIndex) break;
                    clusterIdx = i;
                }

                if (clusterIdx >= 0)
                {
                    deleteStart = clusterStarts[clusterIdx];
                    deleteEnd = (clusterIdx + 1 < clusterStarts.Length)
                        ? clusterStarts[clusterIdx + 1]
                        : lineText.Length;
                }
                else
                {
                    deleteStart = StepBackOneRune(lineText, charIndex);
                    deleteEnd = charIndex;
                }
            }
            else
            {
                // Wcwidth mode: delete one visible rune, plus any trailing zero-width
                // chars (ZWJ, variation selectors) that connect it to the next char
                deleteEnd = charIndex;
                deleteStart = StepBackOneRune(lineText, charIndex);

                // Extend forward past any trailing zero-width runes
                while (deleteEnd < lineText.Length)
                {
                    if (Rune.DecodeFromUtf16(lineText.AsSpan(deleteEnd), out var trailRune, out int trailConsumed)
                        != System.Buffers.OperationStatus.Done)
                        break;
                    if (TextWidth.CharWidth(trailRune, tabWidth) > 0) break;
                    deleteEnd += trailConsumed;
                }
            }

            _buffer.StoreUndo("edit");
            _buffer.DeleteRange(line, (uint)deleteStart, line, (uint)deleteEnd);
        }
        else if (line > 0)
        {
            // At start of line: merge with previous line
            uint prevLineCharLen = _buffer.GetLineLength(line - 1);
            _buffer.StoreUndo("edit");
            _buffer.DeleteRange(line - 1, prevLineCharLen, line, 0);
        }
    }

    /// <summary>Steps back one rune from the given char index in the string.</summary>
    private static int StepBackOneRune(string text, int charIndex)
    {
        if (charIndex <= 0) return 0;
        charIndex--;
        if (charIndex > 0 && char.IsLowSurrogate(text[charIndex]))
            charIndex--; // Skip past high surrogate
        return charIndex;
    }

    /// <summary>
    /// Replaces a range of text with new text (display columns).
    /// Stores an undo checkpoint before the mutation.
    /// </summary>
    public void ReplaceRange(uint startLine, uint startCol, uint endLine, uint endCol, string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        uint startCharCol = (uint)_buffer.DisplayColToCharIndex(startLine, startCol);
        uint endCharCol = (uint)_buffer.DisplayColToCharIndex(endLine, endCol, roundUp: true);
        _buffer.StoreUndo("edit");
        _buffer.DeleteRange(startLine, startCharCol, endLine, endCharCol);
        if (!string.IsNullOrEmpty(text))
        {
            _buffer.InsertText(startLine, startCharCol, text);
        }
    }

    #endregion

    #region Cursor-relative editing

    /// <summary>Inserts text at the current cursor position, advancing cursor past inserted text.</summary>
    public void InsertText(string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrEmpty(text)) return;

        // Convert display col → char index for the underlying buffer
        uint charCol = (uint)_buffer.DisplayColToCharIndex(_cursorLine, _cursorCol);
        _buffer.StoreUndo("edit");
        _buffer.InsertText(_cursorLine, charCol, text);

        // Advance cursor past inserted text using display widths
        byte tabWidth = _buffer.TabWidth;
        int lastNewline = text.LastIndexOf('\n');
        if (lastNewline < 0)
        {
            _cursorCol += ManagedTextBuffer.ComputeDisplayWidth(text.AsSpan(), tabWidth);
        }
        else
        {
            uint newlineCount = 0;
            foreach (char ch in text)
            {
                if (ch == '\n') newlineCount++;
            }
            _cursorLine += newlineCount;
            ReadOnlySpan<char> lastLineText = text.AsSpan(lastNewline + 1);
            _cursorCol = ManagedTextBuffer.ComputeDisplayWidth(lastLineText, tabWidth);
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
        _buffer.StoreUndo("edit");
        _buffer.Clear();
        if (!string.IsNullOrEmpty(text))
            _buffer.SetText(text);
        _cursorLine = 0;
        _cursorCol = 0;
        CursorChanged?.Invoke();
    }

    /// <summary>Sets the entire text content, replacing any existing content and clearing undo history.</summary>
    public void SetText(string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _buffer.Clear();
        _buffer.ClearHistory();
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
            // Move cursor back by one visible character (in display columns)
            string lineText = _buffer.GetLineText(_cursorLine);
            byte tabWidth = _buffer.TabWidth;
            _cursorCol = FindPrevVisibleCharCol(lineText, _cursorCol, tabWidth);
        }
        else if (_cursorLine > 0)
        {
            _cursorLine--;
            _cursorCol = _buffer.LineWidthAt(_cursorLine);
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

        _buffer.StoreUndo("edit");

        if (lineCount == 1)
        {
            uint lineCharLen = _buffer.GetLineLength(0);
            if (lineCharLen > 0)
                _buffer.DeleteRange(0, 0, 0, lineCharLen);
            _cursorLine = 0;
            _cursorCol = 0;
        }
        else if (_cursorLine + 1 < lineCount)
        {
            _buffer.DeleteRange(_cursorLine, 0, _cursorLine + 1, 0);
            _cursorCol = Math.Min(_cursorCol, _buffer.LineWidthAt(_cursorLine));
        }
        else
        {
            uint lineCharLen = _buffer.GetLineLength(_cursorLine);
            uint prevLineCharLen = _buffer.GetLineLength(_cursorLine - 1);
            _buffer.DeleteRange(_cursorLine - 1, prevLineCharLen, _cursorLine, lineCharLen);
            _cursorLine--;
            _cursorCol = Math.Min(_cursorCol, _buffer.LineWidthAt(_cursorLine));
        }
        CursorChanged?.Invoke();
    }

    /// <summary>Deletes from cursor to previous word boundary.</summary>
    public void DeleteWordLeft()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var (boundLine, boundCol) = GetPrevWordBoundary();
        if (boundLine == _cursorLine && boundCol == _cursorCol) return;

        // Convert display cols to char indices for deletion
        uint boundCharCol = (uint)_buffer.DisplayColToCharIndex(boundLine, boundCol);
        uint curCharCol = (uint)_buffer.DisplayColToCharIndex(_cursorLine, _cursorCol, roundUp: true);
        _buffer.StoreUndo("edit");
        _buffer.DeleteRange(boundLine, boundCharCol, _cursorLine, curCharCol);
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

        uint curCharCol = (uint)_buffer.DisplayColToCharIndex(_cursorLine, _cursorCol);
        uint boundCharCol = (uint)_buffer.DisplayColToCharIndex(boundLine, boundCol, roundUp: true);
        _buffer.StoreUndo("edit");
        _buffer.DeleteRange(_cursorLine, curCharCol, boundLine, boundCharCol);
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

        _buffer.StoreUndo("edit");
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

        _buffer.StoreUndo("edit");

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
        var result = _buffer.Undo("current") ?? string.Empty;
        if (result.Length > 0) ClampCursor();
        return result;
    }

    /// <summary>Redoes the last undone operation. Returns the redo description, or empty if unavailable.</summary>
    public string Redo()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var result = _buffer.Redo() ?? string.Empty;
        if (result.Length > 0) ClampCursor();
        return result;
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

    #region Character width helpers

    /// <summary>
    /// Walk backwards from <paramref name="displayCol"/> to find the display column of the
    /// previous visible character's start. Skips zero-width chars (ZWJ, combining marks).
    /// </summary>
    private static uint FindPrevVisibleCharCol(string lineText, uint displayCol, byte tabWidth)
    {
        uint col = 0;
        uint lastVisibleCol = 0;
        foreach (var rune in lineText.EnumerateRunes())
        {
            uint w = TextWidth.CharWidth(rune, tabWidth);
            uint nextCol = col + w;
            if (nextCol > displayCol || (nextCol == displayCol && w > 0))
            {
                // Cursor is strictly inside this wide char (between start and end)
                if (nextCol > displayCol && col < displayCol && w > 1)
                    return lastVisibleCol;
                return w > 0 ? col : lastVisibleCol;
            }
            if (w > 0) lastVisibleCol = col;
            col = nextCol;
        }
        return lastVisibleCol;
    }

    /// <summary>
    /// Walk forward from <paramref name="displayCol"/> to find the display column just after
    /// the next visible character. Skips zero-width chars (ZWJ, combining marks).
    /// </summary>
    private static uint FindNextVisibleCharCol(string lineText, uint displayCol, byte tabWidth)
    {
        uint col = 0;
        bool passedCursor = false;
        foreach (var rune in lineText.EnumerateRunes())
        {
            uint w = TextWidth.CharWidth(rune, tabWidth);
            if (passedCursor && w > 0)
                return col + w;
            // Handle cursor inside wide character: col < displayCol but col+w > displayCol
            if (!passedCursor && (col >= displayCol || (w > 0 && col + w > displayCol)))
                passedCursor = true;
            if (passedCursor && w > 0)
                return col + w;
            col += w;
        }
        return col;
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
    /// Clamps the cursor to valid line/column after an undo or redo that may have changed buffer content.
    /// </summary>
    private void ClampCursor()
    {
        uint lineCount = _buffer.LineCount;
        if (lineCount == 0)
        {
            _cursorLine = 0;
            _cursorCol = 0;
        }
        else
        {
            if (_cursorLine >= lineCount)
                _cursorLine = lineCount - 1;
            uint lineWidth = _buffer.LineWidthAt(_cursorLine);
            if (_cursorCol > lineWidth)
                _cursorCol = lineWidth;
        }
        CursorChanged?.Invoke();
    }

    /// <summary>
    /// Positions the cursor after inserted text, accounting for newlines within the text.
    /// </summary>
    private static void PositionCursorAfterInsert(ManagedTextBufferView view, uint startLine, uint startCol, string text)
    {
        byte tabWidth = view.Buffer.TabWidth;
        // Count newlines in the inserted text
        int lastNewline = text.LastIndexOf('\n');
        if (lastNewline < 0)
        {
            // No newlines: cursor moves right by display width of text
            uint displayWidth = ManagedTextBuffer.ComputeDisplayWidth(text.AsSpan(), tabWidth);
            view.SetCursor(startLine, startCol + displayWidth);
        }
        else
        {
            // Has newlines: count them and position after the last one
            uint newlineCount = 0;
            foreach (char ch in text)
            {
                if (ch == '\n') newlineCount++;
            }
            string afterLastNewline = text.Substring(lastNewline + 1);
            uint colAfterLastNewline = ManagedTextBuffer.ComputeDisplayWidth(afterLastNewline.AsSpan(), tabWidth);
            view.SetCursor(startLine + newlineCount, colAfterLastNewline);
        }
    }

    #endregion
}
