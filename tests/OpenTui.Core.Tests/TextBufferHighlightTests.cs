using Xunit;
using OpenTui.Core;

namespace OpenTui.Core.Tests;

/// <summary>
/// Highlight tests that verify detailed highlight properties
/// (col_start, col_end, style_id) returned by TextBuffer.GetLineHighlights().
/// Complements the count-only assertions in TextBufferExtendedTests.cs with
/// exact per-line highlight verification matching the Zig reference tests
/// in text-buffer-highlights_test.zig.
/// </summary>
public class TextBufferHighlightTests
{

    // =====================================================================
    // LINE-BASED HIGHLIGHT TESTS (using P/Invoke for detail verification)
    // =====================================================================

    #region Line-based highlights

    [Fact]
    public void AddSingleHighlight_VerifyDetails()
    {
        // Zig: "TextBuffer highlights - add single highlight to line"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Hello World");

        tb.AddHighlight(0, new Highlight { Start = 0, End = 5, StyleId = 1, Priority = 0, HlRef = 0 });

        var highlights = tb.GetLineHighlights(0);
        Assert.Single(highlights);
        Assert.Equal(0u, highlights[0].Start);
        Assert.Equal(5u, highlights[0].End);
        Assert.Equal(1u, highlights[0].StyleId);
    }

    [Fact]
    public void AddMultipleHighlights_VerifyStyleIds()
    {
        // Zig: "TextBuffer highlights - add multiple highlights to same line"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Hello World");

        tb.AddHighlight(0, new Highlight { Start = 0, End = 5, StyleId = 1, Priority = 0, HlRef = 0 });
        tb.AddHighlight(0, new Highlight { Start = 6, End = 11, StyleId = 2, Priority = 0, HlRef = 0 });

        var highlights = tb.GetLineHighlights(0);
        Assert.Equal(2, highlights.Length);
        Assert.Equal(1u, highlights[0].StyleId);
        Assert.Equal(2u, highlights[1].StyleId);
    }

    [Fact]
    public void AddHighlightsToMultipleLines_VerifyPerLine()
    {
        // Zig: "TextBuffer highlights - add highlights to multiple lines"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Line 1\nLine 2\nLine 3");

        tb.AddHighlight(0, new Highlight { Start = 0, End = 6, StyleId = 1, Priority = 0, HlRef = 0 });
        tb.AddHighlight(1, new Highlight { Start = 0, End = 6, StyleId = 2, Priority = 0, HlRef = 0 });
        tb.AddHighlight(2, new Highlight { Start = 0, End = 6, StyleId = 3, Priority = 0, HlRef = 0 });

        Assert.Single(tb.GetLineHighlights(0));
        Assert.Single(tb.GetLineHighlights(1));
        Assert.Single(tb.GetLineHighlights(2));
    }

    [Fact]
    public void RemoveByRef_VerifyRemainingHighlightDetails()
    {
        // Zig: "TextBuffer highlights - remove highlights by reference"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Line 1\nLine 2");

        tb.AddHighlight(0, new Highlight { Start = 0, End = 3, StyleId = 1, Priority = 0, HlRef = 100 });
        tb.AddHighlight(0, new Highlight { Start = 3, End = 6, StyleId = 2, Priority = 0, HlRef = 200 });
        tb.AddHighlight(1, new Highlight { Start = 0, End = 6, StyleId = 3, Priority = 0, HlRef = 100 });

        tb.RemoveHighlight(100);

        var line0 = tb.GetLineHighlights(0);
        var line1 = tb.GetLineHighlights(1);

        Assert.Single(line0);
        Assert.Equal(2u, line0[0].StyleId);
        Assert.Empty(line1);
    }

    [Fact]
    public void ClearLineHighlights_VerifyEmpty()
    {
        // Zig: "TextBuffer highlights - clear line highlights"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Line 1\nLine 2");

        tb.AddHighlight(0, new Highlight { Start = 0, End = 6, StyleId = 1, Priority = 0, HlRef = 0 });
        tb.AddHighlight(0, new Highlight { Start = 6, End = 10, StyleId = 2, Priority = 0, HlRef = 0 });

        tb.ClearLineHighlights(0);

        Assert.Empty(tb.GetLineHighlights(0));
    }

