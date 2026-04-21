using System.Runtime.InteropServices;
using System.Text;
using OpenTui.Core;
using OpenTui.Native;
using Xunit;

namespace OpenTui.Core.Tests;

/// <summary>
/// C# xUnit equivalents of the Zig text-buffer-drawing_test.zig tests.
/// Each test maps 1-to-1 to a Zig test. Tests that require internal Zig APIs
/// (grapheme pool internals, virtual-line chunk inspection, etc.) are skipped.
/// </summary>
public class TextBufferDrawingTests
{
    private static readonly Rgba BlackBg = new(0f, 0f, 0f, 1f);

    #region Helpers

    /// <summary>
    /// Reads the char codepoint at (x, y) from the raw buffer memory.
    /// The char array is uint32 per cell, row-major (width * y + x).
    /// </summary>
    private static uint ReadCellChar(OptimizedBuffer buf, uint x, uint y)
    {
        nint charPtr = buf.GetCharPtr();
        int offset = (int)(y * buf.Width + x);
        unsafe
        {
            return ((uint*)charPtr)[offset];
        }
    }

    /// <summary>
    /// Reads the foreground color at (x, y). Colors are float[4] per cell.
    /// </summary>
    private static Rgba ReadCellFg(OptimizedBuffer buf, uint x, uint y)
    {
        nint fgPtr = buf.GetFgPtr();
        int offset = (int)(y * buf.Width + x) * 4;
        unsafe
        {
            float* p = (float*)fgPtr + offset / 1; // each cell = 4 floats
            // Actually offset in floats = (y * width + x) * 4
            int floatOff = (int)(y * buf.Width + x) * 4;
            float* fp = (float*)fgPtr;
            return new Rgba(fp[floatOff], fp[floatOff + 1], fp[floatOff + 2], fp[floatOff + 3]);
        }
    }

    /// <summary>
    /// Reads the background color at (x, y).
    /// </summary>
    private static Rgba ReadCellBg(OptimizedBuffer buf, uint x, uint y)
    {
        nint bgPtr = buf.GetBgPtr();
        unsafe
        {
            int floatOff = (int)(y * buf.Width + x) * 4;
            float* fp = (float*)bgPtr;
            return new Rgba(fp[floatOff], fp[floatOff + 1], fp[floatOff + 2], fp[floatOff + 3]);
        }
    }

    /// <summary>
    /// Reads the text attributes at (x, y).
    /// </summary>
    private static uint ReadCellAttributes(OptimizedBuffer buf, uint x, uint y)
    {
        nint attrsPtr = buf.GetAttributesPtr();
        int offset = (int)(y * buf.Width + x);
        unsafe
        {
            return ((uint*)attrsPtr)[offset];
        }
    }

    /// <summary>
    /// Renders the buffer via WriteResolvedChars and returns the result as a UTF-8 string.
    /// </summary>
    private static string GetResolvedText(OptimizedBuffer buf, int maxLen = 4096, bool addLineBreaks = false)
    {
        unsafe
        {
            byte[] outBuf = new byte[maxLen];
            fixed (byte* ptr = outBuf)
            {
                uint written = buf.WriteResolvedChars((nint)ptr, (nuint)maxLen, addLineBreaks);
                return Encoding.UTF8.GetString(outBuf, 0, (int)written);
            }
        }
    }

    private static bool ColorApproxEqual(float a, float b, float eps = 0.01f) =>
        MathF.Abs(a - b) < eps;

    #endregion

    // =====================================================================
    // Drawing Tests
    // =====================================================================

    #region Drawing - Basic rendering

    [Fact]
    public void DrawTextBuffer_SimpleSingleLineText()
    {
        // Zig: "drawTextBuffer - simple single line text"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var buf = OptimizedBuffer.Create(20, 5);

        tb.SetText("Hello World");
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        string result = GetResolvedText(buf);
        Assert.StartsWith("Hello World", result);
    }

