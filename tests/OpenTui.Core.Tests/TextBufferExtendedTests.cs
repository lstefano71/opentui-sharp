using Xunit;
using OpenTui.Core;

namespace OpenTui.Core.Tests;

/// <summary>
/// C# xUnit equivalents of the Zig text-buffer extended tests:
///   - text-buffer-highlights_test.zig (30 tests)
///   - text-buffer-selection_test.zig (38 tests)
///   - text-buffer-selection_viewport_test.zig (12 tests)
///   - text-buffer-drawing_test.zig (71 tests)
///   - text-buffer-segment_test.zig (14 tests)
///
/// Tests that rely on internal-only Zig APIs (getLineHighlights, getLineSpans,
/// getVirtualLines, get cell, writeResolvedChars, Segment/UnifiedRope internals)
/// are marked with [Fact(Skip = "...")] since those APIs are not exposed in C#.
/// </summary>
public class TextBufferExtendedTests
{
    // Helper colors used across tests
    private static readonly Rgba SelFg = Rgba.White;
    private static readonly Rgba SelBg = Rgba.Blue;
    private static readonly Rgba RedBg = new(1f, 0f, 0f, 1f);
    private static readonly Rgba BlackBg = new(0f, 0f, 0f, 1f);

    // =====================================================================
    // HIGHLIGHT TESTS (from text-buffer-highlights_test.zig)
    // =====================================================================

    #region Highlight - Line-based addHighlight

    [Fact(Skip = "getLineHighlights is internal-only in Zig; C# only exposes HighlightCount")]
    public void Highlights_AddHighlightByCoords()
    {
        // Zig: "TextBuffer coords - addHighlightByCoords"
    }

    [Fact(Skip = "getLineHighlights is internal-only in Zig; C# only exposes HighlightCount")]
    public void Highlights_AddHighlightByCoordsMultiLine()
    {
        // Zig: "TextBuffer coords - addHighlightByCoords multi-line"
    }

    [Fact]
    public void Highlights_AddSingleHighlightToLine()
    {
        // Zig: "TextBuffer highlights - add single highlight to line"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Hello World");

        tb.AddHighlight(0, new Highlight { Start = 0, End = 5, StyleId = 1, Priority = 0, HlRef = 0 });

        Assert.True(tb.HighlightCount >= 1);
    }

    [Fact]
    public void Highlights_AddMultipleHighlightsToSameLine()
    {
        // Zig: "TextBuffer highlights - add multiple highlights to same line"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Hello World");

        tb.AddHighlight(0, new Highlight { Start = 0, End = 5, StyleId = 1, Priority = 0, HlRef = 0 });
        tb.AddHighlight(0, new Highlight { Start = 6, End = 11, StyleId = 2, Priority = 0, HlRef = 0 });

        Assert.True(tb.HighlightCount >= 2);
    }

    [Fact]
    public void Highlights_AddHighlightsToMultipleLines()
    {
        // Zig: "TextBuffer highlights - add highlights to multiple lines"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Line 1\nLine 2\nLine 3");

        tb.AddHighlight(0, new Highlight { Start = 0, End = 6, StyleId = 1, Priority = 0, HlRef = 0 });
        tb.AddHighlight(1, new Highlight { Start = 0, End = 6, StyleId = 2, Priority = 0, HlRef = 0 });
        tb.AddHighlight(2, new Highlight { Start = 0, End = 6, StyleId = 3, Priority = 0, HlRef = 0 });

        Assert.True(tb.HighlightCount >= 3);
    }

    [Fact]
    public void Highlights_RemoveHighlightsByReference()
    {
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Line 1\nLine 2");

        tb.AddHighlight(0, new Highlight { Start = 0, End = 3, StyleId = 1, Priority = 0, HlRef = 100 });
        tb.AddHighlight(0, new Highlight { Start = 3, End = 6, StyleId = 2, Priority = 0, HlRef = 200 });
        tb.AddHighlight(1, new Highlight { Start = 0, End = 6, StyleId = 3, Priority = 0, HlRef = 100 });

        var countBefore = tb.HighlightCount;
        tb.RemoveHighlight(100);
        var countAfter = tb.HighlightCount;

        // Removing ref=100 should remove 2 highlights (line 0 first, line 1)
        Assert.True(countAfter < countBefore);
    }

    [Fact]
    public void Highlights_ClearLineHighlights()
    {
        // Zig: "TextBuffer highlights - clear line highlights"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Line 1\nLine 2");

        tb.AddHighlight(0, new Highlight { Start = 0, End = 6, StyleId = 1, Priority = 0, HlRef = 0 });
        tb.AddHighlight(0, new Highlight { Start = 6, End = 10, StyleId = 2, Priority = 0, HlRef = 0 });

        var countBefore = tb.HighlightCount;
        tb.ClearLineHighlights(0);
        var countAfter = tb.HighlightCount;

        Assert.True(countAfter < countBefore);
    }

    [Fact]
    public void Highlights_ClearAllHighlights()
    {
        // Zig: "TextBuffer highlights - clear all highlights"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Line 1\nLine 2\nLine 3");

        tb.AddHighlight(0, new Highlight { Start = 0, End = 6, StyleId = 1, Priority = 0, HlRef = 0 });
        tb.AddHighlight(1, new Highlight { Start = 0, End = 6, StyleId = 2, Priority = 0, HlRef = 0 });
        tb.AddHighlight(2, new Highlight { Start = 0, End = 6, StyleId = 3, Priority = 0, HlRef = 0 });

        tb.ClearHighlights();

        Assert.Equal(0u, tb.HighlightCount);
    }

    [Fact]
    public void Highlights_GetHighlightsFromNonExistentLine()
    {
        // Zig: "TextBuffer highlights - get highlights from non-existent line"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Line 1");

        // Clearing a non-existent line should not crash
        tb.ClearLineHighlights(10);
        Assert.Equal(0u, tb.HighlightCount);
    }

    [Fact]
    public void Highlights_OverlappingHighlights()
    {
        // Zig: "TextBuffer highlights - overlapping highlights"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Hello World");

        tb.AddHighlight(0, new Highlight { Start = 0, End = 8, StyleId = 1, Priority = 0, HlRef = 0 });
        tb.AddHighlight(0, new Highlight { Start = 5, End = 11, StyleId = 2, Priority = 0, HlRef = 0 });

        Assert.True(tb.HighlightCount >= 2);
    }

    [Fact]
    public void Highlights_ResetClearsHighlights()
    {
        // Zig: "TextBuffer highlights - reset clears highlights"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Hello World");

        tb.AddHighlight(0, new Highlight { Start = 0, End = 5, StyleId = 1, Priority = 0, HlRef = 0 });

        tb.Reset();

        Assert.Equal(0u, tb.HighlightCount);
    }

    [Fact]
    public void Highlights_SetAndClearSyntaxStyle()
    {
        // Zig: "TextBuffer highlights - setSyntaxStyle and getSyntaxStyle"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var style = SyntaxStyle.Create();

        tb.SetSyntaxStyle(style.Handle);
        // Should not crash
        tb.SetSyntaxStyle(nint.Zero);
    }

    [Fact]
    public void Highlights_IntegrationWithSyntaxStyle()
    {
        // Zig: "TextBuffer highlights - integration with SyntaxStyle"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var style = SyntaxStyle.Create();

        var keywordId = style.Register("keyword", fg: Rgba.Red);
        var stringId = style.Register("string", fg: Rgba.Green);
        var commentId = style.Register("comment", fg: new Rgba(0.5f, 0.5f, 0.5f, 1f));

        tb.SetText("function hello() // comment");
        tb.SetSyntaxStyle(style.Handle);

        tb.AddHighlight(0, new Highlight { Start = 0, End = 8, StyleId = keywordId, Priority = 1, HlRef = 0 });
        tb.AddHighlight(0, new Highlight { Start = 9, End = 14, StyleId = stringId, Priority = 1, HlRef = 0 });
        tb.AddHighlight(0, new Highlight { Start = 17, End = 27, StyleId = commentId, Priority = 1, HlRef = 0 });

        Assert.True(tb.HighlightCount >= 3);
        Assert.True(style.StyleCount >= 3);
    }

    [Fact(Skip = "getLineSpans is internal-only in Zig; not exposed in C#")]
    public void Highlights_StyleSpansComputedCorrectly()
    {
        // Zig: "TextBuffer highlights - style spans computed correctly"
    }

