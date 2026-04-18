using OpenTui.Core;
using Xunit;

namespace OpenTui.Core.Tests;

public sealed class ASCIIFontRenderableTests : IDisposable
{
    private readonly CliRenderer _renderer;

    public ASCIIFontRenderableTests()
    {
        _renderer = CliRenderer.Create(new CliRendererConfig
        {
            Testing = true,
            Width = 120,
            Height = 40,
        });
    }

    public void Dispose() => _renderer.Dispose();

    private void RenderFrame() => _renderer.RenderTestFrame();

    private static unsafe uint ReadCellChar(OptimizedBuffer buffer, uint x, uint y)
    {
        nint charPtr = buffer.GetCharPtr();
        int offset = (int)(y * buffer.Width + x);
        return ((uint*)charPtr)[offset];
    }

    private static unsafe Rgba ReadCellFg(OptimizedBuffer buffer, uint x, uint y)
    {
        nint fgPtr = buffer.GetFgPtr();
        int floatOff = (int)(y * buffer.Width + x) * 4;
        float* fp = (float*)fgPtr;
        return new Rgba(fp[floatOff], fp[floatOff + 1], fp[floatOff + 2], fp[floatOff + 3]);
    }

    private static unsafe Rgba ReadCellBg(OptimizedBuffer buffer, uint x, uint y)
    {
        nint bgPtr = buffer.GetBgPtr();
        int floatOff = (int)(y * buffer.Width + x) * 4;
        float* fp = (float*)bgPtr;
        return new Rgba(fp[floatOff], fp[floatOff + 1], fp[floatOff + 2], fp[floatOff + 3]);
    }

    [Fact]
    public void MeasureText_EmptyStringKeepsFontHeight()
    {
        Assert.Equal((0, 2), ASCIIFontRenderable.MeasureText("", "tiny"));
        Assert.Equal((0, 6), ASCIIFontRenderable.MeasureText("", "block"));
        Assert.Equal((0, 8), ASCIIFontRenderable.MeasureText("", "shade"));
    }

    [Fact]
    public void CharacterPositions_AndCoordinateMappingMatchUpstreamSpacing()
    {
        Assert.Equal([0, 4, 7], ASCIIFontRenderable.GetCharacterPositions("AB", "tiny"));

        Assert.Equal(0, ASCIIFontRenderable.CoordinateToCharacterIndex(-1, "AB", "tiny"));
        Assert.Equal(0, ASCIIFontRenderable.CoordinateToCharacterIndex(0, "AB", "tiny"));
        Assert.Equal(1, ASCIIFontRenderable.CoordinateToCharacterIndex(2, "AB", "tiny"));
        Assert.Equal(1, ASCIIFontRenderable.CoordinateToCharacterIndex(4, "AB", "tiny"));
        Assert.Equal(2, ASCIIFontRenderable.CoordinateToCharacterIndex(7, "AB", "tiny"));
    }

    [Fact]
    public void RenderToBuffer_UsesTaggedFontColorIndices()
    {
        using var buffer = OptimizedBuffer.Create(20, 8, id: "ascii-font-colors");
        var red = Rgba.FromHex("#ff0000");
        var blue = Rgba.FromHex("#0000ff");
        var bg = Rgba.FromHex("#101820");

        AsciiFont.RenderToBuffer(buffer, new AsciiFontRenderOptions
        {
            Text = "A",
            Font = "block",
            Colors = [red, blue],
            BackgroundColor = bg,
        });

        Assert.Equal('█', (char)ReadCellChar(buffer, 1, 0));
        Assert.Equal('╗', (char)ReadCellChar(buffer, 6, 0));
        Assert.Equal(red, ReadCellFg(buffer, 1, 0));
        Assert.Equal(blue, ReadCellFg(buffer, 6, 0));
        Assert.Equal(bg, ReadCellBg(buffer, 1, 0));
    }

    [Fact]
    public void SelectionHighlight_RerendersSelectedGlyphRange()
    {
        var selectionBg = Rgba.FromHex("#4a5568");
        var selectionFg = Rgba.FromHex("#ffffff");

        var ascii = new ASCIIFontRenderable(_renderer, new ASCIIFontOptions
        {
            Id = "ascii-selection",
            Text = "ABC",
            Font = "tiny",
            Color = Rgba.FromHex("#f59e0b"),
            BackgroundColor = Rgba.FromHex("#000028"),
            SelectionBg = selectionBg,
            SelectionFg = selectionFg,
        });

        _renderer.Root.Add(ascii);
        RenderFrame();

        int[] positions = ASCIIFontRenderable.GetCharacterPositions("ABC", "tiny");
        var selection = new Selection(ascii, ascii.X, ascii.Y);
        selection.UpdateFocus(ascii.X + positions[1], ascii.Y);

        Assert.True(ascii.OnSelectionChanged(selection));
        RenderFrame();

        var buffer = ascii.Buffer!;
        Assert.True(ascii.HasSelection());
        Assert.Equal("A", ascii.GetSelectedText());
        Assert.Equal(selectionFg, ReadCellFg(buffer, 0, 0));
        Assert.Equal(selectionBg, ReadCellBg(buffer, 0, 0));
    }
}