    [Fact]
    public void ClearAllHighlights_VerifyAllLinesEmpty()
    {
        // Zig: "TextBuffer highlights - clear all highlights"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Line 1\nLine 2\nLine 3");

        tb.AddHighlight(0, new Highlight { Start = 0, End = 6, StyleId = 1, Priority = 0, HlRef = 0 });
        tb.AddHighlight(1, new Highlight { Start = 0, End = 6, StyleId = 2, Priority = 0, HlRef = 0 });
        tb.AddHighlight(2, new Highlight { Start = 0, End = 6, StyleId = 3, Priority = 0, HlRef = 0 });

        tb.ClearHighlights();

        Assert.Empty(tb.GetLineHighlights(0));
        Assert.Empty(tb.GetLineHighlights(1));
        Assert.Empty(tb.GetLineHighlights(2));
    }

    [Fact]
    public void NonExistentLine_ReturnsEmpty()
    {
        // Zig: "TextBuffer highlights - get highlights from non-existent line"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Line 1");

        Assert.Empty(tb.GetLineHighlights(10));
    }

    [Fact]
    public void OverlappingHighlights_VerifyExactCount()
    {
        // Zig: "TextBuffer highlights - overlapping highlights"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Hello World");

        tb.AddHighlight(0, new Highlight { Start = 0, End = 8, StyleId = 1, Priority = 0, HlRef = 0 });
        tb.AddHighlight(0, new Highlight { Start = 5, End = 11, StyleId = 2, Priority = 0, HlRef = 0 });

        Assert.Equal(2, tb.GetLineHighlights(0).Length);
    }

    [Fact]
    public void ResetClearsHighlights_VerifyViaGetLine()
    {
        // Zig: "TextBuffer highlights - reset clears highlights"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Hello World");
        tb.AddHighlight(0, new Highlight { Start = 0, End = 5, StyleId = 1, Priority = 0, HlRef = 0 });

        tb.Reset();

        Assert.Empty(tb.GetLineHighlights(0));
    }

    #endregion

    // =====================================================================
    // CHARACTER RANGE HIGHLIGHT TESTS (P/Invoke detail verification)
    // =====================================================================

    #region Char-range highlights

    [Fact]
    public void CharRange_SingleLine_VerifyColRange()
    {
        // Zig: "TextBuffer char range highlights - single line highlight"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Hello World");

        tb.AddHighlightByCharRange(new Highlight { Start = 0, End = 5, StyleId = 1, Priority = 1, HlRef = 0 });

        var highlights = tb.GetLineHighlights(0);
        Assert.Single(highlights);
        Assert.Equal(0u, highlights[0].Start);
        Assert.Equal(5u, highlights[0].End);
        Assert.Equal(1u, highlights[0].StyleId);
    }

    [Fact]
    public void CharRange_MultiLine_VerifyPerLineRanges()
    {
        // Zig: "TextBuffer char range highlights - multi-line highlight"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Hello\nWorld\nTest");

        // chars 3-9 spans from 'l' in "Hello" to 'd' in "World"
        tb.AddHighlightByCharRange(new Highlight { Start = 3, End = 9, StyleId = 1, Priority = 1, HlRef = 0 });

        var line0 = tb.GetLineHighlights(0);
        var line1 = tb.GetLineHighlights(1);

        Assert.Single(line0);
        Assert.Single(line1);

        // Line 0: col 3 to end (col 5)
        Assert.Equal(3u, line0[0].Start);
        Assert.Equal(5u, line0[0].End);

        // Line 1: col 0 to col 4
        Assert.Equal(0u, line1[0].Start);
        Assert.Equal(4u, line1[0].End);
    }

