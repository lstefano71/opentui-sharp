using Xunit;

namespace OpenTui.Core.Tests;

public sealed class TextRenderableSelectionTests : IDisposable
{
    private readonly CliRenderer _renderer;

    public TextRenderableSelectionTests()
    {
        _renderer = CliRenderer.Create(new CliRendererConfig
        {
            Testing = true,
            Width = 40,
            Height = 8,
        });
    }

    public void Dispose() => _renderer.Dispose();

    [Fact]
    public void TextRenderable_OnSelectionChanged_UsesTextBufferViewSelection()
    {
        var text = new TextRenderable(_renderer, new TextOptions
        {
            Content = "Hello World",
            SelectionFg = Rgba.White,
            SelectionBg = Rgba.Black,
        });
        _renderer.Root.Add(text);
        _renderer.RenderTestFrame();

        var selection = new Selection(text, 0, 0) { IsStart = true };
        text.OnSelectionChanged(selection);

        selection.IsStart = false;
        selection.UpdateFocus(5, 0);
        Assert.True(text.OnSelectionChanged(selection));

        Assert.True(text.HasSelection());
        Assert.Equal("Hello", text.GetSelectedText());

        text.OnSelectionChanged(null);
        Assert.False(text.HasSelection());
        Assert.Equal(string.Empty, text.GetSelectedText());
    }
}