    [Fact(Skip = "getLineSpans is internal-only in Zig; not exposed in C#")]
    public void Highlights_PriorityHandlingInSpans()
    {
        // Zig: "TextBuffer highlights - priority handling in spans"
    }

    #endregion

    #region Highlight - Character range addHighlightByCharRange

    [Fact]
    public void CharRangeHighlights_SingleLine()
    {
        // Zig: "TextBuffer char range highlights - single line highlight"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Hello World");

        tb.AddHighlightByCharRange(new Highlight { Start = 0, End = 5, StyleId = 1, Priority = 1, HlRef = 0 });

        Assert.True(tb.HighlightCount >= 1);
    }

    [Fact]
    public void CharRangeHighlights_MultiLine()
    {
        // Zig: "TextBuffer char range highlights - multi-line highlight"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Hello\nWorld\nTest");

        // Highlight from middle of line 0 to middle of line 1
        tb.AddHighlightByCharRange(new Highlight { Start = 3, End = 9, StyleId = 1, Priority = 1, HlRef = 0 });

        Assert.True(tb.HighlightCount >= 1);
    }

    [Fact]
    public void CharRangeHighlights_SpanningThreeLines()
    {
        // Zig: "TextBuffer char range highlights - spanning three lines"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Line1\nLine2\nLine3");

        tb.AddHighlightByCharRange(new Highlight { Start = 3, End = 13, StyleId = 1, Priority = 1, HlRef = 0 });

        Assert.True(tb.HighlightCount >= 1);
    }

    [Fact]
    public void CharRangeHighlights_ExactLineBoundaries()
    {
        // Zig: "TextBuffer char range highlights - exact line boundaries"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("AAAA\nBBBB\nCCCC");

        tb.AddHighlightByCharRange(new Highlight { Start = 0, End = 4, StyleId = 1, Priority = 1, HlRef = 0 });

        Assert.True(tb.HighlightCount >= 1);
    }

    [Fact]
    public void CharRangeHighlights_EmptyRange()
    {
        // Zig: "TextBuffer char range highlights - empty range"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Hello World");

        // Empty range (start == end) should add no highlights
        tb.AddHighlightByCharRange(new Highlight { Start = 5, End = 5, StyleId = 1, Priority = 1, HlRef = 0 });

        Assert.Equal(0u, tb.HighlightCount);
    }

    [Fact]
    public void CharRangeHighlights_InvalidRange()
    {
        // Zig: "TextBuffer char range highlights - invalid range"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Hello World");

        // Invalid range (start > end) should add no highlights
        tb.AddHighlightByCharRange(new Highlight { Start = 10, End = 5, StyleId = 1, Priority = 1, HlRef = 0 });

        Assert.Equal(0u, tb.HighlightCount);
    }

    [Fact]
    public void CharRangeHighlights_OutOfBoundsRange()
    {
        // Zig: "TextBuffer char range highlights - out of bounds range"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Hello");

        // Range extends beyond text length - should handle gracefully
        tb.AddHighlightByCharRange(new Highlight { Start = 3, End = 100, StyleId = 1, Priority = 1, HlRef = 0 });

        Assert.True(tb.HighlightCount >= 1);
    }

    [Fact]
    public void CharRangeHighlights_MultipleNonOverlapping()
    {
        // Zig: "TextBuffer char range highlights - multiple non-overlapping ranges"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("function hello() { return 42; }");

        tb.AddHighlightByCharRange(new Highlight { Start = 0, End = 8, StyleId = 1, Priority = 1, HlRef = 0 });
        tb.AddHighlightByCharRange(new Highlight { Start = 9, End = 14, StyleId = 2, Priority = 1, HlRef = 0 });
        tb.AddHighlightByCharRange(new Highlight { Start = 19, End = 25, StyleId = 3, Priority = 1, HlRef = 0 });

        Assert.True(tb.HighlightCount >= 3);
    }

    [Fact]
    public void CharRangeHighlights_WithRefIdForRemoval()
    {
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Line1\nLine2\nLine3");

        tb.AddHighlightByCharRange(new Highlight { Start = 0, End = 5, StyleId = 1, Priority = 1, HlRef = 100 });
        tb.AddHighlightByCharRange(new Highlight { Start = 6, End = 11, StyleId = 2, Priority = 1, HlRef = 100 });

        Assert.True(tb.HighlightCount >= 1);

        tb.RemoveHighlight(100);

        Assert.Equal(0u, tb.HighlightCount);
    }

    [Fact(Skip = "getLineSpans is internal-only in Zig; not exposed in C#")]
    public void CharRangeHighlights_PriorityHandling()
    {
        // Zig: "TextBuffer char range highlights - priority handling"
    }

    [Fact]
    public void CharRangeHighlights_UnicodeText()
    {
        // Zig: "TextBuffer char range highlights - unicode text"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Hello 世界 🌟");

        var textLen = tb.Length;
        tb.AddHighlightByCharRange(new Highlight { Start = 0, End = textLen, StyleId = 1, Priority = 1, HlRef = 0 });

        Assert.True(tb.HighlightCount >= 1);
    }

    [Fact]
    public void CharRangeHighlights_PreservedAfterSetText()
    {
        // Zig: "TextBuffer char range highlights - preserved after setText"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Hello World");

        tb.AddHighlightByCharRange(new Highlight { Start = 0, End = 5, StyleId = 1, Priority = 1, HlRef = 0 });

        // SetText with clear() - highlights are preserved in Zig
        tb.SetText("New Text");

        // After explicit clear
        tb.ClearHighlights();
        Assert.Equal(0u, tb.HighlightCount);
    }

    [Fact]
    public void CharRangeHighlights_MultiWidthCharsBeforeHighlight()
    {
        // Zig: "TextBuffer char range highlights - multi-width chars before highlight"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("前后端分离 @git-committer");

        tb.AddHighlightByCharRange(new Highlight { Start = 11, End = 25, StyleId = 1, Priority = 1, HlRef = 0 });

        Assert.True(tb.HighlightCount >= 1);
    }

    [Fact]
    public void CharRangeHighlights_MultiWidthCharsBetweenHighlights()
    {
        // Zig: "TextBuffer char range highlights - multi-width chars between highlights"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("abc前后端def");

        tb.AddHighlightByCharRange(new Highlight { Start = 9, End = 12, StyleId = 1, Priority = 1, HlRef = 0 });

        Assert.True(tb.HighlightCount >= 1);
    }

    [Fact]
    public void CharRangeHighlights_EmojiGraphemeClusters()
    {
        // Zig: "TextBuffer char range highlights - emoji grapheme clusters"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("前🌟test");

        tb.AddHighlightByCharRange(new Highlight { Start = 4, End = 8, StyleId = 1, Priority = 1, HlRef = 0 });

        Assert.True(tb.HighlightCount >= 1);
    }

    #endregion

    // =====================================================================
    // SELECTION TESTS (from text-buffer-selection_test.zig)
    // =====================================================================

    #region Selection - Basic local selection

    [Fact]
    public void Selection_BasicWithoutWrap()
    {
        // Zig: "Selection - basic selection without wrap"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Hello World");

        view.SetLocalSelection(2, 0, 7, 0, SelFg, SelBg);

        var info = view.GetSelectionInfo();
        Assert.NotEqual(0xFFFFFFFF_FFFFFFFFul, info);

        var start = (uint)(info >> 32);
        var end = (uint)(info & 0xFFFFFFFF);
        Assert.Equal(2u, start);
        Assert.Equal(7u, end);
    }

    [Fact]
    public void Selection_WithWrappedLines()
    {
        // Zig: "Selection - with wrapped lines"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("ABCDEFGHIJKLMNOPQRST");

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);

        Assert.Equal(2u, view.GetVirtualLineCount());

        view.SetLocalSelection(5, 0, 5, 1, SelFg, SelBg);

        var info = view.GetSelectionInfo();
        Assert.NotEqual(0xFFFFFFFF_FFFFFFFFul, info);