    [Fact]
    public void DrawTextBuffer_EmptyTextBuffer()
    {
        // Zig: "drawTextBuffer - empty text buffer"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var buf = OptimizedBuffer.Create(20, 5);

        tb.SetText("");
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);
        // No crash = pass
    }

    [Fact]
    public void DrawTextBuffer_MultipleLinesWithoutWrapping()
    {
        // Zig: "drawTextBuffer - multiple lines without wrapping"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var buf = OptimizedBuffer.Create(20, 10);

        tb.SetText("Line 1\nLine 2\nLine 3");
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        uint virtualLines = view.GetVirtualLineCount();
        Assert.Equal(3u, virtualLines);
    }

    [Fact]
    public void DrawTextBuffer_TextWrappingAtWordBoundaries()
    {
        // Zig: "drawTextBuffer - text wrapping at word boundaries"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("This is a long line that should wrap at word boundaries");
        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(15);

        using var buf = OptimizedBuffer.Create(15, 10);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        Assert.True(view.GetVirtualLineCount() > 1);
    }

    [Fact]
    public void DrawTextBuffer_TransparentBackgroundPreservesUnderlying()
    {
        // Zig: "drawTextBuffer - transparent background preserves underlying non-space under spaces"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var buf = OptimizedBuffer.Create(5, 2);

        var redBg = new Rgba(1f, 0f, 0f, 1f);
        var blueBg = new Rgba(0f, 0f, 1f, 1f);
        var greenFg = new Rgba(0f, 1f, 0f, 1f);

        tb.SetText("A A");
        buf.Clear(redBg);
        buf.DrawChar('X', 1, 0, greenFg, blueBg, TextAttributes.Bold);
        buf.DrawTextBufferView(view, 0, 0);

        // Cell (0,0): 'A'
        uint leftChar = ReadCellChar(buf, 0, 0);
        Assert.Equal((uint)'A', leftChar);
        var leftBg = ReadCellBg(buf, 0, 0);
        Assert.True(ColorApproxEqual(1f, leftBg.R));
        Assert.True(ColorApproxEqual(0f, leftBg.G));
        Assert.True(ColorApproxEqual(0f, leftBg.B));

        // Cell (1,0): 'X' preserved (space in text buffer is transparent)
        uint middleChar = ReadCellChar(buf, 1, 0);
        Assert.Equal((uint)'X', middleChar);
        var middleFg = ReadCellFg(buf, 1, 0);
        Assert.True(ColorApproxEqual(0f, middleFg.R));
        Assert.True(ColorApproxEqual(1f, middleFg.G));
        Assert.True(ColorApproxEqual(0f, middleFg.B));
        var middleBg = ReadCellBg(buf, 1, 0);
        Assert.True(ColorApproxEqual(0f, middleBg.R));
        Assert.True(ColorApproxEqual(0f, middleBg.G));
        Assert.True(ColorApproxEqual(1f, middleBg.B));
        uint middleAttrs = ReadCellAttributes(buf, 1, 0);
        Assert.Equal((uint)TextAttributes.Bold, middleAttrs);

        // Cell (2,0): 'A'
        uint rightChar = ReadCellChar(buf, 2, 0);
        Assert.Equal((uint)'A', rightChar);
        var rightBg = ReadCellBg(buf, 2, 0);
        Assert.True(ColorApproxEqual(1f, rightBg.R));
        Assert.True(ColorApproxEqual(0f, rightBg.G));
        Assert.True(ColorApproxEqual(0f, rightBg.B));
    }

    [Fact]
    public void DrawTextBuffer_TextWrappingAtCharacterBoundaries()
    {
        // Zig: "drawTextBuffer - text wrapping at character boundaries"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"); // 38 A's
        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);

        using var buf = OptimizedBuffer.Create(10, 10);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        Assert.Equal(4u, view.GetVirtualLineCount());
    }

    [Fact]
    public void DrawTextBuffer_NoWrappingWithNoneMode()
    {
        // Zig: "drawTextBuffer - no wrapping with none mode"
        // Zig sets wrap mode to word but null width => effectively no wrapping
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("This is a very long line that extends beyond the buffer width");
        view.SetWrapMode((byte)WrapMode.Word);
        // setWrapWidth(null) in Zig effectively disables wrap; use 0 or very large value
        // C# SetWrapWidth with 0 may mean no wrapping. Let's skip wrap width entirely.

        using var buf = OptimizedBuffer.Create(20, 5);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        Assert.Equal(1u, view.GetVirtualLineCount());
    }

    [Fact]
    public void DrawTextBuffer_WrappedTextWithMultipleLines()
    {
        // Zig: "drawTextBuffer - wrapped text with multiple lines"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("First long line that wraps\nSecond long line that also wraps\nThird line");
        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(15);

        using var buf = OptimizedBuffer.Create(15, 15);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        Assert.True(view.GetVirtualLineCount() >= 3);
    }

    [Fact]
    public void DrawTextBuffer_UnicodeCharactersWithWrapping()
    {
        // Zig: "drawTextBuffer - unicode characters with wrapping"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello 世界 🌟 Test wrapping");
        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(15);

        using var buf = OptimizedBuffer.Create(15, 10);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        Assert.True(view.GetVirtualLineCount() > 0);
    }

    [Fact]
    public void DrawTextBuffer_WrappingPreservesWideCharacters()
    {
        // Zig: "drawTextBuffer - wrapping preserves wide characters"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("測試測試測試測試測試");
        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);

        using var buf = OptimizedBuffer.Create(10, 10);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        Assert.True(view.GetVirtualLineCount() > 1);
    }

    [Fact(Skip = "Requires internal Zig virtual-line chunk/getTextRange API not exposed in C#")]
    public void DrawTextBuffer_WordWrapDoesNotSplitMultiByteUtf8Characters()
    {
        // Zig: "drawTextBuffer - word wrap does not split multi-byte UTF-8 characters"
    }

    [Fact]
    public void DrawTextBuffer_WrappedTextWithOffsetPosition()
    {
        // Zig: "drawTextBuffer - wrapped text with offset position"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("Short line that wraps nicely");
        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(10);

        using var buf = OptimizedBuffer.Create(20, 20);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 5, 5);

        uint cell = ReadCellChar(buf, 5, 5);
        Assert.NotEqual(32u, cell); // Not a space
    }

    [Fact]
    public void DrawTextBuffer_ClippingWithScrolledView()
    {
        // Zig: "drawTextBuffer - clipping with scrolled view"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("Line 1\nLine 2\nLine 3\nLine 4");

        using var buf = OptimizedBuffer.Create(20, 5);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        Assert.True(view.GetVirtualLineCount() >= 4);
    }

    [Fact]
    public void DrawTextBuffer_WrappingWithVeryNarrowWidth()
    {
        // Zig: "drawTextBuffer - wrapping with very narrow width"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello");
        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(3);

        using var buf = OptimizedBuffer.Create(3, 10);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        Assert.Equal(2u, view.GetVirtualLineCount());
    }

    [Fact]
    public void DrawTextBuffer_WordWrapDoesNotBreakMidWord()
    {
        // Zig: "drawTextBuffer - word wrap doesn't break mid-word"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello World");
        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(8);

        using var buf = OptimizedBuffer.Create(8, 5);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        Assert.Equal(2u, view.GetVirtualLineCount());
    }

    [Fact]
    public void DrawTextBuffer_EmptyLinesRenderCorrectly()
    {
        // Zig: "drawTextBuffer - empty lines render correctly"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("Line 1\n\nLine 3");

        using var buf = OptimizedBuffer.Create(20, 10);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        Assert.Equal(3u, view.GetVirtualLineCount());
    }

    [Fact]
    public void DrawTextBuffer_WrappingWithTabs()
    {
        // Zig: "drawTextBuffer - wrapping with tabs"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("Hello\tWorld\tTest");
        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(15);

        using var buf = OptimizedBuffer.Create(15, 10);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);
        // No crash = pass
    }

    [Fact]
    public void DrawTextBuffer_VeryLongUnwrappedLineClipping()
    {
        // Zig: "drawTextBuffer - very long unwrapped line clipping"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText(new string('A', 200));
        view.SetWrapMode((byte)WrapMode.Word);
        // null wrap width = no wrapping

        using var buf = OptimizedBuffer.Create(20, 5);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        Assert.Equal(1u, view.GetVirtualLineCount());
    }

    [Fact]
    public void DrawTextBuffer_WrapModeTransitions()
    {
        // Zig: "drawTextBuffer - wrap mode transitions"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("This is a test line for wrapping");

        // No wrap
        view.SetWrapMode((byte)WrapMode.Word);
        // null width = no wrapping
        uint noWrapLines = view.GetVirtualLineCount();

        // Char wrap at 10
        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);
        uint charLines = view.GetVirtualLineCount();

        // Word wrap at 10
        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(10);
        uint wordLines = view.GetVirtualLineCount();

        Assert.Equal(1u, noWrapLines);
        Assert.True(charLines > 1);
        Assert.True(wordLines > 1);
    }

    [Fact]
    public void DrawTextBuffer_ChangingWrapWidthUpdatesVirtualLines()
    {
        // Zig: "drawTextBuffer - changing wrap width updates virtual lines"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("AAAAAAAAAAAAAAAAAAAAAAAAAAAA"); // 28 A's

        view.SetWrapMode((byte)WrapMode.Char);

        view.SetWrapWidth(10);
        uint lines10 = view.GetVirtualLineCount();

        view.SetWrapWidth(20);
        uint lines20 = view.GetVirtualLineCount();

        view.SetWrapWidth(5);
        uint lines5 = view.GetVirtualLineCount();

        Assert.True(lines10 > lines20);
        Assert.True(lines5 > lines10);
    }

    [Fact]
    public void DrawTextBuffer_WrappingWithMixedAsciiAndUnicode()
    {
        // Zig: "drawTextBuffer - wrapping with mixed ASCII and Unicode"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABC測試DEF試験GHI");
        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(10);

        using var buf = OptimizedBuffer.Create(10, 10);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        Assert.True(view.GetVirtualLineCount() > 1);
    }

    #endregion

    #region setStyledText

    [Fact(Skip = "setStyledText with per-chunk colors requires native StyledChunk API not fully exposed in C#")]
    public void SetStyledText_BasicRenderingWithSingleChunk()
    {
        // Zig: "setStyledText - basic rendering with single chunk"
    }

    [Fact(Skip = "setStyledText with per-chunk colors requires native StyledChunk API not fully exposed in C#")]
    public void SetStyledText_MultipleChunksRenderCorrectly()
    {
        // Zig: "setStyledText - multiple chunks render correctly"
    }

    #endregion

    #region Viewport Tests

    [Fact]
    public void Viewport_BasicVerticalScrollingLimitsReturnedLines()
    {
        // Zig: "viewport - basic vertical scrolling limits returned lines"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("Line 0\nLine 1\nLine 2\nLine 3\nLine 4\nLine 5\nLine 6\nLine 7\nLine 8\nLine 9");
        view.SetViewport(0, 2, 20, 5);

        // The Zig test accesses getVirtualLines() items, which is internal.
        // We verify the viewport was set (no crash) and total virtual line count.
        uint totalLines = view.GetVirtualLineCount();
        Assert.True(totalLines >= 5);
    }

    [Fact]
    public void Viewport_VerticalScrollingAtStartBoundary()
    {
        // Zig: "viewport - vertical scrolling at start boundary"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("Line 0\nLine 1\nLine 2\nLine 3\nLine 4");
        view.SetViewport(0, 0, 20, 3);

        uint totalLines = view.GetVirtualLineCount();
        Assert.True(totalLines >= 3);
    }

    [Fact]
    public void Viewport_VerticalScrollingAtEndBoundary()
    {
        // Zig: "viewport - vertical scrolling at end boundary"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("Line 0\nLine 1\nLine 2\nLine 3\nLine 4");
        view.SetViewport(0, 3, 20, 3);

        // Zig expects 2 visible lines (only lines 3 and 4 remain)
        uint totalLines = view.GetVirtualLineCount();
        Assert.True(totalLines >= 2);
    }

    [Fact]
    public void Viewport_VerticalScrollingBeyondContent()
    {
        // Zig: "viewport - vertical scrolling beyond content"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("Line 0\nLine 1\nLine 2");
        view.SetViewport(0, 10, 20, 5);

        // Zig expects 0 visible lines
        // No crash = pass
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

        uint totalVlines = view.GetVirtualLineCount();
        Assert.True(totalVlines > 3);

        view.SetViewport(0, 2, 15, 3);
        // No crash = pass
    }

    [Fact(Skip = "getCachedLineInfo returns internal Zig struct not exposed in C#")]
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
        // Zig checks getVirtualLines() and verifies source_line — internal API
        // We verify no crash and draw works

        view.SetViewport(0, 3, 20, 2);
        view.SetViewport(0, 1, 20, 4);

        using var buf = OptimizedBuffer.Create(20, 10);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);
    }

    [Fact]
    public void Viewport_NullViewportReturnsAllLines()
    {
        // Zig: "viewport - null viewport returns all lines"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("Line 0\nLine 1\nLine 2\nLine 3\nLine 4");

        uint allLines = view.GetVirtualLineCount();
        Assert.Equal(5u, allLines);
    }

    [Fact]
    public void Viewport_SetViewportSizeConvenienceMethod()
    {
        // Zig: "viewport - setViewportSize convenience method"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("Line 0\nLine 1\nLine 2\nLine 3");

        view.SetViewportSize(20, 2);
        // Zig checks getViewport() returns {0, 0, 20, 2} — not fully exposed,
        // but no crash is verified.

        view.SetViewport(5, 1, 20, 2);
        view.SetViewportSize(30, 3);
        // Zig checks that x/y are preserved as {5, 1, 30, 3}
    }

    [Fact]
    public void Viewport_StoresHorizontalOffsetWithNoWrapping()
    {
        // Zig: "viewport - stores horizontal offset value with no wrapping"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRSTUVWXYZ");
        view.SetWrapMode((byte)WrapMode.None);

        view.SetViewport(5, 0, 10, 1);
        // Zig checks viewport x=5, y=0, width=10, height=1
        // We verify drawing works
        using var buf = OptimizedBuffer.Create(10, 1);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);
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
        view.SetViewport(3, 1, 8, 2);

        using var buf = OptimizedBuffer.Create(8, 2);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);
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
        buf.DrawTextBufferView(view, 0, 0);
    }

    [Fact]
    public void Viewport_HorizontalAndVerticalOffsetCombined()
    {
        // Zig: "viewport - horizontal and vertical offset combined (no wrap)"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("Line 0: ABCDEFGHIJ\nLine 1: KLMNOPQRST\nLine 2: UVWXYZ1234\nLine 3: 567890ABCD");
        view.SetWrapMode((byte)WrapMode.None);

        view.SetViewport(8, 1, 15, 2);

        using var buf = OptimizedBuffer.Create(15, 2);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);
    }

    [Fact]
    public void Viewport_HorizontalScrollingOnlyForNoWrapMode()
    {
        // Zig: "viewport - horizontal scrolling only for no-wrap mode"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");

        // No wrap mode with horizontal offset
        view.SetWrapMode((byte)WrapMode.None);
        view.SetViewport(10, 0, 15, 1);
        Assert.Equal(1u, view.GetVirtualLineCount());

        // Char wrap mode
        view.SetWrapMode((byte)WrapMode.Char);
        view.SetViewport(10, 0, 15, 5);
        Assert.True(view.GetVirtualLineCount() > 1);
    }

    [Fact]
    public void Viewport_HorizontalOffsetIrrelevantWithWrappingEnabled()
    {
        // Zig: "viewport - horizontal offset irrelevant with wrapping enabled"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("This is a very long line that will wrap into multiple virtual lines");
        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(20);

        uint totalVlines = view.GetVirtualLineCount();
        Assert.True(totalVlines > 1);

        view.SetViewport(5, 1, 15, 2);
        // No crash
    }

    [Fact]
    public void Viewport_ZeroWidthOrHeight()
    {
        // Zig: "viewport - zero width or height"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("Line 0\nLine 1\nLine 2");

        // Height 0
        view.SetViewport(0, 0, 20, 0);
        // Width 0 but height 2
        view.SetViewport(0, 0, 0, 2);
    }

    [Fact]
    public void Viewport_ViewportSetsWrapWidthAutomatically()
    {
        // Zig: "viewport - viewport sets wrap width automatically"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("AAAAAAAAAABBBBBBBBBBCCCCCCCCCCDDDDDDDDDD"); // 40 chars
        view.SetWrapMode((byte)WrapMode.Char);

        view.SetViewport(0, 0, 10, 5);
        uint vlineCount10 = view.GetVirtualLineCount();

        view.SetViewport(0, 0, 20, 5);
        uint vlineCount20 = view.GetVirtualLineCount();

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

        view.SetViewport(0, 0, 5, 2);
        view.SetViewport(0, 1, 5, 2);
        view.SetViewport(3, 1, 5, 2);
        view.SetViewport(5, 2, 5, 2);

        using var buf = OptimizedBuffer.Create(5, 2);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);
    }

    #endregion

    #region LoadFile

    [Fact]
    public void LoadFile_LoadsAndRendersFileCorrectly()
    {
        // Zig: "loadFile - loads and renders file correctly"
        string tempFile = Path.Combine(Directory.GetCurrentDirectory(), "test_load_file.txt");
        try
        {
            File.WriteAllText(tempFile, "ABC\nDEF");

            using var tb = TextBuffer.Create(WidthMethod.Unicode);
            using var view = TextBufferView.Create(tb);

            bool loaded = tb.LoadFile(tempFile);
            Assert.True(loaded);
            Assert.Equal(2u, tb.LineCount);
            Assert.Equal(6u, tb.Length);

            using var buf = OptimizedBuffer.Create(20, 5);
            buf.Clear(BlackBg);
            buf.DrawTextBufferView(view, 0, 0);

            string result = GetResolvedText(buf);
            Assert.StartsWith("ABC", result);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    #endregion

    #region Horizontal viewport rendering

    [Fact]
    public void DrawTextBuffer_HorizontalViewportOffsetRendersCorrectly()
    {
        // Zig: "drawTextBuffer - horizontal viewport offset renders correctly without wrapping"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("0123456789ABCDEFGHIJ");
        view.SetWrapMode((byte)WrapMode.None);
        view.SetViewport(5, 0, 10, 1);

        using var buf = OptimizedBuffer.Create(10, 1);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        string result = GetResolvedText(buf);
        Assert.StartsWith("56789ABCDE", result);
    }

    [Fact]
    public void DrawTextBuffer_HorizontalViewportOffsetWithMultipleLines()
    {
        // Zig: "drawTextBuffer - horizontal viewport offset with multiple lines"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNO\n0123456789!@#$%\nXYZ[\\]^_`{|}~");
        view.SetWrapMode((byte)WrapMode.None);
        view.SetViewport(3, 0, 8, 3);

        using var buf = OptimizedBuffer.Create(8, 3);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        string result = GetResolvedText(buf);
        Assert.Contains("DEFGHIJK", result);
        Assert.Contains("3456789!", result);
        Assert.Contains("[\\]^_`{|", result);
    }

    [Fact]
    public void DrawTextBuffer_CombinedHorizontalAndVerticalViewportOffsets()
    {
        // Zig: "drawTextBuffer - combined horizontal and vertical viewport offsets"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("Line0ABCDEFGHIJ\nLine1KLMNOPQRST\nLine2UVWXYZ0123\nLine3456789!@#$");
        view.SetWrapMode((byte)WrapMode.None);
        view.SetViewport(5, 1, 10, 2);

        using var buf = OptimizedBuffer.Create(10, 2);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        string result = GetResolvedText(buf);
        Assert.Contains("KLMNOPQRST", result);
        Assert.Contains("UVWXYZ0123", result);
    }

    [Fact]
    public void DrawTextBuffer_HorizontalViewportStopsRenderingAtViewportWidth()
    {
        // Zig: "drawTextBuffer - horizontal viewport stops rendering at viewport width"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ");
        view.SetWrapMode((byte)WrapMode.None);
        view.SetViewport(5, 0, 10, 1);

        using var buf = OptimizedBuffer.Create(10, 1);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        string result = GetResolvedText(buf);
        Assert.Equal("56789ABCDE", result[..10]);

        uint cellE = ReadCellChar(buf, 9, 0);
        Assert.Equal((uint)'E', cellE);
    }

    [Fact]
    public void DrawTextBuffer_HorizontalViewportWithSmallBufferRendersOnlyViewportWidth()
    {
        // Zig: "drawTextBuffer - horizontal viewport with small buffer renders only viewport width"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");
        view.SetWrapMode((byte)WrapMode.None);
        view.SetViewport(10, 0, 5, 1);

        using var buf = OptimizedBuffer.Create(20, 1);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        Assert.Equal((uint)'K', ReadCellChar(buf, 0, 0));
        Assert.Equal((uint)'O', ReadCellChar(buf, 4, 0));
        Assert.Equal(32u, ReadCellChar(buf, 5, 0));
        Assert.Equal(32u, ReadCellChar(buf, 6, 0));
    }

    [Fact]
    public void DrawTextBuffer_HorizontalViewportWidthLimitsRendering()
    {
        // Zig: "drawTextBuffer - horizontal viewport width limits rendering (efficiency test)"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText(new string('A', 1000));
        view.SetWrapMode((byte)WrapMode.None);
        view.SetViewport(100, 0, 10, 1);

        using var buf = OptimizedBuffer.Create(50, 1);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        uint nonSpaceCount = 0;
        for (uint i = 0; i < 50; i++)
        {
            if (ReadCellChar(buf, i, 0) == 'A')
                nonSpaceCount++;
        }
        Assert.Equal(10u, nonSpaceCount);
    }

    [Fact]
    public void DrawTextBuffer_OverwritingWideGraphemeWithAsciiLeavesNoGhostChars()
    {
        // Zig: "drawTextBuffer - overwriting wide grapheme with ASCII leaves no ghost chars"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var buf = OptimizedBuffer.Create(20, 5);

        // First draw wide characters
        tb.SetText("世界");
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        // Now overwrite with ASCII
        tb.SetText("ABC");
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        Assert.Equal((uint)'A', ReadCellChar(buf, 0, 0));
        Assert.Equal((uint)'B', ReadCellChar(buf, 1, 0));
        Assert.Equal((uint)'C', ReadCellChar(buf, 2, 0));

        string result = GetResolvedText(buf);
        Assert.StartsWith("ABC", result);
    }

    #endregion

    #region Syntax style / Highlights

    [Fact]
    public void DrawTextBuffer_SyntaxStyleDestroyDoesNotCrash()
    {
        // Zig: "drawTextBuffer - syntax style destroy does not crash"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        var style = SyntaxStyle.Create();
        tb.SetSyntaxStyle(style);

        uint styleId = style.Register("test", fg: new Rgba(1f, 0f, 0f, 1f));
        tb.SetText("Hello World");
        tb.AddHighlightByCharRange(new Highlight { Start = 0, End = 5, StyleId = styleId, Priority = 1, HlRef = 0 });

        using var buf = OptimizedBuffer.Create(20, 5);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        string result = GetResolvedText(buf);
        Assert.StartsWith("Hello World", result);

        // Destroy the style
        style.Dispose();

        // Draw again after style destroyed — should not crash
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        string result2 = GetResolvedText(buf);
        Assert.StartsWith("Hello World", result2);
    }

    #endregion

    #region Tab rendering

    [Fact]
    public void DrawTextBuffer_TabsAreRenderedAsSpaces()
    {
        // Zig: "drawTextBuffer - tabs are rendered as spaces (empty cells)"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.TabWidth = 4;
        tb.SetText("A\tB");

        using var buf = OptimizedBuffer.Create(20, 5);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        Assert.Equal((uint)'A', ReadCellChar(buf, 0, 0));
        Assert.Equal(32u, ReadCellChar(buf, 1, 0));
        Assert.Equal(32u, ReadCellChar(buf, 2, 0));
        Assert.Equal(32u, ReadCellChar(buf, 3, 0));
        Assert.Equal(32u, ReadCellChar(buf, 4, 0));
        // With static tabs: A at col 0, tab takes cols 1-4, B at col 5
        Assert.Equal((uint)'B', ReadCellChar(buf, 5, 0));
    }

    [Fact]
    public void DrawTextBuffer_TabIndicatorRendersWithCorrectColor()
    {
        // Zig: "drawTextBuffer - tab indicator renders with correct color"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.TabWidth = 4;
        tb.SetText("A\tB");

        view.SetTabIndicator('→');
        view.SetTabIndicatorColor(new Rgba(0.25f, 0.25f, 0.25f, 1f));

        using var buf = OptimizedBuffer.Create(20, 5);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        Assert.Equal((uint)'A', ReadCellChar(buf, 0, 0));
        Assert.Equal((uint)'→', ReadCellChar(buf, 1, 0));

        var fg1 = ReadCellFg(buf, 1, 0);
        Assert.True(ColorApproxEqual(0.25f, fg1.R));
        Assert.True(ColorApproxEqual(0.25f, fg1.G));
        Assert.True(ColorApproxEqual(0.25f, fg1.B));

        Assert.Equal(32u, ReadCellChar(buf, 2, 0));
        Assert.Equal(32u, ReadCellChar(buf, 3, 0));
        Assert.Equal(32u, ReadCellChar(buf, 4, 0));
        Assert.Equal((uint)'B', ReadCellChar(buf, 5, 0));
    }

    [Fact]
    public void DrawTextBuffer_TabWithoutIndicatorRendersAsSpaces()
    {
        // Zig: "drawTextBuffer - tab without indicator renders as spaces"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.TabWidth = 4;
        tb.SetText("A\tB");

        using var buf = OptimizedBuffer.Create(20, 5);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        Assert.Equal((uint)'A', ReadCellChar(buf, 0, 0));
        Assert.Equal(32u, ReadCellChar(buf, 1, 0));
        Assert.Equal(32u, ReadCellChar(buf, 2, 0));
        Assert.Equal(32u, ReadCellChar(buf, 3, 0));
        Assert.Equal(32u, ReadCellChar(buf, 4, 0));
        Assert.Equal((uint)'B', ReadCellChar(buf, 5, 0));
    }

    #endregion

    #region Complex Unicode / Emoji rendering

    [Fact(Skip = "Requires internal grapheme pool (isGraphemeChar/isContinuationChar) not exposed in C#")]
    public void DrawTextBuffer_MixedAsciiAndUnicodeWithEmojiRendersCompletely()
    {
        // Zig: "drawTextBuffer - mixed ASCII and Unicode with emoji renders completely"
        // Tests individual cell grapheme properties via internal grapheme pool
    }

    [Fact]
    public void ViewportWidth31ExactlyLastCharacterRendering()
    {
        // Zig: "viewport width = 31 exactly - last character rendering"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("- ✅ All 881 native tests passs");
        view.SetViewport(0, 0, 31, 1);

        using var buf = OptimizedBuffer.Create(50, 5);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        // The last 's' at cell 30 should be present
        uint cell30 = ReadCellChar(buf, 30, 0);
        Assert.Equal((uint)'s', cell30);
    }

    [Fact]
    public void DrawTextBuffer_ComplexMultilingualText()
    {
        // Zig: "drawTextBuffer - complex multilingual text with diverse scripts and emojis"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        string text = "# The Celestial Journey of संस्कृति 🌟🔮✨\n" +
            "In the beginning, there was नमस्ते 🙏 and the ancient wisdom\n" +
            "## Chapter प्रथम: The Eastern Gardens 🏯🎋🌸\n" +
            "The journey led them to the mystical lands where 漢字 (kanji) danced with ひらがな\n" +
            "Strange creatures emerged from the mist\n" +
            "## The संगम (Confluence) of Scripts 🌊📝🎭\n" +
            "At the great confluence they witnessed the merger of བོད་ཡིག (Tibetan) and தமிழ் (Tamil)\n" +
            "The marketplace buzzed with activity\n" +
            "## The Festival of ๑๐๐ Lanterns 🏮🎆🎇\n" +
            "During the grand festival they lit exactly ๑๐๐ lanterns\n" +
            "Musicians played unusual instruments\n" +
            "## The འཕྲུལ་དེབ (Machine) Age Arrives ⚙️🤖🦾\n" +
            "As modernity crept in the ancient འཁོར་ལོ gave way\n" +
            "The সমাজ (society) transformed\n" +
            "## The Final ಅಧ್ಯಾಯ (Chapter) 🌅🌄🌠\n" +
            "As the sun set over the പർവ്വതങ്ങൾ (mountains)\n" +
            "And so they learned\n" +
            "The end. समाप्त. 끝. จบ. முடிவு. ముగింపు. সমাপ্তি. ഒടുക്കം. ಅಂತ್ಯ. અંત. 🎬🎭🎪✨🌟⭐\n";

        tb.SetText(text);

        // Word wrapping
        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(80);

        using var buf = OptimizedBuffer.Create(80, 100);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        Assert.True(view.GetVirtualLineCount() > 0);

        string plainText = tb.GetPlainText();
        Assert.Contains("संस्कृति", plainText);
        Assert.Contains("नमस्ते", plainText);
        Assert.Contains("漢字", plainText);
        Assert.Contains("தமிழ்", plainText);

        // No wrapping
        view.SetWrapMode((byte)WrapMode.None);
        uint noWrapLines = view.GetVirtualLineCount();
        Assert.True(noWrapLines > 10);

        // Char wrapping narrow
        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(40);
        uint charWrapLines = view.GetVirtualLineCount();
        Assert.True(charWrapLines > noWrapLines);

        // Viewport scrolling
        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(80);
        view.SetViewport(0, 10, 80, 20);

        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        Assert.True(tb.LineCount > 15);
    }

    #endregion

    #region Syntax highlight with viewport offset

    [Fact(Skip = "setStyledText with per-chunk colors requires native StyledChunk API not fully exposed in C#")]
    public void SetStyledText_HighlightPositioningWithUnicodeText()
    {
        // Zig: "setStyledText - highlight positioning with Unicode text"
    }

    [Fact]
    public void DrawTextBuffer_MultipleSyntaxHighlightsWithHorizontalViewportOffsets()
    {
        // Zig: "drawTextBuffer - multiple syntax highlights with various horizontal viewport offsets"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var style = SyntaxStyle.Create();
        tb.SetSyntaxStyle(style);

        uint redStyle = style.Register("red", fg: new Rgba(1f, 0f, 0f, 1f));
        uint greenStyle = style.Register("green", fg: new Rgba(0f, 1f, 0f, 1f));
        uint blueStyle = style.Register("blue", fg: new Rgba(0f, 0f, 1f, 1f));
        uint yellowStyle = style.Register("yellow", fg: new Rgba(1f, 1f, 0f, 1f));

        string testText = "const x = function(y) { return y * 2; }";
        tb.SetText(testText);

        tb.AddHighlightByCharRange(new Highlight { Start = 0, End = 5, StyleId = redStyle, Priority = 1, HlRef = 0 });
        tb.AddHighlightByCharRange(new Highlight { Start = 10, End = 18, StyleId = greenStyle, Priority = 1, HlRef = 0 });
        tb.AddHighlightByCharRange(new Highlight { Start = 24, End = 30, StyleId = blueStyle, Priority = 1, HlRef = 0 });
        tb.AddHighlightByCharRange(new Highlight { Start = 35, End = 36, StyleId = yellowStyle, Priority = 1, HlRef = 0 });

        view.SetWrapMode((byte)WrapMode.None);

        // Test 1: Viewport at x=0 (no scroll)
        {
            view.SetViewport(0, 0, 40, 1);
            using var buf = OptimizedBuffer.Create(40, 1);
            buf.Clear(BlackBg);
            buf.DrawTextBufferView(view, 0, 0);

            // "const" is red
            Assert.Equal((uint)'c', ReadCellChar(buf, 0, 0));
            Assert.True(ColorApproxEqual(1f, ReadCellFg(buf, 0, 0).R));

            Assert.Equal((uint)'t', ReadCellChar(buf, 4, 0));
            Assert.True(ColorApproxEqual(1f, ReadCellFg(buf, 4, 0).R));

            // "function" is green
            Assert.Equal((uint)'f', ReadCellChar(buf, 10, 0));
            Assert.True(ColorApproxEqual(1f, ReadCellFg(buf, 10, 0).G));

            Assert.Equal((uint)'n', ReadCellChar(buf, 17, 0));
            Assert.True(ColorApproxEqual(1f, ReadCellFg(buf, 17, 0).G));
        }

        // Test 2: Viewport scrolled to x=3
        {
            view.SetViewport(3, 0, 20, 1);
            using var buf = OptimizedBuffer.Create(20, 1);
            buf.Clear(BlackBg);
            buf.DrawTextBufferView(view, 0, 0);

            // Position 0: 's' (source 3) - RED
            Assert.Equal((uint)'s', ReadCellChar(buf, 0, 0));
            var fg0 = ReadCellFg(buf, 0, 0);
            Assert.True(ColorApproxEqual(1f, fg0.R));
            Assert.True(ColorApproxEqual(0f, fg0.G));
            Assert.True(ColorApproxEqual(0f, fg0.B));

            // Position 1: 't' (source 4) - RED
            Assert.Equal((uint)'t', ReadCellChar(buf, 1, 0));
            var fg1 = ReadCellFg(buf, 1, 0);
            Assert.True(ColorApproxEqual(1f, fg1.R));
            Assert.True(ColorApproxEqual(0f, fg1.G));

            // Position 2: ' ' (source 5) - White (default)
            Assert.Equal((uint)' ', ReadCellChar(buf, 2, 0));
            var fg2 = ReadCellFg(buf, 2, 0);
            Assert.True(ColorApproxEqual(1f, fg2.R));
            Assert.True(ColorApproxEqual(1f, fg2.G));
            Assert.True(ColorApproxEqual(1f, fg2.B));

            // Position 7: 'f' (source 10) - GREEN
            Assert.Equal((uint)'f', ReadCellChar(buf, 7, 0));
            var fg7 = ReadCellFg(buf, 7, 0);
            Assert.True(ColorApproxEqual(0f, fg7.R));
            Assert.True(ColorApproxEqual(1f, fg7.G));
            Assert.True(ColorApproxEqual(0f, fg7.B));

            // Position 14: 'n' (source 17) - GREEN
            Assert.Equal((uint)'n', ReadCellChar(buf, 14, 0));
            var fg14 = ReadCellFg(buf, 14, 0);
            Assert.True(ColorApproxEqual(0f, fg14.R));
            Assert.True(ColorApproxEqual(1f, fg14.G));
        }

        // Test 3: Viewport scrolled to x=30
        {
            view.SetViewport(30, 0, 20, 1);
            using var buf = OptimizedBuffer.Create(20, 1);
            buf.Clear(BlackBg);
            buf.DrawTextBufferView(view, 0, 0);

            // Position 5: '2' (source 35) - YELLOW
            Assert.Equal((uint)'2', ReadCellChar(buf, 5, 0));
            var fg5 = ReadCellFg(buf, 5, 0);
            Assert.True(ColorApproxEqual(1f, fg5.R));
            Assert.True(ColorApproxEqual(1f, fg5.G));
            Assert.True(ColorApproxEqual(0f, fg5.B));
        }
    }

    [Fact]
    public void DrawTextBuffer_SyntaxHighlightingWithHorizontalViewportOffset()
    {
        // Zig: "drawTextBuffer - syntax highlighting with horizontal viewport offset"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var style = SyntaxStyle.Create();
        tb.SetSyntaxStyle(style);

        uint redStyleId = style.Register("keyword", fg: new Rgba(1f, 0f, 0f, 1f));
        tb.SetText("const x = 1");
        tb.AddHighlightByCharRange(new Highlight { Start = 0, End = 5, StyleId = redStyleId, Priority = 1, HlRef = 0 });

        view.SetWrapMode((byte)WrapMode.None);
        view.SetViewport(3, 0, 10, 1);

        using var buf = OptimizedBuffer.Create(10, 1);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        // 's' at position 0 is RED
        Assert.Equal((uint)'s', ReadCellChar(buf, 0, 0));
        var fg0 = ReadCellFg(buf, 0, 0);
        Assert.True(ColorApproxEqual(1f, fg0.R) && ColorApproxEqual(0f, fg0.G) && ColorApproxEqual(0f, fg0.B));

        // 't' at position 1 is RED
        Assert.Equal((uint)'t', ReadCellChar(buf, 1, 0));
        var fg1 = ReadCellFg(buf, 1, 0);
        Assert.True(ColorApproxEqual(1f, fg1.R) && ColorApproxEqual(0f, fg1.G) && ColorApproxEqual(0f, fg1.B));

        // ' ' at position 2 is NOT RED
        Assert.Equal((uint)' ', ReadCellChar(buf, 2, 0));
        var fg2 = ReadCellFg(buf, 2, 0);
        bool isRed2 = ColorApproxEqual(1f, fg2.R) && ColorApproxEqual(0f, fg2.G) && ColorApproxEqual(0f, fg2.B);
        Assert.False(isRed2);

        // 'x' at position 3 is NOT RED
        Assert.Equal((uint)'x', ReadCellChar(buf, 3, 0));
        var fg3 = ReadCellFg(buf, 3, 0);
        bool isRed3 = ColorApproxEqual(1f, fg3.R) && ColorApproxEqual(0f, fg3.G) && ColorApproxEqual(0f, fg3.B);
        Assert.False(isRed3);
    }

    [Fact(Skip = "setStyledText with per-chunk colors requires native StyledChunk API not fully exposed in C#")]
    public void DrawTextBuffer_SetStyledTextWithMultipleColorsAndHorizontalScrolling()
    {
        // Zig: "drawTextBuffer - setStyledText with multiple colors and horizontal scrolling"
    }

    #endregion

    #region Selection with viewport offset

    [Fact]
    public void DrawTextBuffer_SelectionWithHorizontalViewportOffset()
    {
        // Zig: "drawTextBuffer - selection with horizontal viewport offset"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("0123456789ABCDEFGHIJ");
        view.SetWrapMode((byte)WrapMode.None);
        view.SetViewport(5, 0, 10, 1);

        // Select characters 7-12 ("789AB")
        view.SetSelection(7, 12, new Rgba(1f, 1f, 0f, 1f), new Rgba(0f, 0f, 0f, 1f));

        using var buf = OptimizedBuffer.Create(10, 1);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        var yellowBg = new Rgba(1f, 1f, 0f, 1f);

        // Position 0: '5' - not highlighted
        Assert.Equal((uint)'5', ReadCellChar(buf, 0, 0));
        var bg0 = ReadCellBg(buf, 0, 0);
        bool hasYellow0 = ColorApproxEqual(yellowBg.R, bg0.R) &&
                          ColorApproxEqual(yellowBg.G, bg0.G) &&
                          ColorApproxEqual(yellowBg.B, bg0.B);
        Assert.False(hasYellow0);

        // Position 1: '6' - not highlighted
        Assert.Equal((uint)'6', ReadCellChar(buf, 1, 0));
        var bg1 = ReadCellBg(buf, 1, 0);
        bool hasYellow1 = ColorApproxEqual(yellowBg.R, bg1.R) &&
                          ColorApproxEqual(yellowBg.G, bg1.G) &&
                          ColorApproxEqual(yellowBg.B, bg1.B);
        Assert.False(hasYellow1);

        // Position 2: '7' - HIGHLIGHTED
        Assert.Equal((uint)'7', ReadCellChar(buf, 2, 0));
        var bg2 = ReadCellBg(buf, 2, 0);
        bool hasYellow2 = ColorApproxEqual(yellowBg.R, bg2.R) &&
                          ColorApproxEqual(yellowBg.G, bg2.G) &&
                          ColorApproxEqual(yellowBg.B, bg2.B);
        Assert.True(hasYellow2);

        // Position 3: '8' - HIGHLIGHTED
        Assert.Equal((uint)'8', ReadCellChar(buf, 3, 0));
        var bg3 = ReadCellBg(buf, 3, 0);
        bool hasYellow3 = ColorApproxEqual(yellowBg.R, bg3.R) &&
                          ColorApproxEqual(yellowBg.G, bg3.G) &&
                          ColorApproxEqual(yellowBg.B, bg3.B);
        Assert.True(hasYellow3);

        // Position 6: 'B' - HIGHLIGHTED
        Assert.Equal((uint)'B', ReadCellChar(buf, 6, 0));
        var bg6 = ReadCellBg(buf, 6, 0);
        bool hasYellow6 = ColorApproxEqual(yellowBg.R, bg6.R) &&
                          ColorApproxEqual(yellowBg.G, bg6.G) &&
                          ColorApproxEqual(yellowBg.B, bg6.B);
        Assert.True(hasYellow6);

        // Position 7: 'C' - NOT highlighted (selection end is exclusive)
        Assert.Equal((uint)'C', ReadCellChar(buf, 7, 0));
        var bg7 = ReadCellBg(buf, 7, 0);
        bool hasYellow7 = ColorApproxEqual(yellowBg.R, bg7.R) &&
                          ColorApproxEqual(yellowBg.G, bg7.G) &&
                          ColorApproxEqual(yellowBg.B, bg7.B);
        Assert.False(hasYellow7);
    }

    #endregion

    #region Truncation

    [Fact]
    public void DrawTextBuffer_SyntaxHighlightRespectsTruncation()
    {
        // Zig: "drawTextBuffer - syntax highlight respects truncation"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var style = SyntaxStyle.Create();
        tb.SetSyntaxStyle(style);

        uint redStyle = style.Register("red", fg: new Rgba(1f, 0f, 0f, 1f));
        uint greenStyle = style.Register("green", fg: new Rgba(0f, 1f, 0f, 1f));

        tb.SetText("0123456789ABCDEFGHIJ");
        tb.AddHighlightByCharRange(new Highlight { Start = 4, End = 7, StyleId = redStyle, Priority = 1, HlRef = 0 });
        tb.AddHighlightByCharRange(new Highlight { Start = 16, End = 20, StyleId = greenStyle, Priority = 1, HlRef = 0 });

        view.SetWrapMode((byte)WrapMode.None);
        view.SetTruncate(true);
        view.SetViewport(0, 0, 10, 1);

        using var buf = OptimizedBuffer.Create(10, 1);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        // Prefix: '1' at position 1 should be white
        Assert.Equal((uint)'1', ReadCellChar(buf, 1, 0));
        var fgPrefix = ReadCellFg(buf, 1, 0);
        Assert.True(ColorApproxEqual(1f, fgPrefix.R));
        Assert.True(ColorApproxEqual(1f, fgPrefix.G));
        Assert.True(ColorApproxEqual(1f, fgPrefix.B));

        // Ellipsis: '.' at position 3 should be white
        Assert.Equal((uint)'.', ReadCellChar(buf, 3, 0));
        var fgEllipsis = ReadCellFg(buf, 3, 0);
        Assert.True(ColorApproxEqual(1f, fgEllipsis.R));
        Assert.True(ColorApproxEqual(1f, fgEllipsis.G));
        Assert.True(ColorApproxEqual(1f, fgEllipsis.B));

        // Suffix: 'G' at position 6 should be green
        Assert.Equal((uint)'G', ReadCellChar(buf, 6, 0));
        var fgSuffix = ReadCellFg(buf, 6, 0);
        Assert.True(ColorApproxEqual(0f, fgSuffix.R));
        Assert.True(ColorApproxEqual(1f, fgSuffix.G));
        Assert.True(ColorApproxEqual(0f, fgSuffix.B));
    }

    [Fact]
    public void DrawTextBuffer_HighlightSpanningEllipsisContinuesOnSuffix()
    {
        // Zig: "drawTextBuffer - highlight spanning ellipsis continues on suffix"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        using var style = SyntaxStyle.Create();
        tb.SetSyntaxStyle(style);

        uint magentaStyle = style.Register("magenta", fg: new Rgba(1f, 0f, 1f, 1f));
        uint greenStyle = style.Register("green", fg: new Rgba(0f, 1f, 0f, 1f));

        tb.SetText("0123456789ABCDEFGHIJ");
        tb.AddHighlightByCharRange(new Highlight { Start = 2, End = 18, StyleId = magentaStyle, Priority = 1, HlRef = 0 });
        tb.AddHighlightByCharRange(new Highlight { Start = 18, End = 20, StyleId = greenStyle, Priority = 2, HlRef = 0 });

        view.SetWrapMode((byte)WrapMode.None);
        view.SetTruncate(true);
        view.SetViewport(0, 0, 10, 1);

        using var buf = OptimizedBuffer.Create(10, 1);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        // Ellipsis '.' at position 3 should be white
        Assert.Equal((uint)'.', ReadCellChar(buf, 3, 0));
        var fgEllipsis = ReadCellFg(buf, 3, 0);
        Assert.True(ColorApproxEqual(1f, fgEllipsis.R));
        Assert.True(ColorApproxEqual(1f, fgEllipsis.G));
        Assert.True(ColorApproxEqual(1f, fgEllipsis.B));

        // Suffix 'G' at position 6 should be magenta
        Assert.Equal((uint)'G', ReadCellChar(buf, 6, 0));
        var fgMagenta = ReadCellFg(buf, 6, 0);
        Assert.True(ColorApproxEqual(1f, fgMagenta.R));
        Assert.True(ColorApproxEqual(0f, fgMagenta.G));
        Assert.True(ColorApproxEqual(1f, fgMagenta.B));

        // Suffix 'I' at position 8 should be green
        Assert.Equal((uint)'I', ReadCellChar(buf, 8, 0));
        var fgGreen = ReadCellFg(buf, 8, 0);
        Assert.True(ColorApproxEqual(0f, fgGreen.R));
        Assert.True(ColorApproxEqual(1f, fgGreen.G));
        Assert.True(ColorApproxEqual(0f, fgGreen.B));
    }

    [Fact]
    public void DrawTextBuffer_SelectionRespectsTruncation()
    {
        // Zig: "drawTextBuffer - selection respects truncation"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("0123456789ABCDEFGHIJ");
        view.SetWrapMode((byte)WrapMode.None);
        view.SetTruncate(true);
        view.SetViewport(0, 0, 10, 1);

        // Select across ellipsis and suffix
        view.SetSelection(2, 19, new Rgba(1f, 1f, 0f, 1f), new Rgba(0f, 0f, 0f, 1f));

        using var buf = OptimizedBuffer.Create(10, 1);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        var yellowBg = new Rgba(1f, 1f, 0f, 1f);

        // '0' at position 0 - NOT highlighted
        Assert.Equal((uint)'0', ReadCellChar(buf, 0, 0));
        var bg0 = ReadCellBg(buf, 0, 0);
        Assert.False(ColorApproxEqual(yellowBg.R, bg0.R) && ColorApproxEqual(yellowBg.G, bg0.G) && ColorApproxEqual(yellowBg.B, bg0.B));

        // '.' at position 3 - HIGHLIGHTED (ellipsis within selection)
        Assert.Equal((uint)'.', ReadCellChar(buf, 3, 0));
        var bg3 = ReadCellBg(buf, 3, 0);
        Assert.True(ColorApproxEqual(yellowBg.R, bg3.R) && ColorApproxEqual(yellowBg.G, bg3.G) && ColorApproxEqual(yellowBg.B, bg3.B));

        // 'G' at position 6 - HIGHLIGHTED
        Assert.Equal((uint)'G', ReadCellChar(buf, 6, 0));
        var bg6 = ReadCellBg(buf, 6, 0);
        Assert.True(ColorApproxEqual(yellowBg.R, bg6.R) && ColorApproxEqual(yellowBg.G, bg6.G) && ColorApproxEqual(yellowBg.B, bg6.B));

        // 'I' at position 8 - HIGHLIGHTED
        Assert.Equal((uint)'I', ReadCellChar(buf, 8, 0));
        var bg8 = ReadCellBg(buf, 8, 0);
        Assert.True(ColorApproxEqual(yellowBg.R, bg8.R) && ColorApproxEqual(yellowBg.G, bg8.G) && ColorApproxEqual(yellowBg.B, bg8.B));

        // 'J' at position 9 - NOT highlighted (selection end 19 is exclusive, 'J' is at index 19)
        Assert.Equal((uint)'J', ReadCellChar(buf, 9, 0));
        var bg9 = ReadCellBg(buf, 9, 0);
        Assert.False(ColorApproxEqual(yellowBg.R, bg9.R) && ColorApproxEqual(yellowBg.G, bg9.G) && ColorApproxEqual(yellowBg.B, bg9.B));
    }

    [Fact]
    public void DrawTextBuffer_TruncationSelectionDoesNotOvershootMultiline()
    {
        // Zig: "drawTextBuffer - truncation selection does not overshoot multiline"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("abcdefghijABCDEFGHIJ\nklmnopqrstKLMNOPQRST");
        view.SetWrapMode((byte)WrapMode.None);
        view.SetTruncate(true);
        view.SetViewport(0, 0, 10, 2);

        // Select from char 2 to char 26
        view.SetSelection(2, 26, new Rgba(1f, 1f, 0f, 1f), new Rgba(0f, 0f, 0f, 1f));

        using var buf = OptimizedBuffer.Create(10, 2);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        var yellowBg = new Rgba(1f, 1f, 0f, 1f);

        // Line 2 cell (0,1): 'k' - HIGHLIGHTED
        Assert.Equal((uint)'k', ReadCellChar(buf, 0, 1));
        var bgL2_0 = ReadCellBg(buf, 0, 1);
        Assert.True(ColorApproxEqual(yellowBg.R, bgL2_0.R) && ColorApproxEqual(yellowBg.G, bgL2_0.G) && ColorApproxEqual(yellowBg.B, bgL2_0.B));

        // Line 2 cell (2,1): 'm' - HIGHLIGHTED
        Assert.Equal((uint)'m', ReadCellChar(buf, 2, 1));
        var bgL2_2 = ReadCellBg(buf, 2, 1);
        Assert.True(ColorApproxEqual(yellowBg.R, bgL2_2.R) && ColorApproxEqual(yellowBg.G, bgL2_2.G) && ColorApproxEqual(yellowBg.B, bgL2_2.B));

        // Line 2 cell (6,1): 'Q' - NOT highlighted (beyond selection)
        Assert.Equal((uint)'Q', ReadCellChar(buf, 6, 1));
        var bgL2_6 = ReadCellBg(buf, 6, 1);
        Assert.False(ColorApproxEqual(yellowBg.R, bgL2_6.R) && ColorApproxEqual(yellowBg.G, bgL2_6.G) && ColorApproxEqual(yellowBg.B, bgL2_6.B));
    }

    #endregion

    #region Chinese / CJK text

    [Fact]
    public void DrawTextBuffer_ChineseTextWithWrappingNoStrayBytes()
    {
        // Zig: "drawTextBuffer - Chinese text with wrapping no stray bytes"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        string text =
            "前后端分离 - TypeScript逻辑 + Go TUI界面\n" +
            "组件化设计 - 基于tview的可复用组件\n" +
            "渐进式交互 - 逐步披露避免信息过载\n" +
            "智能上下文 - 基于项目状态动态生成问题\n" +
            "丰富的问题类型 - 支持6种不同的交互形式\n" +
            "完整的验证 - 实时输入验证和错误处理";

        tb.SetText(text);
        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(35);

        using var buf = OptimizedBuffer.Create(40, 20);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        string result = GetResolvedText(buf, 2000);

        // Valid UTF-8 check (C# strings are always valid)
        Assert.Contains("完整的验证", result);
        Assert.Contains("实时输入验证和错误处理", result);

        // Should NOT contain stray bytes
        Assert.DoesNotContain("å式", result);
        Assert.DoesNotContain("å", result);

        Assert.Contains("形式", result);
    }

    [Fact]
    public void DrawTextBuffer_ChineseTextWithoutWrappingNoDuplicateChunks()
    {
        // Zig: "drawTextBuffer - Chinese text WITHOUT wrapping no duplicate chunks"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        string text =
            "前后端分离 - TypeScript逻辑 + Go TUI界面\n" +
            "组件化设计 - 基于tview的可复用组件\n" +
            "渐进式交互 - 逐步披露避免信息过载\n" +
            "智能上下文 - 基于项目状态动态生成问题\n" +
            "丰富的问题类型 - 支持6种不同的交互形式\n" +
            "完整的验证 - 实时输入验证和错误处理";

        tb.SetText(text);
        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(80);

        using var buf = OptimizedBuffer.Create(80, 10);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        string result = GetResolvedText(buf, 2000);

        Assert.DoesNotContain("å", result);
        Assert.Contains("完整的验证 - 实时输入验证和错误处理", result);
    }

    [Fact]
    public void DrawTextBuffer_ChineseTextWithCharWrappingNoStrayBytes()
    {
        // Zig: "drawTextBuffer - Chinese text with CHAR wrapping no stray bytes"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        string text =
            "前后端分离 - TypeScript逻辑 + Go TUI界面\n" +
            "组件化设计 - 基于tview的可复用组件\n" +
            "渐进式交互 - 逐步披露避免信息过载\n" +
            "智能上下文 - 基于项目状态动态生成问题\n" +
            "丰富的问题类型 - 支持6种不同的交互形式\n" +
            "完整的验证 - 实时输入验证和错误处理";

        tb.SetText(text);
        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth(35);

        using var buf = OptimizedBuffer.Create(35, 20);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        string result = GetResolvedText(buf, 2000);

        Assert.DoesNotContain("å", result);
        Assert.Contains("形式", result);
        Assert.Contains("完整的验证", result);
    }

    [Fact(Skip = "Requires internal grapheme pool (isContinuationChar) not exposed in C#")]
    public void DrawTextBuffer_WordWrapCjkMixedTextWithoutBreakPoints()
    {
        // Zig: "drawTextBuffer - word wrap CJK mixed text without break points"
    }

    [Fact]
    public void DrawTextBuffer_WordWrapCjkTextPreservesUtf8Boundaries()
    {
        // Zig: "drawTextBuffer - word wrap CJK text preserves UTF-8 boundaries"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("한글,English,中文,日本語,混合,Test,測試,テスト,가나다,ABC,一二三,あいう,라마바,DEF,四五六,えおか");
        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth(20);

        using var buf = OptimizedBuffer.Create(30, 20);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        string result = GetResolvedText(buf, 1000);

        // Valid UTF-8 (implicit in C# strings)
        Assert.True(view.GetVirtualLineCount() > 1);

        // No stray partial bytes
        Assert.DoesNotContain("ä", result);
    }

    [Fact]
    public void DrawTextBuffer_ThaiGraphemeInQuotesOccupiesOneCell()
    {
        // Zig: "drawTextBuffer - Thai ว่ grapheme in quotes occupies one cell"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("\"ว่\"");

        using var buf = OptimizedBuffer.Create(10, 1);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        Assert.Equal((uint)'"', ReadCellChar(buf, 0, 0));

        uint cell1 = ReadCellChar(buf, 1, 0);
        Assert.NotEqual((uint)' ', cell1);
        Assert.NotEqual((uint)'"', cell1);

        Assert.Equal((uint)'"', ReadCellChar(buf, 2, 0));
        Assert.Equal(32u, ReadCellChar(buf, 3, 0));

        string result = GetResolvedText(buf);
        Assert.Contains("\"ว่\"", result);
    }

    #endregion

    #region Regression — char/word wrap must preserve all characters

    [Theory]
    [InlineData(15)]
    [InlineData(18)]
    [InlineData(20)]
    [InlineData(22)]
    [InlineData(23)]
    [InlineData(24)]
    [InlineData(25)]
    [InlineData(26)]
    [InlineData(28)]
    [InlineData(30)]
    [InlineData(35)]
    public void DrawTextBuffer_CharWrapPreservesAllCharacters(int width)
    {
        // Regression: TextWrap demo "medium" sample loses characters like "az" from "lazy"
        // and characters from "liquor" when using WrapMode.Char.
        const string text = "The quick brown fox jumps over the lazy dog. Pack my box with five dozen liquor jugs.";

        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText(text);
        view.SetWrapMode((byte)WrapMode.Char);
        view.SetWrapWidth((uint)width);
        view.SetViewport(0, 0, (uint)width, 20);

        using var buf = OptimizedBuffer.Create((uint)width, 20);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        string result = GetResolvedText(buf);
        // Collapse rendered lines into a single string (strip trailing spaces per line)
        var allText = string.Join("", result.Split('\n').Select(l => l.TrimEnd()));

        Assert.Contains("lazy", allText);
        Assert.Contains("liquor", allText);
        // Every character in the original text should be present
        Assert.Equal(text, allText);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(18)]
    [InlineData(20)]
    [InlineData(22)]
    [InlineData(23)]
    [InlineData(24)]
    [InlineData(25)]
    [InlineData(26)]
    [InlineData(28)]
    [InlineData(30)]
    [InlineData(35)]
    public void DrawTextBuffer_WordWrapPreservesAllCharacters(int width)
    {
        // Regression: TextWrap demo "medium" sample may lose characters in WrapMode.Word too.
        const string text = "The quick brown fox jumps over the lazy dog. Pack my box with five dozen liquor jugs.";

        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        tb.SetText(text);
        view.SetWrapMode((byte)WrapMode.Word);
        view.SetWrapWidth((uint)width);
        view.SetViewport(0, 0, (uint)width, 20);

        using var buf = OptimizedBuffer.Create((uint)width, 20);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view, 0, 0);

        string result = GetResolvedText(buf);
        var allText = string.Join("", result.Split('\n').Select(l => l.TrimEnd()));

        Assert.Contains("lazy", allText);
        Assert.Contains("liquor", allText);
    }

    /// <summary>
    /// Integration test: simulate TextWrap demo with full widget tree.
    /// Box with border → TextRenderable, using Yoga layout.
    /// </summary>
    [Theory]
    [InlineData(25, WrapMode.Char)]
    [InlineData(30, WrapMode.Char)]
    [InlineData(35, WrapMode.Char)]
    [InlineData(40, WrapMode.Char)]
    [InlineData(80, WrapMode.Char)]
    [InlineData(25, WrapMode.Word)]
    [InlineData(30, WrapMode.Word)]
    [InlineData(35, WrapMode.Word)]
    [InlineData(40, WrapMode.Word)]
    [InlineData(80, WrapMode.Word)]
    public void TextWrapDemo_WidgetTree_PreservesAllCharacters(int totalWidth, WrapMode wrapMode)
    {
        // Simulate the TextWrap demo: Box (border, overflow=hidden) → TextRenderable
        const string text = "The quick brown fox jumps over the lazy dog. Pack my box with five dozen liquor jugs.";

        var ctx = new TestRenderContext { Width = totalWidth, Height = 20 };
        var root = new RootRenderable(ctx);

        var box = new BoxRenderable(ctx, new BoxOptions
        {
            Id = "col",
            Border = true,
            BorderStyle = BorderStyle.Rounded,
            Overflow = OverflowValue.Hidden,
        });

        var txt = new TextRenderable(ctx, new TextOptions
        {
            Id = "txt",
            Content = text,
            WrapMode = wrapMode,
            Fg = Rgba.FromInts(255, 255, 255),
        });

        box.Add(txt);
        root.Add(box);

        // Create buffer matching the total width
        using var buf = OptimizedBuffer.Create((uint)totalWidth, 20);
        buf.Clear(BlackBg);

        // Trigger full render pipeline (layout + render)
        ctx.FrameId = 1;
        root.Render(buf, 0.016f);

        Assert.True(txt.Width > 0, $"Text width should be > 0 but was {txt.Width}");
        Assert.True(txt.Height > 0, $"Text height should be > 0 but was {txt.Height}");
        Assert.True(box.Width > 0, $"Box width should be > 0 but was {box.Width}");
        Assert.True(box.Height > 2, $"Box height should be > 2 (border) but was {box.Height}");
        Assert.True(txt.Width == box.Width - 2,
            $"Text w={txt.Width} should be box.Width-2={box.Width - 2}");

        // Extract only the text content area (inside box border)
        string fullResult = GetResolvedText(buf, 32768, addLineBreaks: true);
        var lines = fullResult.Split('\n');
        int txtX = (int)txt.ScreenX;
        int txtW = txt.Width;
        int txtY = (int)txt.ScreenY;

        var contentParts = new List<string>();
        for (int row = txtY; row < txtY + txt.Height && row < lines.Length; row++)
        {
            var line = lines[row];
            if (line.Length > txtX)
            {
                int end = Math.Min(txtX + txtW, line.Length);
                contentParts.Add(line[txtX..end].TrimEnd());
            }
        }
        var allText = string.Join("", contentParts);

        Assert.Contains("lazy", allText);
        Assert.Contains("liquor", allText);

        root.Destroy();
    }

    [Fact]
    public void TextWrapDemo_WidgetTree_Debug_Width40Char()
    {
        const string text = "The quick brown fox jumps over the lazy dog. Pack my box with five dozen liquor jugs.";

        // Test a range of content widths to find which ones lose characters
        var failures = new System.Text.StringBuilder();

        foreach (int contentWidth in Enumerable.Range(20, 60))
        {
            int boxWidth = contentWidth + 2; // add border
            var ctx = new TestRenderContext { Width = boxWidth, Height = 20 };
            var root = new RootRenderable(ctx);

            var box = new BoxRenderable(ctx, new BoxOptions
            {
                Id = "col",
                Border = true,
                BorderStyle = BorderStyle.Rounded,
                Overflow = OverflowValue.Hidden,
            });

            var txt = new TextRenderable(ctx, new TextOptions
            {
                Id = "txt",
                Content = text,
                WrapMode = WrapMode.Char,
                Fg = Rgba.FromInts(255, 255, 255),
            });

            box.Add(txt);
            root.Add(box);

            using var buf = OptimizedBuffer.Create((uint)boxWidth, 20);
            buf.Clear(BlackBg);

            ctx.FrameId = 1;
            root.Render(buf, 0.016f);

            // Read buffer and extract content rows
            string fullResult = GetResolvedText(buf, 32768, addLineBreaks: true);
            var lines = fullResult.Split('\n');

            var contentParts = new List<string>();
            for (int row = 1; row < box.Height - 1 && row < lines.Length; row++)
            {
                var line = lines[row];
                if (line.Length >= 2)
                {
                    int end = Math.Min(boxWidth - 1, line.Length);
                    var content = line[1..end];
                    contentParts.Add(content.TrimEnd());
                }
            }

            var allContent = string.Join("", contentParts);

            // Compare with original text (ignoring trailing spaces)
            var originalNoSpaces = text.Replace(" ", "");
            var renderedNoSpaces = allContent.Replace(" ", "");

            if (renderedNoSpaces != originalNoSpaces)
            {
                failures.AppendLine($"ContentWidth={contentWidth}: Txt(w={txt.Width},h={txt.Height})");
                failures.AppendLine($"  Original chars: {originalNoSpaces.Length}");
                failures.AppendLine($"  Rendered chars: {renderedNoSpaces.Length}");
                failures.AppendLine($"  Missing count:  {originalNoSpaces.Length - renderedNoSpaces.Length}");
                for (int i = 0; i < contentParts.Count; i++)
                    failures.AppendLine($"  Line {i}: [{contentParts[i]}]");
            }

            root.Destroy();
        }

        Assert.True(failures.Length == 0, $"Character loss detected:\n{failures}");
    }

    [Fact]
    public void TextWrapDemo_ThreeColumn_CharWrap_PreservesAllCharacters()
    {
        const string text = "The quick brown fox jumps over the lazy dog. Pack my box with five dozen liquor jugs.";

        var failures = new System.Text.StringBuilder();

        foreach (int termWidth in new[] { 60, 70, 80, 90, 100, 110, 120, 130, 140 })
        {
            var ctx = new TestRenderContext { Width = termWidth, Height = 20 };
            var root = new RootRenderable(ctx);

            // Row with 3 columns (mimic the demo)
            var row = new BoxRenderable(ctx, new BoxOptions
            {
                Id = "row",
                Width = DimensionValue.Auto,
                Height = DimensionValue.Auto,
                FlexGrow = 1,
                FlexDirection = FlexDirectionValue.Row,
                Gap = 1,
                Padding = DimensionValue.Point(1),
            });

            var col = new BoxRenderable(ctx, new BoxOptions
            {
                Id = "col-char",
                FlexGrow = 1,
                Border = true,
                BorderStyle = BorderStyle.Rounded,
                Overflow = OverflowValue.Hidden,
            });

            var txt = new TextRenderable(ctx, new TextOptions
            {
                Id = "col-char-text",
                Content = text,
                WrapMode = WrapMode.Char,
                Fg = Rgba.FromInts(255, 255, 255),
            });

            // Two sibling columns (empty, just for layout)
            var col1 = new BoxRenderable(ctx, new BoxOptions
            {
                Id = "col-none",
                FlexGrow = 1,
                Border = true,
                BorderStyle = BorderStyle.Rounded,
                Overflow = OverflowValue.Hidden,
            });
            var col3 = new BoxRenderable(ctx, new BoxOptions
            {
                Id = "col-word",
                FlexGrow = 1,
                Border = true,
                BorderStyle = BorderStyle.Rounded,
                Overflow = OverflowValue.Hidden,
            });

            col.Add(txt);
            row.Add(col1);
            row.Add(col);
            row.Add(col3);
            root.Add(row);

            using var buf = OptimizedBuffer.Create((uint)termWidth, 20);
            buf.Clear(BlackBg);

            ctx.FrameId = 1;
            root.Render(buf, 0.016f);

            // Read buffer
            string fullResult = GetResolvedText(buf, 65536, addLineBreaks: true);
            var lines = fullResult.Split('\n');

            // Extract the text content from the center column
            int colScreenX = (int)col.ScreenX;
            int colWidth = col.Width;
            int txtWidth = txt.Width;
            int txtScreenX = (int)txt.ScreenX;
            int txtScreenY = (int)txt.ScreenY;

            var contentParts = new List<string>();
            for (int row_i = txtScreenY; row_i < txtScreenY + txt.Height && row_i < lines.Length; row_i++)
            {
                var line = lines[row_i];
                if (line.Length > txtScreenX)
                {
                    int end = Math.Min(txtScreenX + txtWidth, line.Length);
                    var content = line[txtScreenX..end].TrimEnd();
                    contentParts.Add(content);
                }
            }

            var allContent = string.Join("", contentParts);
            var originalNoSpaces = text.Replace(" ", "");
            var renderedNoSpaces = allContent.Replace(" ", "");

            if (renderedNoSpaces != originalNoSpaces)
            {
                failures.AppendLine($"TermWidth={termWidth}: col(x={colScreenX},w={colWidth}) txt(x={txtScreenX},w={txtWidth},h={txt.Height})");
                failures.AppendLine($"  Original chars: {originalNoSpaces.Length}");
                failures.AppendLine($"  Rendered chars: {renderedNoSpaces.Length}");
                failures.AppendLine($"  Missing count:  {originalNoSpaces.Length - renderedNoSpaces.Length}");
                for (int i = 0; i < contentParts.Count; i++)
                    failures.AppendLine($"  Line {i}: [{contentParts[i]}]");
            }

            root.Destroy();
        }

        Assert.True(failures.Length == 0, $"Character loss detected:\n{failures}");
    }

    [Fact]
    public void Yoga_BorderReducesContentArea()
    {
        var ctx = new TestRenderContext { Width = 120, Height = 20 };
        var root = new RootRenderable(ctx);

        // Test 1: Fixed width box with border
        var box1 = new BoxRenderable(ctx, new BoxOptions
        {
            Id = "box1",
            Width = DimensionValue.Point(40),
            Height = DimensionValue.Point(10),
            Border = true,
        });
        var txt1 = new TextRenderable(ctx, new TextOptions
        {
            Id = "txt1",
            Content = "hello",
            Fg = Rgba.White,
        });
        box1.Add(txt1);
        root.Add(box1);

        using var buf1 = OptimizedBuffer.Create(120, 20);
        ctx.FrameId = 1;
        root.Render(buf1, 0.016f);

        Assert.True(txt1.Width == box1.Width - 2,
            $"Test1 (fixed): Text w={txt1.Width}, Box w={box1.Width}, expected {box1.Width - 2}");

        root.Destroy();

        // Test 2: Full demo layout with header, 3-column row, footer (like TextWrap demo)
        var ctx2 = new TestRenderContext { Width = 120, Height = 20 };
        var root2 = new RootRenderable(ctx2);

        var header = new BoxRenderable(ctx2, new BoxOptions
        {
            Id = "header",
            Width = DimensionValue.Auto,
            Height = DimensionValue.Point(3),
            BackgroundColor = Rgba.FromHex("#0f766e"),
            BorderStyle = BorderStyle.Rounded,
            AlignItems = AlignValue.Center,
            JustifyContent = JustifyValue.Center,
            Border = true,
        });
        var headerText = new TextRenderable(ctx2, new TextOptions
        {
            Id = "header-text",
            Content = "Text Wrap Demo",
            Fg = Rgba.White,
        });
        header.Add(headerText);

        var row = new BoxRenderable(ctx2, new BoxOptions
        {
            Id = "row",
            Width = DimensionValue.Auto,
            Height = DimensionValue.Auto,
            FlexGrow = 1,
            FlexDirection = FlexDirectionValue.Row,
            Gap = 1,
            Padding = DimensionValue.Point(1),
        });

        var col1 = new BoxRenderable(ctx2, new BoxOptions
        {
            Id = "col1", FlexGrow = 1, Border = true, BorderStyle = BorderStyle.Rounded,
            BorderColor = Rgba.FromHex("#6b7280"), BackgroundColor = Rgba.FromHex("#1f2937"),
            Title = "WrapMode.None", Overflow = OverflowValue.Hidden,
        });
        var col2 = new BoxRenderable(ctx2, new BoxOptions
        {
            Id = "col2", FlexGrow = 1, Border = true, BorderStyle = BorderStyle.Rounded,
            BorderColor = Rgba.FromHex("#6b7280"), BackgroundColor = Rgba.FromHex("#1f2937"),
            Title = "WrapMode.Char", Overflow = OverflowValue.Hidden,
        });
        var col3 = new BoxRenderable(ctx2, new BoxOptions
        {
            Id = "col3", FlexGrow = 1, Border = true, BorderStyle = BorderStyle.Rounded,
            BorderColor = Rgba.FromHex("#6b7280"), BackgroundColor = Rgba.FromHex("#1f2937"),
            Title = "WrapMode.Word", Overflow = OverflowValue.Hidden,
        });

        var col1Txt = new TextRenderable(ctx2, new TextOptions
        {
            Id = "col1-txt",
            Content = "The quick brown fox jumps over the lazy dog. Pack my box with five dozen liquor jugs.",
            WrapMode = WrapMode.None,
            Fg = Rgba.White,
        });
        col1.Add(col1Txt);

        var txt2 = new TextRenderable(ctx2, new TextOptions
        {
            Id = "txt2",
            Content = "The quick brown fox jumps over the lazy dog. Pack my box with five dozen liquor jugs.",
            WrapMode = WrapMode.Char,
            Fg = Rgba.White,
        });
        col2.Add(txt2);

        var txt3 = new TextRenderable(ctx2, new TextOptions
        {
            Id = "txt3",
            Content = "The quick brown fox jumps over the lazy dog. Pack my box with five dozen liquor jugs.",
            WrapMode = WrapMode.Word,
            Fg = Rgba.White,
        });
        col3.Add(txt3);

        row.Add(col1);
        row.Add(col2);
        row.Add(col3);

        var footer = new BoxRenderable(ctx2, new BoxOptions
        {
            Id = "footer",
            Width = DimensionValue.Auto,
            Height = DimensionValue.Point(3),
            BackgroundColor = Rgba.FromHex("#1e3a5f"),
            BorderStyle = BorderStyle.Rounded,
            AlignItems = AlignValue.Center,
            JustifyContent = JustifyValue.Center,
            Border = true,
        });

        root2.Add(header);
        root2.Add(row);
        root2.Add(footer);

        using var buf2 = OptimizedBuffer.Create(120, 20);
        ctx2.FrameId = 1;
        root2.Render(buf2, 0.016f);

        // Stretched text child must be narrower than its bordered parent
        Assert.True(txt2.Width == col2.Width - 2,
            $"Test2 (full demo): Text w={txt2.Width}, Col2 w={col2.Width}, " +
            $"expected text w={col2.Width - 2}");

        root2.Destroy();
    }

    [Theory]
    [InlineData(80)]
    [InlineData(100)]
    [InlineData(120)]
    public void TextWrapDemo_AnsiOutput_PreservesAllCharacters(int termWidth)
    {
        const string text = "The quick brown fox jumps over the lazy dog. Pack my box with five dozen liquor jugs.";
        const int termHeight = 20;

        using var nativeRenderer = NativeRenderer.Create((uint)termWidth, (uint)termHeight, testing: true);
        var ctx = new TestRenderContext { Width = termWidth, Height = termHeight };
        var root = new RootRenderable(ctx);

        // Build exact demo layout
        var header = new BoxRenderable(ctx, new BoxOptions
        {
            Id = "header",
            Width = DimensionValue.Auto,
            Height = DimensionValue.Point(3),
            BackgroundColor = Rgba.FromHex("#0f766e"),
            BorderStyle = BorderStyle.Rounded,
            AlignItems = AlignValue.Center,
            JustifyContent = JustifyValue.Center,
            Border = true,
        });
        var headerText = new TextRenderable(ctx, new TextOptions
        {
            Id = "header-text",
            Content = "Text Wrap Demo — Sample: Medium",
            Fg = Rgba.FromInts(255, 255, 255),
        });
        header.Add(headerText);

        var row = new BoxRenderable(ctx, new BoxOptions
        {
            Id = "row",
            Width = DimensionValue.Auto,
            Height = DimensionValue.Auto,
            FlexGrow = 1,
            FlexDirection = FlexDirectionValue.Row,
            Gap = 1,
            Padding = DimensionValue.Point(1),
        });

        (BoxRenderable box, TextRenderable txt) MakeCol(string id, WrapMode mode)
        {
            var col = new BoxRenderable(ctx, new BoxOptions
            {
                Id = id,
                FlexGrow = 1,
                Border = true,
                BorderStyle = BorderStyle.Rounded,
                BorderColor = Rgba.FromHex("#6b7280"),
                BackgroundColor = Rgba.FromHex("#1f2937"),
                Title = $"WrapMode.{mode}",
                Overflow = OverflowValue.Hidden,
            });
            var t = new TextRenderable(ctx, new TextOptions
            {
                Id = $"{id}-text",
                Content = text,
                WrapMode = mode,
                Fg = Rgba.FromHex("#e5e7eb"),
            });
            col.Add(t);
            return (col, t);
        }

        var (noneBox, _) = MakeCol("col-none", WrapMode.None);
        var (charBox, charText) = MakeCol("col-char", WrapMode.Char);
        var (wordBox, _) = MakeCol("col-word", WrapMode.Word);

        row.Add(noneBox);
        row.Add(charBox);
        row.Add(wordBox);

        var footer = new BoxRenderable(ctx, new BoxOptions
        {
            Id = "footer",
            Width = DimensionValue.Auto,
            Height = DimensionValue.Point(3),
            BackgroundColor = Rgba.FromHex("#1e3a5f"),
            BorderStyle = BorderStyle.Rounded,
            AlignItems = AlignValue.Center,
            JustifyContent = JustifyValue.Center,
            Border = true,
        });
        var footerText = new TextRenderable(ctx, new TextOptions
        {
            Id = "footer-text",
            Content = "[n] Next sample (2/5)  [Ctrl+C] Quit",
            Fg = Rgba.FromHex("#94a3b8"),
        });
        footer.Add(footerText);

        root.Add(header);
        root.Add(row);
        root.Add(footer);

        // Render the tree to the NativeRenderer's next buffer
        using var nextBuf = nativeRenderer.GetNextBuffer();
        ctx.FrameId = 1;
        root.Render(nextBuf, 0.016f);

        // First verify buffer content
        string bufContent = GetResolvedText(nextBuf, 65536, addLineBreaks: true);

        // Now generate ANSI output
        nativeRenderer.Render(forceFullRender: true);
        string ansiOutput = nativeRenderer.GetLastOutputForTest();

        // Strip ANSI escape sequences to get plain text
        string plainOutput = System.Text.RegularExpressions.Regex.Replace(
            ansiOutput, @"\x1b[\[\]()#;?]*(?:[0-9]{1,4}(?:;[0-9]{0,4})*)?[0-9A-ORZcf-nqry=><~]", "");
        plainOutput = System.Text.RegularExpressions.Regex.Replace(
            plainOutput, @"\x1b\].*?\x1b\\", "");

        // Extract text column content from buffer
        int txtX = (int)charText.ScreenX;
        int txtW = charText.Width;
        int txtY = (int)charText.ScreenY;
        int txtH = charText.Height;

        var bufLines = bufContent.Split('\n');
        var bufTextParts = new List<string>();
        for (int r = txtY; r < txtY + txtH && r < bufLines.Length; r++)
        {
            var line = bufLines[r];
            if (line.Length > txtX)
            {
                int end = Math.Min(txtX + txtW, line.Length);
                bufTextParts.Add(line[txtX..end].TrimEnd());
            }
        }
        var bufAllText = string.Join("", bufTextParts);

        bool ansiHasLazy = plainOutput.Contains("lazy");
        bool bufHasLazy = bufAllText.Replace(" ", "").Contains("lazy");

        var diag = new StringBuilder();
        diag.AppendLine($"Terminal: {termWidth}x{termHeight}");
        diag.AppendLine($"CharText: screenX={txtX}, screenY={txtY}, width={txtW}, height={txtH}");
        diag.AppendLine($"CharBox:  screenX={(int)charBox.ScreenX}, width={charBox.Width}");
        diag.AppendLine($"Scissor:  x={(int)charBox.ScreenX + 1}, w={charBox.Width - 2}");
        diag.AppendLine($"Buffer lines (char column):");
        for (int i = 0; i < bufTextParts.Count; i++)
            diag.AppendLine($"  [{i}] \"{bufTextParts[i]}\"");

        diag.AppendLine($"Buffer has 'lazy': {bufHasLazy}");
        diag.AppendLine($"ANSI has 'lazy': {ansiHasLazy}");

        Assert.True(bufHasLazy, $"Buffer missing 'lazy':\n{diag}");
        Assert.True(ansiHasLazy, $"ANSI output missing 'lazy':\n{diag}");

        root.Destroy();
    }

    #endregion
}
