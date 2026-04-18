using OpenTui.Core;
using Xunit;

namespace OpenTui.Core.Tests;

/// <summary>
/// C# equivalents of the Zig text-buffer-selection_test.zig tests.
/// Exercises selection set/get, local selection, wrapping interactions,
/// grapheme boundary snapping, update/extend, and clear behavior.
/// </summary>
public class TextBufferSelectionTests
{
    // Zig tests pass null for selection colors; C# API requires a value.
    private static readonly Rgba SelFg = Rgba.White;
    private static readonly Rgba SelBg = Rgba.Black;

    private const ulong NoSelection = 0xFFFFFFFF_FFFFFFFF;

    private static uint PackedStart(ulong packed) => (uint)(packed >> 32);
    private static uint PackedEnd(ulong packed) => (uint)(packed & 0xFFFFFFFF);

    // ── Basic selection without wrap ──────────────────────────────

    [Fact]
    public void Selection_BasicSelectionWithoutWrap()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");

        view.SetLocalSelection(2, 0, 7, 0, SelFg, SelBg);

        var packed = view.GetSelectionInfo();
        Assert.NotEqual(NoSelection, packed);

        Assert.Equal(2u, PackedStart(packed));
        Assert.Equal(7u, PackedEnd(packed));
    }

    // ── With wrapped lines ───────────────────────────────────────

    [Fact]
    public void Selection_WithWrappedLines()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRST");

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);

        Assert.Equal(2u, view.GetVirtualLineCount());

        view.SetLocalSelection(5, 0, 5, 1, SelFg, SelBg);

        var packed = view.GetSelectionInfo();
        Assert.NotEqual(NoSelection, packed);

        Assert.Equal(5u, PackedStart(packed));
        Assert.Equal(15u, PackedEnd(packed));
    }

    // ── No selection returns all bits set ─────────────────────────

    [Fact]
    public void Selection_NoSelectionReturnsAllBitsSet()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");

        Assert.Equal(NoSelection, view.GetSelectionInfo());
    }

    // ── With newline characters ──────────────────────────────────

    [Fact]
    public void Selection_WithNewlineCharacters()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Line 1\nLine 2\nLine 3");

        view.SetLocalSelection(2, 1, 4, 2, SelFg, SelBg);

        var packed = view.GetSelectionInfo();
        Assert.NotEqual(NoSelection, packed);

        var text = view.GetSelectedText();
        Assert.Contains("ne 2", text);
        Assert.Contains("\n", text);
    }

    // ── Across empty lines ───────────────────────────────────────

    [Fact]
    public void Selection_AcrossEmptyLines()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Line 1\nLine 2\n\nLine 4");

        view.SetLocalSelection(0, 0, 2, 2, SelFg, SelBg);

        var packed = view.GetSelectionInfo();
        Assert.NotEqual(NoSelection, packed);

        Assert.Equal(0u, PackedStart(packed));
        Assert.Equal(14u, PackedEnd(packed));
    }

    // ── Ending in empty line ─────────────────────────────────────

    [Fact]
    public void Selection_EndingInEmptyLine()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Line 1\n\nLine 3");

        view.SetLocalSelection(0, 0, 3, 1, SelFg, SelBg);

        var packed = view.GetSelectionInfo();
        Assert.NotEqual(NoSelection, packed);

        Assert.Equal(0u, PackedStart(packed));
    }

    // ── Spanning multiple lines completely ────────────────────────

    [Fact]
    public void Selection_SpanningMultipleLinesCompletely()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("First\nSecond\nThird");

        view.SetLocalSelection(0, 1, 6, 1, SelFg, SelBg);

        var text = view.GetSelectedText();
        Assert.Equal("Second", text);
    }

    // ── Including multiple line breaks ───────────────────────────

    [Fact]
    public void Selection_IncludingMultipleLineBreaks()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("A\nB\nC\nD");

        view.SetLocalSelection(0, 1, 1, 2, SelFg, SelBg);

        var text = view.GetSelectedText();
        Assert.Contains("\n", text);
        Assert.Contains("B", text);
        Assert.Contains("C", text);
    }

    // ── At line boundaries ───────────────────────────────────────

    [Fact]
    public void Selection_AtLineBoundaries()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Line1\nLine2\nLine3");

        view.SetLocalSelection(4, 0, 2, 1, SelFg, SelBg);

        var text = view.GetSelectedText();
        Assert.Contains("1", text);
        Assert.Contains("\n", text);
        Assert.Contains("Li", text);
    }

    // ── Empty text ───────────────────────────────────────────────

    [Fact]
    public void Selection_EmptyText()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("");

        view.SetLocalSelection(0, 0, 0, 0, SelFg, SelBg);

        Assert.Equal(NoSelection, view.GetSelectionInfo());
    }

    // ── Single character ─────────────────────────────────────────

    [Fact]
    public void Selection_SingleCharacter()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("A");

        view.SetLocalSelection(0, 0, 1, 0, SelFg, SelBg);

        var packed = view.GetSelectionInfo();
        Assert.NotEqual(NoSelection, packed);

        Assert.Equal(0u, PackedStart(packed));
        Assert.Equal(1u, PackedEnd(packed));

        Assert.Equal("A", view.GetSelectedText());
    }

    // ── Zero-width selection ─────────────────────────────────────

    [Fact]
    public void Selection_ZeroWidthSelection()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");

        view.SetLocalSelection(5, 0, 5, 0, SelFg, SelBg);

        Assert.Equal(NoSelection, view.GetSelectionInfo());
    }

    // ── Beyond text bounds ───────────────────────────────────────

    [Fact]
    public void Selection_BeyondTextBounds()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hi");

        view.SetLocalSelection(0, 0, 10, 0, SelFg, SelBg);

        var packed = view.GetSelectionInfo();
        Assert.NotEqual(NoSelection, packed);

        Assert.Equal(0u, PackedStart(packed));
        Assert.Equal(2u, PackedEnd(packed));
    }

    // ── Clear selection ──────────────────────────────────────────

    [Fact]
    public void Selection_ClearSelection()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");

        view.SetLocalSelection(0, 0, 5, 0, SelFg, SelBg);
        Assert.NotEqual(NoSelection, view.GetSelectionInfo());

        view.ResetLocalSelection();
        Assert.Equal(NoSelection, view.GetSelectionInfo());
    }

    // ── At wrap boundary ─────────────────────────────────────────

    [Fact]
    public void Selection_AtWrapBoundary()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRST");

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);

        view.SetLocalSelection(9, 0, 1, 1, SelFg, SelBg);

        var packed = view.GetSelectionInfo();
        Assert.NotEqual(NoSelection, packed);

        Assert.Equal(9u, PackedStart(packed));
        Assert.Equal(11u, PackedEnd(packed));
    }

    // ── Spanning multiple wrapped lines ──────────────────────────

    [Fact]
    public void Selection_SpanningMultipleWrappedLines()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123");

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);
        Assert.Equal(3u, view.GetVirtualLineCount());

        view.SetLocalSelection(2, 0, 8, 2, SelFg, SelBg);

        var packed = view.GetSelectionInfo();
        Assert.NotEqual(NoSelection, packed);

        Assert.Equal(2u, PackedStart(packed));
        Assert.Equal(28u, PackedEnd(packed));
    }

    // ── Changes when wrap width changes ──────────────────────────

    [Fact]
    public void Selection_ChangesWhenWrapWidthChanges()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRST");

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);
        view.SetLocalSelection(5, 0, 5, 1, SelFg, SelBg);

        var packed = view.GetSelectionInfo();
        Assert.Equal(5u, PackedStart(packed));
        Assert.Equal(15u, PackedEnd(packed));

        view.SetWrapWidth(5);
        view.SetLocalSelection(5, 0, 5, 1, SelFg, SelBg);

        packed = view.GetSelectionInfo();
        Assert.NotEqual(NoSelection, packed);
    }

    // ── With newlines and wrapping ───────────────────────────────

    [Fact]
    public void Selection_WithNewlinesAndWrapping()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNO\nPQRSTUVWXYZ");

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);

        Assert.True(view.GetVirtualLineCount() >= 3);

        view.SetLocalSelection(5, 0, 5, 2, SelFg, SelBg);

        var packed = view.GetSelectionInfo();
        Assert.NotEqual(NoSelection, packed);
    }

    // ── getSelectedText simple ───────────────────────────────────

    [Fact]
    public void Selection_GetSelectedTextSimple()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");
        view.SetSelection(6, 11, SelFg, SelBg);

        Assert.Equal("World", view.GetSelectedText());
    }

    // ── getSelectedText with newlines ────────────────────────────

    [Fact]
    public void Selection_GetSelectedTextWithNewlines()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Line 1\nLine 2\nLine 3");
        view.SetSelection(0, 9, SelFg, SelBg);

        Assert.Equal("Line 1\nLi", view.GetSelectedText());
    }

    // ── Spanning multiple lines with getSelectedText ─────────────

    [Fact]
    public void Selection_SpanningMultipleLinesGetSelectedText()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Red\nBlue");
        view.SetSelection(2, 5, SelFg, SelBg);

        Assert.Equal("d\nB", view.GetSelectedText());
    }

    // ── With graphemes ───────────────────────────────────────────

    [Fact]
    public void Selection_WithGraphemes()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello \U0001F30D World");
        view.SetSelection(0, 8, SelFg, SelBg);

        var text = view.GetSelectedText();
        Assert.Contains("Hello", text);
        Assert.Contains("\U0001F30D", text);
    }

    // ── Wide emoji at boundary ───────────────────────────────────

    [Fact]
    public void Selection_WideEmojiAtBoundary()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello \U0001F30D World");
        view.SetSelection(0, 7, SelFg, SelBg);

        Assert.Equal("Hello \U0001F30D", view.GetSelectedText());
    }

    // ── Wide emoji BEFORE selection start should be excluded ─────

    [Fact]
    public void Selection_WideEmojiBeforeSelectionStartExcluded()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello \U0001F30D World");
        view.SetSelection(7, 10, SelFg, SelBg);

        Assert.Equal("\U0001F30D W", view.GetSelectedText());
    }

    // ── Start at second cell of width=2 grapheme snaps backward ──

    [Fact]
    public void Selection_StartAtSecondCellSnapsBackward()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("AB\U0001F30DCD");
        view.SetSelection(3, 5, SelFg, SelBg);

        Assert.Equal("\U0001F30DC", view.GetSelectedText());
    }

    // ── End at first cell of width=2 grapheme snaps forward ──────

    [Fact]
    public void Selection_EndAtFirstCellSnapsForward()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("AB\U0001F30DCD");
        view.SetSelection(1, 3, SelFg, SelBg);

        Assert.Equal("B\U0001F30D", view.GetSelectedText());
    }

    // ── Both boundaries at cells of width=2 graphemes ────────────

    [Fact]
    public void Selection_BothBoundariesAtWidth2Graphemes()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("A\U0001F30DB\U0001F30EC");
        view.SetSelection(2, 5, SelFg, SelBg);

        Assert.Equal("\U0001F30DB\U0001F30E", view.GetSelectedText());
    }

    // ── updateSelection extends existing selection ───────────────

    [Fact]
    public void Selection_UpdateSelectionExtendsExisting()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");

        view.SetSelection(0, 5, SelFg, SelBg);

        var packed = view.GetSelectionInfo();
        Assert.Equal(0u, PackedStart(packed));
        Assert.Equal(5u, PackedEnd(packed));

        view.UpdateSelection(11, SelFg, SelBg);

        packed = view.GetSelectionInfo();
        Assert.Equal(0u, PackedStart(packed));
        Assert.Equal(11u, PackedEnd(packed));

        Assert.Equal("Hello World", view.GetSelectedText());
    }

    // ── updateSelection with no existing selection does nothing ──

    [Fact]
    public void Selection_UpdateSelectionNoExistingDoesNothing()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");

        Assert.Equal(NoSelection, view.GetSelectionInfo());

        view.UpdateSelection(5, SelFg, SelBg);

        Assert.Equal(NoSelection, view.GetSelectionInfo());
    }

    // ── updateSelection can shrink selection ─────────────────────

    [Fact]
    public void Selection_UpdateSelectionCanShrink()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");

        view.SetSelection(0, 11, SelFg, SelBg);

        view.UpdateSelection(5, SelFg, SelBg);

        var packed = view.GetSelectionInfo();
        Assert.Equal(0u, PackedStart(packed));
        Assert.Equal(5u, PackedEnd(packed));

        Assert.Equal("Hello", view.GetSelectedText());
    }

    // ── updateLocalSelection extends focus position ──────────────

    [Fact]
    public void Selection_UpdateLocalSelectionExtendsFocus()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");

        view.SetLocalSelection(0, 0, 5, 0, SelFg, SelBg);

        var packed = view.GetSelectionInfo();
        Assert.Equal(0u, PackedStart(packed));
        Assert.Equal(5u, PackedEnd(packed));

        var changed = view.UpdateLocalSelection(0, 0, 11, 0, SelFg, SelBg);
        Assert.True(changed);

        packed = view.GetSelectionInfo();
        Assert.Equal(0u, PackedStart(packed));
        Assert.Equal(11u, PackedEnd(packed));

        Assert.Equal("Hello World", view.GetSelectedText());
    }

    // ── updateLocalSelection with no existing selection falls back to setLocalSelection ──

    [Fact]
    public void Selection_UpdateLocalSelectionNoExistingFallsBack()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");

        var changed = view.UpdateLocalSelection(0, 0, 5, 0, SelFg, SelBg);
        Assert.True(changed);

        var packed = view.GetSelectionInfo();
        Assert.Equal(0u, PackedStart(packed));
        Assert.Equal(5u, PackedEnd(packed));
    }

    // ── updateLocalSelection can shrink selection ────────────────

    [Fact]
    public void Selection_UpdateLocalSelectionCanShrink()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");

        view.SetLocalSelection(0, 0, 11, 0, SelFg, SelBg);

        var changed = view.UpdateLocalSelection(0, 0, 5, 0, SelFg, SelBg);
        Assert.True(changed);

        var packed = view.GetSelectionInfo();
        Assert.Equal(0u, PackedStart(packed));
        Assert.Equal(5u, PackedEnd(packed));

        Assert.Equal("Hello", view.GetSelectedText());
    }

    // ── updateLocalSelection across multiple lines ───────────────

    [Fact]
    public void Selection_UpdateLocalSelectionAcrossMultipleLines()
    {
        using var tb = TextBuffer.Create();
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

    // ── updateLocalSelection backward selection ──────────────────

    [Fact]
    public void Selection_UpdateLocalSelectionBackward()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World!");

        view.SetLocalSelection(11, 0, 11, 0, SelFg, SelBg);

        var changed = view.UpdateLocalSelection(11, 0, 6, 0, SelFg, SelBg);
        Assert.True(changed);

        var packed = view.GetSelectionInfo();
        Assert.Equal(6u, PackedStart(packed));
        Assert.Equal(12u, PackedEnd(packed));

        Assert.Equal("World!", view.GetSelectedText());
    }

    // ── updateLocalSelection with wrapped lines ──────────────────

    [Fact]
    public void Selection_UpdateLocalSelectionWithWrappedLines()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRST");

        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);

        Assert.Equal(2u, view.GetVirtualLineCount());

        view.SetLocalSelection(0, 0, 0, 0, SelFg, SelBg);

        var changed = view.UpdateLocalSelection(0, 0, 5, 1, SelFg, SelBg);
        Assert.True(changed);

        var packed = view.GetSelectionInfo();
        Assert.Equal(0u, PackedStart(packed));
        Assert.Equal(15u, PackedEnd(packed));

        Assert.Equal("ABCDEFGHIJKLMNO", view.GetSelectedText());
    }

    // ── updateLocalSelection with same focus maintains selection ──

    [Fact]
    public void Selection_UpdateLocalSelectionSameFocusMaintains()
    {
        using var tb = TextBuffer.Create();
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");

        view.SetLocalSelection(0, 0, 5, 0, SelFg, SelBg);

        view.UpdateLocalSelection(0, 0, 5, 0, SelFg, SelBg);

        var packed = view.GetSelectionInfo();
        Assert.Equal(0u, PackedStart(packed));
        Assert.Equal(5u, PackedEnd(packed));
    }

    // ── updateLocalSelection preserves anchor correctly ──────────

    [Fact]
    public void Selection_UpdateLocalSelectionPreservesAnchor()
    {
        using var tb = TextBuffer.Create();
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
}
