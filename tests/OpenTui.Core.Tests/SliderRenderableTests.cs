using OpenTui.Core;
using Xunit;

namespace OpenTui.Core.Tests;

public sealed class SliderRenderableTests : IDisposable
{
    private readonly CliRenderer _renderer = CliRenderer.Create(new CliRendererConfig
    {
        Testing = true,
        Width = 80,
        Height = 24,
    });

    public void Dispose() => _renderer.Dispose();

    private void RenderFrame() => _renderer.RenderTestFrame();

    private static uint ReadCellChar(OptimizedBuffer buf, uint x, uint y) =>
        buf.GetCharAt(x, y);

    /// <summary>
    /// Regression: ViewPortSize setter used Math.Clamp(value, 0.01f, _max - _min)
    /// which throws ArgumentException when _max - _min &lt; 0.01 (e.g. both are 0 at startup).
    /// The fix uses Math.Max(0.01f, Math.Min(value, _max - _min)) to match the TS reference.
    /// </summary>
    [Fact]
    public void ViewPortSize_WhenMaxEqualsMin_DoesNotThrow()
    {
        var ctx = new TestRenderContext();
        var slider = new SliderRenderable(ctx, new SliderOptions
        {
            Orientation = SliderOrientation.Horizontal,
            Min = 0,
            Max = 0,
        });

        // Should not throw - defaults to 0.01f floor
        slider.ViewPortSize = 5;
        Assert.Equal(0.01f, slider.ViewPortSize);

        slider.Destroy();
    }

    [Fact]
    public void ViewPortSize_ClampsToRange()
    {
        var ctx = new TestRenderContext();
        var slider = new SliderRenderable(ctx, new SliderOptions
        {
            Orientation = SliderOrientation.Horizontal,
            Min = 0,
            Max = 100,
        });

        slider.ViewPortSize = 50;
        Assert.Equal(50f, slider.ViewPortSize);

        // Exceeds range → clamped to max - min
        slider.ViewPortSize = 200;
        Assert.Equal(100f, slider.ViewPortSize);

        // Below floor → clamped to 0.01f
        slider.ViewPortSize = 0;
        Assert.Equal(0.01f, slider.ViewPortSize);

        slider.Destroy();
    }

    [Fact]
    public void BufferedHorizontalSlider_RendersThumbAtScreenPosition()
    {
        var slider = new SliderRenderable(_renderer, new SliderOptions
        {
            Id = "buffered-horizontal-slider",
            Orientation = SliderOrientation.Horizontal,
            Min = 0,
            Max = 100,
            Value = 0,
            ViewPortSize = 50,
            Width = DimensionValue.Point(10),
            Height = DimensionValue.Point(1),
            MarginLeft = DimensionValue.Point(7),
            MarginTop = DimensionValue.Point(3),
            Buffered = true,
        });

        _renderer.Root.Add(slider);
        RenderFrame();

        Assert.True(slider.ScreenX > 0, "Expected buffered slider to render away from the origin.");
        Assert.Equal((uint)'█', ReadCellChar(_renderer.NextRenderBuffer, (uint)slider.ScreenX, (uint)slider.ScreenY));
    }

    [Fact]
    public void BufferedVerticalSlider_RendersThumbAtScreenPosition()
    {
        var slider = new SliderRenderable(_renderer, new SliderOptions
        {
            Id = "buffered-vertical-slider",
            Orientation = SliderOrientation.Vertical,
            Min = 0,
            Max = 100,
            Value = 0,
            ViewPortSize = 50,
            Width = DimensionValue.Point(2),
            Height = DimensionValue.Point(6),
            MarginLeft = DimensionValue.Point(9),
            MarginTop = DimensionValue.Point(4),
            Buffered = true,
        });

        _renderer.Root.Add(slider);
        RenderFrame();

        Assert.True(slider.ScreenY > 0, "Expected buffered slider to render away from the origin.");
        Assert.Equal((uint)'█', ReadCellChar(_renderer.NextRenderBuffer, (uint)slider.ScreenX, (uint)slider.ScreenY));
    }

    [Fact]
    public void MouseDown_OnTrack_UpdatesValueAndFocusesSlider()
    {
        var slider = new SliderRenderable(_renderer, new SliderOptions
        {
            Id = "interactive-horizontal-slider",
            Orientation = SliderOrientation.Horizontal,
            Min = 0,
            Max = 100,
            Value = 50,
            Width = DimensionValue.Point(20),
            Height = DimensionValue.Point(1),
            MarginLeft = DimensionValue.Point(5),
            MarginTop = DimensionValue.Point(2),
        });

        _renderer.Root.Add(slider);
        RenderFrame();

        slider.ProcessMouseEvent(new UiMouseEvent
        {
            Type = MouseEventType.Down,
            Button = (int)MouseButton.Left,
            X = (int)slider.ScreenX + 15,
            Y = (int)slider.ScreenY,
            Target = slider,
        });

        Assert.True(slider.Focused);
        Assert.Equal(75f, slider.Value, 1);
    }

    [Fact]
    public void MouseDrag_OnThumb_UpdatesHorizontalSliderValue()
    {
        var slider = new SliderRenderable(_renderer, new SliderOptions
        {
            Id = "drag-horizontal-slider",
            Orientation = SliderOrientation.Horizontal,
            Min = 0,
            Max = 100,
            Value = 0,
            Width = DimensionValue.Point(20),
            Height = DimensionValue.Point(1),
        });

        _renderer.Root.Add(slider);
        RenderFrame();

        int startX = (int)slider.ScreenX;
        int y = (int)slider.ScreenY;

        slider.ProcessMouseEvent(new UiMouseEvent
        {
            Type = MouseEventType.Down,
            Button = (int)MouseButton.Left,
            X = startX,
            Y = y,
            Target = slider,
        });

        slider.ProcessMouseEvent(new UiMouseEvent
        {
            Type = MouseEventType.Drag,
            Button = (int)MouseButton.Left,
            X = startX + 10,
            Y = y,
            Target = slider,
        });

        Assert.InRange(slider.Value, 53.5f, 54.5f);
    }

    [Fact]
    public void FocusedHorizontalSlider_RespondsToArrowKeys()
    {
        var slider = new SliderRenderable(_renderer, new SliderOptions
        {
            Id = "keyboard-horizontal-slider",
            Orientation = SliderOrientation.Horizontal,
            Min = 0,
            Max = 100,
            Value = 50,
            Width = DimensionValue.Point(20),
            Height = DimensionValue.Point(1),
        });

        _renderer.Root.Add(slider);
        slider.Focus();

        _renderer.InternalKeyInput.ProcessParsedKey(new ParsedKey
        {
            Name = "right",
            Sequence = "\u001b[C",
        });

        Assert.True(slider.Value > 50);
    }
}
