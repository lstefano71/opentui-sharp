using OpenTui.Core;
using Xunit;

namespace OpenTui.Core.Tests;

public sealed class TextareaRenderableTests : IDisposable
{
    private readonly CliRenderer _renderer;

    public TextareaRenderableTests()
    {
        _renderer = CliRenderer.Create(new CliRendererConfig
        {
            Testing = true,
            Width = 80,
            Height = 24,
        });
    }

    public void Dispose() => _renderer.Dispose();

    private void RenderFrame() => _renderer.RenderTestFrame();

    private static unsafe uint ReadCellChar(OptimizedBuffer buf, uint x, uint y)
    {
        nint charPtr = buf.GetCharPtr();
        int offset = (int)(y * buf.Width + x);
        return ((uint*)charPtr)[offset];
    }

    private static string ReadRowText(OptimizedBuffer buf, int x, int y, int width)
    {
        var chars = new char[width];
        for (int i = 0; i < width; i++)
        {
            uint codepoint = ReadCellChar(buf, (uint)(x + i), (uint)y);
            chars[i] = codepoint is > 0 and <= char.MaxValue ? (char)codepoint : ' ';
        }

        return new string(chars);
    }

    [Fact]
    public void TextareaRenderable_ShiftRight_CreatesSelectionAndSelectedText()
    {
        var textarea = new TextareaRenderable(_renderer, new TextareaOptions
        {
            Id = "textarea-selection",
            Width = DimensionValue.Point(12),
            Height = DimensionValue.Point(4),
            InitialValue = "abc",
            SelectionBg = Rgba.FromHex("#264F78"),
            SelectionFg = Rgba.FromHex("#FFFFFF"),
        });

        _renderer.Root.Add(textarea);
        textarea.Focus();
        textarea.GotoBufferHome();

        _renderer.InternalKeyInput.ProcessParsedKey(new ParsedKey
        {
            Name = "right",
            Sequence = "\u001b[C",
            Shift = true,
        });

        Assert.True(textarea.HasSelection());
        Assert.Equal("a", textarea.GetSelectedText());
    }

    [Fact]
    public void TextareaRenderable_DeleteToLineStart_AtColumnZero_JoinsPreviousLine()
    {
        var textarea = new TextareaRenderable(_renderer, new TextareaOptions
        {
            Id = "textarea-delete-line-start",
            Width = DimensionValue.Point(12),
            Height = DimensionValue.Point(4),
            InitialValue = "ab\ncd",
        });

        _renderer.Root.Add(textarea);
        textarea.EditBuffer.SetCursor(1, 0);

        textarea.DeleteToLineStart();

        Assert.Equal("abcd", textarea.GetText());
    }

    [Fact]
    public void TextareaRenderable_ProvidesLogicalLineInfo_ForLineNumberRenderable()
    {
        var textarea = new TextareaRenderable(_renderer, new TextareaOptions
        {
            Id = "textarea-lines",
            Width = DimensionValue.Point(10),
            Height = DimensionValue.Point(4),
            InitialValue = "one two three four",
            WrapMode = 2,
        });

        var lineNumbers = new LineNumberRenderable(_renderer, new LineNumberOptions
        {
            Id = "textarea-gutter",
            Target = textarea,
            Width = DimensionValue.Percent(100),
            Height = DimensionValue.Percent(100),
        });

        _renderer.Root.Add(lineNumbers);
        RenderFrame();

        var lineInfo = textarea.GetCachedLineInfo();
        Assert.NotNull(lineInfo);
        Assert.NotEmpty(lineInfo!.LineSources);
    }

    [Fact]
    public void TextareaRenderable_WithPlaceholder_RendersWithoutCrash()
    {
        var textarea = new TextareaRenderable(_renderer, new TextareaOptions
        {
            Id = "textarea-placeholder",
            Width = DimensionValue.Point(10),
            Height = DimensionValue.Point(4),
            Placeholder = "Enter text here...",
            PlaceholderColor = Rgba.FromHex("#333333"),
        });

        _renderer.Root.Add(textarea);

        RenderFrame();

        Assert.Equal(string.Empty, textarea.GetText());
    }

    [Fact]
    public void LineNumberRenderable_ShowLineNumbersFalse_CollapsesGutterWidth()
    {
        var textarea = new TextareaRenderable(_renderer, new TextareaOptions
        {
            Id = "textarea-toggle-lines",
            Width = DimensionValue.Point(10),
            Height = DimensionValue.Point(4),
            InitialValue = "one\ntwo\nthree",
        });

        var lineNumbers = new LineNumberRenderable(_renderer, new LineNumberOptions
        {
            Id = "textarea-toggle-gutter",
            Target = textarea,
            Width = DimensionValue.Percent(100),
            Height = DimensionValue.Percent(100),
        });

        _renderer.Root.Add(lineNumbers);
        RenderFrame();

        var gutter = Assert.Single(lineNumbers.GetChildren().Where(child => !ReferenceEquals(child, textarea)));
        Assert.True(gutter.Width > 0);
        Assert.Contains('1', ReadRowText(_renderer.NextRenderBuffer, (int)gutter.ScreenX, (int)gutter.ScreenY, gutter.Width));

        lineNumbers.ShowLineNumbers = false;
        RenderFrame();

        Assert.DoesNotContain('1', ReadRowText(_renderer.NextRenderBuffer, (int)gutter.ScreenX, (int)gutter.ScreenY, gutter.Width));
    }
}
