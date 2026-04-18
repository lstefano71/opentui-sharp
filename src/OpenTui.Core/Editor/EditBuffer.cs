using System.Runtime.InteropServices;
using OpenTui.Core.Native;
using OpenTui.Native;

namespace OpenTui.Core;

/// <summary>
/// Managed wrapper around the native edit buffer, providing text editing,
/// cursor movement, word boundaries, undo/redo, and text range queries.
/// </summary>
public sealed class EditBuffer : IDisposable
{
    private nint _handle;
    private bool _disposed;

    private EditBuffer(nint handle)
    {
        _handle = handle;
    }

    /// <summary>Creates a new edit buffer with the specified width method.</summary>
    public static EditBuffer Create(byte widthMethod = 0)
    {
        var handle = OpenTuiNative.CreateEditBuffer(widthMethod);
        if (handle == nint.Zero)
            throw new InvalidOperationException("Failed to create native edit buffer.");
        return new EditBuffer(handle);
    }

    /// <summary>Gets the native edit buffer handle.</summary>
    internal nint Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _handle;
        }
    }

    #region Text Operations

    /// <summary>Sets the entire text content of the edit buffer.</summary>
    public void SetText(string text)
    {
        var utf8 = new Utf8String(text);
        utf8.WithPtr((ptr, len) => OpenTuiNative.EditBufferSetText(Handle, ptr, len));
    }

    /// <summary>Sets the text content from a registered memory buffer.</summary>
    public void SetTextFromMem(byte memId) =>
        OpenTuiNative.EditBufferSetTextFromMem(Handle, memId);

    /// <summary>Inserts text at the current cursor position.</summary>
    public void InsertText(string text)
    {
        var utf8 = new Utf8String(text);
        utf8.WithPtr((ptr, len) => OpenTuiNative.EditBufferInsertText(Handle, ptr, len));
    }

    /// <summary>Inserts a character at the cursor position.</summary>
    public void InsertChar(string ch)
    {
        var utf8 = new Utf8String(ch);
        utf8.WithPtr((ptr, len) => OpenTuiNative.EditBufferInsertChar(Handle, ptr, len));
    }

    /// <summary>Replaces the current text content.</summary>
    public void ReplaceText(string text)
    {
        var utf8 = new Utf8String(text);
        utf8.WithPtr((ptr, len) => OpenTuiNative.EditBufferReplaceText(Handle, ptr, len));
    }

    /// <summary>Replaces the current text content from a registered memory buffer.</summary>
    public void ReplaceTextFromMem(byte memId) =>
        OpenTuiNative.EditBufferReplaceTextFromMem(Handle, memId);

    /// <summary>Deletes the character at the cursor position (forward delete).</summary>
    public void DeleteChar() => OpenTuiNative.EditBufferDeleteChar(Handle);

    /// <summary>Deletes the character before the cursor position (backspace).</summary>
    public void DeleteCharBackward() => OpenTuiNative.EditBufferDeleteCharBackward(Handle);

    /// <summary>Deletes a range of text specified by row/column coordinates.</summary>
    public void DeleteRange(uint startRow, uint startCol, uint endRow, uint endCol) =>
        OpenTuiNative.EditBufferDeleteRange(Handle, startRow, startCol, endRow, endCol);

    /// <summary>Deletes the current line.</summary>
    public void DeleteLine() => OpenTuiNative.EditBufferDeleteLine(Handle);

    /// <summary>Inserts a newline at the cursor position.</summary>
    public void NewLine() => OpenTuiNative.EditBufferNewLine(Handle);

    /// <summary>Clears all content from the edit buffer.</summary>
    public void Clear() => OpenTuiNative.EditBufferClear(Handle);

    /// <summary>Clears the entire undo/redo history.</summary>
    public void ClearHistory() => OpenTuiNative.EditBufferClearHistory(Handle);

    #endregion

    #region Cursor

    /// <summary>Gets the current cursor position as a logical row/col/offset.</summary>
    public LogicalCursor GetCursorPosition()
    {
        LogicalCursor cursor = default;
        unsafe
        {
            OpenTuiNative.EditBufferGetCursorPosition(Handle, (nint)(&cursor));
        }
        return cursor;
    }

    /// <summary>Sets the cursor to the specified row and column.</summary>
    public void SetCursor(uint row, uint col) =>
        OpenTuiNative.EditBufferSetCursor(Handle, row, col);

    /// <summary>Sets the cursor to the specified line and column.</summary>
    public void SetCursorToLineCol(uint line, uint col) =>
        OpenTuiNative.EditBufferSetCursorToLineCol(Handle, line, col);

    /// <summary>Sets the cursor position by character offset.</summary>
    public void SetCursorByOffset(uint offset) =>
        OpenTuiNative.EditBufferSetCursorByOffset(Handle, offset);

    #endregion

    #region Movement

    /// <summary>Moves the cursor one position to the left.</summary>
    public void MoveCursorLeft() => OpenTuiNative.EditBufferMoveCursorLeft(Handle);

    /// <summary>Moves the cursor one position to the right.</summary>
    public void MoveCursorRight() => OpenTuiNative.EditBufferMoveCursorRight(Handle);

    /// <summary>Moves the cursor one line up.</summary>
    public void MoveCursorUp() => OpenTuiNative.EditBufferMoveCursorUp(Handle);

    /// <summary>Moves the cursor one line down.</summary>
    public void MoveCursorDown() => OpenTuiNative.EditBufferMoveCursorDown(Handle);

    /// <summary>Moves the cursor to the specified line.</summary>
    public void GotoLine(uint line) => OpenTuiNative.EditBufferGotoLine(Handle, line);

    #endregion

    #region Word Boundaries

    /// <summary>Gets the previous word boundary cursor position.</summary>
    public LogicalCursor GetPrevWordBoundary()
    {
        LogicalCursor cursor = default;
        unsafe
        {
            OpenTuiNative.EditBufferGetPrevWordBoundary(Handle, (nint)(&cursor));
        }
        return cursor;
    }

    /// <summary>Gets the next word boundary cursor position.</summary>
    public LogicalCursor GetNextWordBoundary()
    {
        LogicalCursor cursor = default;
        unsafe
        {
            OpenTuiNative.EditBufferGetNextWordBoundary(Handle, (nint)(&cursor));
        }
        return cursor;
    }

    /// <summary>Gets the end-of-line cursor position.</summary>
    public LogicalCursor GetEOL()
    {
        LogicalCursor cursor = default;
        unsafe
        {
            OpenTuiNative.EditBufferGetEOL(Handle, (nint)(&cursor));
        }
        return cursor;
    }

    #endregion

    #region Undo / Redo

    /// <summary>Undoes the last edit operation. Returns the undo description, or empty if unavailable.</summary>
    public string Undo() =>
        Utf8String.GetString((outBuf, maxLen) => OpenTuiNative.EditBufferUndo(Handle, outBuf, maxLen));

    /// <summary>Redoes the last undone operation. Returns the redo description, or empty if unavailable.</summary>
    public string Redo() =>
        Utf8String.GetString((outBuf, maxLen) => OpenTuiNative.EditBufferRedo(Handle, outBuf, maxLen));

    /// <summary>Gets whether an undo operation is available.</summary>
    public bool CanUndo() => OpenTuiNative.EditBufferCanUndo(Handle);

    /// <summary>Gets whether a redo operation is available.</summary>
    public bool CanRedo() => OpenTuiNative.EditBufferCanRedo(Handle);

    #endregion

    #region Text Access

    /// <summary>Gets the entire text content of the buffer.</summary>
    public string GetText() =>
        Utf8String.GetString((outPtr, maxLen) => OpenTuiNative.EditBufferGetText(Handle, outPtr, maxLen));

    /// <summary>Gets the underlying text buffer handle.</summary>
    public nint GetTextBuffer() => OpenTuiNative.EditBufferGetTextBuffer(Handle);

    /// <summary>Gets the unique identifier of the edit buffer.</summary>
    public ushort GetId() => OpenTuiNative.EditBufferGetId(Handle);

    #endregion

    #region Default Styling

    /// <summary>Sets the default foreground color for unstyled text in the edit buffer.</summary>
    public void SetForeground(Rgba? fg) =>
        RgbaMarshalling.WithColorPtr(fg, ptr =>
            OpenTuiNative.TextBufferSetDefaultFg(GetTextBuffer(), ptr));

    /// <summary>Sets the default background color for unstyled text in the edit buffer.</summary>
    public void SetBackground(Rgba? bg) =>
        RgbaMarshalling.WithColorPtr(bg, ptr =>
            OpenTuiNative.TextBufferSetDefaultBg(GetTextBuffer(), ptr));

    /// <summary>Sets the default text attributes for unstyled text in the edit buffer.</summary>
    public unsafe void SetAttributes(TextAttributes attrs)
    {
        uint value = (uint)attrs;
        OpenTuiNative.TextBufferSetDefaultAttributes(GetTextBuffer(), (nint)(&value));
    }

    #endregion

    #region Position Conversion

    /// <summary>Converts a character offset to a logical cursor position.</summary>
    public bool OffsetToPosition(uint offset, out LogicalCursor cursor)
    {
        cursor = default;
        unsafe
        {
            fixed (LogicalCursor* p = &cursor)
            {
                return OpenTuiNative.EditBufferOffsetToPosition(Handle, offset, (nint)p);
            }
        }
    }

    /// <summary>Converts a row/column position to a character offset.</summary>
    public uint PositionToOffset(uint row, uint col) =>
        OpenTuiNative.EditBufferPositionToOffset(Handle, row, col);

    /// <summary>Gets the byte offset of the start of the specified line.</summary>
    public uint GetLineStartOffset(uint line) =>
        OpenTuiNative.EditBufferGetLineStartOffset(Handle, line);

    /// <summary>Gets a range of text by character offsets.</summary>
    public string GetTextRange(uint start, uint end) =>
        Utf8String.GetString((outPtr, maxLen) =>
            OpenTuiNative.EditBufferGetTextRange(Handle, start, end, outPtr, maxLen));

    /// <summary>Gets a range of text by row/column coordinates.</summary>
    public string GetTextRangeByCoords(uint startRow, uint startCol, uint endRow, uint endCol) =>
        Utf8String.GetString((outPtr, maxLen) =>
            OpenTuiNative.EditBufferGetTextRangeByCoords(Handle, startRow, startCol, endRow, endCol, outPtr, maxLen));

    #endregion

    #region Debug

    /// <summary>Dumps the internal rope structure for debugging.</summary>
    public void DebugLogRope() => OpenTuiNative.EditBufferDebugLogRope(Handle);

    #endregion

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            OpenTuiNative.EditBufferDestroy(_handle);
            _handle = nint.Zero;
        }
    }

    #endregion
}
