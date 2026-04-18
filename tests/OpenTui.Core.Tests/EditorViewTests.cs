using OpenTui.Core;
using Xunit;

namespace OpenTui.Core.Tests;

/// <summary>
/// C# port of the Zig editor-view_test.zig tests.
/// Tests that require internal Zig structures (rope, opt_buffer, placeholder internals,
/// logicalToVisualCursor, visualToLogicalCursor) are skipped since the C# wrapper
/// does not expose those APIs.
/// </summary>
public class EditorViewTests
{
    private const string TwentyLines =
        "Line 0\nLine 1\nLine 2\nLine 3\nLine 4\nLine 5\nLine 6\nLine 7\nLine 8\nLine 9\n" +
        "Line 10\nLine 11\nLine 12\nLine 13\nLine 14\nLine 15\nLine 16\nLine 17\nLine 18\nLine 19";

    private const string TenLines =
        "Line 0\nLine 1\nLine 2\nLine 3\nLine 4\nLine 5\nLine 6\nLine 7\nLine 8\nLine 9";

    private const string LongAlpha =
        "AAAAAAAAAABBBBBBBBBBCCCCCCCCCCDDDDDDDDDDEEEEEEEEEEFFFFFFFFFFFFGGGGGGGGGG" +
        "HHHHHHHHHHIIIIIIIIIIJJJJJJJJJJKKKKKKKKKKLLLLLLLLLLMMMMMMMMMM" +
        "NNNNNNNNNNOOOOOOOOOOPPPPPPPPPPQQQQQQQQQQRRRRRRRRRRSSSSSSSSSS" +
        "TTTTTTTTTTUUUUUUUUUUVVVVVVVVVVWWWWWWWWWWXXXXXXXXXXYYYYYYYYYYZZZZZZZZZZ";

    private const string LongLine160 =
        "AAAAAAAAAABBBBBBBBBBCCCCCCCCCCDDDDDDDDDDEEEEEEEEEEFFFFFFFFFFFFGGGGGGGGGG" +
        "HHHHHHHHHHIIIIIIIIIIJJJJJJJJJJKKKKKKKKKKLLLLLLLLLLMMMMMMMMMM" +
        "NNNNNNNNNNOOOOOOOOOOPPPPPPPPPP";

    private static readonly Rgba DummyFg = Rgba.White;
    private static readonly Rgba DummyBg = Rgba.Black;

    #region 1. Init and Deinit

