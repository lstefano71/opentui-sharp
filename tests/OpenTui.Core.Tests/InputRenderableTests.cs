using OpenTui.Core;
using Xunit;

namespace OpenTui.Core.Tests;

public sealed class InputRenderableTests : IDisposable
{
    private readonly CliRenderer _renderer;

    public InputRenderableTests()
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

    private static Rgba ReadCellFg(OptimizedBuffer buf, uint x, uint y) =>
        buf.GetFgAt(x, y);

    private static uint ReadCellChar(OptimizedBuffer buf, uint x, uint y) =>
        buf.GetCharAt(x, y);

    [Fact]
    public void InputRenderable_PreservesWidthWhileForcingSingleLineHeight()
    {
        var input = new InputRenderable(_renderer, new InputOptions
        {
            Id = "input",
            Width = DimensionValue.Point(20),
            Height = DimensionValue.Point(4),
            Value = "hello",
        });

        _renderer.Root.Add(input);
        RenderFrame();

        Assert.Equal(20, input.Width);
        Assert.Equal(1, input.Height);
        Assert.True(input.WidthDimension.IsPoint);
        Assert.Equal(20, input.WidthDimension.Value);
    }

    [Fact]
    public void InputRenderable_RendersTextWithConfiguredForegroundColor()
    {
        var textColor = Rgba.FromHex("#e2e8f0");
        var backgroundColor = Rgba.FromHex("#111827");

        var input = new InputRenderable(_renderer, new InputOptions
        {
            Id = "input-colored",
            Width = DimensionValue.Point(12),
            Placeholder = "Enter text...",
            TextColor = textColor,
            BackgroundColor = backgroundColor,
            FocusedTextColor = textColor,
            FocusedBackgroundColor = backgroundColor,
        });

        _renderer.Root.Add(input);
        input.Focus();
        input.InsertText("A");
        RenderFrame();

        var fg = ReadCellFg(_renderer.NextRenderBuffer, (uint)input.ScreenX, (uint)input.ScreenY);
        Assert.Equal(textColor.R, fg.R, 3);
        Assert.Equal(textColor.G, fg.G, 3);
        Assert.Equal(textColor.B, fg.B, 3);
    }

    [Fact]
    public void InputRenderable_RendersTypedTextWithDefaultForegroundColor()
    {
        var input = new InputRenderable(_renderer, new InputOptions
        {
            Id = "input-default-color",
            Width = DimensionValue.Point(12),
            Placeholder = "Enter text...",
        });

        _renderer.Root.Add(input);
        input.Focus();
        input.InsertText("A");
        RenderFrame();

        var fg = ReadCellFg(_renderer.NextRenderBuffer, (uint)input.ScreenX, (uint)input.ScreenY);
        Assert.True(fg.R > 0.9f && fg.G > 0.9f && fg.B > 0.9f,
            $"Expected default input text to render light/white, but got ({fg.R:F2}, {fg.G:F2}, {fg.B:F2}).");
    }

    [Fact]
    public void BufferedInputRenderable_RendersTypedTextAtScreenPosition()
    {
        var textColor = Rgba.FromHex("#e2e8f0");
        var backgroundColor = Rgba.FromHex("#111827");

        var input = new InputRenderable(_renderer, new InputOptions
        {
            Id = "buffered-input-colored",
            Width = DimensionValue.Point(12),
            MarginLeft = DimensionValue.Point(5),
            MarginTop = DimensionValue.Point(2),
            TextColor = textColor,
            BackgroundColor = backgroundColor,
            FocusedTextColor = textColor,
            FocusedBackgroundColor = backgroundColor,
            Buffered = true,
        });

        _renderer.Root.Add(input);
        input.Focus();
        input.InsertText("A");
        RenderFrame();

        Assert.True(input.ScreenX > 0, "Expected buffered input to render away from the origin.");
        var x = (uint)input.ScreenX;
        var y = (uint)input.ScreenY;

        Assert.Equal((uint)'A', ReadCellChar(_renderer.NextRenderBuffer, x, y));

        var fg = ReadCellFg(_renderer.NextRenderBuffer, x, y);
        Assert.Equal(textColor.R, fg.R, 3);
        Assert.Equal(textColor.G, fg.G, 3);
        Assert.Equal(textColor.B, fg.B, 3);
    }

    [Fact]
    public void InputRenderable_ReturnKey_EmitsEnterAndDoesNotInsertNewline()
    {
        var input = new InputRenderable(_renderer, new InputOptions
        {
            Id = "input-submit",
            Width = DimensionValue.Point(12),
            Value = "abc",
        });

        string? entered = null;
        input.On<string>(InputRenderable.Events.Enter, value => entered = value);

        _renderer.Root.Add(input);
        input.Focus();

        _renderer.InternalKeyInput.ProcessParsedKey(new ParsedKey
        {
            Name = "return",
            Sequence = "\r",
        });

        Assert.Equal("abc", input.Value);
        Assert.Equal("abc", entered);
    }

    [Fact]
    public void InputRenderable_Keypress_EmitsInputAndUpdatesValue()
    {
        var input = new InputRenderable(_renderer, new InputOptions
        {
            Id = "input-keypress",
            Width = DimensionValue.Point(12),
        });

        string? lastValue = null;
        input.On<string>(InputRenderable.Events.Input, value => lastValue = value);

        _renderer.Root.Add(input);
        input.Focus();

        _renderer.InternalKeyInput.ProcessParsedKey(new ParsedKey
        {
            Name = "l",
            Sequence = "l",
        });

        Assert.Equal("l", input.Value);
        Assert.Equal("l", lastValue);
    }

    [Fact]
    public void InputRenderable_Backspace_DeletesTextAndEmitsInput()
    {
        var input = new InputRenderable(_renderer, new InputOptions
        {
            Id = "input-backspace",
            Width = DimensionValue.Point(12),
            Value = "lime",
        });

        string? lastValue = null;
        input.On<string>(InputRenderable.Events.Input, value => lastValue = value);

        _renderer.Root.Add(input);
        input.Focus();

        _renderer.InternalKeyInput.ProcessParsedKey(new ParsedKey
        {
            Name = "backspace",
            Sequence = "\b",
        });

        Assert.Equal("lim", input.Value);
        Assert.Equal("lim", lastValue);
    }
}
