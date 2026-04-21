using OpenTui.Core.Managed;
using OpenTui.Core.Managed.Unicode;
using Xunit;

namespace OpenTui.Core.Tests;

/// <summary>
/// Tests for ManagedBuffer.DrawTextBufferView and DrawEditorView.
/// </summary>
public class ManagedBufferDrawTextBufferViewTests : IDisposable
{
    private static readonly Rgba Black = new(0f, 0f, 0f, 1f);
    private static readonly Rgba White = new(1f, 1f, 1f, 1f);

    private ManagedBuffer _buf = ManagedBuffer.Create(20, 5);

    public void Dispose() => _buf.Dispose();

    #region Basic rendering

    [Fact]
    public void DrawTextBufferView_SimpleSingleLineText()
    {
        using var tb = ManagedTextBuffer.Create();
        using var view = ManagedTextBufferView.Create(tb, 20, 5);

        tb.SetText("Hello World");
        _buf.Clear(Black);
        _buf.DrawTextBufferView(view, 0, 0);

        Assert.Equal((uint)'H', _buf.Get(0, 0)!.Value.Char);
        Assert.Equal((uint)'e', _buf.Get(1, 0)!.Value.Char);
        Assert.Equal((uint)'l', _buf.Get(2, 0)!.Value.Char);
        Assert.Equal((uint)'l', _buf.Get(3, 0)!.Value.Char);
        Assert.Equal((uint)'o', _buf.Get(4, 0)!.Value.Char);
        Assert.Equal((uint)' ', _buf.Get(5, 0)!.Value.Char);
        Assert.Equal((uint)'W', _buf.Get(6, 0)!.Value.Char);
    }

    [Fact]
    public void DrawTextBufferView_EmptyText_NoCrash()
    {
        using var tb = ManagedTextBuffer.Create();
        using var view = ManagedTextBufferView.Create(tb, 20, 5);

        tb.SetText("");
        _buf.Clear(Black);
        _buf.DrawTextBufferView(view, 0, 0);
        // No crash = pass
    }

    [Fact]
    public void DrawTextBufferView_MultipleLines()
    {
        using var tb = ManagedTextBuffer.Create();
        using var view = ManagedTextBufferView.Create(tb, 20, 5);

        tb.SetText("Line 1\nLine 2\nLine 3");
        _buf.Clear(Black);
        _buf.DrawTextBufferView(view, 0, 0);

        // Line 1 at row 0
        Assert.Equal((uint)'L', _buf.Get(0, 0)!.Value.Char);
        Assert.Equal((uint)'1', _buf.Get(5, 0)!.Value.Char);

        // Line 2 at row 1
        Assert.Equal((uint)'L', _buf.Get(0, 1)!.Value.Char);
        Assert.Equal((uint)'2', _buf.Get(5, 1)!.Value.Char);

        // Line 3 at row 2
        Assert.Equal((uint)'L', _buf.Get(0, 2)!.Value.Char);
        Assert.Equal((uint)'3', _buf.Get(5, 2)!.Value.Char);
    }

    [Fact]
    public void DrawTextBufferView_WithOffset()
    {
        using var tb = ManagedTextBuffer.Create();
        using var view = ManagedTextBufferView.Create(tb, 10, 3);

        tb.SetText("Hi");
        _buf.Clear(Black);
        _buf.DrawTextBufferView(view, 5, 2);

        // Before offset should be untouched (black background, space char)
        Assert.Equal(ManagedBuffer.DefaultSpaceChar, _buf.Get(0, 0)!.Value.Char);
        Assert.Equal(ManagedBuffer.DefaultSpaceChar, _buf.Get(4, 2)!.Value.Char);

        // At offset position (5, 2) we should see 'H'
        Assert.Equal((uint)'H', _buf.Get(5, 2)!.Value.Char);
        Assert.Equal((uint)'i', _buf.Get(6, 2)!.Value.Char);
    }

    #endregion

    #region Transparency

    [Fact]
    public void DrawTextBufferView_SpacePreservesUnderlyingNonSpaceChar()
    {
        using var tb = ManagedTextBuffer.Create();
        using var view = ManagedTextBufferView.Create(tb, 20, 5);

        var greenFg = new Rgba(0f, 1f, 0f, 1f);
        var blueBg = new Rgba(0f, 0f, 1f, 1f);

        // Pre-fill cell (1,0) with 'X'
        _buf.Clear(Black);
        _buf.DrawChar('X', 1, 0, greenFg, blueBg, (uint)TextAttributes.Bold);

        // Text has a space at position 1: "A A"
        tb.SetText("A A");
        _buf.DrawTextBufferView(view, 0, 0);

        // Cell (0,0): 'A' from text buffer
        Assert.Equal((uint)'A', _buf.Get(0, 0)!.Value.Char);

        // Cell (1,0): 'X' should be preserved (space with transparent bg)
        Assert.Equal((uint)'X', _buf.Get(1, 0)!.Value.Char);

        // Cell (2,0): 'A' from text buffer
        Assert.Equal((uint)'A', _buf.Get(2, 0)!.Value.Char);
    }

    #endregion

    #region Wide characters (CJK)

