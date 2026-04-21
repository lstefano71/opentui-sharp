using OpenTui.Core;
using OpenTui.Core.Managed;
using Xunit;

namespace OpenTui.Core.Tests;

/// <summary>
/// Regression tests for ManagedEditBuffer API parity methods.
/// </summary>
public class ManagedEditBufferApiTests
{
    #region GetId

    [Fact]
    public void GetId_ReturnsUniqueIds()
    {
        using var eb1 = ManagedEditBuffer.Create();
        using var eb2 = ManagedEditBuffer.Create();
        Assert.NotEqual(eb1.GetId(), eb2.GetId());
    }

    [Fact]
    public void GetId_ReturnsNonZero()
    {
        using var eb = ManagedEditBuffer.Create();
        Assert.NotEqual((ushort)0, eb.GetId());
    }

    #endregion

    #region GetTextBuffer

    [Fact]
    public void GetTextBuffer_ReturnsSameBuffer()
    {
        using var eb = ManagedEditBuffer.Create();
        var tb = eb.GetTextBuffer();
        Assert.NotNull(tb);
        Assert.Same(eb.Buffer, tb);
    }

    #endregion

    #region GetCursorPosition

    [Fact]
    public void GetCursorPosition_ReturnsLogicalCursor()
    {
        using var eb = ManagedEditBuffer.Create();
        eb.InsertText("Hello\nWorld");
        eb.SetCursor(1, 3);
        var cursor = eb.GetCursorPosition();
        Assert.Equal(1u, cursor.Row);
        Assert.Equal(3u, cursor.Col);
        Assert.Equal(9u, cursor.Offset); // "Hello\n" = 6, + 3 = 9
    }

    #endregion

    #region SetCursorByOffset

    [Fact]
    public void SetCursorByOffset_SetsCorrectPosition()
    {
        using var eb = ManagedEditBuffer.Create();
        eb.InsertText("Hello\nWorld");
        eb.SetCursorByOffset(7); // offset 7 = line 1, col 1 ("W")
        var (line, col) = eb.GetPrimaryCursor();
        Assert.Equal(1u, line);
        Assert.Equal(1u, col);
    }

    [Fact]
    public void SetCursorByOffset_Zero_MovesToStart()
    {
        using var eb = ManagedEditBuffer.Create();
        eb.InsertText("Hello");
        eb.SetCursorByOffset(0);
        var (line, col) = eb.GetPrimaryCursor();
        Assert.Equal(0u, line);
        Assert.Equal(0u, col);
    }

    #endregion

    #region MoveCursorUp / MoveCursorDown

    [Fact]
    public void MoveCursorUp_FromMiddleLine()
    {
        using var eb = ManagedEditBuffer.Create();
        eb.InsertText("Hello\nWorld\nFoo");
        eb.SetCursor(2, 2);
        eb.MoveCursorUp();
        var (line, col) = eb.GetPrimaryCursor();
        Assert.Equal(1u, line);
        Assert.Equal(2u, col);
    }

    [Fact]
    public void MoveCursorUp_FromLine0_MovesToCol0()
    {
        using var eb = ManagedEditBuffer.Create();
        eb.InsertText("Hello");
        eb.SetCursor(0, 3);
        eb.MoveCursorUp();
        var (line, col) = eb.GetPrimaryCursor();
        Assert.Equal(0u, line);
        Assert.Equal(0u, col);
    }

    [Fact]
    public void MoveCursorUp_ClampsColToShorterLine()
    {
        using var eb = ManagedEditBuffer.Create();
        eb.InsertText("Hi\nHello");
        eb.SetCursor(1, 4);
        eb.MoveCursorUp();
        var (line, col) = eb.GetPrimaryCursor();
        Assert.Equal(0u, line);
        Assert.Equal(2u, col); // "Hi" has length 2
    }

