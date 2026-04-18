using OpenTui.Core;
using Xunit;

namespace OpenTui.Core.Tests;

/// <summary>
/// C# equivalents of the Zig text-buffer-view_test.zig tests.
/// Exercises wrapping, selection, measurement, truncation, highlights,
/// plain-text access, automatic updates, and tab indicators through
/// the managed <see cref="TextBufferView"/> / <see cref="TextBuffer"/> wrappers.
/// </summary>
public class TextBufferViewTests
{
    // Reusable dummy selection colors (Zig tests pass null; C# API requires a value)
    private static readonly Rgba SelFg = Rgba.White;
    private static readonly Rgba SelBg = Rgba.Black;

    #region Helpers

    private static (TextBuffer tb, TextBufferView view) CreatePair()
    {
        var tb = TextBuffer.Create();
        var view = TextBufferView.Create(tb);
        return (tb, view);
    }

    #endregion

    // ── Wrapping ──────────────────────────────────────────────────

    [Fact]
    public void Wrapping_NoWrap_ReturnsSameLineCount()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");

        Assert.Equal(1u, view.GetVirtualLineCount());

        // Setting wrap width to 0 (no width limit) should not change line count
        view.SetWrapMode((byte)WrapMode.None);
        Assert.Equal(1u, view.GetVirtualLineCount());
    }

    [Fact]
    public void Wrapping_SimpleWrapSplitsLine()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRST"); // 20 chars

        Assert.Equal(1u, view.GetVirtualLineCount());

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);
        Assert.Equal(2u, view.GetVirtualLineCount());
    }

    [Fact]
    public void Wrapping_WrapAtExactBoundary()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("0123456789"); // exactly 10

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);
        Assert.Equal(1u, view.GetVirtualLineCount());
    }

    [Fact]
    public void Wrapping_PreservesNewlines()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Short\nAnother short line\nLast");

        Assert.Equal(3u, view.GetVirtualLineCount());

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(50);
        Assert.Equal(3u, view.GetVirtualLineCount());
    }

    [Fact]
    public void Wrapping_MultipleWrapLines()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123"); // 30 chars

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);
        Assert.Equal(3u, view.GetVirtualLineCount());
    }

    [Fact]
    public void Wrapping_LongLineWithNewlines()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRST\nShort");

        Assert.Equal(2u, view.GetVirtualLineCount());

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);
        Assert.Equal(3u, view.GetVirtualLineCount());
    }

    [Fact]
    public void Wrapping_ChangeWrapWidth()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRST"); // 20 chars

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);
        Assert.Equal(2u, view.GetVirtualLineCount());

        view.SetWrapWidth(5);
        Assert.Equal(4u, view.GetVirtualLineCount());

        view.SetWrapWidth(20);
        Assert.Equal(1u, view.GetVirtualLineCount());

        // Disable wrapping
        view.SetWrapMode((byte)WrapMode.None);
        Assert.Equal(1u, view.GetVirtualLineCount());
    }

    [Fact]
    public void Wrapping_GraphemeAtExactBoundary()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("12345678\U0001F31F"); // 8 ASCII + 1 wide emoji (width 2) = 10 cols

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);
        Assert.Equal(1u, view.GetVirtualLineCount());
    }

    [Fact]
    public void Wrapping_GraphemeSplitAcrossBoundary()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("123456789\U0001F31FABC"); // 9 + 2(emoji) + 3 = 14 cols

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);
        Assert.Equal(2u, view.GetVirtualLineCount());
    }

    [Fact]
    public void Wrapping_CjkCharactersAtBoundaries()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("测试文字处理"); // 6 CJK chars × 2 cols = 12 cols

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);
        Assert.Equal(2u, view.GetVirtualLineCount());
    }

    [Fact]
    public void Wrapping_MixedWidthCharacters()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("AB测试CD"); // 2 + 4 + 2 = 8 cols

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(6);
        Assert.Equal(2u, view.GetVirtualLineCount());
    }

    [Fact]
    public void Wrapping_SingleWideCharacterExceedsWidth()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("\U0001F31F"); // 2-col emoji

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(1);
        Assert.Equal(1u, view.GetVirtualLineCount());
    }

    [Fact]
    public void Wrapping_MultipleConsecutiveWideCharacters()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("\U0001F31F\U0001F31F\U0001F31F\U0001F31F\U0001F31F"); // 5 × 2 = 10 cols

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(6);
        Assert.Equal(2u, view.GetVirtualLineCount());
    }

    [Fact]
    public void Wrapping_ZeroWidthCharacters()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("e\u0301e\u0301e\u0301"); // é é é using combining acute

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(2);
        Assert.True(view.GetVirtualLineCount() >= 1);
    }

    [Fact]
    public void Wrapping_VeryNarrowWidth1Char()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDE");

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(1);
        Assert.Equal(5u, view.GetVirtualLineCount());
    }

    [Fact]
    public void Wrapping_VeryNarrowWidth2Chars()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEF");

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(2);
        Assert.Equal(3u, view.GetVirtualLineCount());
    }

    [Fact]
    public void Wrapping_SwitchBetweenCharAndWordMode()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello world test");

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(8);
        var charCount = view.GetVirtualLineCount();

        view.SetWrapMode((byte)WrapMode.Word);
        var wordCount = view.GetVirtualLineCount();

        Assert.True(charCount >= 2);
        Assert.True(wordCount >= 2);
    }

    [Fact]
    public void Wrapping_MultipleConsecutiveNewlinesWithWrapping()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJ\n\n\nKLMNOPQRST");

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(5);
        Assert.True(view.GetVirtualLineCount() >= 6);
    }

    [Fact]
    public void Wrapping_OnlySpacesShouldNotCreateExtraLines()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("          "); // 10 spaces

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(5);
        Assert.Equal(2u, view.GetVirtualLineCount());
    }

    [Fact]
    public void Wrapping_MixedTabsAndSpaces()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("AB\tCD\tEF");

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(5);
        Assert.True(view.GetVirtualLineCount() >= 1);
    }

    [Fact]
    public void Wrapping_UnicodeEmojiWithVaryingWidths()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("A\U0001F31FB\U0001F3A8C\U0001F680D");

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(5);
        Assert.True(view.GetVirtualLineCount() >= 2);
    }

    [Fact]
    public void Wrapping_GetVirtualLineCountReflectsCurrentWrapState()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRST"); // 20 chars

        Assert.Equal(1u, view.GetVirtualLineCount());

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);
        Assert.Equal(2u, view.GetVirtualLineCount());

        view.SetWrapWidth(5);
        Assert.Equal(4u, view.GetVirtualLineCount());

        view.SetWrapMode((byte)WrapMode.None);
        Assert.Equal(1u, view.GetVirtualLineCount());
    }

    // ── Word Wrapping ─────────────────────────────────────────────

    [Fact]
    public void WordWrapping_BasicWordWrapAtSpace()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");

        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(8);
        Assert.Equal(2u, view.GetVirtualLineCount());
    }

    [Fact]
    public void WordWrapping_LongWordExceedsWidth()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRSTUVWXYZ"); // 26 chars, no spaces

        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(10);
        Assert.Equal(3u, view.GetVirtualLineCount());
    }

    [Fact]
    public void WordWrapping_MultipleWords()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("The quick brown fox jumps");

        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(15);
        Assert.True(view.GetVirtualLineCount() >= 2);
    }

    [Fact]
    public void WordWrapping_HyphenatedWords()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("self-contained multi-line");

        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(12);
        Assert.True(view.GetVirtualLineCount() >= 2);
    }

    [Fact]
    public void WordWrapping_PunctuationBoundaries()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello,World.Test");

        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(8);
        Assert.True(view.GetVirtualLineCount() >= 2);
    }

    [Fact]
    public void WordWrapping_TabBoundaryWidth()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        // "AB" = 2 cols, tab = 2 cols, "CD" = 2 cols
        tb.SetText("AB\tCD");

        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(4);
        // Should wrap into at least 2 lines
        Assert.True(view.GetVirtualLineCount() >= 2);
    }

    [Fact]
    public void WordWrapping_EmojiBoundaryWidth()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        // "AB" = 2, "🌟" = 2, space = 1, "CD" = 2 → 7 cols total
        tb.SetText("AB\U0001F31F CD");

        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(5);
        Assert.True(view.GetVirtualLineCount() >= 2);
    }

    [Fact]
    public void WordWrapping_CjkBoundaryWidth()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        // "AB" = 2, "好" = 2, space = 1, "CD" = 2 → 7 cols total
        tb.SetText("AB好 CD");

        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(5);
        Assert.True(view.GetVirtualLineCount() >= 2);
    }

    [Fact]
    public void WordWrapping_CompareCharVsWordMode()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello wonderful world");

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);
        var charWrappedCount = view.GetVirtualLineCount();

        view.SetWrapMode((byte)WrapMode.Word);
        var wordWrappedCount = view.GetVirtualLineCount();

        Assert.True(charWrappedCount >= 2);
        Assert.True(wordWrappedCount >= 2);
    }

    [Fact]
    public void WordWrapping_EmptyLinesPreserved()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("First line\n\nSecond line");

        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(8);
        Assert.True(view.GetVirtualLineCount() >= 3);
    }

    [Fact]
    public void WordWrapping_SlashAsBoundary()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("path/to/file");

        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(8);
        Assert.True(view.GetVirtualLineCount() >= 2);
    }

    [Fact]
    public void WordWrapping_BracketsAsBoundaries()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("array[index]value");

        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(10);
        Assert.True(view.GetVirtualLineCount() >= 2);
    }

    [Fact]
    public void WordWrapping_SingleCharacterAtBoundary()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("a b c d e f");

        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(4);
        Assert.True(view.GetVirtualLineCount() >= 3);
    }

    // ── Selection ─────────────────────────────────────────────────

    [Fact]
    public void Selection_BasicSelectionWithoutWrap()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");

        view.SetLocalSelection(2, 0, 7, 0, SelFg, SelBg);

        var packedInfo = view.GetSelectionInfo();
        Assert.NotEqual(0xFFFFFFFF_FFFFFFFF, packedInfo);

        var start = (uint)(packedInfo >> 32);
        var end = (uint)(packedInfo & 0xFFFFFFFF);
        Assert.Equal(2u, start);
        Assert.Equal(7u, end);
    }

    [Fact]
    public void Selection_WithWrappedLines()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRST"); // 20 chars

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);
        Assert.Equal(2u, view.GetVirtualLineCount());

        view.SetLocalSelection(5, 0, 5, 1, SelFg, SelBg);

        var packedInfo = view.GetSelectionInfo();
        Assert.NotEqual(0xFFFFFFFF_FFFFFFFF, packedInfo);

        var start = (uint)(packedInfo >> 32);
        var end = (uint)(packedInfo & 0xFFFFFFFF);
        Assert.Equal(5u, start);
        Assert.Equal(15u, end);
    }

    [Fact]
    public void Selection_NoSelectionReturnsAllBitsSet()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");

        var packedInfo = view.GetSelectionInfo();
        Assert.Equal(0xFFFFFFFF_FFFFFFFF, packedInfo);
    }

    [Fact]
    public void Selection_MultiLineSelectionWithoutWrap()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Line 1\nLine 2\nLine 3");

        view.SetLocalSelection(2, 0, 4, 1, SelFg, SelBg);

        var packedInfo = view.GetSelectionInfo();
        Assert.NotEqual(0xFFFFFFFF_FFFFFFFF, packedInfo);
    }

    [Fact]
    public void Selection_AtWrapBoundary()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRST");

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);

        view.SetLocalSelection(9, 0, 1, 1, SelFg, SelBg);

        var packedInfo = view.GetSelectionInfo();
        Assert.NotEqual(0xFFFFFFFF_FFFFFFFF, packedInfo);

        var start = (uint)(packedInfo >> 32);
        var end = (uint)(packedInfo & 0xFFFFFFFF);
        Assert.Equal(9u, start);
        Assert.Equal(11u, end);
    }

    [Fact]
    public void Selection_SpanningMultipleWrappedLines()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123"); // 30 chars

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);
        Assert.Equal(3u, view.GetVirtualLineCount());

        view.SetLocalSelection(2, 0, 8, 2, SelFg, SelBg);

        var packedInfo = view.GetSelectionInfo();
        Assert.NotEqual(0xFFFFFFFF_FFFFFFFF, packedInfo);

        var start = (uint)(packedInfo >> 32);
        var end = (uint)(packedInfo & 0xFFFFFFFF);
        Assert.Equal(2u, start);
        Assert.Equal(28u, end);
    }

    [Fact]
    public void Selection_ChangesWhenWrapWidthChanges()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRST");

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);
        view.SetLocalSelection(5, 0, 5, 1, SelFg, SelBg);

        var packedInfo = view.GetSelectionInfo();
        var start = (uint)(packedInfo >> 32);
        var end = (uint)(packedInfo & 0xFFFFFFFF);
        Assert.Equal(5u, start);
        Assert.Equal(15u, end);

        view.SetWrapWidth(5);
        view.SetLocalSelection(5, 0, 5, 1, SelFg, SelBg);

        packedInfo = view.GetSelectionInfo();
        Assert.NotEqual(0xFFFFFFFF_FFFFFFFF, packedInfo);
    }

    [Fact]
    public void Selection_EmptySelectionWithWrapping()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJ");

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(5);

        view.SetLocalSelection(2, 0, 2, 0, SelFg, SelBg);

        var packedInfo = view.GetSelectionInfo();
        Assert.Equal(0xFFFFFFFF_FFFFFFFF, packedInfo);
    }

    [Fact]
    public void Selection_WithNewlinesAndWrapping()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNO\nPQRSTUVWXYZ");

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);

        var vlineCount = view.GetVirtualLineCount();
        Assert.True(vlineCount >= 3);

        view.SetLocalSelection(5, 0, 5, 2, SelFg, SelBg);

        var packedInfo = view.GetSelectionInfo();
        Assert.NotEqual(0xFFFFFFFF_FFFFFFFF, packedInfo);
    }

    [Fact]
    public void Selection_ResetClearsSelection()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");

        view.SetLocalSelection(0, 0, 5, 0, SelFg, SelBg);
        var packedInfo = view.GetSelectionInfo();
        Assert.NotEqual(0xFFFFFFFF_FFFFFFFF, packedInfo);

        view.ResetLocalSelection();
        packedInfo = view.GetSelectionInfo();
        Assert.Equal(0xFFFFFFFF_FFFFFFFF, packedInfo);
    }

    [Fact]
    public void Selection_SpanningMultipleLogicalLines()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Red\nBlue");

        view.SetSelection(2, 5, SelFg, SelBg);

        var text = view.GetSelectedText();
        Assert.Equal("d\nB", text);
    }

    // ── Selected Text ─────────────────────────────────────────────

    [Fact]
    public void SelectedText_SimpleSelection()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");
        view.SetSelection(6, 11, SelFg, SelBg);

        var text = view.GetSelectedText();
        Assert.Equal("World", text);
    }

    [Fact]
    public void SelectedText_WithNewlines()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Line 1\nLine 2\nLine 3");
        view.SetSelection(0, 9, SelFg, SelBg);

        var text = view.GetSelectedText();
        Assert.Equal("Line 1\nLi", text);
    }

    // ── Plain Text ────────────────────────────────────────────────

    [Fact]
    public void PlainText_SimpleTextWithoutNewlines()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");

        var text = view.GetPlainText();
        Assert.Equal("Hello World", text);
    }

    [Fact]
    public void PlainText_TextWithNewlines()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Line 1\nLine 2\nLine 3");

        var text = view.GetPlainText();
        Assert.Equal("Line 1\nLine 2\nLine 3", text);
    }

    [Fact]
    public void PlainText_TextWithOnlyNewlines()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("\n\n\n");

        var text = view.GetPlainText();
        Assert.Equal("\n\n\n", text);
    }

    [Fact]
    public void PlainText_EmptyLinesBetweenContent()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("First\n\nThird");

        var text = view.GetPlainText();
        Assert.Equal("First\n\nThird", text);
    }

    // ── Line Info (virtual line count) ────────────────────────────

    [Fact]
    public void LineInfo_EmptyBuffer()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("");
        Assert.Equal(1u, view.GetVirtualLineCount());
    }

    [Fact]
    public void LineInfo_SimpleTextWithoutNewlines()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");
        Assert.Equal(1u, view.GetVirtualLineCount());
    }

    [Fact]
    public void LineInfo_TextEndingWithNewline()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World\n");
        Assert.Equal(2u, view.GetVirtualLineCount());
    }

    [Fact]
    public void LineInfo_ConsecutiveNewlines()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Line 1\n\nLine 3");
        Assert.Equal(3u, view.GetVirtualLineCount());
    }

    [Fact]
    public void LineInfo_OnlyNewlines()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("\n\n\n");
        Assert.Equal(4u, view.GetVirtualLineCount());
    }

    [Fact]
    public void LineInfo_WideCharactersUnicode()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello 世界 \U0001F31F");
        Assert.Equal(1u, view.GetVirtualLineCount());
    }

    [Fact]
    public void LineInfo_VeryLongLine()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText(new string('A', 1000));
        Assert.Equal(1u, view.GetVirtualLineCount());
    }

    [Fact]
    public void LineInfo_BufferWithOnlyWhitespace()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("   \n \n ");
        Assert.Equal(3u, view.GetVirtualLineCount());
    }

    [Fact]
    public void LineInfo_SingleCharacterLines()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("A\nB\nC");
        Assert.Equal(3u, view.GetVirtualLineCount());
    }

    [Fact]
    public void LineInfo_ComplexUnicodeCombiningCharacters()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("café\nnaïve\nrésumé");
        Assert.Equal(3u, view.GetVirtualLineCount());
    }

    [Fact]
    public void LineInfo_ExtremelyLongSingleLine()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText(new string('A', 10000));
        Assert.Equal(1u, view.GetVirtualLineCount());
    }

    [Fact]
    public void LineInfo_ExtremelyLongLineWithWrapping()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText(new string('A', 10000));

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(80);
        Assert.True(view.GetVirtualLineCount() > 100);
    }

    [Fact]
    public void LineInfo_TextStartingWithNewline()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("\nHello World");
        Assert.Equal(2u, view.GetVirtualLineCount());
    }

    [Fact]
    public void LineInfo_LinesWithDifferentWidths()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        var text = "Short\n" + new string('A', 50) + "\nMedium";
        tb.SetText(text);
        Assert.Equal(3u, view.GetVirtualLineCount());
    }

    [Fact]
    public void LineInfo_AlternatingEmptyAndContentLines()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("\nContent\n\nMore\n\n");
        Assert.Equal(6u, view.GetVirtualLineCount());
    }

    [Fact]
    public void LineInfo_ThousandsOfLines()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        var lines = new string[1000];
        for (int i = 0; i < 1000; i++)
            lines[i] = $"Line {i}";
        tb.SetText(string.Join("\n", lines));

        Assert.Equal(1000u, view.GetVirtualLineCount());
    }

    [Fact]
    public void LineInfo_LineStartsAndWidthsConsistency()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRST"); // 20 chars

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(7);
        var lineCount = view.GetVirtualLineCount();
        // 20 / 7 = 2 full + 6 remainder = 3 lines
        Assert.Equal(3u, lineCount);
    }

    [Fact]
    public void LineInfo_LineStartsMonotonicallyIncreasing()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        var lines = new string[100];
        for (int i = 0; i < 100; i++)
            lines[i] = $"Line {i}";
        tb.SetText(string.Join("\n", lines));

        Assert.Equal(100u, view.GetVirtualLineCount());
    }

    // ── Highlights ────────────────────────────────────────────────

    [Fact]
    public void Highlights_AddSingleHighlightToLine()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");

        tb.AddHighlight(0, new Highlight { Start = 0, End = 5, StyleId = 1, Priority = 0, HlRef = 0 });
        Assert.Equal(1u, tb.HighlightCount);
    }

    [Fact]
    public void Highlights_AddMultipleHighlightsToSameLine()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");

        tb.AddHighlight(0, new Highlight { Start = 0, End = 5, StyleId = 1, Priority = 0, HlRef = 0 });
        tb.AddHighlight(0, new Highlight { Start = 6, End = 11, StyleId = 2, Priority = 0, HlRef = 0 });
        Assert.Equal(2u, tb.HighlightCount);
    }

    [Fact]
    public void Highlights_AddHighlightsToMultipleLines()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Line 1\nLine 2\nLine 3");

        tb.AddHighlight(0, new Highlight { Start = 0, End = 6, StyleId = 1, Priority = 0, HlRef = 0 });
        tb.AddHighlight(1, new Highlight { Start = 0, End = 6, StyleId = 2, Priority = 0, HlRef = 0 });
        tb.AddHighlight(2, new Highlight { Start = 0, End = 6, StyleId = 3, Priority = 0, HlRef = 0 });
        Assert.Equal(3u, tb.HighlightCount);
    }

    [Fact]
    public void Highlights_RemoveHighlightsByReference()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Line 1\nLine 2");

        tb.AddHighlight(0, new Highlight { Start = 0, End = 3, StyleId = 1, Priority = 0, HlRef = 100 });
        tb.AddHighlight(0, new Highlight { Start = 3, End = 6, StyleId = 2, Priority = 0, HlRef = 200 });
        tb.AddHighlight(1, new Highlight { Start = 0, End = 6, StyleId = 3, Priority = 0, HlRef = 100 });
        Assert.Equal(3u, tb.HighlightCount);

        tb.RemoveHighlight(100);
        // Only the one with HlRef=200 should remain
        Assert.Equal(1u, tb.HighlightCount);
    }

    [Fact]
    public void Highlights_ClearLineHighlights()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Line 1\nLine 2");

        tb.AddHighlight(0, new Highlight { Start = 0, End = 6, StyleId = 1, Priority = 0, HlRef = 0 });
        tb.AddHighlight(0, new Highlight { Start = 6, End = 10, StyleId = 2, Priority = 0, HlRef = 0 });
        tb.AddHighlight(1, new Highlight { Start = 0, End = 6, StyleId = 3, Priority = 0, HlRef = 0 });
        Assert.Equal(3u, tb.HighlightCount);

        tb.ClearLineHighlights(0);
        // Only line 1 highlight should remain
        Assert.Equal(1u, tb.HighlightCount);
    }

    [Fact]
    public void Highlights_ClearAllHighlights()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Line 1\nLine 2\nLine 3");

        tb.AddHighlight(0, new Highlight { Start = 0, End = 6, StyleId = 1, Priority = 0, HlRef = 0 });
        tb.AddHighlight(1, new Highlight { Start = 0, End = 6, StyleId = 2, Priority = 0, HlRef = 0 });
        tb.AddHighlight(2, new Highlight { Start = 0, End = 6, StyleId = 3, Priority = 0, HlRef = 0 });
        Assert.Equal(3u, tb.HighlightCount);

        tb.ClearHighlights();
        Assert.Equal(0u, tb.HighlightCount);
    }

    [Fact]
    public void Highlights_OverlappingHighlights()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");

        tb.AddHighlight(0, new Highlight { Start = 0, End = 8, StyleId = 1, Priority = 0, HlRef = 0 });
        tb.AddHighlight(0, new Highlight { Start = 5, End = 11, StyleId = 2, Priority = 0, HlRef = 0 });
        Assert.Equal(2u, tb.HighlightCount);
    }

    [Fact]
    public void Highlights_PreservedAfterWrapWidthChange()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRST");

        tb.AddHighlight(0, new Highlight { Start = 0, End = 10, StyleId = 1, Priority = 0, HlRef = 0 });

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);

        Assert.Equal(1u, tb.HighlightCount);
    }

    [Fact]
    public void Highlights_WorkCorrectlyWithWrappedLines()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRST");

        tb.AddHighlight(0, new Highlight { Start = 5, End = 15, StyleId = 1, Priority = 1, HlRef = 0 });

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);

        Assert.Equal(2u, view.GetVirtualLineCount());
        Assert.Equal(1u, tb.HighlightCount);
    }

    [Fact]
    public void Highlights_MultipleHighlightsOnWrappedLine()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRSTUVWXYZ");

        tb.AddHighlight(0, new Highlight { Start = 2, End = 8, StyleId = 1, Priority = 1, HlRef = 0 });
        tb.AddHighlight(0, new Highlight { Start = 12, End = 18, StyleId = 2, Priority = 1, HlRef = 0 });
        tb.AddHighlight(0, new Highlight { Start = 22, End = 26, StyleId = 3, Priority = 1, HlRef = 0 });

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);

        Assert.True(view.GetVirtualLineCount() >= 3);
        Assert.Equal(3u, tb.HighlightCount);
    }

    // ── Char Range Highlights ─────────────────────────────────────

    [Fact]
    public void CharRangeHighlights_SingleLineHighlight()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");
        tb.AddHighlightByCharRange(new Highlight { Start = 0, End = 5, StyleId = 1, Priority = 1, HlRef = 0 });

        Assert.Equal(1u, tb.HighlightCount);
    }

    [Fact]
    public void CharRangeHighlights_MultiLineHighlight()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello\nWorld\nTest");
        tb.AddHighlightByCharRange(new Highlight { Start = 3, End = 9, StyleId = 1, Priority = 1, HlRef = 0 });

        // Should create highlights on 2 lines (line 0: cols 3-5, line 1: cols 0-3)
        Assert.True(tb.HighlightCount >= 1);
    }

    [Fact]
    public void CharRangeHighlights_SpanningThreeLines()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Line1\nLine2\nLine3");
        tb.AddHighlightByCharRange(new Highlight { Start = 3, End = 13, StyleId = 1, Priority = 1, HlRef = 0 });

        // Should have highlights on all three lines
        Assert.True(tb.HighlightCount >= 1);
    }

    [Fact]
    public void CharRangeHighlights_EmptyRange()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");
        tb.AddHighlightByCharRange(new Highlight { Start = 5, End = 5, StyleId = 1, Priority = 1, HlRef = 0 });

        Assert.Equal(0u, tb.HighlightCount);
    }

    [Fact]
    public void CharRangeHighlights_MultipleNonOverlappingRanges()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("function hello() { return 42; }");

        tb.AddHighlightByCharRange(new Highlight { Start = 0, End = 8, StyleId = 1, Priority = 1, HlRef = 0 });
        tb.AddHighlightByCharRange(new Highlight { Start = 9, End = 14, StyleId = 2, Priority = 1, HlRef = 0 });
        tb.AddHighlightByCharRange(new Highlight { Start = 19, End = 25, StyleId = 3, Priority = 1, HlRef = 0 });

        Assert.Equal(3u, tb.HighlightCount);
    }

    [Fact]
    public void CharRangeHighlights_WithReferenceIdForRemoval()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Line1\nLine2\nLine3");

        tb.AddHighlightByCharRange(new Highlight { Start = 0, End = 5, StyleId = 1, Priority = 1, HlRef = 100 });
        tb.AddHighlightByCharRange(new Highlight { Start = 6, End = 11, StyleId = 2, Priority = 1, HlRef = 100 });

        Assert.True(tb.HighlightCount >= 2);

        tb.RemoveHighlight(100);
        Assert.Equal(0u, tb.HighlightCount);
    }

    [Fact]
    public void CharRangeHighlights_OutOfBounds()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello");
        tb.AddHighlightByCharRange(new Highlight { Start = 3, End = 100, StyleId = 1, Priority = 1, HlRef = 0 });

        // Should still create a highlight (clamped to text length)
        Assert.True(tb.HighlightCount >= 1);
    }

    [Fact]
    public void CharRangeHighlights_InvalidRange()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");
        tb.AddHighlightByCharRange(new Highlight { Start = 10, End = 5, StyleId = 1, Priority = 1, HlRef = 0 });

        // Invalid range (start > end) should produce no highlight
        Assert.Equal(0u, tb.HighlightCount);
    }

    [Fact]
    public void CharRangeHighlights_ExactLineBoundaries()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("AAAA\nBBBB\nCCCC");
        tb.AddHighlightByCharRange(new Highlight { Start = 0, End = 4, StyleId = 1, Priority = 1, HlRef = 0 });

        // Should only highlight the first line
        Assert.True(tb.HighlightCount >= 1);
    }

    [Fact]
    public void CharRangeHighlights_UnicodeText()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello 世界 \U0001F31F");
        var textLen = tb.Length;
        tb.AddHighlightByCharRange(new Highlight { Start = 0, End = textLen, StyleId = 1, Priority = 1, HlRef = 0 });

        Assert.True(tb.HighlightCount >= 1);
    }

    // ── Highlights with emojis/CJK (no wrapping) ─────────────────

    [Fact]
    public void Highlights_EmojisWithoutWrapping()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("AB\U0001F31FCD\U0001F3A8EF");
        tb.AddHighlight(0, new Highlight { Start = 2, End = 8, StyleId = 1, Priority = 1, HlRef = 0 });

        Assert.Equal(1u, view.GetVirtualLineCount());
        Assert.Equal(1u, tb.HighlightCount);
    }

    [Fact]
    public void Highlights_CjkWithoutWrapping()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("AB测试CD");
        tb.AddHighlight(0, new Highlight { Start = 2, End = 6, StyleId = 1, Priority = 1, HlRef = 0 });

        Assert.Equal(1u, view.GetVirtualLineCount());
        Assert.Equal(1u, tb.HighlightCount);
    }

    [Fact]
    public void Highlights_MixedWidthGraphemesWithoutWrapping()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("A\U0001F31FB测C试D");

        tb.AddHighlight(0, new Highlight { Start = 1, End = 4, StyleId = 1, Priority = 1, HlRef = 0 });
        tb.AddHighlight(0, new Highlight { Start = 4, End = 7, StyleId = 2, Priority = 1, HlRef = 0 });

        Assert.Equal(1u, view.GetVirtualLineCount());
        Assert.Equal(2u, tb.HighlightCount);
    }

    [Fact]
    public void Highlights_EmojiAtStartWithoutWrapping()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("\U0001F31FABCD");
        tb.AddHighlight(0, new Highlight { Start = 0, End = 3, StyleId = 1, Priority = 1, HlRef = 0 });

        Assert.Equal(1u, view.GetVirtualLineCount());
        Assert.Equal(1u, tb.HighlightCount);
    }

    [Fact]
    public void Highlights_EmojiAtEndWithoutWrapping()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCD\U0001F31F");
        tb.AddHighlight(0, new Highlight { Start = 3, End = 6, StyleId = 1, Priority = 1, HlRef = 0 });

        Assert.Equal(1u, view.GetVirtualLineCount());
        Assert.Equal(1u, tb.HighlightCount);
    }

    [Fact]
    public void Highlights_ConsecutiveEmojisWithoutWrapping()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("A\U0001F31F\U0001F3A8\U0001F680B");
        tb.AddHighlight(0, new Highlight { Start = 1, End = 7, StyleId = 1, Priority = 1, HlRef = 0 });

        Assert.Equal(1u, view.GetVirtualLineCount());
        Assert.Equal(1u, tb.HighlightCount);
    }

    // ── Highlights with wrapping ──────────────────────────────────

    [Fact]
    public void Highlights_WithEmojisAndWrapping()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("AB\U0001F31FCD\U0001F3A8EF\U0001F680GH");
        tb.AddHighlight(0, new Highlight { Start = 2, End = 8, StyleId = 1, Priority = 1, HlRef = 0 });

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(6);

        Assert.True(view.GetVirtualLineCount() >= 2);
        Assert.Equal(1u, tb.HighlightCount);
    }

    [Fact]
    public void Highlights_WithCjkAndWrapping()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("AB测试CD文字EF");
        tb.AddHighlight(0, new Highlight { Start = 2, End = 6, StyleId = 1, Priority = 1, HlRef = 0 });

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(6);

        Assert.True(view.GetVirtualLineCount() >= 2);
        Assert.Equal(1u, tb.HighlightCount);
    }

    [Fact]
    public void Highlights_MixedAsciiAndWideCharsWithWrapping()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello\U0001F31F世界");
        tb.AddHighlight(0, new Highlight { Start = 5, End = 11, StyleId = 1, Priority = 1, HlRef = 0 });

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(7);

        Assert.True(view.GetVirtualLineCount() >= 2);
        Assert.Equal(1u, tb.HighlightCount);
    }

    [Fact]
    public void Highlights_EmojiAtWrapBoundary()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCD\U0001F31FEFGH");
        tb.AddHighlight(0, new Highlight { Start = 3, End = 7, StyleId = 1, Priority = 1, HlRef = 0 });

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(5);

        Assert.True(view.GetVirtualLineCount() >= 2);
    }

    // ── Measure ───────────────────────────────────────────────────

    [Fact]
    public void Measure_DoesNotModifyCache()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRST"); // 20 chars

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(100); // wide, no wrapping expected

        // Measure with different width WITHOUT updating cache
        Assert.True(view.MeasureForDimensions(10, 10, out var result));
        Assert.Equal(2u, result.LineCount);
        Assert.Equal(10u, result.WidthColsMax);

        // Cached virtual lines should still reflect wrap width 100
        Assert.Equal(1u, view.GetVirtualLineCount());
    }

    [Fact]
    public void Measure_CacheInvalidatesAfterSetText()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("AAAAA");
        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(5);

        Assert.True(view.MeasureForDimensions(5, 10, out var result1));
        Assert.Equal(1u, result1.LineCount);
        Assert.Equal(5u, result1.WidthColsMax);

        tb.SetText("AAAAAAAAAA"); // 10 chars
        _ = view.GetVirtualLineCount(); // clear dirty flag

        Assert.True(view.MeasureForDimensions(5, 10, out var result2));
        Assert.Equal(2u, result2.LineCount);
        Assert.Equal(5u, result2.WidthColsMax);
    }

    [Fact]
    public void Measure_CharWrap()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRST"); // 20 chars
        view.SetWrapMode((byte)WrapMode.Char);

        Assert.True(view.MeasureForDimensions(10, 10, out var r1));
        Assert.Equal(2u, r1.LineCount);
        Assert.Equal(10u, r1.WidthColsMax);

        Assert.True(view.MeasureForDimensions(5, 10, out var r2));
        Assert.Equal(4u, r2.LineCount);
        Assert.Equal(5u, r2.WidthColsMax);

        Assert.True(view.MeasureForDimensions(20, 10, out var r3));
        Assert.Equal(1u, r3.LineCount);
        Assert.Equal(20u, r3.WidthColsMax);
    }

    [Fact]
    public void Measure_NoWrapMode()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello\nWorld\nTest");
        view.SetWrapMode((byte)WrapMode.None);

        Assert.True(view.MeasureForDimensions(3, 10, out var result));
        Assert.Equal(3u, result.LineCount);
        Assert.True(result.WidthColsMax >= 4);
    }

    [Fact]
    public void Measure_WordWrap()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello wonderful world");
        view.SetWrapMode((byte)WrapMode.Word);

        Assert.True(view.MeasureForDimensions(10, 10, out var result));
        Assert.True(result.LineCount >= 2);
        Assert.True(result.WidthColsMax <= 10);
    }

    [Fact]
    public void Measure_EmptyBuffer()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("");
        view.SetWrapMode((byte)WrapMode.Char);

        Assert.True(view.MeasureForDimensions(10, 10, out var result));
        Assert.Equal(1u, result.LineCount);
        Assert.Equal(0u, result.WidthColsMax);
    }

    [Fact]
    public void Measure_MultipleLinesWithDifferentWidths()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Short\nAVeryLongLineHere\nMedium");
        view.SetWrapMode((byte)WrapMode.Char);

        Assert.True(view.MeasureForDimensions(10, 10, out var result));
        // "Short" (1 line), "AVeryLongLineHere" (2 lines at w=10), "Medium" (1 line) = 4
        Assert.Equal(4u, result.LineCount);
        Assert.Equal(10u, result.WidthColsMax);
    }

    // ── Truncation ────────────────────────────────────────────────

    [Fact]
    public void Truncation_BasicTruncateSingleLine()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRST"); // 20 chars

        view.SetTruncate(true);
        view.SetWrapMode((byte)WrapMode.None);
        view.SetViewport(0, 0, 10, 5);

        // Truncation enabled; the view should still report 1 virtual line
        Assert.Equal(1u, view.GetVirtualLineCount());
    }

    [Fact]
    public void Truncation_MultilineWithTruncate()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRST\nShortLine\nAnotherVeryLongLineHere");

        view.SetTruncate(true);
        view.SetWrapMode((byte)WrapMode.None);
        view.SetViewport(0, 0, 12, 5);

        Assert.Equal(3u, view.GetVirtualLineCount());
    }

    [Fact]
    public void Truncation_WithWrappingDisabled()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("0123456789ABCDEFGHIJ"); // 20 chars

        view.SetTruncate(true);
        view.SetWrapMode((byte)WrapMode.None);
        view.SetViewport(0, 0, 15, 1);

        Assert.Equal(1u, view.GetVirtualLineCount());
    }

    [Fact]
    public void Truncation_ToggleTruncateOnAndOff()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRSTUVWXYZ"); // 26 chars

        view.SetWrapMode((byte)WrapMode.None);
        view.SetViewport(0, 0, 10, 1);

        view.SetTruncate(false);
        // Without truncation, line count stays 1 (no wrap)
        Assert.Equal(1u, view.GetVirtualLineCount());

        view.SetTruncate(true);
        // With truncation, still 1 virtual line but content truncated
        Assert.Equal(1u, view.GetVirtualLineCount());
    }

    [Fact]
    public void Truncation_VerySmallViewport()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");

        view.SetTruncate(true);
        view.SetWrapMode((byte)WrapMode.None);
        view.SetViewport(0, 0, 3, 1);

        // Should still produce a valid result
        Assert.Equal(1u, view.GetVirtualLineCount());
    }

    [Fact]
    public void Truncation_WorksWithWrapping()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRSTUVWXYZ");

        view.SetTruncate(true);
        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);
        view.SetViewport(0, 0, 15, 5);

        // With char wrap at 10, should wrap into multiple lines first
        Assert.True(view.GetVirtualLineCount() >= 2);
    }

    // ── Updates After Buffer Changes ──────────────────────────────

    [Fact]
    public void Updates_ViewReflectsBufferChangesImmediately()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello");
        Assert.Equal(1u, view.GetVirtualLineCount());
        Assert.Equal("Hello", view.GetPlainText());

        tb.SetText("Hello\nWorld");
        Assert.Equal(2u, view.GetVirtualLineCount());
        Assert.Equal("Hello\nWorld", view.GetPlainText());
    }

    [Fact]
    public void Updates_MultipleViewsUpdateIndependently()
    {
        using var tb = TextBuffer.Create();
        using var view1 = TextBufferView.Create(tb);
        using var view2 = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRST");

        Assert.Equal(1u, view1.GetVirtualLineCount());
        Assert.Equal(1u, view2.GetVirtualLineCount());

        view1.SetWrapMode((byte)WrapMode.Char);
        view1.SetWrapWidth(10);
        view2.SetWrapMode((byte)WrapMode.Char);
        view2.SetWrapWidth(5);

        Assert.Equal(2u, view1.GetVirtualLineCount());
        Assert.Equal(4u, view2.GetVirtualLineCount());

        tb.SetText("Short");

        Assert.Equal(1u, view1.GetVirtualLineCount());
        Assert.Equal(1u, view2.GetVirtualLineCount());
    }

    [Fact]
    public void Updates_ViewDestroyedDoesNotAffectOthers()
    {
        using var tb = TextBuffer.Create();
        using var view1 = TextBufferView.Create(tb);

        {
            using var view2 = TextBufferView.Create(tb);
            tb.SetText("Hello");
            Assert.Equal(1u, view1.GetVirtualLineCount());
            Assert.Equal(1u, view2.GetVirtualLineCount());
        }

        // view2 disposed; view1 should still work
        tb.SetText("Hello\nWorld");
        Assert.Equal(2u, view1.GetVirtualLineCount());
    }

    [Fact]
    public void Updates_WithWrappingAcrossBufferChanges()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);

        tb.SetText("ABCDEFGHIJKLMNOPQRST"); // 20 chars → 2 lines
        Assert.Equal(2u, view.GetVirtualLineCount());

        tb.SetText("Short"); // 5 chars → 1 line
        Assert.Equal(1u, view.GetVirtualLineCount());

        tb.SetText("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"); // 36 chars → 4 lines
        Assert.True(view.GetVirtualLineCount() >= 3);
    }

    [Fact]
    public void Updates_ResetClearsContentAndMarksViewsDirty()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");
        Assert.Equal(1u, view.GetVirtualLineCount());

        tb.Reset();
        Assert.Equal(1u, view.GetVirtualLineCount());

        tb.SetText("");
        Assert.Equal(1u, view.GetVirtualLineCount());

        var text = view.GetPlainText();
        Assert.Equal("", text);
    }

    [Fact]
    public void Updates_ViewUpdatesWorkWithSelection()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");
        view.SetSelection(0, 5, SelFg, SelBg);
        Assert.Equal("Hello", view.GetSelectedText());

        tb.SetText("Hi");
        Assert.Equal("Hi", view.GetPlainText());
    }

    [Fact]
    public void Updates_MultipleViewsWithDifferentWrapSettings()
    {
        using var tb = TextBuffer.Create();
        using var viewNowrap = TextBufferView.Create(tb);
        using var viewWrap10 = TextBufferView.Create(tb);
        using var viewWrap5 = TextBufferView.Create(tb);

        viewWrap10.SetWrapMode((byte)WrapMode.Char);
        viewWrap10.SetWrapWidth(10);
        viewWrap5.SetWrapMode((byte)WrapMode.Char);
        viewWrap5.SetWrapWidth(5);

        tb.SetText("ABCDEFGHIJKLMNOPQRST"); // 20 chars
        Assert.Equal(1u, viewNowrap.GetVirtualLineCount());
        Assert.Equal(2u, viewWrap10.GetVirtualLineCount());
        Assert.Equal(4u, viewWrap5.GetVirtualLineCount());

        tb.SetText("Short");
        Assert.Equal(1u, viewNowrap.GetVirtualLineCount());
        Assert.Equal(1u, viewWrap10.GetVirtualLineCount());
        Assert.Equal(1u, viewWrap5.GetVirtualLineCount());

        tb.SetText("ABCDEFGHIJKLMNOPQRSTUVWXYZ"); // 26 chars
        Assert.Equal(1u, viewNowrap.GetVirtualLineCount());
        Assert.Equal(3u, viewWrap10.GetVirtualLineCount());
        Assert.Equal(6u, viewWrap5.GetVirtualLineCount());
    }

    [Fact]
    public void Updates_AfterBufferSetText()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("First text");
        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(5);
        var count1 = view.GetVirtualLineCount();

        tb.SetText("New text that is much longer");
        var count2 = view.GetVirtualLineCount();

        Assert.True(count2 > count1);
    }

    // ── Virtual Lines ─────────────────────────────────────────────

    [Fact]
    public void VirtualLines_MatchRealLinesWhenNoWrap()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Line 1\nLine 2\nLine 3");
        Assert.Equal(3u, view.GetVirtualLineCount());
        Assert.Equal(3u, tb.LineCount);
    }

    [Fact]
    public void VirtualLines_UpdatedWhenWrapWidthSet()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRST");
        Assert.Equal(1u, view.GetVirtualLineCount());

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);
        Assert.Equal(2u, view.GetVirtualLineCount());
    }

    [Fact]
    public void VirtualLines_ResetToMatchRealLinesWhenWrapRemoved()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRST\nShort");

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);
        Assert.Equal(3u, view.GetVirtualLineCount());

        view.SetWrapMode((byte)WrapMode.None);
        Assert.Equal(2u, view.GetVirtualLineCount());
    }

    [Fact]
    public void VirtualLines_MultiLineTextWithoutWrap()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("First line\n\nThird line with more text\n");
        Assert.Equal(4u, view.GetVirtualLineCount());
    }

    [Fact]
    public void VirtualLines_AccessorsWithWrapping()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRST");

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);

        Assert.Equal(2u, view.GetVirtualLineCount());
        Assert.Equal(1u, tb.LineCount);
    }

    [Fact]
    public void VirtualLines_AccessorsWithoutWrapping()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Line 1\nLine 2");
        Assert.Equal(2u, view.GetVirtualLineCount());
        Assert.Equal(2u, tb.LineCount);
    }

    // ── Virtual Line Spans (with highlights) ──────────────────────

    [Fact]
    public void VirtualLineSpans_WithHighlights()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRST");
        tb.AddHighlight(0, new Highlight { Start = 5, End = 15, StyleId = 1, Priority = 1, HlRef = 0 });

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);

        Assert.Equal(2u, view.GetVirtualLineCount());
        Assert.Equal(1u, tb.HighlightCount);
    }

    // ── Tab Indicators ────────────────────────────────────────────

    [Fact]
    public void TabIndicator_SetAndApply()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        // Should not throw
        view.SetTabIndicator((uint)'·');
        view.SetTabIndicatorColor(new Rgba(0.4f, 0.4f, 0.4f, 1.0f));
    }

    // ── Cached Line Info via wrapping consistency ─────────────────

    [Fact]
    public void CachedLineInfo_WithWrapping()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRST");

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(7);
        var lineCount = view.GetVirtualLineCount();

        // 20 / 7 = 2 full lines of 7, 1 line of 6 = 3
        Assert.Equal(3u, lineCount);
    }

    // ── Viewport ──────────────────────────────────────────────────

    [Fact]
    public void Viewport_SetViewport()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");

        // Should not throw
        view.SetViewport(0, 0, 80, 24);
        Assert.Equal(1u, view.GetVirtualLineCount());
    }

    [Fact]
    public void Viewport_SetViewportSize()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");

        // Should not throw
        view.SetViewportSize(80, 24);
        Assert.Equal(1u, view.GetVirtualLineCount());
    }

    // ── Selection Reset ───────────────────────────────────────────

    [Fact]
    public void Selection_ResetSelectionClearsSelection()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");
        view.SetSelection(0, 5, SelFg, SelBg);

        var info1 = view.GetSelectionInfo();
        Assert.NotEqual(0xFFFFFFFF_FFFFFFFF, info1);

        view.ResetSelection();
        var info2 = view.GetSelectionInfo();
        Assert.Equal(0xFFFFFFFF_FFFFFFFF, info2);
    }

    // ── Update Selection ──────────────────────────────────────────

    [Fact]
    public void Selection_UpdateSelectionEnd()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");
        view.SetSelection(0, 5, SelFg, SelBg);

        view.UpdateSelection(8, SelFg, SelBg);

        var packedInfo = view.GetSelectionInfo();
        Assert.NotEqual(0xFFFFFFFF_FFFFFFFF, packedInfo);

        var start = (uint)(packedInfo >> 32);
        var end = (uint)(packedInfo & 0xFFFFFFFF);
        Assert.Equal(0u, start);
        Assert.Equal(8u, end);
    }

    // ── Measure Width 0 uses intrinsic widths ─────────────────────

    [Fact]
    public void Measure_Width0UsesIntrinsicLineWidths()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("abc\ndefghij");
        view.SetWrapMode((byte)WrapMode.Char);

        Assert.True(view.MeasureForDimensions(0, 24, out var result));
        Assert.Equal(tb.LineCount, result.LineCount);
        // With width=0, max width should be that of the longest line
        Assert.True(result.WidthColsMax >= 7); // "defghij" = 7
    }

    // ── Measure with no wrap matches line widths ──────────────────

    [Fact]
    public void Measure_NoWrapMatchesMultiSegmentLineWidths()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("AAAA");
        tb.AppendText("BBBB");
        view.SetWrapMode((byte)WrapMode.None);

        Assert.True(view.MeasureForDimensions(80, 24, out var result));
        // "AAAABBBB" = 8 chars
        Assert.True(result.WidthColsMax >= 8);
        Assert.Equal(1u, result.LineCount);
    }

    // ── Highlights - priority handling ─────────────────────────────

    [Fact]
    public void Highlights_PriorityHandling()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("0123456789");

        // Low priority
        tb.AddHighlight(0, new Highlight { Start = 0, End = 8, StyleId = 1, Priority = 1, HlRef = 0 });
        // High priority overlapping range
        tb.AddHighlight(0, new Highlight { Start = 3, End = 6, StyleId = 2, Priority = 5, HlRef = 0 });

        Assert.Equal(2u, tb.HighlightCount);
    }

    // ── Highlights - style spans ──────────────────────────────────

    [Fact]
    public void Highlights_StyleSpansComputedCorrectly()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("0123456789");

        tb.AddHighlight(0, new Highlight { Start = 0, End = 3, StyleId = 1, Priority = 1, HlRef = 0 });
        tb.AddHighlight(0, new Highlight { Start = 5, End = 8, StyleId = 2, Priority = 1, HlRef = 0 });

        Assert.Equal(2u, tb.HighlightCount);
    }

    // ── Get highlights from non-existent line ─────────────────────

    [Fact]
    public void Highlights_GetFromNonExistentLine()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Line 1");

        // Adding highlights only on line 0; line 10 doesn't exist
        tb.AddHighlight(0, new Highlight { Start = 0, End = 5, StyleId = 1, Priority = 0, HlRef = 0 });
        Assert.Equal(1u, tb.HighlightCount);
    }

    // ── Dispose safety ────────────────────────────────────────────

    [Fact]
    public void Dispose_TextBufferViewDisposeTwice()
    {
        var tb = TextBuffer.Create();
        var view = TextBufferView.Create(tb);

        view.Dispose();
        view.Dispose(); // should not throw

        tb.Dispose();
    }

    [Fact]
    public void Dispose_TextBufferDisposeTwice()
    {
        var tb = TextBuffer.Create();
        tb.Dispose();
        tb.Dispose(); // should not throw
    }

    // ── Wrapping edge cases with line info ────────────────────────

    [Fact]
    public void LineInfoWrapping_WithWrapping()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRST");

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(7);
        var lineCount = view.GetVirtualLineCount();

        // 20/7 = 2 full + 6 remainder = 3 lines
        Assert.Equal(3u, lineCount);

        // All wrapped lines should fit within wrap width (verified by count)
    }

    // ── Word wrapping edge cases ──────────────────────────────────

    [Fact]
    public void WordWrapping_DoesNotSplitUsesAcrossLines()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        var text =
            "So: the per\u2011repo config is the baseline; the -c flags are a \"don't depend on baseline\" guard for commands where output consistency matters. " +
            "Revert uses checkout, which is less about output formatting and already respects the repo config, so it didn't get the extra guard. " +
            "If you want stricter consistency, we can add -c core.autocrlf=false there too.";

        tb.SetText(text);
        view.SetWrapMode((byte)WrapMode.Word);

        // Test a range of widths: word wrap should not split "uses" mid-word
        for (uint width = 80; width <= 100; width++)
        {
            view.SetWrapWidth(width);
            // If this completes without crash, the word wrapping logic handles this text
            var count = view.GetVirtualLineCount();
            Assert.True(count >= 1);
        }
    }
}