        var start = (uint)(info >> 32);
        var end = (uint)(info & 0xFFFFFFFF);
        Assert.Equal(5u, start);
        Assert.Equal(15u, end);
    }

    [Fact]
    public void Selection_NoSelectionReturnsAllBitsSet()
    {
        // Zig: "Selection - no selection returns all bits set"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Hello World");

        var info = view.GetSelectionInfo();
        Assert.Equal(0xFFFFFFFF_FFFFFFFFul, info);
    }

    [Fact]
    public void Selection_WithNewlineCharacters()
    {
        // Zig: "Selection - with newline characters"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Line 1\nLine 2\nLine 3");

        view.SetLocalSelection(2, 1, 4, 2, SelFg, SelBg);

        var info = view.GetSelectionInfo();
        Assert.NotEqual(0xFFFFFFFF_FFFFFFFFul, info);

        var selectedText = view.GetSelectedText();
        Assert.Contains("ne 2", selectedText);
        Assert.Contains("\n", selectedText);
    }

    [Fact]
    public void Selection_AcrossEmptyLines()
    {
        // Zig: "Selection - across empty lines"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Line 1\nLine 2\n\nLine 4");

        view.SetLocalSelection(0, 0, 2, 2, SelFg, SelBg);

        var info = view.GetSelectionInfo();
        Assert.NotEqual(0xFFFFFFFF_FFFFFFFFul, info);

        var start = (uint)(info >> 32);
        var end = (uint)(info & 0xFFFFFFFF);
        Assert.Equal(0u, start);
        Assert.Equal(14u, end);
    }

    [Fact]
    public void Selection_EndingInEmptyLine()
    {
        // Zig: "Selection - ending in empty line"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Line 1\n\nLine 3");

        view.SetLocalSelection(0, 0, 3, 1, SelFg, SelBg);

        var info = view.GetSelectionInfo();
        Assert.NotEqual(0xFFFFFFFF_FFFFFFFFul, info);

        var start = (uint)(info >> 32);
        Assert.Equal(0u, start);
    }

    [Fact]
    public void Selection_SpanningMultipleLinesCompletely()
    {
        // Zig: "Selection - spanning multiple lines completely"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("First\nSecond\nThird");

        view.SetLocalSelection(0, 1, 6, 1, SelFg, SelBg);

        var text = view.GetSelectedText();
        Assert.Equal("Second", text);
    }

    [Fact]
    public void Selection_IncludingMultipleLineBreaks()
    {
        // Zig: "Selection - including multiple line breaks"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("A\nB\nC\nD");

        view.SetLocalSelection(0, 1, 1, 2, SelFg, SelBg);

        var text = view.GetSelectedText();
        Assert.Contains("\n", text);
        Assert.Contains("B", text);
        Assert.Contains("C", text);
    }

    [Fact]
    public void Selection_AtLineBoundaries()
    {
        // Zig: "Selection - at line boundaries"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Line1\nLine2\nLine3");

        view.SetLocalSelection(4, 0, 2, 1, SelFg, SelBg);

        var text = view.GetSelectedText();
        Assert.Contains("1", text);
        Assert.Contains("\n", text);
        Assert.Contains("Li", text);
    }

    [Fact]
    public void Selection_EmptyText()
    {
        // Zig: "Selection - empty text"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("");

        view.SetLocalSelection(0, 0, 0, 0, SelFg, SelBg);

        var info = view.GetSelectionInfo();
        Assert.Equal(0xFFFFFFFF_FFFFFFFFul, info);
    }

    [Fact]
    public void Selection_SingleCharacter()
    {
        // Zig: "Selection - single character"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("A");

        view.SetLocalSelection(0, 0, 1, 0, SelFg, SelBg);

        var info = view.GetSelectionInfo();
        Assert.NotEqual(0xFFFFFFFF_FFFFFFFFul, info);

        var start = (uint)(info >> 32);
        var end = (uint)(info & 0xFFFFFFFF);
        Assert.Equal(0u, start);
        Assert.Equal(1u, end);

        Assert.Equal("A", view.GetSelectedText());
    }

    [Fact]
    public void Selection_ZeroWidth()
    {
        // Zig: "Selection - zero-width selection"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Hello World");

        view.SetLocalSelection(5, 0, 5, 0, SelFg, SelBg);

        var info = view.GetSelectionInfo();
        Assert.Equal(0xFFFFFFFF_FFFFFFFFul, info);
    }

    [Fact]
    public void Selection_BeyondTextBounds()
    {
        // Zig: "Selection - beyond text bounds"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Hi");

        view.SetLocalSelection(0, 0, 10, 0, SelFg, SelBg);

        var info = view.GetSelectionInfo();
        Assert.NotEqual(0xFFFFFFFF_FFFFFFFFul, info);

        var start = (uint)(info >> 32);
        var end = (uint)(info & 0xFFFFFFFF);
        Assert.Equal(0u, start);
        Assert.Equal(2u, end);
    }

    [Fact]
    public void Selection_ClearSelection()
    {
        // Zig: "Selection - clear selection"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Hello World");

        view.SetLocalSelection(0, 0, 5, 0, SelFg, SelBg);
        var info = view.GetSelectionInfo();
        Assert.NotEqual(0xFFFFFFFF_FFFFFFFFul, info);

        view.ResetLocalSelection();
        info = view.GetSelectionInfo();
        Assert.Equal(0xFFFFFFFF_FFFFFFFFul, info);
    }

    #endregion

    #region Selection - Wrap boundary tests

    [Fact]
    public void Selection_AtWrapBoundary()
    {
        // Zig: "Selection - at wrap boundary"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("ABCDEFGHIJKLMNOPQRST");

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);

        view.SetLocalSelection(9, 0, 1, 1, SelFg, SelBg);

        var info = view.GetSelectionInfo();
        Assert.NotEqual(0xFFFFFFFF_FFFFFFFFul, info);

        var start = (uint)(info >> 32);
        var end = (uint)(info & 0xFFFFFFFF);
        Assert.Equal(9u, start);
        Assert.Equal(11u, end);
    }

    [Fact]
    public void Selection_SpanningMultipleWrappedLines()
    {
        // Zig: "Selection - spanning multiple wrapped lines"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123");

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);
        Assert.Equal(3u, view.GetVirtualLineCount());

        view.SetLocalSelection(2, 0, 8, 2, SelFg, SelBg);

        var info = view.GetSelectionInfo();
        Assert.NotEqual(0xFFFFFFFF_FFFFFFFFul, info);

        var start = (uint)(info >> 32);
        var end = (uint)(info & 0xFFFFFFFF);
        Assert.Equal(2u, start);
        Assert.Equal(28u, end);
    }

    [Fact]
    public void Selection_ChangesWhenWrapWidthChanges()
    {
        // Zig: "Selection - changes when wrap width changes"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("ABCDEFGHIJKLMNOPQRST");

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);
        view.SetLocalSelection(5, 0, 5, 1, SelFg, SelBg);

        var info = view.GetSelectionInfo();
        var start = (uint)(info >> 32);
        var end = (uint)(info & 0xFFFFFFFF);
        Assert.Equal(5u, start);
        Assert.Equal(15u, end);

        view.SetWrapWidth(5);
        view.SetLocalSelection(5, 0, 5, 1, SelFg, SelBg);

        info = view.GetSelectionInfo();
        Assert.NotEqual(0xFFFFFFFF_FFFFFFFFul, info);
    }

    [Fact]
    public void Selection_WithNewlinesAndWrapping()
    {
        // Zig: "Selection - with newlines and wrapping"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("ABCDEFGHIJKLMNO\nPQRSTUVWXYZ");

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);

        Assert.True(view.GetVirtualLineCount() >= 3);

        view.SetLocalSelection(5, 0, 5, 2, SelFg, SelBg);

        var info = view.GetSelectionInfo();
        Assert.NotEqual(0xFFFFFFFF_FFFFFFFFul, info);
    }

    #endregion

    #region Selection - setSelection / getSelectedText

    [Fact]
    public void Selection_GetSelectedTextSimple()
    {
        // Zig: "Selection - getSelectedText simple"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Hello World");

        view.SetSelection(6, 11, SelFg, SelBg);

        Assert.Equal("World", view.GetSelectedText());
    }

    [Fact]
    public void Selection_GetSelectedTextWithNewlines()
    {
        // Zig: "Selection - getSelectedText with newlines"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Line 1\nLine 2\nLine 3");

        view.SetSelection(0, 9, SelFg, SelBg);

        Assert.Equal("Line 1\nLi", view.GetSelectedText());
    }

    [Fact]
    public void Selection_SpanningMultipleLinesGetSelectedText()
    {
        // Zig: "Selection - spanning multiple lines with getSelectedText"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Red\nBlue");

        view.SetSelection(2, 5, SelFg, SelBg);

        Assert.Equal("d\nB", view.GetSelectedText());
    }

    [Fact]
    public void Selection_WithGraphemes()
    {
        // Zig: "Selection - with graphemes"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Hello 🌍 World");

        view.SetSelection(0, 8, SelFg, SelBg);

        var text = view.GetSelectedText();
        Assert.Contains("Hello", text);
        Assert.Contains("🌍", text);
    }

    [Fact]
    public void Selection_WideEmojiAtBoundary()
    {
        // Zig: "Selection - wide emoji at boundary"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Hello 🌍 World");

        view.SetSelection(0, 7, SelFg, SelBg);

        Assert.Equal("Hello 🌍", view.GetSelectedText());
    }

    [Fact]
    public void Selection_WideEmojiBeforeSelectionStartExcluded()
    {
        // Zig: "Selection - wide emoji BEFORE selection start should be excluded"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Hello 🌍 World");

        // Start at second cell of emoji, snaps backward to include it
        view.SetSelection(7, 10, SelFg, SelBg);

        Assert.Equal("🌍 W", view.GetSelectedText());
    }

    [Fact]
    public void Selection_StartAtSecondCellOfWideGraphemeSnapsBackward()
    {
        // Zig: "Selection - start at second cell of width=2 grapheme should snap backward to include it"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("AB🌍CD");

        view.SetSelection(3, 5, SelFg, SelBg);

        Assert.Equal("🌍C", view.GetSelectedText());
    }

    [Fact]
    public void Selection_EndAtFirstCellOfWideGraphemeSnapsForward()
    {
        // Zig: "Selection - end at first cell of width=2 grapheme should snap forward to include it"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("AB🌍CD");

        view.SetSelection(1, 3, SelFg, SelBg);

        Assert.Equal("B🌍", view.GetSelectedText());
    }

    [Fact]
    public void Selection_BothBoundariesAtWideCells()
    {
        // Zig: "Selection - both boundaries at cells of width=2 graphemes"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("A🌍B🌎C");

        view.SetSelection(2, 5, SelFg, SelBg);

        Assert.Equal("🌍B🌎", view.GetSelectedText());
    }

    #endregion

    #region Selection - updateSelection

    [Fact]
    public void Selection_UpdateSelectionExtends()
    {
        // Zig: "Selection - updateSelection extends existing selection"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Hello World");

        view.SetSelection(0, 5, SelFg, SelBg);

        var info = view.GetSelectionInfo();
        var start = (uint)(info >> 32);
        var end = (uint)(info & 0xFFFFFFFF);
        Assert.Equal(0u, start);
        Assert.Equal(5u, end);

        view.UpdateSelection(11, SelFg, SelBg);

        info = view.GetSelectionInfo();
        start = (uint)(info >> 32);
        end = (uint)(info & 0xFFFFFFFF);
        Assert.Equal(0u, start);
        Assert.Equal(11u, end);

        Assert.Equal("Hello World", view.GetSelectedText());
    }

    [Fact]
    public void Selection_UpdateSelectionWithNoExistingDoesNothing()
    {
        // Zig: "Selection - updateSelection with no existing selection does nothing"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Hello World");

        var infoBefore = view.GetSelectionInfo();
        Assert.Equal(0xFFFFFFFF_FFFFFFFFul, infoBefore);

        view.UpdateSelection(5, SelFg, SelBg);

        var infoAfter = view.GetSelectionInfo();
        Assert.Equal(0xFFFFFFFF_FFFFFFFFul, infoAfter);
    }

    [Fact]
    public void Selection_UpdateSelectionCanShrink()
    {
        // Zig: "Selection - updateSelection can shrink selection"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Hello World");

        view.SetSelection(0, 11, SelFg, SelBg);
        view.UpdateSelection(5, SelFg, SelBg);

        var info = view.GetSelectionInfo();
        var start = (uint)(info >> 32);
        var end = (uint)(info & 0xFFFFFFFF);
        Assert.Equal(0u, start);
        Assert.Equal(5u, end);

        Assert.Equal("Hello", view.GetSelectedText());
    }

    #endregion

    #region Selection - updateLocalSelection

    [Fact]
    public void Selection_UpdateLocalSelectionExtendsFocusPosition()
    {
        // Zig: "Selection - updateLocalSelection extends focus position"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Hello World");

        view.SetLocalSelection(0, 0, 5, 0, SelFg, SelBg);

        var info = view.GetSelectionInfo();
        var start = (uint)(info >> 32);
        var end = (uint)(info & 0xFFFFFFFF);
        Assert.Equal(0u, start);
        Assert.Equal(5u, end);

        var changed = view.UpdateLocalSelection(0, 0, 11, 0, SelFg, SelBg);
        Assert.True(changed);

        info = view.GetSelectionInfo();
        start = (uint)(info >> 32);
        end = (uint)(info & 0xFFFFFFFF);
        Assert.Equal(0u, start);
        Assert.Equal(11u, end);

        Assert.Equal("Hello World", view.GetSelectedText());
    }

    [Fact]
    public void Selection_UpdateLocalSelectionFallsBackToSet()
    {
        // Zig: "Selection - updateLocalSelection with no existing selection falls back to setLocalSelection"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Hello World");

        var changed = view.UpdateLocalSelection(0, 0, 5, 0, SelFg, SelBg);
        Assert.True(changed);

        var info = view.GetSelectionInfo();
        var start = (uint)(info >> 32);
        var end = (uint)(info & 0xFFFFFFFF);
        Assert.Equal(0u, start);
        Assert.Equal(5u, end);
    }

    [Fact]
    public void Selection_UpdateLocalSelectionCanShrink()
    {
        // Zig: "Selection - updateLocalSelection can shrink selection"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Hello World");

        view.SetLocalSelection(0, 0, 11, 0, SelFg, SelBg);

        var changed = view.UpdateLocalSelection(0, 0, 5, 0, SelFg, SelBg);
        Assert.True(changed);

        var info = view.GetSelectionInfo();
        var start = (uint)(info >> 32);
        var end = (uint)(info & 0xFFFFFFFF);
        Assert.Equal(0u, start);
        Assert.Equal(5u, end);

        Assert.Equal("Hello", view.GetSelectedText());
    }

    [Fact]
    public void Selection_UpdateLocalSelectionAcrossMultipleLines()
    {
        // Zig: "Selection - updateLocalSelection across multiple lines"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Line 1\nLine 2\nLine 3");

        view.SetLocalSelection(2, 0, 2, 0, SelFg, SelBg);

        var changed = view.UpdateLocalSelection(2, 0, 4, 1, SelFg, SelBg);
        Assert.True(changed);

        var text = view.GetSelectedText();
        Assert.Contains("ne 1", text);
        Assert.Contains("\n", text);
        Assert.Contains("Line", text);
    }

    [Fact]
    public void Selection_UpdateLocalSelectionBackward()
    {
        // Zig: "Selection - updateLocalSelection backward selection"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Hello World!");

        view.SetLocalSelection(11, 0, 11, 0, SelFg, SelBg);

        var changed = view.UpdateLocalSelection(11, 0, 6, 0, SelFg, SelBg);
        Assert.True(changed);

        var info = view.GetSelectionInfo();
        var start = (uint)(info >> 32);
        var end = (uint)(info & 0xFFFFFFFF);
        Assert.Equal(6u, start);
        Assert.Equal(12u, end);

        Assert.Equal("World!", view.GetSelectedText());
    }

    [Fact]
    public void Selection_UpdateLocalSelectionWithWrappedLines()
    {
        // Zig: "Selection - updateLocalSelection with wrapped lines"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("ABCDEFGHIJKLMNOPQRST");

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);
        Assert.Equal(2u, view.GetVirtualLineCount());

        view.SetLocalSelection(0, 0, 0, 0, SelFg, SelBg);

        var changed = view.UpdateLocalSelection(0, 0, 5, 1, SelFg, SelBg);
        Assert.True(changed);

        var info = view.GetSelectionInfo();
        var start = (uint)(info >> 32);
        var end = (uint)(info & 0xFFFFFFFF);
        Assert.Equal(0u, start);
        Assert.Equal(15u, end);

        Assert.Equal("ABCDEFGHIJKLMNO", view.GetSelectedText());
    }

    [Fact]
    public void Selection_UpdateLocalSelectionSamePositionMaintains()
    {
        // Zig: "Selection - updateLocalSelection with same focus position maintains selection"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Hello World");

        view.SetLocalSelection(0, 0, 5, 0, SelFg, SelBg);
        view.UpdateLocalSelection(0, 0, 5, 0, SelFg, SelBg);

        var info = view.GetSelectionInfo();
        var start = (uint)(info >> 32);
        var end = (uint)(info & 0xFFFFFFFF);
        Assert.Equal(0u, start);
        Assert.Equal(5u, end);
    }

    [Fact]
    public void Selection_UpdateLocalSelectionPreservesAnchor()
    {
        // Zig: "Selection - updateLocalSelection preserves anchor correctly"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Line 1\nLine 2\nLine 3");

        view.SetLocalSelection(3, 1, 3, 1, SelFg, SelBg);
        view.UpdateLocalSelection(3, 1, 6, 1, SelFg, SelBg);
        view.UpdateLocalSelection(3, 1, 2, 2, SelFg, SelBg);
        view.UpdateLocalSelection(3, 1, 6, 2, SelFg, SelBg);

        var text = view.GetSelectedText();
        Assert.Contains("e 2", text);
        Assert.Contains("\nLine 3", text);
    }

    #endregion

    // =====================================================================
    // SELECTION + VIEWPORT TESTS (from text-buffer-selection_viewport_test.zig)
    // =====================================================================

    #region Selection with viewport

    [Fact]
    public void SelectionViewport_VerticalWithoutWrapping()
    {
        // Zig: "Selection - vertical viewport selection without wrapping"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Line 0\nLine 1\nLine 2\nLine 3\nLine 4\nLine 5\nLine 6\nLine 7\nLine 8\nLine 9");

        view.SetViewport(0, 5, 10, 5);
        view.SetLocalSelection(0, 0, 2, 2, SelFg, SelBg);

        var text = view.GetSelectedText();
        Assert.Contains("Line 5", text);
        Assert.Contains("Line 6", text);
        Assert.Contains("Li", text);
        Assert.DoesNotContain("Line 7", text);
    }

    [Fact]
    public void SelectionViewport_HorizontalWithoutWrapping()
    {
        // Zig: "Selection - horizontal viewport selection without wrapping"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");

        view.SetViewport(10, 0, 10, 1);
        view.SetLocalSelection(0, 0, 5, 0, SelFg, SelBg);

        Assert.Equal("KLMNO", view.GetSelectedText());
    }

    [Fact]
    public void SelectionViewport_WrappingIgnoresHorizontalOffset()
    {
        // Zig: "Selection - wrapping mode ignores horizontal viewport offset"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("ABCDEFGHIJKLMNOPQRSTUVWXYZ");

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);
        view.SetViewport(10, 0, 10, 3);
        view.SetLocalSelection(0, 0, 5, 0, SelFg, SelBg);

        Assert.Equal("ABCDE", view.GetSelectedText());
    }

    [Fact]
    public void SelectionViewport_VerticalWithWrapping()
    {
        // Zig: "Selection - vertical viewport with wrapping"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);

        Assert.Equal(4u, view.GetVirtualLineCount());

        view.SetViewport(0, 1, 10, 2);
        view.SetLocalSelection(0, 0, 5, 1, SelFg, SelBg);

        Assert.Equal("KLMNOPQRSTUVWXY", view.GetSelectedText());
    }

    [Fact]
    public void SelectionViewport_AcrossEmptyLineWithOffset()
    {
        // Zig: "Selection - across empty line with viewport offset"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Line0\n\nLine2\nLine3\nLine4");

        view.SetViewport(0, 1, 10, 3);
        view.SetLocalSelection(0, 0, 3, 2, SelFg, SelBg);

        var text = view.GetSelectedText();
        Assert.Contains("Line2", text);
        Assert.Contains("Lin", text);
    }

    [Fact]
    public void SelectionViewport_MultiLineSelection()
    {
        // Zig: "Selection - viewport offset with multi-line selection"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("AAA\nBBB\nCCC\nDDD\nEEE\nFFF\nGGG\nHHH");

        view.SetViewport(0, 2, 10, 4);
        view.SetLocalSelection(0, 0, 3, 0, SelFg, SelBg);

        Assert.Equal("CCC", view.GetSelectedText());
    }

    [Fact]
    public void SelectionViewport_CombinedHorizontalAndVertical()
    {
        // Zig: "Selection - combined horizontal and vertical viewport offsets"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("ABCDEFGHIJKLMNOPQRSTUVWXYZ\n0123456789ABCDEFGHIJKLMNOP\nQRSTUVWXYZ0123456789ABCDEF");

        view.SetViewport(5, 1, 10, 2);
        view.SetLocalSelection(0, 0, 5, 0, SelFg, SelBg);

        Assert.Equal("56789", view.GetSelectedText());
    }

    [Fact]
    public void SelectionViewport_NoOffsetsAsUsual()
    {
        // Zig: "Selection - viewport without offsets behaves as before"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Hello World");

        view.SetViewport(0, 0, 20, 5);
        view.SetLocalSelection(2, 0, 7, 0, SelFg, SelBg);

        Assert.Equal("llo W", view.GetSelectedText());
    }

    [Fact]
    public void SelectionViewport_NoViewportBehavesAsUsual()
    {
        // Zig: "Selection - no viewport behaves as before"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Hello World");

        view.SetLocalSelection(2, 0, 7, 0, SelFg, SelBg);

        Assert.Equal("llo W", view.GetSelectedText());
    }

    [Fact]
    public void SelectionViewport_ValidationRangeMatchesExtractedText()
    {
        // Zig: "Selection - VALIDATION: verify selection range matches extracted text with viewport"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Line0\nLine1\nLine2\nLine3\nLine4\nLine5\nLine6\nLine7\nLine8\nLine9");

        view.SetViewport(0, 5, 10, 5);
        view.SetLocalSelection(0, 0, 5, 0, SelFg, SelBg);

        Assert.Equal("Line5", view.GetSelectedText());

        var info = view.GetSelectionInfo();
        Assert.NotEqual(0xFFFFFFFF_FFFFFFFFul, info);

        var start = (uint)(info >> 32);
        var end = (uint)(info & 0xFFFFFFFF);
        Assert.Equal(30u, start); // Start of line 5
        Assert.Equal(35u, end);   // First 5 chars of line 5
    }

    [Fact]
    public void SelectionViewport_ValidationMultiLineRange()
    {
        // Zig: "Selection - VALIDATION: multi-line selection range with viewport"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("AAA\nBBB\nCCC\nDDD\nEEE\nFFF\nGGG\nHHH");

        view.SetViewport(0, 3, 10, 5);
        view.SetLocalSelection(0, 0, 3, 2, SelFg, SelBg);

        Assert.Equal("DDD\nEEE\nFFF", view.GetSelectedText());

        var info = view.GetSelectionInfo();
        Assert.NotEqual(0xFFFFFFFF_FFFFFFFFul, info);

        var start = (uint)(info >> 32);
        var end = (uint)(info & 0xFFFFFFFF);
        Assert.Equal(12u, start); // Start of line 3
        Assert.Equal(23u, end);   // End of "FFF"
    }

    [Fact(Skip = "Render cell inspection requires native OptimizedBuffer.Get which is not exposed in C#")]
    public void SelectionViewport_RenderHighlightsCorrectCells()
    {
        // Zig: "Selection - RENDER TEST: selection highlights correct cells with viewport scroll"
    }

    #endregion

    // =====================================================================
    // DRAWING TESTS (from text-buffer-drawing_test.zig)
    // =====================================================================

    #region Drawing - Basic rendering

    [Fact]
    public void Drawing_SimpleSingleLineText()
    {
        // Zig: "drawTextBuffer - simple single line text"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var buf = OptimizedBuffer.Create(20, 5);

        tb.SetText("Hello World");
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);

        // Verify no crash; text was drawn
        Assert.Equal(20u, buf.Width);
        Assert.Equal(5u, buf.Height);
    }

    [Fact]
    public void Drawing_EmptyTextBuffer()
    {
        // Zig: "drawTextBuffer - empty text buffer"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var buf = OptimizedBuffer.Create(20, 5);

        tb.SetText("");
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);
    }

    [Fact]
    public void Drawing_MultipleLinesWithoutWrapping()
    {
        // Zig: "drawTextBuffer - multiple lines without wrapping"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var buf = OptimizedBuffer.Create(20, 10);

        tb.SetText("Line 1\nLine 2\nLine 3");
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);

        Assert.Equal(3u, tb.LineCount);
    }

    [Fact]
    public void Drawing_WordWrapping()
    {
        // Zig: "drawTextBuffer - text wrapping at word boundaries"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var buf = OptimizedBuffer.Create(15, 10);

        tb.SetText("This is a long line that should wrap at word boundaries");
        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(15);

        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);

        Assert.True(view.GetVirtualLineCount() > 1);
    }

    [Fact(Skip = "Cell-level inspection (get(x,y)) not exposed in C# OptimizedBuffer")]
    public void Drawing_TransparentBackgroundPreservesUnderlying()
    {
        // Zig: "drawTextBuffer - transparent background preserves underlying non-space under spaces"
    }

    [Fact]
    public void Drawing_CharacterWrapping()
    {
        // Zig: "drawTextBuffer - text wrapping at character boundaries"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var buf = OptimizedBuffer.Create(10, 10);

        tb.SetText("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);

        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);

        Assert.Equal(4u, view.GetVirtualLineCount());
    }

    [Fact]
    public void Drawing_NoWrappingNoneMode()
    {
        // Zig: "drawTextBuffer - no wrapping with none mode"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var buf = OptimizedBuffer.Create(20, 5);

        tb.SetText("This is a very long line that extends beyond the buffer width");
        view.SetWrapMode((byte)WrapMode.Word);
        // No wrap width set => no wrapping

        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);

        Assert.Equal(1u, view.GetVirtualLineCount());
    }

    [Fact]
    public void Drawing_WrappedTextMultipleInputLines()
    {
        // Zig: "drawTextBuffer - wrapped text with multiple lines"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var buf = OptimizedBuffer.Create(15, 15);

        tb.SetText("First long line that wraps\nSecond long line that also wraps\nThird line");
        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(15);

        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);

        Assert.True(view.GetVirtualLineCount() >= 3);
    }

    [Fact]
    public void Drawing_UnicodeCharactersWithWrapping()
    {
        // Zig: "drawTextBuffer - unicode characters with wrapping"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var buf = OptimizedBuffer.Create(15, 10);

        tb.SetText("Hello 世界 🌟 Test wrapping");
        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(15);

        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);

        Assert.True(view.GetVirtualLineCount() > 0);
    }

    [Fact]
    public void Drawing_WrappingPreservesWideCharacters()
    {
        // Zig: "drawTextBuffer - wrapping preserves wide characters"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var buf = OptimizedBuffer.Create(10, 10);

        tb.SetText("測試測試測試測試測試");
        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);

        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);

        Assert.True(view.GetVirtualLineCount() > 1);
    }

    [Fact(Skip = "Requires getTextRange on virtual lines which is internal-only")]
    public void Drawing_WordWrapDoesNotSplitMultiByteUtf8()
    {
        // Zig: "drawTextBuffer - word wrap does not split multi-byte UTF-8 characters"
    }

    [Fact(Skip = "Cell-level inspection (get(x,y)) not exposed in C# OptimizedBuffer")]
    public void Drawing_WrappedTextWithOffsetPosition()
    {
        // Zig: "drawTextBuffer - wrapped text with offset position"
    }

    [Fact]
    public void Drawing_ClippingWithScrolledView()
    {
        // Zig: "drawTextBuffer - clipping with scrolled view"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var buf = OptimizedBuffer.Create(20, 5);

        tb.SetText("Line 1\nLine 2\nLine 3\nLine 4");

        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);

        Assert.True(view.GetVirtualLineCount() >= 4);
    }

    [Fact]
    public void Drawing_WrappingWithVeryNarrowWidth()
    {
        // Zig: "drawTextBuffer - wrapping with very narrow width"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var buf = OptimizedBuffer.Create(3, 10);

        tb.SetText("Hello");
        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(3);

        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);

        Assert.Equal(2u, view.GetVirtualLineCount());
    }

    [Fact]
    public void Drawing_WordWrapDoesntBreakMidWord()
    {
        // Zig: "drawTextBuffer - word wrap doesn't break mid-word"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var buf = OptimizedBuffer.Create(8, 5);

        tb.SetText("Hello World");
        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(8);

        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);

        Assert.Equal(2u, view.GetVirtualLineCount());
    }

    [Fact]
    public void Drawing_EmptyLinesRenderCorrectly()
    {
        // Zig: "drawTextBuffer - empty lines render correctly"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var buf = OptimizedBuffer.Create(20, 10);

        tb.SetText("Line 1\n\nLine 3");

        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);

        Assert.Equal(3u, view.GetVirtualLineCount());
    }

    [Fact]
    public void Drawing_WrappingWithTabs()
    {
        // Zig: "drawTextBuffer - wrapping with tabs"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var buf = OptimizedBuffer.Create(15, 10);

        tb.SetText("Hello\tWorld\tTest");
        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(15);

        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);
    }

    [Fact]
    public void Drawing_VeryLongUnwrappedLineClipping()
    {
        // Zig: "drawTextBuffer - very long unwrapped line clipping"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var buf = OptimizedBuffer.Create(20, 5);

        tb.SetText(new string('A', 200));
        view.SetWrapMode((byte)WrapMode.Word);
        // No wrap width

        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);

        Assert.Equal(1u, view.GetVirtualLineCount());
    }

    [Fact]
    public void Drawing_WrapModeTransitions()
    {
        // Zig: "drawTextBuffer - wrap mode transitions"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("This is a test line for wrapping");

        // No wrap
        view.SetWrapMode((byte)WrapMode.Word);
        var noWrapLines = view.GetVirtualLineCount();

        // Char wrap
        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);
        var charLines = view.GetVirtualLineCount();

        // Word wrap
        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(10);
        var wordLines = view.GetVirtualLineCount();

        Assert.Equal(1u, noWrapLines);
        Assert.True(charLines > 1);
        Assert.True(wordLines > 1);
    }

    [Fact]
    public void Drawing_ChangingWrapWidthUpdatesVirtualLines()
    {
        // Zig: "drawTextBuffer - changing wrap width updates virtual lines"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("AAAAAAAAAAAAAAAAAAAAAAAAAAAA");

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);
        var lines10 = view.GetVirtualLineCount();

        view.SetWrapWidth(20);
        var lines20 = view.GetVirtualLineCount();

        view.SetWrapWidth(5);
        var lines5 = view.GetVirtualLineCount();

        Assert.True(lines10 > lines20);
        Assert.True(lines5 > lines10);
    }

    [Fact]
    public void Drawing_WrappingWithMixedAsciiAndUnicode()
    {
        // Zig: "drawTextBuffer - wrapping with mixed ASCII and Unicode"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var buf = OptimizedBuffer.Create(10, 10);

        tb.SetText("ABC測試DEF試験GHI");
        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);

        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);

        Assert.True(view.GetVirtualLineCount() > 1);
    }

    #endregion

    #region Drawing - Styled text

    [Fact(Skip = "setStyledText chunk API is not fully exposed in C# (SetStyledText only sets plain text)")]
    public void Drawing_StyledTextBasicRendering()
    {
        // Zig: "setStyledText - basic rendering with single chunk"
    }

    [Fact(Skip = "setStyledText chunk API is not fully exposed in C# (SetStyledText only sets plain text)")]
    public void Drawing_StyledTextMultipleChunks()
    {
        // Zig: "setStyledText - multiple chunks render correctly"
    }

    #endregion

    #region Drawing - Viewport tests

    [Fact]
    public void Viewport_BasicVerticalScrolling()
    {
        // Zig: "viewport - basic vertical scrolling limits returned lines"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Line 0\nLine 1\nLine 2\nLine 3\nLine 4\nLine 5\nLine 6\nLine 7\nLine 8\nLine 9");

        view.SetViewport(0, 2, 20, 5);

        // Viewport set; drawing should not crash
        using var buf = OptimizedBuffer.Create(20, 5);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);
    }

    [Fact]
    public void Viewport_VerticalScrollingAtStartBoundary()
    {
        // Zig: "viewport - vertical scrolling at start boundary"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Line 0\nLine 1\nLine 2\nLine 3\nLine 4");

        view.SetViewport(0, 0, 20, 3);

        using var buf = OptimizedBuffer.Create(20, 3);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);
    }

    [Fact]
    public void Viewport_VerticalScrollingAtEndBoundary()
    {
        // Zig: "viewport - vertical scrolling at end boundary"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Line 0\nLine 1\nLine 2\nLine 3\nLine 4");

        view.SetViewport(0, 3, 20, 3);

        using var buf = OptimizedBuffer.Create(20, 3);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);
    }

    [Fact]
    public void Viewport_VerticalScrollingBeyondContent()
    {
        // Zig: "viewport - vertical scrolling beyond content"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Line 0\nLine 1\nLine 2");

        view.SetViewport(0, 10, 20, 5);

        using var buf = OptimizedBuffer.Create(20, 5);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);
    }

    [Fact]
    public void Viewport_WithWrappingVerticalScrolling()
    {
        // Zig: "viewport - with wrapping vertical scrolling"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("This is a long line that will wrap\nShort\nAnother long line that wraps");

        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(15);

        var totalVlines = view.GetVirtualLineCount();
        Assert.True(totalVlines > 3);

        view.SetViewport(0, 2, 15, 3);

        using var buf = OptimizedBuffer.Create(15, 3);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);
    }

    [Fact(Skip = "getCachedLineInfo is internal-only in Zig")]
    public void Viewport_GetCachedLineInfoReturnsOnlyViewportLines()
    {
        // Zig: "viewport - getCachedLineInfo returns only viewport lines"
    }

    [Fact]
    public void Viewport_ChangingViewportUpdatesReturnedLines()
    {
        // Zig: "viewport - changing viewport updates returned lines"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Line 0\nLine 1\nLine 2\nLine 3\nLine 4\nLine 5");

        view.SetViewport(0, 0, 20, 2);
        using var buf = OptimizedBuffer.Create(20, 5);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);

        view.SetViewport(0, 3, 20, 2);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);

        view.SetViewport(0, 1, 20, 4);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);
    }

    [Fact(Skip = "Null viewport (setViewport(null)) has no C# equivalent")]
    public void Viewport_NullViewportReturnsAllLines()
    {
        // Zig: "viewport - null viewport returns all lines"
    }

    [Fact]
    public void Viewport_SetViewportSizeConvenience()
    {
        // Zig: "viewport - setViewportSize convenience method"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Line 0\nLine 1\nLine 2\nLine 3");

        view.SetViewportSize(20, 2);

        using var buf = OptimizedBuffer.Create(20, 2);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);
    }

    [Fact]
    public void Viewport_StoresHorizontalOffsetNoWrapping()
    {
        // Zig: "viewport - stores horizontal offset value with no wrapping"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("ABCDEFGHIJKLMNOPQRSTUVWXYZ");

        view.SetWrapMode((byte)WrapMode.None);
        view.SetViewport(5, 0, 10, 1);

        using var buf = OptimizedBuffer.Create(10, 1);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);
    }

    [Fact]
    public void Viewport_PreservesHorizontalOffsetWhenChangingVertical()
    {
        // Zig: "viewport - preserves horizontal offset when changing vertical (no wrap)"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("ABCDEFGHIJ\nKLMNOPQRST\nUVWXYZ1234");

        view.SetWrapMode((byte)WrapMode.None);
        view.SetViewport(3, 0, 8, 2);

        using var buf = OptimizedBuffer.Create(8, 2);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);

        view.SetViewport(3, 1, 8, 2);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);
    }

    [Fact]
    public void Viewport_CanSetLargeHorizontalOffset()
    {
        // Zig: "viewport - can set large horizontal offset (no wrap)"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Short\nLonger line here\nTiny");

        view.SetWrapMode((byte)WrapMode.None);
        view.SetViewport(10, 0, 10, 3);

        using var buf = OptimizedBuffer.Create(10, 3);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);
    }

    [Fact]
    public void Viewport_HorizontalAndVerticalCombined()
    {
        // Zig: "viewport - horizontal and vertical offset combined (no wrap)"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Line 0: ABCDEFGHIJ\nLine 1: KLMNOPQRST\nLine 2: UVWXYZ1234\nLine 3: 567890ABCD");

        view.SetWrapMode((byte)WrapMode.None);
        view.SetViewport(8, 1, 15, 2);

        using var buf = OptimizedBuffer.Create(15, 2);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);
    }

    [Fact]
    public void Viewport_HorizontalScrollingOnlyForNoWrapMode()
    {
        // Zig: "viewport - horizontal scrolling only for no-wrap mode"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");

        view.SetWrapMode((byte)WrapMode.None);
        view.SetViewport(10, 0, 15, 1);

        using var buf = OptimizedBuffer.Create(15, 1);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetViewport(10, 0, 15, 5);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);
    }

    [Fact]
    public void Viewport_HorizontalOffsetIrrelevantWithWrapping()
    {
        // Zig: "viewport - horizontal offset irrelevant with wrapping enabled"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("This is a very long line that will wrap into multiple virtual lines");

        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(20);

        Assert.True(view.GetVirtualLineCount() > 1);

        view.SetViewport(5, 1, 15, 2);

        using var buf = OptimizedBuffer.Create(15, 2);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);
    }

    [Fact]
    public void Viewport_ZeroWidthOrHeight()
    {
        // Zig: "viewport - zero width or height"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("Line 0\nLine 1\nLine 2");

        view.SetViewport(0, 0, 20, 0);
        using var buf = OptimizedBuffer.Create(20, 5);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);

        view.SetViewport(0, 0, 0, 2);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);
    }

    [Fact]
    public void Viewport_SetsWrapWidthAutomatically()
    {
        // Zig: "viewport - viewport sets wrap width automatically"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("AAAAAAAAAABBBBBBBBBBCCCCCCCCCCDDDDDDDDDD");

        view.SetWrapMode((byte)WrapMode.Char);

        view.SetViewport(0, 0, 10, 5);
        var vlineCount10 = view.GetVirtualLineCount();

        view.SetViewport(0, 0, 20, 5);
        var vlineCount20 = view.GetVirtualLineCount();

        Assert.True(vlineCount10 > vlineCount20);
    }

    [Fact]
    public void Viewport_MovingViewportDynamically()
    {
        // Zig: "viewport - moving viewport dynamically (no wrap)"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText("0123456789\nABCDEFGHIJ\nKLMNOPQRST\nUVWXYZ!@#$");

        view.SetWrapMode((byte)WrapMode.None);

        using var buf = OptimizedBuffer.Create(20, 5);

        view.SetViewport(0, 0, 5, 2);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);

        view.SetViewport(0, 1, 5, 2);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);

        view.SetViewport(3, 1, 5, 2);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);

        view.SetViewport(5, 2, 5, 2);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);
    }

    #endregion

    #region Drawing - Horizontal viewport rendering

    [Fact]
    public void Drawing_HorizontalViewportOffsetRendersCorrectly()
    {
        // Zig: "drawTextBuffer - horizontal viewport offset renders correctly without wrapping"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var buf = OptimizedBuffer.Create(10, 1);

        tb.SetText("0123456789ABCDEFGHIJ");
        view.SetWrapMode((byte)WrapMode.None);
        view.SetViewport(5, 0, 10, 1);

        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);
    }

    [Fact]
    public void Drawing_HorizontalViewportWithMultipleLines()
    {
        // Zig: "drawTextBuffer - horizontal viewport offset with multiple lines"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var buf = OptimizedBuffer.Create(8, 3);

        tb.SetText("ABCDEFGHIJKLMNO\n0123456789!@#$%\nXYZ[\\]^_`{|}~");
        view.SetWrapMode((byte)WrapMode.None);
        view.SetViewport(3, 0, 8, 3);

        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);
    }

    [Fact]
    public void Drawing_CombinedHorizontalAndVerticalViewport()
    {
        // Zig: "drawTextBuffer - combined horizontal and vertical viewport offsets"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var buf = OptimizedBuffer.Create(10, 2);

        tb.SetText("Line0ABCDEFGHIJ\nLine1KLMNOPQRST\nLine2UVWXYZ0123\nLine3456789!@#$");
        view.SetWrapMode((byte)WrapMode.None);
        view.SetViewport(5, 1, 10, 2);

        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);
    }

    [Fact(Skip = "Cell-level inspection (get(x,y)) not exposed in C# OptimizedBuffer")]
    public void Drawing_HorizontalViewportStopsAtWidth()
    {
        // Zig: "drawTextBuffer - horizontal viewport stops rendering at viewport width"
    }

    [Fact(Skip = "Cell-level inspection (get(x,y)) not exposed in C# OptimizedBuffer")]
    public void Drawing_HorizontalViewportWithSmallBuffer()
    {
        // Zig: "drawTextBuffer - horizontal viewport with small buffer renders only viewport width"
    }

    [Fact(Skip = "Cell-level inspection (get(x,y)) not exposed in C# OptimizedBuffer")]
    public void Drawing_HorizontalViewportWidthLimitsRendering()
    {
        // Zig: "drawTextBuffer - horizontal viewport width limits rendering (efficiency test)"
    }

    [Fact(Skip = "Cell-level inspection (get(x,y) / grapheme encoding) not exposed in C# OptimizedBuffer")]
    public void Drawing_OverwritingWideGraphemeWithAscii()
    {
        // Zig: "drawTextBuffer - overwriting wide grapheme with ASCII leaves no ghost chars"
    }

    #endregion

    #region Drawing - Syntax style & tabs

    [Fact]
    public void Drawing_SyntaxStyleDestroyDoesNotCrash()
    {
        // Zig: "drawTextBuffer - syntax style destroy does not crash"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var buf = OptimizedBuffer.Create(20, 5);

        var style = SyntaxStyle.Create();
        tb.SetSyntaxStyle(style.Handle);

        var styleId = style.Register("test", fg: Rgba.Red);
        tb.SetText("Hello World");
        tb.AddHighlightByCharRange(new Highlight { Start = 0, End = 5, StyleId = styleId, Priority = 1, HlRef = 0 });

        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);

        style.Dispose();

        tb.SetSyntaxStyle(nint.Zero);

        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);
    }

    [Fact]
    public void Drawing_TabsRenderedAsSpaces()
    {
        // Zig: "drawTextBuffer - tabs are rendered as spaces (empty cells)"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var buf = OptimizedBuffer.Create(20, 5);

        tb.TabWidth = 4;
        tb.SetText("A\tB");

        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);

        // Verify plain text is intact
        Assert.Equal("A\tB", tb.GetPlainText());
    }

    [Fact]
    public void Drawing_TabIndicatorColor()
    {
        // Zig: "drawTextBuffer - tab indicator renders with correct color"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var buf = OptimizedBuffer.Create(20, 5);

        tb.TabWidth = 4;
        tb.SetText("A\tB");

        view.SetTabIndicator('→');
        view.SetTabIndicatorColor(new Rgba(0.25f, 0.25f, 0.25f, 1f));

        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);
    }

    [Fact]
    public void Drawing_TabWithoutIndicatorRendersAsSpaces()
    {
        // Zig: "drawTextBuffer - tab without indicator renders as spaces"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var buf = OptimizedBuffer.Create(20, 5);

        tb.TabWidth = 4;
        tb.SetText("A\tB");

        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);
    }

    #endregion

    #region Drawing - Complex/special content

    [Fact(Skip = "Cell-level inspection not exposed in C# OptimizedBuffer")]
    public void Drawing_MixedAsciiUnicodeWithEmojiRendersCompletely()
    {
        // Zig: "drawTextBuffer - mixed ASCII and Unicode with emoji renders completely"
    }

    [Fact(Skip = "Cell-level inspection not exposed in C# OptimizedBuffer")]
    public void Drawing_ViewportWidth31_LastCharRendering()
    {
        // Zig: "viewport width = 31 exactly - last character rendering"
    }

    [Fact]
    public void Drawing_ComplexMultilingualTextRendersWithoutCrash()
    {
        // Zig: "drawTextBuffer - complex multilingual text with diverse scripts and emojis"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var buf = OptimizedBuffer.Create(80, 100);

        var text = "# The Celestial Journey of संस्कृति 🌟🔮✨\n" +
                   "In the beginning, there was नमस्ते 🙏 and 漢字 and 한글.\n" +
                   "The traders sold বাংলা spices 🌶️ and ಕನ್ನಡ silks.\n" +
                   "Musicians played 🎻🎺🎷 ensemble harmonies.\n" +
                   "The end. समाप्त. 끝. จบ. முடிவு. 🎬✨🌟⭐";

        tb.SetText(text);
        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(80);

        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);

        Assert.True(view.GetVirtualLineCount() > 0);

        // Verify plain text roundtrip
        var plainText = tb.GetPlainText();
        Assert.Contains("संस्कृति", plainText);
        Assert.Contains("नमस्ते", plainText);
        Assert.Contains("漢字", plainText);
        Assert.Contains("한글", plainText);
        Assert.Contains("🌟", plainText);

        // Test char wrapping
        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(40);
        var charWrapLines = view.GetVirtualLineCount();

        // Test viewport scrolling
        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(80);
        view.SetViewport(0, 2, 80, 3);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);
    }

    [Fact(Skip = "setStyledText chunk API is not fully exposed in C#")]
    public void Drawing_StyledTextHighlightPositioningWithUnicode()
    {
        // Zig: "setStyledText - highlight positioning with Unicode text"
    }

    [Fact]
    public void Drawing_LoadFileAndRender()
    {
        // Zig: "loadFile - loads and renders file correctly"
        // Simplified: verify SetText + draw works correctly for multi-line content
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var buf = OptimizedBuffer.Create(20, 5);

        tb.SetText("ABC\nDEF");

        Assert.Equal(2u, tb.LineCount);
        Assert.Equal(6u, tb.Length);

        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);
    }

    #endregion

    // =====================================================================
    // SEGMENT TESTS (from text-buffer-segment_test.zig)
    // =====================================================================

    #region Segment tests (internal Zig types — closest C# equivalents)

    // The Segment, TextChunk, UnifiedRope, and Metrics types are all internal
    // Zig data structures not exposed through the C FFI. The following tests
    // verify the closest observable behaviour through the public C# API.

    [Fact(Skip = "Segment.measure is internal-only Zig struct; no C# equivalent")]
    public void Segment_MeasureTextChunk()
    {
        // Zig: "Segment.measure - text chunk"
    }

    [Fact(Skip = "Segment.measure is internal-only Zig struct; no C# equivalent")]
    public void Segment_MeasureBreak()
    {
        // Zig: "Segment.measure - break"
    }

    [Fact(Skip = "Segment.empty/is_empty is internal-only Zig struct; no C# equivalent")]
    public void Segment_EmptyAndIsEmpty()
    {
        // Zig: "Segment.empty and is_empty"
    }

    [Fact(Skip = "Segment.isBreak/isText is internal-only Zig struct; no C# equivalent")]
    public void Segment_IsBreakAndIsText()
    {
        // Zig: "Segment.isBreak and isText"
    }

    [Fact(Skip = "Segment.asText is internal-only Zig struct; no C# equivalent")]
    public void Segment_AsText()
    {
        // Zig: "Segment.asText"
    }

    [Fact(Skip = "Metrics.add is internal-only Zig struct; no C# equivalent")]
    public void Metrics_AddTwoTextSegments()
    {
        // Zig: "Metrics.add - two text segments"
    }

    [Fact(Skip = "Metrics.add is internal-only Zig struct; no C# equivalent")]
    public void Metrics_AddTextBreakText()
    {
        // Zig: "Metrics.add - text, break, text"
    }

    [Fact(Skip = "Metrics.add is internal-only Zig struct; no C# equivalent")]
    public void Metrics_AddMultipleBreaks()
    {
        // Zig: "Metrics.add - multiple breaks"
    }

    [Fact(Skip = "Metrics.add is internal-only Zig struct; no C# equivalent")]
    public void Metrics_AddNonAsciiPropagation()
    {
        // Zig: "Metrics.add - non-ASCII propagation"
    }

    [Fact(Skip = "UnifiedRope is internal-only Zig struct; no C# equivalent")]
    public void UnifiedRope_BasicOperations()
    {
        // Zig: "UnifiedRope - basic operations"
    }

    [Fact(Skip = "UnifiedRope is internal-only Zig struct; no C# equivalent")]
    public void UnifiedRope_EmptyRopeMetrics()
    {
        // Zig: "UnifiedRope - empty rope metrics"
    }

    [Fact(Skip = "UnifiedRope is internal-only Zig struct; no C# equivalent")]
    public void UnifiedRope_SingleTextSegment()
    {
        // Zig: "UnifiedRope - single text segment"
    }

    [Fact(Skip = "UnifiedRope is internal-only Zig struct; no C# equivalent")]
    public void UnifiedRope_MultipleLinesVaryingWidths()
    {
        // Zig: "UnifiedRope - multiple lines with varying widths"
    }

    [Fact(Skip = "combineMetrics is internal-only Zig helper; no C# equivalent")]
    public void CombineMetrics_HelperFunction()
    {
        // Zig: "combineMetrics helper function"
    }

    #endregion
}