    [Fact]
    public void MoveCursorDown_FromMiddleLine()
    {
        using var eb = ManagedEditBuffer.Create();
        eb.InsertText("Hello\nWorld\nFoo");
        eb.SetCursor(0, 3);
        eb.MoveCursorDown();
        var (line, col) = eb.GetPrimaryCursor();
        Assert.Equal(1u, line);
        Assert.Equal(3u, col);
    }

    [Fact]
    public void MoveCursorDown_FromLastLine_MovesToEnd()
    {
        using var eb = ManagedEditBuffer.Create();
        eb.InsertText("Hello");
        eb.SetCursor(0, 2);
        eb.MoveCursorDown();
        var (line, col) = eb.GetPrimaryCursor();
        Assert.Equal(0u, line);
        Assert.Equal(5u, col);
    }

    [Fact]
    public void MoveCursorDown_ClampsColToShorterLine()
    {
        using var eb = ManagedEditBuffer.Create();
        eb.InsertText("Hello\nHi");
        eb.SetCursor(0, 4);
        eb.MoveCursorDown();
        var (line, col) = eb.GetPrimaryCursor();
        Assert.Equal(1u, line);
        Assert.Equal(2u, col); // "Hi" has length 2
    }

    #endregion

    #region GotoLine

    [Fact]
    public void GotoLine_MovesToStartOfLine()
    {
        using var eb = ManagedEditBuffer.Create();
        eb.InsertText("Line0\nLine1\nLine2");
        eb.GotoLine(1);
        var (line, col) = eb.GetPrimaryCursor();
        Assert.Equal(1u, line);
        Assert.Equal(0u, col);
    }

    [Fact]
    public void GotoLine_ClampsToLastLine()
    {
        using var eb = ManagedEditBuffer.Create();
        eb.InsertText("A\nB");
        eb.GotoLine(999);
        var (line, col) = eb.GetPrimaryCursor();
        Assert.Equal(1u, line);
        Assert.Equal(0u, col);
    }

    #endregion

    #region InsertChar / NewLine

    [Fact]
    public void InsertChar_InsertsAtCursor()
    {
        using var eb = ManagedEditBuffer.Create();
        eb.InsertText("AB");
        eb.SetCursor(0, 1);
        eb.InsertChar("X");
        Assert.Equal("AXB", eb.GetText());
    }

    [Fact]
    public void NewLine_SplitsLine()
    {
        using var eb = ManagedEditBuffer.Create();
        eb.InsertText("AB");
        eb.SetCursor(0, 1);
        eb.NewLine();
        Assert.Equal("A\nB", eb.GetText());
        var (line, col) = eb.GetPrimaryCursor();
        Assert.Equal(1u, line);
        Assert.Equal(0u, col);
    }

    #endregion

    #region ReplaceText / SetText / Clear

    [Fact]
    public void ReplaceText_ReplacesAllContent()
    {
        using var eb = ManagedEditBuffer.Create();
        eb.InsertText("Old content");
        eb.ReplaceText("New content");
        Assert.Equal("New content", eb.GetText());
        var (line, col) = eb.GetPrimaryCursor();
        Assert.Equal(0u, line);
        Assert.Equal(0u, col);
    }

    [Fact]
    public void ReplaceText_CanBeUndone()
    {
        using var eb = ManagedEditBuffer.Create();
        eb.InsertText("Original");
        eb.StoreUndoCheckpoint("before-replace");
        eb.ReplaceText("Replaced");
        Assert.Equal("Replaced", eb.GetText());
        eb.Undo(); // undo replace
        Assert.Equal("Original", eb.GetText());
    }

    [Fact]
    public void SetText_ReplacesContent()
    {
        using var eb = ManagedEditBuffer.Create();
        eb.InsertText("Initial");
        eb.SetText("Replaced");
        Assert.Equal("Replaced", eb.GetText());
    }

