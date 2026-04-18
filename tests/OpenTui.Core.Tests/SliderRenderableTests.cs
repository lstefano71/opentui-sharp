using OpenTui.Core;
using Xunit;

namespace OpenTui.Core.Tests;

public class SliderRenderableTests
{
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
}