    [Fact]
    public void CharRange_SpanningThreeLines_VerifyAllLines()
    {
        // Zig: "TextBuffer char range highlights - spanning three lines"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Line1\nLine2\nLine3");

        tb.AddHighlightByCharRange(new Highlight { Start = 3, End = 13, StyleId = 1, Priority = 1, HlRef = 0 });

        var line0 = tb.GetLineHighlights(0);
        var line1 = tb.GetLineHighlights(1);
        var line2 = tb.GetLineHighlights(2);

        Assert.Single(line0);
        Assert.Single(line1);
        Assert.Single(line2);

        Assert.Equal(3u, line0[0].Start);
        Assert.Equal(0u, line1[0].Start);
        Assert.Equal(0u, line2[0].Start);
    }

    [Fact]
    public void CharRange_ExactLineBoundaries_VerifyNoSpill()
    {
        // Zig: "TextBuffer char range highlights - exact line boundaries"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("AAAA\nBBBB\nCCCC");

        tb.AddHighlightByCharRange(new Highlight { Start = 0, End = 4, StyleId = 1, Priority = 1, HlRef = 0 });

        var line0 = tb.GetLineHighlights(0);
        Assert.Single(line0);
        Assert.Equal(0u, line0[0].Start);
        Assert.Equal(4u, line0[0].End);

        // Line 1 should have no highlights
        Assert.Empty(tb.GetLineHighlights(1));
    }

    [Fact]
    public void CharRange_EmptyRange_NoHighlightsAdded()
    {
        // Zig: "TextBuffer char range highlights - empty range"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Hello World");

        tb.AddHighlightByCharRange(new Highlight { Start = 5, End = 5, StyleId = 1, Priority = 1, HlRef = 0 });

        Assert.Empty(tb.GetLineHighlights(0));
    }

    [Fact]
    public void CharRange_InvalidRange_NoHighlightsAdded()
    {
        // Zig: "TextBuffer char range highlights - invalid range"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Hello World");

        tb.AddHighlightByCharRange(new Highlight { Start = 10, End = 5, StyleId = 1, Priority = 1, HlRef = 0 });

        Assert.Empty(tb.GetLineHighlights(0));
    }

    [Fact]
    public void CharRange_OutOfBounds_VerifyColStart()
    {
        // Zig: "TextBuffer char range highlights - out of bounds range"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Hello");

        tb.AddHighlightByCharRange(new Highlight { Start = 3, End = 100, StyleId = 1, Priority = 1, HlRef = 0 });

        var highlights = tb.GetLineHighlights(0);
        Assert.Single(highlights);
        Assert.Equal(3u, highlights[0].Start);
    }

    [Fact]
    public void CharRange_MultipleNonOverlapping_VerifyStyleOrder()
    {
        // Zig: "TextBuffer char range highlights - multiple non-overlapping ranges"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("function hello() { return 42; }");

        tb.AddHighlightByCharRange(new Highlight { Start = 0, End = 8, StyleId = 1, Priority = 1, HlRef = 0 });
        tb.AddHighlightByCharRange(new Highlight { Start = 9, End = 14, StyleId = 2, Priority = 1, HlRef = 0 });
        tb.AddHighlightByCharRange(new Highlight { Start = 19, End = 25, StyleId = 3, Priority = 1, HlRef = 0 });

        var highlights = tb.GetLineHighlights(0);
        Assert.Equal(3, highlights.Length);
        Assert.Equal(1u, highlights[0].StyleId);
        Assert.Equal(2u, highlights[1].StyleId);
        Assert.Equal(3u, highlights[2].StyleId);
    }

    [Fact]
    public void CharRange_WithRefId_VerifyRemoval()
    {
        // Zig: "TextBuffer char range highlights - with reference ID for removal"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Line1\nLine2\nLine3");

        tb.AddHighlightByCharRange(new Highlight { Start = 0, End = 5, StyleId = 1, Priority = 1, HlRef = 100 });
        tb.AddHighlightByCharRange(new Highlight { Start = 6, End = 11, StyleId = 2, Priority = 1, HlRef = 100 });

        Assert.Single(tb.GetLineHighlights(0));
        Assert.Single(tb.GetLineHighlights(1));

        tb.RemoveHighlight(100);
        Assert.Empty(tb.GetLineHighlights(0));
        Assert.Empty(tb.GetLineHighlights(1));
    }