    [Fact]
    public void DrawTextBufferView_WideCharacter_WritesContinuationCell()
    {
        using var tb = ManagedTextBuffer.Create();
        using var view = ManagedTextBufferView.Create(tb, 20, 5);

        // CJK character '中' is width 2
        tb.SetText("中");
        _buf.Clear(Black);
        _buf.DrawTextBufferView(view, 0, 0);

        uint mainChar = _buf.Get(0, 0)!.Value.Char;
        uint contChar = _buf.Get(1, 0)!.Value.Char;

        Assert.Equal((uint)'中', mainChar);
        // Continuation cell has CharFlagContinuation set
        Assert.True((contChar & 0xC000_0000) == 0xC000_0000,
            $"Expected continuation flag, got 0x{contChar:X8}");
    }

    #endregion

    #region DrawEditorView

    [Fact]
    public void DrawEditorView_DelegatesToDrawTextBufferView()
    {
        using var eb = ManagedEditBuffer.Create();
        using var ev = ManagedEditorView.Create(eb, 20, 5);

        eb.InsertText("Hello");
        _buf.Clear(Black);
        _buf.DrawEditorView(ev, 0, 0);

        Assert.Equal((uint)'H', _buf.Get(0, 0)!.Value.Char);
        Assert.Equal((uint)'o', _buf.Get(4, 0)!.Value.Char);
    }

    #endregion

    #region Syntax highlighting

    [Fact]
    public void DrawTextBufferView_SyntaxHighlighting_AppliesColors()
    {
        using var tb = ManagedTextBuffer.Create();
        using var view = ManagedTextBufferView.Create(tb, 20, 5);

        var red = new Rgba(1f, 0f, 0f, 1f);
        var style = new ManagedSyntaxStyle();
        uint styleId = style.Register("keyword", fg: red);
        tb.SyntaxStyle = style;

        tb.SetText("Hello");
        // Style columns 0..4 (the whole word) with red foreground
        tb.SetStyleSpans(0, [new StyleSpan(0, 5, styleId)]);

        _buf.Clear(Black);
        _buf.DrawTextBufferView(view, 0, 0);

        var cell = _buf.Get(0, 0)!.Value;
        Assert.Equal((uint)'H', cell.Char);
        // Foreground should be red from syntax highlighting
        Assert.Equal(1f, cell.Fg.R);
        Assert.Equal(0f, cell.Fg.G);
        Assert.Equal(0f, cell.Fg.B);
    }

    #endregion

    #region Selection

    [Fact]
    public void DrawTextBufferView_Selection_SwapsFgBg()
    {
        using var tb = ManagedTextBuffer.Create();
        using var view = ManagedTextBufferView.Create(tb, 20, 5);

        tb.SetText("Hello");
        // Select entire text (offsets 0..5)
        var selBg = new Rgba(0f, 0f, 1f, 1f);
        var selFg = new Rgba(1f, 1f, 0f, 1f);
        view.SetSelection(0, 5, selBg, selFg);

        _buf.Clear(Black);
        _buf.DrawTextBufferView(view, 0, 0);

        var cell = _buf.Get(0, 0)!.Value;
        Assert.Equal((uint)'H', cell.Char);
        // Should have selection colors
        Assert.Equal(selFg.R, cell.Fg.R);
        Assert.Equal(selFg.G, cell.Fg.G);
        Assert.Equal(selBg.R, cell.Bg.R);
        Assert.Equal(selBg.B, cell.Bg.B);
    }

    #endregion

    #region Clipping

    [Fact]
    public void DrawTextBufferView_NegativeY_ClipsTopRows()
    {
        using var tb = ManagedTextBuffer.Create();
        using var view = ManagedTextBufferView.Create(tb, 20, 5);

        tb.SetText("Line 1\nLine 2\nLine 3");
        _buf.Clear(Black);
        _buf.DrawTextBufferView(view, 0, -1);

        // Line 2 (virtual line 1) should be at screen row 0
        Assert.Equal((uint)'L', _buf.Get(0, 0)!.Value.Char);
        Assert.Equal((uint)'2', _buf.Get(5, 0)!.Value.Char);

        // Line 3 at screen row 1
        Assert.Equal((uint)'L', _buf.Get(0, 1)!.Value.Char);
        Assert.Equal((uint)'3', _buf.Get(5, 1)!.Value.Char);
    }

    [Fact]
    public void DrawTextBufferView_ExceedsHeight_ClipsBottomRows()
    {
        using var buf = ManagedBuffer.Create(20, 2);
        using var tb = ManagedTextBuffer.Create();
        using var view = ManagedTextBufferView.Create(tb, 20, 5);

        tb.SetText("Line 1\nLine 2\nLine 3\nLine 4");
        buf.Clear(Black);
        buf.DrawTextBufferView(view, 0, 0);

        // Only 2 rows fit
        Assert.Equal((uint)'L', buf.Get(0, 0)!.Value.Char);
        Assert.Equal((uint)'1', buf.Get(5, 0)!.Value.Char);
        Assert.Equal((uint)'L', buf.Get(0, 1)!.Value.Char);
        Assert.Equal((uint)'2', buf.Get(5, 1)!.Value.Char);
    }

    #endregion
}