    [Fact]
    public void InitAndDeinit()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 24);

        var vp = ev.GetViewport();
        Assert.Equal(80, vp.Width);
        Assert.Equal(24, vp.Height);
        Assert.Equal(0, vp.Y);
    }

    #endregion

    #region 2. EnsureCursorVisible – scrolls down

    [Fact]
    public void EnsureCursorVisible_ScrollsDown()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 10);

        eb.InsertText(TwentyLines);

        var cursor = eb.GetCursorPosition();
        Assert.Equal(19u, cursor.Row);

        ev.GetVirtualLineCount(); // trigger layout

        var vp = ev.GetViewport();
        Assert.True(vp.Y > 0);
        Assert.True(cursor.Row >= (uint)vp.Y);
        Assert.True(cursor.Row < (uint)(vp.Y + vp.Height));
    }

    #endregion

    #region 3. EnsureCursorVisible – scrolls up

    [Fact]
    public void EnsureCursorVisible_ScrollsUp()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 10);

        eb.InsertText(TwentyLines);
        ev.GetVirtualLineCount();

        var vp = ev.GetViewport();
        Assert.True(vp.Y > 0);

        eb.GotoLine(0);
        ev.GetVirtualLineCount();

        var cursor = eb.GetCursorPosition();
        Assert.Equal(0u, cursor.Row);

        vp = ev.GetViewport();
        Assert.Equal(0, vp.Y);
    }

    #endregion

    #region 4. MoveDown scrolls viewport automatically

    [Fact]
    public void MoveDown_ScrollsViewportAutomatically()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 10);

        eb.InsertText(TwentyLines);
        eb.SetCursor(0, 0);
        ev.GetVirtualLineCount();

        var vp = ev.GetViewport();
        Assert.Equal(0, vp.Y);

        for (int i = 0; i < 15; i++)
            eb.MoveCursorDown();

        ev.GetVirtualLineCount();

        var cursor = eb.GetCursorPosition();
        Assert.Equal(15u, cursor.Row);

        vp = ev.GetViewport();
        Assert.True(cursor.Row >= (uint)vp.Y);
        Assert.True(cursor.Row < (uint)(vp.Y + vp.Height));
    }

    #endregion

    #region 5. MoveUp scrolls viewport automatically

    [Fact]
    public void MoveUp_ScrollsViewportAutomatically()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 10);

        eb.InsertText(TwentyLines);
        ev.GetVirtualLineCount();

        var vp = ev.GetViewport();
        var initialY = vp.Y;
        Assert.True(initialY > 0);

        for (int i = 0; i < 10; i++)
            eb.MoveCursorUp();

        ev.GetVirtualLineCount();

        var cursor = eb.GetCursorPosition();
        Assert.Equal(9u, cursor.Row);

        vp = ev.GetViewport();
        Assert.True(vp.Y < initialY);
        Assert.True(cursor.Row >= (uint)vp.Y);
        Assert.True(cursor.Row < (uint)(vp.Y + vp.Height));
    }

    #endregion

    #region 6. Scroll margin keeps cursor away from edges

    [Fact]
    public void ScrollMargin_KeepsCursorFromEdges()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 10);

        ev.SetScrollMargin(0.2f);
        eb.InsertText(TwentyLines);
        eb.GotoLine(5);

        var cursor = eb.GetCursorPosition();
        Assert.Equal(5u, cursor.Row);

        var vp = ev.GetViewport();
        var cursorOffsetInViewport = cursor.Row - (uint)vp.Y;

        Assert.True(cursorOffsetInViewport >= 2);
        Assert.True(cursorOffsetInViewport < (uint)vp.Height - 2);
    }

    #endregion

    #region 7. InsertText with newlines maintains cursor visibility

    [Fact]
    public void InsertText_NewlinesMaintainCursorVisibility()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 5);

        eb.InsertText(TenLines);
        ev.GetVirtualLineCount();

        var cursor = eb.GetCursorPosition();
        var vp = ev.GetViewport();

        Assert.True(cursor.Row >= (uint)vp.Y);
        Assert.True(cursor.Row < (uint)(vp.Y + vp.Height));
    }

    #endregion

    #region 8. Backspace at line start maintains visibility

    [Fact]
    public void Backspace_AtLineStartMaintainsVisibility()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 5);

        eb.InsertText(TenLines);
        eb.DeleteCharBackward();
        ev.GetVirtualLineCount();

        var cursor = eb.GetCursorPosition();
        var vp = ev.GetViewport();

        Assert.True(cursor.Row >= (uint)vp.Y);
        Assert.True(cursor.Row < (uint)(vp.Y + vp.Height));
    }

    #endregion

    #region 9. DeleteForward at line end maintains visibility

    [Fact]
    public void DeleteForward_AtLineEndMaintainsVisibility()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 5);

        eb.InsertText(TenLines);
        eb.SetCursor(8, 6);
        eb.DeleteChar();
        ev.GetVirtualLineCount();

        var cursor = eb.GetCursorPosition();
        var vp = ev.GetViewport();

        Assert.True(cursor.Row >= (uint)vp.Y);
        Assert.True(cursor.Row < (uint)(vp.Y + vp.Height));
    }

    #endregion

    #region 10. DeleteRange maintains cursor visibility

    [Fact]
    public void DeleteRange_MaintainsCursorVisibility()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 5);

        eb.InsertText(TenLines);
        eb.DeleteRange(2, 0, 7, 6);
        ev.GetVirtualLineCount();

        var cursor = eb.GetCursorPosition();
        var vp = ev.GetViewport();

        Assert.True(cursor.Row >= (uint)vp.Y);
        Assert.True(cursor.Row < (uint)(vp.Y + vp.Height));
    }

    #endregion

    #region 11. DeleteLine maintains cursor visibility

    [Fact]
    public void DeleteLine_MaintainsCursorVisibility()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 5);

        eb.InsertText(TenLines);
        eb.GotoLine(7);
        eb.DeleteLine();
        ev.GetVirtualLineCount();

        var cursor = eb.GetCursorPosition();
        var vp = ev.GetViewport();

        Assert.True(cursor.Row >= (uint)vp.Y);
        Assert.True(cursor.Row < (uint)(vp.Y + vp.Height));
    }

    #endregion

    #region 12. SetText resets viewport to top

    [Fact]
    public void SetText_ResetsViewportToTop()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 5);

        eb.InsertText(TenLines);
        ev.GetVirtualLineCount();

        var vp = ev.GetViewport();
        Assert.True(vp.Y > 0);

        eb.SetText("New Line 0\nNew Line 1\nNew Line 2");

        var cursor = eb.GetCursorPosition();
        Assert.Equal(0u, cursor.Row);

        ev.GetVirtualLineCount();

        vp = ev.GetViewport();
        Assert.Equal(0, vp.Y);
    }

    #endregion

    #region 13. Viewport respects total line count as max offset

    [Fact]
    public void Viewport_RespectsLinecountAsMaxOffset()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 10);

        eb.InsertText("Line 0\nLine 1\nLine 2\nLine 3\nLine 4");
        eb.GotoLine(4);

        var vp = ev.GetViewport();
        Assert.Equal(0, vp.Y);
    }

    #endregion

    #region 14. Horizontal movement doesn't affect vertical scroll

    [Fact]
    public void HorizontalMovement_DoesNotAffectVerticalScroll()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 10);

        eb.InsertText("Line 0\nLine 1\nLine 2\nLine 3\nLine 4");
        eb.SetCursor(2, 0);

        var vpBefore = ev.GetViewport();

        eb.MoveCursorRight();
        eb.MoveCursorRight();
        eb.MoveCursorRight();

        var vpAfter = ev.GetViewport();
        Assert.Equal(vpBefore.Y, vpAfter.Y);
    }

    #endregion

    #region 15. Cursor at boundaries doesn't cause invalid viewport

    [Fact]
    public void CursorAtBoundaries_DoesNotCauseInvalidViewport()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 10);

        eb.SetCursor(0, 0);

        var vp = ev.GetViewport();
        Assert.Equal(0, vp.Y);

        eb.InsertText("First line");
        eb.SetCursor(0, 0);

        vp = ev.GetViewport();
        Assert.Equal(0, vp.Y);

        eb.MoveCursorLeft();
        vp = ev.GetViewport();
        Assert.Equal(0, vp.Y);

        eb.MoveCursorUp();
        vp = ev.GetViewport();
        Assert.Equal(0, vp.Y);
    }

    #endregion

    #region 16. Rapid cursor movements maintain visibility

    [Fact]
    public void RapidCursorMovements_MaintainVisibility()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 10);

        eb.InsertText(
            "Line 0\nLine 1\nLine 2\nLine 3\nLine 4\nLine 5\nLine 6\nLine 7\nLine 8\nLine 9\n" +
            "Line 10\nLine 11\nLine 12\nLine 13\nLine 14\nLine 15\nLine 16\nLine 17\nLine 18\nLine 19\n" +
            "Line 20\nLine 21\nLine 22\nLine 23\nLine 24\nLine 25\nLine 26\nLine 27\nLine 28\nLine 29");

        eb.GotoLine(0);
        eb.GotoLine(29);
        eb.GotoLine(15);
        eb.GotoLine(5);
        eb.GotoLine(25);

        ev.GetVirtualLineCount();

        var cursor = eb.GetCursorPosition();
        var vp = ev.GetViewport();

        Assert.True(cursor.Row >= (uint)vp.Y);
        Assert.True(cursor.Row < (uint)(vp.Y + vp.Height));
    }

    #endregion

    #region 17. VisualCursor without wrapping

    [Fact]
    public void VisualCursor_WithoutWrapping()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 10);

        eb.InsertText("Hello World\nSecond Line\nThird Line");
        eb.SetCursor(1, 3);

        var vcursor = ev.GetVisualCursor();
        Assert.Equal(1u, vcursor.VisualRow);
        Assert.Equal(3u, vcursor.VisualCol);
        Assert.Equal(1u, vcursor.LogicalRow);
        Assert.Equal(3u, vcursor.LogicalCol);
    }

    #endregion

    #region 18. VisualCursor with character wrapping

    [Fact]
    public void VisualCursor_WithCharWrapping()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 20, 10);

        ev.SetWrapMode((byte)WrapMode.Char);
        eb.SetText("This is a very long line that will definitely wrap at 20 characters");
        eb.SetCursor(0, 25);

        var vcursor = ev.GetVisualCursor();
        Assert.Equal(0u, vcursor.LogicalRow);
        Assert.Equal(25u, vcursor.LogicalCol);
        Assert.True(vcursor.VisualRow > 0);
        Assert.True(vcursor.VisualCol <= 20);
    }

    #endregion

    #region 19. VisualCursor with word wrapping

    [Fact]
    public void VisualCursor_WithWordWrapping()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 20, 10);

        ev.SetWrapMode((byte)WrapMode.Word);
        eb.SetText("Hello world this is a test of word wrapping");

        // Just verify getVisualCursor completes without error
        _ = ev.GetVisualCursor();
    }

    #endregion

    #region 20. MoveUpVisual with wrapping

    [Fact]
    public void MoveUpVisual_WithWrapping()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 20, 10);

        ev.SetWrapMode((byte)WrapMode.Char);
        eb.SetText("This is a very long line that will definitely wrap multiple times at twenty characters");
        eb.SetCursor(0, 50);

        var vcursorBefore = ev.GetVisualCursor();
        var visualRowBefore = vcursorBefore.VisualRow;

        ev.MoveUpVisual();

        var vcursorAfter = ev.GetVisualCursor();
        Assert.Equal(visualRowBefore - 1, vcursorAfter.VisualRow);
        Assert.Equal(0u, vcursorAfter.LogicalRow);
    }

    #endregion

    #region 21. MoveDownVisual with wrapping

    [Fact]
    public void MoveDownVisual_WithWrapping()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 20, 10);

        ev.SetWrapMode((byte)WrapMode.Char);
        eb.SetText("This is a very long line that will definitely wrap multiple times at twenty characters");
        eb.SetCursor(0, 0);

        var vcursorBefore = ev.GetVisualCursor();
        Assert.Equal(0u, vcursorBefore.VisualRow);

        ev.MoveDownVisual();

        var vcursorAfter = ev.GetVisualCursor();
        Assert.Equal(1u, vcursorAfter.VisualRow);
        Assert.Equal(0u, vcursorAfter.LogicalRow);
    }

    #endregion

    // Test 22 (visualToLogicalCursor) skipped – no C# API

    #region 23. MoveUpVisual at top boundary

    [Fact]
    public void MoveUpVisual_AtTopBoundary()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 20, 10);

        ev.SetWrapMode((byte)WrapMode.Char);
        eb.SetText("Short line");
        eb.SetCursor(0, 0);

        var before = eb.GetCursorPosition();
        ev.MoveUpVisual();
        var after = eb.GetCursorPosition();

        Assert.Equal(before.Row, after.Row);
        Assert.Equal(before.Col, after.Col);
    }

    #endregion

    #region 24. MoveDownVisual at bottom boundary

    [Fact]
    public void MoveDownVisual_AtBottomBoundary()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 20, 10);

        ev.SetWrapMode((byte)WrapMode.Char);
        eb.SetText("Short line\nSecond line");
        eb.SetCursor(1, 0);

        var before = eb.GetCursorPosition();
        ev.MoveDownVisual();
        var after = eb.GetCursorPosition();

        Assert.Equal(before.Row, after.Row);
    }

    #endregion

    #region 25. VisualCursor preserves desired column across wrapped lines

    [Fact]
    public void VisualCursor_PreservesDesiredColumn()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 20, 10);

        ev.SetWrapMode((byte)WrapMode.Char);
        eb.SetText("12345678901234567890123456789012345678901234567890");
        eb.SetCursor(0, 15);

        ev.MoveDownVisual();
        ev.MoveDownVisual();
        ev.MoveUpVisual();

        var vcursor = ev.GetVisualCursor();
        Assert.True(vcursor.VisualCol <= 20);
    }

    #endregion

    #region 26. VisualCursor with multiple logical lines and wrapping

    [Fact]
    public void VisualCursor_MultipleLogicalLinesWithWrapping()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 20, 10);

        ev.SetWrapMode((byte)WrapMode.Char);
        eb.SetText("Short line 1\nThis is a very long line that will wrap multiple times\nShort line 3");
        eb.SetCursor(1, 30);

        var vcursor = ev.GetVisualCursor();
        Assert.Equal(1u, vcursor.LogicalRow);
        Assert.True(vcursor.VisualRow > 1);
    }

    #endregion

    // Test 27 (logicalToVisualCursor handles cursor past line end) skipped – no C# API

    #region 28. GetTextBufferView returns non-null handle

    [Fact]
    public void GetTextBufferView_ReturnsNonZeroHandle()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 10);

        var tbv = ev.GetTextBufferView();
        Assert.NotEqual(nint.Zero, tbv);
    }

    #endregion

    // Test 29 (getEditBuffer returns correct buffer) skipped – no C# API

    #region 30. SetViewportSize maintains cursor visibility

    [Fact]
    public void SetViewportSize_MaintainsCursorVisibility()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 10);

        eb.InsertText("Line 0\nLine 1\nLine 2\nLine 3\nLine 4\nLine 5\nLine 6\nLine 7\nLine 8\nLine 9\nLine 10\nLine 11\nLine 12\nLine 13\nLine 14");
        eb.GotoLine(10);

        ev.SetViewportSize(80, 5);

        var vp = ev.GetViewport();
        Assert.Equal(80, vp.Width);
        Assert.Equal(5, vp.Height);
    }

    #endregion

    #region 31. MoveDownVisual across empty line preserves desired column

    [Fact]
    public void MoveDownVisual_AcrossEmptyLine_PreservesDesiredColumn()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 10);

        eb.SetText("Line with some text\n\nAnother line with text");
        eb.SetCursor(0, 10);

        var vcursorBefore = ev.GetVisualCursor();
        Assert.Equal(10u, vcursorBefore.VisualCol);

        ev.MoveDownVisual();

        var vcursorEmpty = ev.GetVisualCursor();
        Assert.Equal(1u, vcursorEmpty.LogicalRow);
        Assert.Equal(0u, vcursorEmpty.VisualCol);

        ev.MoveDownVisual();

        var vcursorAfter = ev.GetVisualCursor();
        Assert.Equal(2u, vcursorAfter.LogicalRow);
        Assert.Equal(10u, vcursorAfter.VisualCol);
    }

    #endregion

    #region 32. MoveUpVisual across empty line preserves desired column

    [Fact]
    public void MoveUpVisual_AcrossEmptyLine_PreservesDesiredColumn()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 10);

        eb.SetText("Line with some text\n\nAnother line with text");
        eb.SetCursor(2, 10);

        var vcursorBefore = ev.GetVisualCursor();
        Assert.Equal(10u, vcursorBefore.VisualCol);

        ev.MoveUpVisual();

        var vcursorEmpty = ev.GetVisualCursor();
        Assert.Equal(1u, vcursorEmpty.LogicalRow);
        Assert.Equal(0u, vcursorEmpty.VisualCol);

        ev.MoveUpVisual();

        var vcursorAfter = ev.GetVisualCursor();
        Assert.Equal(0u, vcursorAfter.LogicalRow);
        Assert.Equal(10u, vcursorAfter.VisualCol);
    }

    #endregion

    #region 33. Horizontal movement resets desired visual column

    [Fact]
    public void HorizontalMovement_ResetsDesiredVisualColumn()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 10);

        eb.SetText("Line with some text\n\nAnother line with text");
        eb.SetCursor(0, 10);

        var vcursorInitial = ev.GetVisualCursor();
        Assert.Equal(10u, vcursorInitial.VisualCol);

        ev.MoveDownVisual();
        ev.MoveDownVisual();

        var vcursorAfter = ev.GetVisualCursor();
        Assert.Equal(2u, vcursorAfter.LogicalRow);
        Assert.Equal(10u, vcursorAfter.VisualCol);

        eb.MoveCursorRight();

        var vcursorAfterRight = ev.GetVisualCursor();
        Assert.Equal(11u, vcursorAfterRight.VisualCol);

        ev.MoveUpVisual();
        ev.MoveUpVisual();

        var vcursorFinal = ev.GetVisualCursor();
        Assert.Equal(0u, vcursorFinal.LogicalRow);
        Assert.Equal(11u, vcursorFinal.VisualCol);
    }

    #endregion

    // Test 34 (inserting newlines maintains rope integrity) skipped – rope internals not exposed

    #region 35. Visual cursor stays in sync after scrolling and moving up

    [Fact]
    public void VisualCursor_StaysInSyncAfterScrollAndMoveUp()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 10);

        eb.InsertText("Line 0\nLine 1\nLine 2\nLine 3\nLine 4");

        var cursor = eb.GetCursorPosition();
        Assert.Equal(4u, cursor.Row);
        Assert.Equal(6u, cursor.Col);

        ev.GetVirtualLineCount();

        var vp = ev.GetViewport();
        Assert.Equal(0, vp.Y);

        for (int i = 0; i < 6; i++)
        {
            eb.InsertText("\n");
            ev.GetVirtualLineCount();
        }

        cursor = eb.GetCursorPosition();
        Assert.Equal(10u, cursor.Row);
        Assert.Equal(0u, cursor.Col);

        vp = ev.GetViewport();
        Assert.True(vp.Y > 0);

        var vcursorBefore = ev.GetVisualCursor();
        Assert.Equal(10u, vcursorBefore.LogicalRow);

        ev.MoveUpVisual();
        ev.GetVirtualLineCount();

        var vcursorAfterUp = ev.GetVisualCursor();
        var logicalAfterUp = eb.GetCursorPosition();

        Assert.Equal(9u, logicalAfterUp.Row);
        Assert.Equal(9u, vcursorAfterUp.LogicalRow);
        Assert.True(vcursorAfterUp.VisualRow < vcursorBefore.VisualRow);

        eb.InsertText("X");
        ev.GetVirtualLineCount();

        var cursorAfterInsert = eb.GetCursorPosition();
        var vcursorAfterInsert = ev.GetVisualCursor();

        Assert.Equal(9u, cursorAfterInsert.Row);
        Assert.Equal(1u, cursorAfterInsert.Col);
        Assert.Equal(9u, vcursorAfterInsert.LogicalRow);
        Assert.Equal(1u, vcursorAfterInsert.LogicalCol);

        // Verify line 9 starts with "X"
        var text = ev.GetText();
        var lines = text.Split('\n');
        Assert.True(lines.Length > 9);
        Assert.StartsWith("X", lines[9]);
    }

    #endregion

    #region 36. Cursor positioning after wide grapheme

    [Fact]
    public void CursorPositioning_AfterWideGrapheme()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 10);

        eb.InsertText("AB東CD");

        var cursor = eb.GetCursorPosition();
        Assert.Equal(0u, cursor.Row);
        Assert.Equal(6u, cursor.Col);

        eb.SetCursor(0, 4);
        var cursorAfterMove = eb.GetCursorPosition();
        Assert.Equal(4u, cursorAfterMove.Col);

        var vcursor = ev.GetVisualCursor();
        Assert.Equal(0u, vcursor.LogicalRow);
        Assert.Equal(4u, vcursor.LogicalCol);
        Assert.Equal(4u, vcursor.VisualCol);
    }

    #endregion

    #region 37. Backspace after wide grapheme updates cursor correctly

    [Fact]
    public void Backspace_AfterWideGrapheme()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 10);

        eb.InsertText("AB東CD");
        eb.SetCursor(0, 4);
        eb.DeleteCharBackward();

        var cursor = eb.GetCursorPosition();
        Assert.Equal(0u, cursor.Row);
        Assert.Equal(2u, cursor.Col);

        var vcursor = ev.GetVisualCursor();
        Assert.Equal(2u, vcursor.LogicalCol);
        Assert.Equal(2u, vcursor.VisualCol);

        var text = eb.GetText();
        Assert.Equal("ABCD", text);
    }

    #endregion

    #region 38. Viewport scrolling with wrapped lines: down + edit + up

    [Fact]
    public void WrappedViewportScroll_DownEditUp()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 20, 10);

        ev.SetWrapMode((byte)WrapMode.Char);
        ev.SetViewport(0, 0, 20, 10, true);
        eb.SetText(LongAlpha);
        eb.SetCursor(0, 0);
        ev.GetVirtualLineCount();

        var vp = ev.GetViewport();
        Assert.Equal(0, vp.Y);

        ev.MoveDownVisual();
        ev.MoveDownVisual();
        ev.MoveDownVisual();
        ev.GetVirtualLineCount();

        _ = ev.GetVisualCursor();

        eb.InsertText("X");
        ev.GetVirtualLineCount();

        ev.MoveUpVisual();
        ev.MoveUpVisual();
        ev.MoveUpVisual();
        ev.GetVirtualLineCount();

        vp = ev.GetViewport();
        var vcursorFinal = ev.GetVisualCursor();

        Assert.Equal(0u, vcursorFinal.VisualRow);
        Assert.Equal(0, vp.Y);
    }

    #endregion

    #region 39. Viewport scrolling with wrapped lines: aggressive down + edit + up

    [Fact]
    public void WrappedViewportScroll_AggressiveDownEditUp()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 20, 10);

        ev.SetWrapMode((byte)WrapMode.Char);
        ev.SetViewport(0, 0, 20, 10, true);
        eb.SetText(LongAlpha);
        eb.SetCursor(0, 0);
        ev.GetVirtualLineCount();

        var totalVlines = ev.GetTotalVirtualLineCount();
        Assert.True(totalVlines > 10);

        var vp = ev.GetViewport();
        Assert.Equal(0, vp.Y);

        for (int i = 0; i < 12; i++)
            ev.MoveDownVisual();
        ev.GetVirtualLineCount();

        vp = ev.GetViewport();
        Assert.True(vp.Y > 0);

        eb.InsertText("TEST");
        ev.GetVirtualLineCount();

        for (int i = 0; i < 12; i++)
            ev.MoveUpVisual();
        ev.GetVirtualLineCount();

        vp = ev.GetViewport();
        var vcursor = ev.GetVisualCursor();

        Assert.Equal(0u, vcursor.VisualRow);
        Assert.Equal(0, vp.Y);
    }

    #endregion

    #region 40. Viewport scrolling with wrapped lines: multiple edits and movements

    [Fact]
    public void WrappedViewportScroll_MultipleEditsAndMovements()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 15, 8);

        ev.SetWrapMode((byte)WrapMode.Char);
        ev.SetViewport(0, 0, 15, 8, true);
        eb.SetText(
            "AAAAAAAAAABBBBBBBBBBCCCCCCCCCCDDDDDDDDDDEEEEEEEEEEFFFFFFFFFFFFGGGGGGGGGG" +
            "HHHHHHHHHHIIIIIIIIIIJJJJJJJJJJKKKKKKKKKKLLLLLLLLLLMMMMMMMMMM" +
            "NNNNNNNNNNOOOOOOOOOOPPPPPPPPPPQQQQQQQQQQRRRRRRRRRRSSSSSSSSSS" +
            "TTTTTTTTTTUUUUUUUUUUVVVVVVVVVV");
        eb.SetCursor(0, 0);
        ev.GetVirtualLineCount();

        ev.MoveDownVisual();
        ev.MoveDownVisual();
        ev.GetVirtualLineCount();

        eb.InsertText("A");
        ev.GetVirtualLineCount();

        ev.MoveDownVisual();
        ev.GetVirtualLineCount();

        eb.InsertText("B");
        ev.GetVirtualLineCount();

        ev.MoveUpVisual();
        ev.MoveUpVisual();
        ev.MoveUpVisual();
        ev.GetVirtualLineCount();

        var vp = ev.GetViewport();
        var vcursor = ev.GetVisualCursor();

        Assert.Equal(0u, vcursor.VisualRow);
        Assert.Equal(0, vp.Y);
    }

    #endregion

    #region 41. Viewport scrolling with wrapped lines: verify viewport consistency

    [Fact]
    public void WrappedViewportScroll_VerifyConsistency()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 20, 10);

        ev.SetWrapMode((byte)WrapMode.Char);
        ev.SetViewport(0, 0, 20, 10, true);
        eb.SetText(LongAlpha);
        eb.SetCursor(0, 0);
        ev.GetVirtualLineCount();

        var vlineCount = ev.GetTotalVirtualLineCount();
        Assert.True(vlineCount >= 10);

        uint movementsDown = 0;
        for (int i = 0; i < 5; i++)
        {
            var vcursorBefore = ev.GetVisualCursor();
            ev.MoveDownVisual();
            var vcursorAfter = ev.GetVisualCursor();
            if (vcursorAfter.VisualRow > vcursorBefore.VisualRow)
                movementsDown++;
        }
        ev.GetVirtualLineCount();

        eb.InsertText("EDITED");
        ev.GetVirtualLineCount();

        for (uint i = 0; i < movementsDown; i++)
            ev.MoveUpVisual();
        ev.GetVirtualLineCount();

        var vpFinal = ev.GetViewport();
        var vcursorFinal = ev.GetVisualCursor();

        Assert.Equal(0u, vcursorFinal.VisualRow);
        Assert.Equal(0, vpFinal.Y);
    }

    #endregion

    #region 42. Viewport scrolling with wrapped lines: backspace after scroll

    [Fact]
    public void WrappedViewportScroll_BackspaceAfterScroll()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 20, 10);

        ev.SetWrapMode((byte)WrapMode.Char);
        ev.SetViewport(0, 0, 20, 10, true);
        eb.SetText(LongAlpha);
        eb.SetCursor(0, 0);
        ev.GetVirtualLineCount();

        ev.MoveDownVisual();
        ev.MoveDownVisual();
        ev.GetVirtualLineCount();

        eb.DeleteCharBackward();
        ev.GetVirtualLineCount();

        ev.MoveUpVisual();
        ev.MoveUpVisual();
        ev.GetVirtualLineCount();

        var vp = ev.GetViewport();
        var vcursor = ev.GetVisualCursor();

        Assert.Equal(0u, vcursor.VisualRow);
        Assert.Equal(0, vp.Y);
    }

    #endregion

    #region 43. Viewport scrolling with wrapped lines: viewport follows cursor precisely

    [Fact]
    public void WrappedViewportScroll_FollowsCursorPrecisely()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 20, 5);

        ev.SetWrapMode((byte)WrapMode.Char);
        ev.SetViewport(0, 0, 20, 5, true);
        eb.SetText(LongAlpha);
        eb.SetCursor(0, 0);
        ev.GetVirtualLineCount();

        for (int i = 0; i < 10; i++)
        {
            ev.MoveDownVisual();
            ev.GetVirtualLineCount();

            var vp = ev.GetViewport();
            var vcursor = ev.GetVisualCursor();

            Assert.True(vcursor.VisualRow < (uint)vp.Height);
        }

        eb.InsertText("MIDDLE");
        ev.GetVirtualLineCount();

        var vpMiddle = ev.GetViewport();
        var vcursorMiddle = ev.GetVisualCursor();
        Assert.True(vcursorMiddle.VisualRow < (uint)vpMiddle.Height);

        for (int i = 0; i < 10; i++)
        {
            ev.MoveUpVisual();
            ev.GetVirtualLineCount();

            var vp = ev.GetViewport();
            var vcursor = ev.GetVisualCursor();

            Assert.True(vcursor.VisualRow < (uint)vp.Height);
        }

        var vpFinal = ev.GetViewport();
        var vcursorFinal = ev.GetVisualCursor();

        Assert.Equal(0u, vcursorFinal.VisualRow);
        Assert.Equal(0, vpFinal.Y);
    }

    #endregion

    #region 44. Wrapped lines: specific scenario with insert and deletions

    [Fact]
    public void WrappedLines_InsertAndDeletions()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 20, 10);

        ev.SetWrapMode((byte)WrapMode.Char);
        ev.SetViewport(0, 0, 20, 10, true);
        eb.SetText(LongAlpha);
        eb.SetCursor(0, 0);
        ev.GetVirtualLineCount();

        var vp = ev.GetViewport();
        Assert.Equal(0, vp.Y);

        for (int i = 0; i < 5; i++)
            ev.MoveDownVisual();
        ev.GetVirtualLineCount();

        var vcursorMid = ev.GetVisualCursor();
        Assert.Equal(5u, vcursorMid.VisualRow);

        eb.InsertText("XXX");
        ev.GetVirtualLineCount();

        vp = ev.GetViewport();
        var vcursorAfterInsert = ev.GetVisualCursor();
        Assert.True(vcursorAfterInsert.VisualRow < (uint)vp.Height);

        eb.DeleteCharBackward();
        eb.DeleteCharBackward();
        eb.DeleteCharBackward();
        ev.GetVirtualLineCount();

        for (int i = 0; i < 5; i++)
            ev.MoveUpVisual();
        ev.GetVirtualLineCount();

        vp = ev.GetViewport();
        var vcursorFinal = ev.GetVisualCursor();

        Assert.Equal(0u, vcursorFinal.VisualRow);
        Assert.Equal(0, vp.Y);
    }

    #endregion

    #region 45. Wrapped lines: many small edits with viewport scrolling

    [Fact]
    public void WrappedLines_ManySmallEdits()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 15, 8);

        ev.SetWrapMode((byte)WrapMode.Char);
        ev.SetViewport(0, 0, 15, 8, true);
        eb.SetText(
            "AAAAAAAAAABBBBBBBBBBCCCCCCCCCCDDDDDDDDDDEEEEEEEEEEFFFFFFFFFFFFGGGGGGGGGG" +
            "HHHHHHHHHHIIIIIIIIIIJJJJJJJJJJKKKKKKKKKKLLLLLLLLLLMMMMMMMMMM" +
            "NNNNNNNNNNOOOOOOOOOOPPPPPPPPPPQQQQQQQQQQRRRRRRRRRRSSSSSSSSSS" +
            "TTTTTTTTTTUUUUUUUUUUVVVVVVVVVV");
        eb.SetCursor(0, 0);
        ev.GetVirtualLineCount();

        ev.MoveDownVisual();
        ev.MoveDownVisual();
        ev.GetVirtualLineCount();
        eb.InsertText("1");
        ev.GetVirtualLineCount();

        ev.MoveDownVisual();
        ev.GetVirtualLineCount();
        eb.InsertText("2");
        ev.GetVirtualLineCount();

        ev.MoveDownVisual();
        ev.GetVirtualLineCount();
        eb.InsertText("3");
        ev.GetVirtualLineCount();

        ev.MoveUpVisual();
        ev.GetVirtualLineCount();
        eb.InsertText("4");
        ev.GetVirtualLineCount();

        ev.MoveUpVisual();
        ev.MoveUpVisual();
        ev.MoveUpVisual();
        ev.GetVirtualLineCount();

        var vp = ev.GetViewport();
        var vcursor = ev.GetVisualCursor();

        Assert.Equal(0u, vcursor.VisualRow);
        Assert.Equal(0, vp.Y);
    }

    #endregion

    #region 46. Horizontal scroll: cursor moves right beyond viewport

    [Fact]
    public void HorizontalScroll_CursorMovesRightBeyondViewport()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 20, 10);

        eb.SetText("This is a very long line that exceeds the viewport width of 20 characters");
        eb.SetCursor(0, 0);
        ev.GetVirtualLineCount();

        var vp = ev.GetViewport();
        Assert.Equal(0, vp.X);

        eb.SetCursor(0, 50);
        ev.GetVirtualLineCount();

        vp = ev.GetViewport();
        Assert.True(vp.X > 0);

        var cursor = eb.GetCursorPosition();
        Assert.True(cursor.Col >= (uint)vp.X);
        Assert.True(cursor.Col < (uint)(vp.X + vp.Width));
    }

    #endregion

    #region 47. Horizontal scroll: cursor moves left to beginning

    [Fact]
    public void HorizontalScroll_CursorMovesLeftToBeginning()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 20, 10);

        eb.SetText("This is a very long line that exceeds the viewport width of 20 characters");
        eb.SetCursor(0, 50);
        ev.GetVirtualLineCount();

        var vp = ev.GetViewport();
        Assert.True(vp.X > 0);

        eb.SetCursor(0, 0);
        ev.GetVirtualLineCount();

        vp = ev.GetViewport();
        Assert.Equal(0, vp.X);
    }

    #endregion

    #region 48. Horizontal scroll: moveRight scrolls viewport

    [Fact]
    public void HorizontalScroll_MoveRightScrollsViewport()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 20, 10);

        eb.SetText(LongLine160);
        eb.SetCursor(0, 0);
        ev.GetVirtualLineCount();

        var vp = ev.GetViewport();
        Assert.Equal(0, vp.X);

        for (int i = 0; i < 50; i++)
            eb.MoveCursorRight();

        ev.GetVirtualLineCount();

        vp = ev.GetViewport();
        Assert.True(vp.X > 0);

        var cursor = eb.GetCursorPosition();
        Assert.Equal(50u, cursor.Col);
        Assert.True(cursor.Col >= (uint)vp.X);
        Assert.True(cursor.Col < (uint)(vp.X + vp.Width));
    }

    #endregion

    #region 49. Horizontal scroll: moveLeft scrolls viewport back

    [Fact]
    public void HorizontalScroll_MoveLeftScrollsBack()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 20, 10);

        eb.SetText(LongLine160);
        eb.SetCursor(0, 50);
        ev.GetVirtualLineCount();

        var vp = ev.GetViewport();
        var initialX = vp.X;
        Assert.True(initialX > 0);

        for (int i = 0; i < 30; i++)
            eb.MoveCursorLeft();

        ev.GetVirtualLineCount();

        vp = ev.GetViewport();
        Assert.True(vp.X < initialX);

        var cursor = eb.GetCursorPosition();
        Assert.True(cursor.Col >= (uint)vp.X);
        Assert.True(cursor.Col < (uint)(vp.X + vp.Width));
    }

    #endregion

    #region 50. Horizontal scroll: editing in scrolled view

    [Fact]
    public void HorizontalScroll_EditingInScrolledView()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 20, 10);

        eb.SetText(LongLine160);
        eb.SetCursor(0, 50);
        ev.GetVirtualLineCount();

        var vp = ev.GetViewport();
        Assert.True(vp.X > 0);

        eb.InsertText("XYZ");
        ev.GetVirtualLineCount();

        vp = ev.GetViewport();
        var cursor = eb.GetCursorPosition();
        Assert.Equal(53u, cursor.Col);
        Assert.True(cursor.Col >= (uint)vp.X);
        Assert.True(cursor.Col < (uint)(vp.X + vp.Width));

        var text = eb.GetText();
        Assert.Contains("XYZ", text);
    }

    #endregion

    #region 51. Horizontal scroll: backspace in scrolled view

    [Fact]
    public void HorizontalScroll_BackspaceInScrolledView()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 20, 10);

        eb.SetText(LongLine160);
        eb.SetCursor(0, 50);
        ev.GetVirtualLineCount();

        var vp = ev.GetViewport();
        Assert.True(vp.X > 0);

        eb.DeleteCharBackward();
        eb.DeleteCharBackward();
        eb.DeleteCharBackward();
        ev.GetVirtualLineCount();

        vp = ev.GetViewport();
        var cursor = eb.GetCursorPosition();
        Assert.Equal(47u, cursor.Col);
        Assert.True(cursor.Col >= (uint)vp.X);
        Assert.True(cursor.Col < (uint)(vp.X + vp.Width));
    }

    #endregion

    #region 52. Horizontal scroll: short lines reset scroll

    [Fact]
    public void HorizontalScroll_ShortLinesResetScroll()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 20, 10);

        eb.SetText(
            "Short line\n" +
            "AAAAAAAABBBBBBBBBBCCCCCCCCCCDDDDDDDDDDEEEEEEEEEEFFFFFFFFFFFFGGGGGGGGGGHHHHHHHHHHIIIIIIIIIIJJJJJJJJJJ\n" +
            "Another short");

        eb.SetCursor(1, 50);
        ev.GetVirtualLineCount();

        var vp = ev.GetViewport();
        Assert.True(vp.X > 0);

        eb.SetCursor(0, 5);
        ev.GetVirtualLineCount();

        vp = ev.GetViewport();
        Assert.True(vp.X <= 5);

        eb.SetCursor(1, 50);
        ev.GetVirtualLineCount();

        vp = ev.GetViewport();
        Assert.True(vp.X > 0);
    }

    #endregion

    #region 53. Horizontal scroll: scroll margin works

    [Fact]
    public void HorizontalScroll_ScrollMarginWorks()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 20, 10);

        ev.SetScrollMargin(0.2f);
        eb.SetText(LongLine160);
        eb.SetCursor(0, 0);
        ev.GetVirtualLineCount();

        for (int i = 0; i < 25; i++)
            eb.MoveCursorRight();

        ev.GetVirtualLineCount();

        var vp = ev.GetViewport();
        var cursor = eb.GetCursorPosition();

        var cursorOffsetInViewport = cursor.Col - (uint)vp.X;
        Assert.True(cursorOffsetInViewport >= 4);
        Assert.True(cursorOffsetInViewport < (uint)vp.Width - 4);
    }

    #endregion

    #region 54. Horizontal scroll: no scrolling with wrapping enabled

    [Fact]
    public void HorizontalScroll_NoScrollingWithWrapping()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 20, 10);

        ev.SetWrapMode((byte)WrapMode.Char);
        eb.SetText(LongLine160);
        eb.SetCursor(0, 50);
        ev.GetVirtualLineCount();

        var vp = ev.GetViewport();
        Assert.Equal(0, vp.X);
    }

    #endregion

    #region 55. Horizontal scroll: cursor position correct after scrolling

    [Fact]
    public void HorizontalScroll_CursorPositionCorrectAfterScrolling()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 20, 10);

        eb.SetText(LongLine160);
        eb.SetCursor(0, 0);
        ev.GetVirtualLineCount();

        for (int i = 0; i < 50; i++)
        {
            eb.MoveCursorRight();
            ev.GetVirtualLineCount();

            var cursor = eb.GetCursorPosition();
            var vp = ev.GetViewport();
            var vcursor = ev.GetVisualCursor();

            Assert.Equal(cursor.Col, vcursor.LogicalCol);
            Assert.True(cursor.Col >= (uint)vp.X);
            Assert.True(cursor.Col < (uint)(vp.X + vp.Width));
        }
    }

    #endregion

    #region 56. Horizontal scroll: rapid movements maintain visibility

    [Fact]
    public void HorizontalScroll_RapidMovementsMaintainVisibility()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 20, 10);

        eb.SetText(LongLine160);
        eb.SetCursor(0, 0);
        eb.SetCursor(0, 80);
        eb.SetCursor(0, 40);
        eb.SetCursor(0, 10);
        eb.SetCursor(0, 60);
        ev.GetVirtualLineCount();

        var vp = ev.GetViewport();
        var cursor = eb.GetCursorPosition();

        Assert.Equal(60u, cursor.Col);
        Assert.True(cursor.Col >= (uint)vp.X);
        Assert.True(cursor.Col < (uint)(vp.X + vp.Width));
    }

    #endregion

    #region 57. Horizontal scroll: goto end of long line

    [Fact]
    public void HorizontalScroll_GotoEndOfLongLine()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 20, 10);

        eb.SetText(LongLine160);
        eb.SetCursor(0, 0);
        ev.GetVirtualLineCount();

        eb.SetCursor(0, (uint)LongLine160.Length);
        ev.GetVirtualLineCount();

        var vp = ev.GetViewport();
        var cursor = eb.GetCursorPosition();

        Assert.True(vp.X > 0);
        Assert.True(cursor.Col >= (uint)vp.X);
        Assert.True(cursor.Col < (uint)(vp.X + vp.Width));
    }

    #endregion

    #region 58. Cursor at second cell of width=2 grapheme

    [Fact]
    public void CursorAtSecondCellOfWideGrapheme()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 24);

        eb.SetText("(emoji 🌟 and CJK 世界)");

        eb.SetCursor(0, 7);
        var cursor = eb.GetCursorPosition();
        Assert.Equal(7u, cursor.Col);

        // Move right - should jump over emoji to col 9
        eb.MoveCursorRight();
        cursor = eb.GetCursorPosition();
        Assert.Equal(9u, cursor.Col);

        // Set cursor to col 8 (second cell of emoji at 7-8)
        eb.SetCursor(0, 8);
        cursor = eb.GetCursorPosition();
        Assert.Equal(8u, cursor.Col);

        // Should jump to col 9
        eb.MoveCursorRight();
        cursor = eb.GetCursorPosition();
        Assert.Equal(9u, cursor.Col);

        eb.SetCursor(0, 8);
        cursor = eb.GetCursorPosition();
        Assert.Equal(8u, cursor.Col);

        eb.MoveCursorLeft();
        cursor = eb.GetCursorPosition();
        Assert.Equal(6u, cursor.Col);
    }

    #endregion

    #region 59. Cursor lands after closing paren with wide graphemes

    [Fact]
    public void CursorLandsAfterClosingParen_WithWideGraphemes()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 24);

        eb.SetText("(emoji 🌟 and CJK 世界)\nNext line");
        eb.SetCursor(0, 0);

        var cursor = eb.GetCursorPosition();
        for (int i = 0; i < 30; i++)
        {
            var prevRow = cursor.Row;
            eb.MoveCursorRight();
            cursor = eb.GetCursorPosition();

            if (prevRow == 0 && cursor.Row == 1)
                break;

            if (i > 25)
                break;
        }

        Assert.Equal(1u, cursor.Row);
        Assert.Equal(0u, cursor.Col);
    }

    #endregion

    #region 60. Visual cursor stays on same line with wide graphemes

    [Fact]
    public void VisualCursor_StaysOnSameLineWithWideGraphemes()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 24);

        eb.SetText("(emoji 🌟 and CJK 世界)\nNext line");
        eb.SetCursor(0, 0);

        for (int i = 0; i < 30; i++)
        {
            eb.MoveCursorRight();
            var cursor = eb.GetCursorPosition();
            var vcursor = ev.GetVisualCursor();

            if (cursor.Row == 0)
            {
                Assert.Equal(0u, vcursor.VisualRow);
                Assert.Equal(cursor.Col, vcursor.VisualCol);
            }

            if (cursor.Row == 1)
            {
                Assert.Equal(1u, vcursor.VisualRow);
                break;
            }

            if (i > 25) break;
        }
    }

    #endregion

    // Test 61 (placeholder styled text renders with correct highlights) skipped – requires opt_buffer internals

    #region 62. GetNextWordBoundary returns correct position

    [Fact]
    public void GetNextWordBoundary_ReturnsCorrectPosition()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 10);

        eb.InsertText("Hello World Test");
        eb.SetCursor(0, 0);

        var next = ev.GetNextWordBoundary();
        Assert.Equal(0u, next.LogicalRow);
        Assert.Equal(6u, next.LogicalCol);
    }

    #endregion

    #region 63. GetPrevWordBoundary returns correct position

    [Fact]
    public void GetPrevWordBoundary_ReturnsCorrectPosition()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 10);

        eb.InsertText("Hello World Test");
        eb.SetCursor(0, 12);

        var prev = ev.GetPrevWordBoundary();
        Assert.Equal(0u, prev.LogicalRow);
        Assert.Equal(6u, prev.LogicalCol);
    }

    #endregion

    #region 64. Word boundary with wrapping

    [Fact]
    public void WordBoundary_WithWrapping()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 20, 10);

        ev.SetWrapMode((byte)WrapMode.Char);
        eb.SetText("This is a very long line that will wrap and has multiple words");
        eb.SetCursor(0, 0);

        var next = ev.GetNextWordBoundary();
        Assert.Equal(0u, next.LogicalRow);
        Assert.Equal(5u, next.LogicalCol);
    }

    #endregion

    #region 65. Word boundary across lines

    [Fact]
    public void WordBoundary_AcrossLines()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 10);

        eb.InsertText("Hello\nWorld");
        eb.SetCursor(0, 5);

        var next = ev.GetNextWordBoundary();
        Assert.Equal(1u, next.LogicalRow);
        Assert.Equal(0u, next.LogicalCol);
    }

    #endregion

    #region 66. Word boundary prev across lines

    [Fact]
    public void WordBoundaryPrev_AcrossLines()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 10);

        eb.InsertText("Hello\nWorld");
        eb.SetCursor(1, 0);

        var prev = ev.GetPrevWordBoundary();
        Assert.Equal(0u, prev.LogicalRow);
        Assert.Equal(5u, prev.LogicalCol);
    }

    #endregion

    #region 67. Word boundary with punctuation

    [Fact]
    public void WordBoundary_WithPunctuation()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 10);

        eb.InsertText("self-contained multi-word");
        eb.SetCursor(0, 0);

        var next = ev.GetNextWordBoundary();
        Assert.Equal(0u, next.LogicalRow);
        Assert.Equal(5u, next.LogicalCol);
    }

    #endregion

    #region 68. Word boundary at end of buffer

    [Fact]
    public void WordBoundary_AtEndOfBuffer()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 10);

        eb.InsertText("Hello World");
        eb.SetCursor(0, 11);

        var next = ev.GetNextWordBoundary();
        Assert.Equal(0u, next.LogicalRow);
        Assert.Equal(11u, next.LogicalCol);
    }

    #endregion

    #region 69. Word boundary at start of buffer

    [Fact]
    public void WordBoundary_AtStartOfBuffer()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 10);

        eb.InsertText("Hello World");
        eb.SetCursor(0, 0);

        var prev = ev.GetPrevWordBoundary();
        Assert.Equal(0u, prev.LogicalRow);
        Assert.Equal(0u, prev.LogicalCol);
    }

    #endregion

    #region 70. Horizontal scroll: combined vertical and horizontal scrolling

    [Fact]
    public void HorizontalScroll_CombinedVerticalAndHorizontal()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 20, 10);

        const string line0 = "AAAAAAAAAABBBBBBBBBBCCCCCCCCCCDDDDDDDDDDEEEEEEEEEEFFFFFFFFFFFFGGGGGGGGGGHHHHHHHHHHIIIIIIIIIIJJJJJJJJJJ";
        const string repeatedLine = "\nAAAAAAAABBBBBBBBBBCCCCCCCCCCDDDDDDDDDDEEEEEEEEEEFFFFFFFFFFGGGGGGGGGGHHHHHHHHHHIIIIIIIIIIJJJJJJJJJJ";

        var text = line0;
        for (int i = 1; i < 20; i++)
            text += repeatedLine;

        eb.SetText(text);
        eb.SetCursor(15, 60);
        ev.GetVirtualLineCount();

        var vp = ev.GetViewport();
        var cursor = eb.GetCursorPosition();

        Assert.True(vp.Y > 0);
        Assert.True(vp.X > 0);

        Assert.True(cursor.Row >= (uint)vp.Y);
        Assert.True(cursor.Row < (uint)(vp.Y + vp.Height));
        Assert.True(cursor.Col >= (uint)vp.X);
        Assert.True(cursor.Col < (uint)(vp.X + vp.Width));
    }

    #endregion

    #region 71. DeleteSelectedText single line

    [Fact]
    public void DeleteSelectedText_SingleLine()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 24);

        eb.SetText("Hello World");
        ev.SetSelection(0, 5, DummyFg, DummyBg);

        ev.DeleteSelectedText();

        var text = ev.GetText();
        Assert.Equal(" World", text);

        var cursor = eb.GetCursorPosition();
        Assert.Equal(0u, cursor.Row);
        Assert.Equal(0u, cursor.Col);
    }

    #endregion

    #region 72. DeleteSelectedText multi-line

    [Fact]
    public void DeleteSelectedText_MultiLine()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 24);

        eb.SetText("Line 1\nLine 2\nLine 3");
        ev.SetSelection(2, 15, DummyFg, DummyBg);

        ev.DeleteSelectedText();

        var text = ev.GetText();
        Assert.Equal("Liine 3", text);

        var cursor = eb.GetCursorPosition();
        Assert.Equal(0u, cursor.Row);
        Assert.Equal(2u, cursor.Col);
    }

    #endregion

    #region 73. DeleteSelectedText with wrapping

    [Fact]
    public void DeleteSelectedText_WithWrapping()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 20, 10);

        ev.SetWrapMode((byte)WrapMode.Char);
        eb.SetText("ABCDEFGHIJKLMNOPQRSTUVWXYZ");

        var vlineCount = ev.GetTotalVirtualLineCount();
        Assert.True(vlineCount >= 2);

        ev.SetSelection(5, 15, DummyFg, DummyBg);
        ev.DeleteSelectedText();

        var text = ev.GetText();
        Assert.Equal("ABCDEPQRSTUVWXYZ", text);

        var cursor = eb.GetCursorPosition();
        Assert.Equal(0u, cursor.Row);
        Assert.Equal(5u, cursor.Col);
    }

    #endregion

    #region 74. DeleteSelectedText with viewport scrolled

    [Fact]
    public void DeleteSelectedText_WithViewportScrolled()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 40, 5);

        eb.SetText(TwentyLines);
        eb.GotoLine(10);
        ev.GetVirtualLineCount();

        var vp = ev.GetViewport();
        Assert.True(vp.Y > 0);

        ev.SetSelection(50, 70, DummyFg, DummyBg);
        ev.DeleteSelectedText();

        ev.GetVirtualLineCount();
        vp = ev.GetViewport();
        var cursor = eb.GetCursorPosition();

        Assert.True(cursor.Row >= (uint)vp.Y);
        Assert.True(cursor.Row < (uint)(vp.Y + vp.Height));
    }

    #endregion

    #region 75. DeleteSelectedText with no selection

    [Fact]
    public void DeleteSelectedText_WithNoSelection()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 24);

        eb.SetText("Hello World");

        ev.DeleteSelectedText();

        var text = ev.GetText();
        Assert.Equal("Hello World", text);
    }

    #endregion

    #region 76. DeleteSelectedText entire line

    [Fact]
    public void DeleteSelectedText_EntireLine()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 24);

        eb.SetText("First\nSecond\nThird\n");
        ev.SetSelection(5, 13, DummyFg, DummyBg);

        ev.DeleteSelectedText();

        var text = ev.GetText();
        Assert.Equal("FirstThird\n", text);

        var cursor = eb.GetCursorPosition();
        Assert.Equal(0u, cursor.Row);
        Assert.Equal(5u, cursor.Col);
    }

    #endregion

    #region 77. DeleteSelectedText respects selection with empty lines

    [Fact]
    public void DeleteSelectedText_WithEmptyLines()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 40, 10);

        ev.SetWrapMode((byte)WrapMode.Word);
        eb.SetText("AAAA\n\nBBBB\n\nCCCC");
        eb.SetCursor(2, 0);

        ev.SetLocalSelection(0, 2, 4, 2, DummyFg, DummyBg);

        var selectedText = ev.GetSelectedText();
        Assert.Equal("BBBB", selectedText);

        ev.DeleteSelectedText();

        var text = ev.GetText();
        Assert.Equal("AAAA\n\n\n\nCCCC", text);

        var cursor = eb.GetCursorPosition();
        Assert.Equal(2u, cursor.Row);
        Assert.Equal(0u, cursor.Col);
    }

    #endregion

    #region 78. Word wrapping with space insertion maintains cursor sync

    [Fact]
    public void WordWrapping_SpaceInsertionMaintainsCursorSync()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 15, 10);

        ev.SetWrapMode((byte)WrapMode.Word);
        ev.SetViewport(0, 0, 15, 10, true);

        eb.SetText("AAAAAAAAAAAAAAAAAAA");
        eb.SetCursor(0, 7);
        eb.InsertText(" ");

        var logicalCursor = eb.GetCursorPosition();
        var vcursor = ev.GetVisualCursor();

        Assert.Equal(0u, logicalCursor.Row);
        Assert.Equal(8u, logicalCursor.Col);
        Assert.Equal(0u, vcursor.LogicalRow);
        Assert.Equal(1u, vcursor.VisualRow);

        eb.DeleteCharBackward();

        var logicalCursorAfter = eb.GetCursorPosition();
        Assert.Equal(0u, logicalCursorAfter.Row);
        Assert.Equal(7u, logicalCursorAfter.Col);
    }

    #endregion

    #region 79. GetVisualCursor always returns on empty buffer

    [Fact]
    public void GetVisualCursor_ReturnsOnEmptyBuffer()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 80, 24);

        var vcursor = ev.GetVisualCursor();
        Assert.Equal(0u, vcursor.VisualRow);
        Assert.Equal(0u, vcursor.VisualCol);
        Assert.Equal(0u, vcursor.LogicalRow);
        Assert.Equal(0u, vcursor.LogicalCol);
    }

    #endregion

    // Tests 80-81 (logicalToVisualCursor clamps) skipped – no C# API for logicalToVisualCursor
    // Tests 82-84 (placeholder shows/cleared/styled) skipped – internal field access required
    // Tests 85-86 (placeholder renders/shrink) skipped – requires opt_buffer internals
    // Tests 87-88 (tab indicator set/get/render) skipped – getTabIndicator not exposed in C#

    #region 89. Word wrapping during editing: incremental wrapping

    [Fact]
    public void WordWrapping_IncrementalTyping()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 17, 10);

        ev.SetWrapMode((byte)WrapMode.Word);

        // Type "Hello world ddddddddd" character by character
        var textToType = "Hello world ddddddddd";

        foreach (var ch in textToType)
        {
            eb.InsertChar(ch.ToString());
            ev.GetVirtualLineCount();
        }

        // After "Hello world ddddddddd" (21 chars) with word wrapping at width=17
        var vlineCount = ev.GetTotalVirtualLineCount();
        Assert.Equal(2u, vlineCount);

        // Backspace to remove 7 d's → "Hello world dd" (14 chars, fits on one line)
        for (int i = 0; i < 7; i++)
        {
            eb.DeleteCharBackward();
            ev.GetVirtualLineCount();
        }

        vlineCount = ev.GetTotalVirtualLineCount();
        Assert.Equal(1u, vlineCount);

        var cursor = eb.GetCursorPosition();
        Assert.Equal(0u, cursor.Row);
        Assert.Equal(14u, cursor.Col);

        // Type more d's to trigger wrapping again
        for (int i = 0; i < 7; i++)
        {
            eb.InsertChar("d");
            ev.GetVirtualLineCount();
        }

        // Should wrap again
        vlineCount = ev.GetTotalVirtualLineCount();
        Assert.Equal(2u, vlineCount);

        cursor = eb.GetCursorPosition();
        Assert.Equal(0u, cursor.Row);

        var text = eb.GetText();
        Assert.Equal("Hello world ddddddddd", text);
    }

    #endregion

    #region 90. Cursor movement with emoji skin tone modifier (wcwidth)

    [Fact]
    public void CursorMovement_EmojiSkinTone_Wcwidth()
    {
        using var eb = EditBuffer.Create(0); // wcwidth
        using var ev = EditorView.Create(eb, 80, 24);

        eb.SetText("👋🏿");

        eb.SetCursor(0, 0);
        var cursor = eb.GetCursorPosition();
        Assert.Equal(0u, cursor.Col);

        // Move right – past first codepoint (2 columns)
        eb.MoveCursorRight();
        cursor = eb.GetCursorPosition();
        Assert.Equal(2u, cursor.Col);

        // Move right – past second codepoint (2 more columns)
        eb.MoveCursorRight();
        cursor = eb.GetCursorPosition();
        Assert.Equal(4u, cursor.Col);

        // Move left – back to col 2
        eb.MoveCursorLeft();
        cursor = eb.GetCursorPosition();
        Assert.Equal(2u, cursor.Col);

        // Move left – back to beginning
        eb.MoveCursorLeft();
        cursor = eb.GetCursorPosition();
        Assert.Equal(0u, cursor.Col);
    }

    #endregion

    #region 91. Cursor movement with emoji skin tone modifier (unicode)

    [Fact]
    public void CursorMovement_EmojiSkinTone_Unicode()
    {
        using var eb = EditBuffer.Create(1); // unicode
        using var ev = EditorView.Create(eb, 80, 24);

        eb.SetText("👋🏿");

        eb.SetCursor(0, 0);
        var cursor = eb.GetCursorPosition();
        Assert.Equal(0u, cursor.Col);

        // Move right – past entire grapheme cluster (2 columns in unicode)
        eb.MoveCursorRight();
        cursor = eb.GetCursorPosition();
        Assert.Equal(2u, cursor.Col);

        // Move left – back to beginning
        eb.MoveCursorLeft();
        cursor = eb.GetCursorPosition();
        Assert.Equal(0u, cursor.Col);
    }

    #endregion

    #region 92. Backspace emoji skin tone modifier (wcwidth)

    [Fact]
    public void Backspace_EmojiSkinTone_Wcwidth()
    {
        using var eb = EditBuffer.Create(0); // wcwidth
        using var ev = EditorView.Create(eb, 80, 24);

        eb.SetText("👋🏿");

        // Move to end
        eb.MoveCursorRight();
        var cursor = eb.GetCursorPosition();
        Assert.Equal(2u, cursor.Col);

        eb.MoveCursorRight();
        cursor = eb.GetCursorPosition();
        Assert.Equal(4u, cursor.Col);

        var textBefore = eb.GetText();
        Assert.Equal("👋🏿", textBefore);

        // First backspace deletes the skin tone modifier
        eb.DeleteCharBackward();
        cursor = eb.GetCursorPosition();
        Assert.Equal(2u, cursor.Col);

        var textMiddle = eb.GetText();
        Assert.Equal("👋", textMiddle);

        // Second backspace deletes the hand emoji
        eb.DeleteCharBackward();
        cursor = eb.GetCursorPosition();
        Assert.Equal(0u, cursor.Col);

        var textAfter = eb.GetText();
        Assert.Equal("", textAfter);
    }

    #endregion

    #region 93. Backspace emoji skin tone modifier (unicode)

    [Fact]
    public void Backspace_EmojiSkinTone_Unicode()
    {
        using var eb = EditBuffer.Create(1); // unicode
        using var ev = EditorView.Create(eb, 80, 24);

        eb.SetText("👋🏿");

        // Move to end (2 columns in unicode)
        eb.MoveCursorRight();
        var cursor = eb.GetCursorPosition();
        Assert.Equal(2u, cursor.Col);

        var textBefore = eb.GetText();
        Assert.Equal("👋🏿", textBefore);

        // Backspace deletes entire grapheme cluster
        eb.DeleteCharBackward();
        cursor = eb.GetCursorPosition();
        Assert.Equal(0u, cursor.Col);

        var textAfter = eb.GetText();
        Assert.Equal("", textAfter);
    }

    #endregion

    #region 94. Mouse selection doesn't scroll when focus within viewport

    [Fact]
    public void MouseSelection_DoesNotScrollWithinViewport()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 40, 10);

        for (int i = 0; i < 50; i++)
        {
            if (i > 0) eb.InsertText("\n");
            eb.InsertText($"Line {i}");
        }

        eb.SetCursor(0, 0);
        ev.GetVirtualLineCount();

        var vpInitial = ev.GetViewport();
        Assert.Equal(0, vpInitial.Y);

        // Select within the viewport (lines 0-5)
        ev.SetLocalSelection(0, 0, 5, 5, DummyFg, DummyBg, extend: true);
        ev.GetVirtualLineCount();

        var vpAfter = ev.GetViewport();
        Assert.Equal(vpInitial.Y, vpAfter.Y);
        Assert.Equal(vpInitial.X, vpAfter.X);
    }

    #endregion

    #region 95. Mouse selection outside buffer bounds clamps

    [Fact]
    public void MouseSelection_OutsideBufferBounds_Clamps()
    {
        using var eb = EditBuffer.Create();
        using var ev = EditorView.Create(eb, 40, 10);

        for (int i = 0; i < 10; i++)
        {
            if (i > 0) eb.InsertText("\n");
            eb.InsertText($"Line {i}");
        }

        eb.SetCursor(0, 0);
        ev.GetVirtualLineCount();

        // Select beyond buffer (to line 100)
        ev.SetLocalSelection(0, 0, 5, 100, DummyFg, DummyBg, extend: true);
        ev.GetVirtualLineCount();

        var cursor = eb.GetCursorPosition();
        Assert.Equal(9u, cursor.Row);
    }

    #endregion
}