    [Fact]
    public void CharRange_UnicodeText_VerifyPresence()
    {
        // Zig: "TextBuffer char range highlights - unicode text"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Hello 世界 🌟");

        var textLen = tb.Length;
        tb.AddHighlightByCharRange(new Highlight { Start = 0, End = textLen, StyleId = 1, Priority = 1, HlRef = 0 });

        Assert.Single(tb.GetLineHighlights(0));
    }

    [Fact]
    public void CharRange_PreservedAfterSetText_ThenClear()
    {
        // Zig: "TextBuffer char range highlights - preserved after setText"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Hello World");
        tb.AddHighlightByCharRange(new Highlight { Start = 0, End = 5, StyleId = 1, Priority = 1, HlRef = 0 });

        tb.SetText("New Text");
        Assert.Single(tb.GetLineHighlights(0));

        tb.ClearHighlights();
        Assert.Empty(tb.GetLineHighlights(0));
    }

    [Fact]
    public void CharRange_MultiWidthCharsBeforeHighlight_VerifyColRange()
    {
        // Zig: "TextBuffer char range highlights - multi-width chars before highlight"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("前后端分离 @git-committer");
        tb.AddHighlightByCharRange(new Highlight { Start = 11, End = 25, StyleId = 1, Priority = 1, HlRef = 0 });

        var highlights = tb.GetLineHighlights(0);
        Assert.Single(highlights);
        Assert.Equal(11u, highlights[0].Start);
        Assert.Equal(25u, highlights[0].End);
    }

    [Fact]
    public void CharRange_MultiWidthCharsBetweenHighlights_VerifyColRange()
    {
        // Zig: "TextBuffer char range highlights - multi-width chars between highlights"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("abc前后端def");
        tb.AddHighlightByCharRange(new Highlight { Start = 9, End = 12, StyleId = 1, Priority = 1, HlRef = 0 });

        var highlights = tb.GetLineHighlights(0);
        Assert.Single(highlights);
        Assert.Equal(9u, highlights[0].Start);
        Assert.Equal(12u, highlights[0].End);
    }

    [Fact]
    public void CharRange_EmojiGraphemeClusters_VerifyColRange()
    {
        // Zig: "TextBuffer char range highlights - emoji grapheme clusters"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("前🌟test");
        tb.AddHighlightByCharRange(new Highlight { Start = 4, End = 8, StyleId = 1, Priority = 1, HlRef = 0 });

        var highlights = tb.GetLineHighlights(0);
        Assert.Single(highlights);
        Assert.Equal(4u, highlights[0].Start);
        Assert.Equal(8u, highlights[0].End);
    }

    #endregion

    // =====================================================================
    // SKIPPED TESTS (APIs not available in C)
    // =====================================================================

    #region Skipped

    [Fact(Skip = "addHighlightByCoords is not exported in the native C API")]
    public void AddHighlightByCoords() { }

    [Fact(Skip = "addHighlightByCoords is not exported in the native C API")]
    public void AddHighlightByCoordsMultiLine() { }

    [Fact(Skip = "getLineSpans is internal-only in Zig; not exposed via C API")]
    public void StyleSpansComputedCorrectly() { }

    [Fact(Skip = "getLineSpans is internal-only in Zig; not exposed via C API")]
    public void PriorityHandlingInSpans() { }

    [Fact(Skip = "getLineSpans is internal-only in Zig; not exposed via C API")]
    public void CharRange_PriorityHandlingInSpans() { }

    [Fact(Skip = "setSyntaxStyle / getSyntaxStyle already covered in TextBufferExtendedTests")]
    public void SetSyntaxStyleAndGetSyntaxStyle() { }

    [Fact(Skip = "SyntaxStyle integration already covered in TextBufferExtendedTests")]
    public void IntegrationWithSyntaxStyle() { }

    #endregion
}
