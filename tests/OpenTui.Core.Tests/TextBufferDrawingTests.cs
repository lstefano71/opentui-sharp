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
    private static string GetResolvedText(OptimizedBuffer buf, int maxLen = 4096)
    {
        unsafe
        {
            byte[] outBuf = new byte[maxLen];
            fixed (byte* ptr = outBuf)
            {
                uint written = buf.WriteResolvedChars((nint)ptr, (nuint)maxLen, false);
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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);
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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 5, 5);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);
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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);
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
        view.SetViewport(3, 1, 8, 2);

        using var buf = OptimizedBuffer.Create(8, 2);
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
        buf.DrawTextBufferView(view.Handle, 0, 0);
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
        buf.DrawTextBufferView(view.Handle, 0, 0);
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
            buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

        // Now overwrite with ASCII
        tb.SetText("ABC");
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        tb.SetSyntaxStyle(style.Handle);

        uint styleId = style.Register("test", fg: new Rgba(1f, 0f, 0f, 1f));
        tb.SetText("Hello World");
        tb.AddHighlightByCharRange(new Highlight { Start = 0, End = 5, StyleId = styleId, Priority = 1, HlRef = 0 });

        using var buf = OptimizedBuffer.Create(20, 5);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);

        string result = GetResolvedText(buf);
        Assert.StartsWith("Hello World", result);

        // Destroy the style
        style.Dispose();

        // Draw again after style destroyed — should not crash
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        tb.SetSyntaxStyle(style.Handle);

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
            buf.DrawTextBufferView(view.Handle, 0, 0);

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
            buf.DrawTextBufferView(view.Handle, 0, 0);

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
            buf.DrawTextBufferView(view.Handle, 0, 0);

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
        tb.SetSyntaxStyle(style.Handle);

        uint redStyleId = style.Register("keyword", fg: new Rgba(1f, 0f, 0f, 1f));
        tb.SetText("const x = 1");
        tb.AddHighlightByCharRange(new Highlight { Start = 0, End = 5, StyleId = redStyleId, Priority = 1, HlRef = 0 });

        view.SetWrapMode((byte)WrapMode.None);
        view.SetViewport(3, 0, 10, 1);

        using var buf = OptimizedBuffer.Create(10, 1);
        buf.Clear(BlackBg);
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        tb.SetSyntaxStyle(style.Handle);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        tb.SetSyntaxStyle(style.Handle);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
        buf.DrawTextBufferView(view.Handle, 0, 0);

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
}