    [Fact]
    public void Clear_RemovesAllContent()
    {
        using var eb = ManagedEditBuffer.Create();
        eb.InsertText("Some text");
        eb.Clear();
        Assert.Equal("", eb.GetText());
        var (line, col) = eb.GetPrimaryCursor();
        Assert.Equal(0u, line);
        Assert.Equal(0u, col);
    }

    #endregion

    #region Undo / Redo returning strings

    [Fact]
    public void Undo_ReturnsMetaString()
    {
        using var eb = ManagedEditBuffer.Create();
        eb.InsertText("Hello"); // stores "edit" undo checkpoint
        var meta = eb.Undo();
        Assert.Equal("edit", meta);
    }

    [Fact]
    public void Redo_ReturnsMetaString()
    {
        using var eb = ManagedEditBuffer.Create();
        eb.InsertText("Hello");
        eb.Undo();
        var meta = eb.Redo();
        // Redo returns a meta string (may be empty or the original insert meta)
        Assert.NotNull(meta);
    }

    [Fact]
    public void Undo_ReturnsEmptyWhenNothingToUndo()
    {
        using var eb = ManagedEditBuffer.Create();
        var meta = eb.Undo();
        Assert.Equal(string.Empty, meta);
    }

    [Fact]
    public void Redo_ReturnsEmptyWhenNothingToRedo()
    {
        using var eb = ManagedEditBuffer.Create();
        var meta = eb.Redo();
        Assert.Equal(string.Empty, meta);
    }

    #endregion

    #region OffsetToPosition / PositionToOffset / GetLineStartOffset

    [Fact]
    public void OffsetToPosition_ValidOffset()
    {
        using var eb = ManagedEditBuffer.Create();
        eb.InsertText("Hello\nWorld");
        bool found = eb.OffsetToPosition(7, out var cursor);
        Assert.True(found);
        Assert.Equal(1u, cursor.Row);
        Assert.Equal(1u, cursor.Col);
        Assert.Equal(7u, cursor.Offset);
    }

    [Fact]
    public void OffsetToPosition_OutOfRange_ClampsToEnd()
    {
        using var eb = ManagedEditBuffer.Create();
        eb.InsertText("Hi");
        bool found = eb.OffsetToPosition(999, out var cursor);
        // OffsetToCoords clamps out-of-range to end of last line
        Assert.True(found);
        Assert.Equal(0u, cursor.Row);
        Assert.Equal(2u, cursor.Col);
    }

    [Fact]
    public void PositionToOffset_Roundtrip()
    {
        using var eb = ManagedEditBuffer.Create();
        eb.InsertText("Hello\nWorld");
        uint offset = eb.PositionToOffset(1, 2);
        Assert.Equal(8u, offset); // "Hello\n" = 6, + 2 = 8
    }

    [Fact]
    public void GetLineStartOffset_FirstLine()
    {
        using var eb = ManagedEditBuffer.Create();
        eb.InsertText("Hello\nWorld");
        Assert.Equal(0u, eb.GetLineStartOffset(0));
    }

    [Fact]
    public void GetLineStartOffset_SecondLine()
    {
        using var eb = ManagedEditBuffer.Create();
        eb.InsertText("Hello\nWorld");
        Assert.Equal(6u, eb.GetLineStartOffset(1)); // "Hello" = 5 bytes + 1 newline
    }

    #endregion

    #region GetTextRangeByCoords

    [Fact]
    public void GetTextRangeByCoords_SingleLine()
    {
        using var eb = ManagedEditBuffer.Create();
        eb.InsertText("Hello World");
        var text = eb.GetTextRangeByCoords(0, 0, 0, 5);
        Assert.Equal("Hello", text);
    }

    [Fact]
    public void GetTextRangeByCoords_AcrossLines()
    {
        using var eb = ManagedEditBuffer.Create();
        eb.InsertText("Hello\nWorld");
        var text = eb.GetTextRangeByCoords(0, 3, 1, 3);
        Assert.Equal("lo\nWor", text);
    }

    #endregion
}
