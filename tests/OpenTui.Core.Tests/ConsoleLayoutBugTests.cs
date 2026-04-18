using Facebook.Yoga;
using OpenTui.Core;
using Xunit;

namespace OpenTui.Core.Tests;

public class ConsoleLayoutBugTests : IDisposable
{
    private readonly CliRenderer _renderer;

    public ConsoleLayoutBugTests()
    {
        _renderer = CliRenderer.Create(new CliRendererConfig
        {
            Testing = true,
            Width = 80,
            Height = 40,
        });
    }

    public void Dispose() => _renderer.Dispose();

    private void RenderFrame() => _renderer.RenderTestFrame();

    [Fact]
    public void TextRenderable_StaticLayout_IsCorrect()
    {
        // Content set before first render: layout works correctly
        var logArea = new BoxRenderable(_renderer, new BoxOptions
        {
            Id = "log-area",
            FlexGrow = 1,
            Border = true,
            FlexDirection = FlexDirectionValue.Column,
            Overflow = OverflowValue.Hidden,
        });
        var logText = new TextRenderable(_renderer, new TextOptions
        {
            Id = "log-text",
            Content = string.Join("\n", Enumerable.Range(0, 50).Select(i => $"Line {i}")),
            WrapMode = WrapMode.Char,
        });
        logArea.Add(logText);
        _renderer.Root.Add(logArea);

        RenderFrame();

        // 40-row terminal, logArea=40, inner=38 (borders)
        Assert.Equal(40, logArea.Height);
        Assert.Equal(38, logText.Height);
        Assert.Equal(50u, logText.LineCount);
    }

    [Fact]
    public void TextRenderable_DynamicContentUpdate_RecalculatesHeight()
    {
        // Regression: dynamic content changes must trigger Yoga re-measurement.
        // Previously, UpdateFromLayout was skipped on subsequent frames because
        // tests didn't advance _frameId.
        var logArea = new BoxRenderable(_renderer, new BoxOptions
        {
            Id = "log-area",
            FlexGrow = 1,
            Border = true,
            FlexDirection = FlexDirectionValue.Column,
            Overflow = OverflowValue.Hidden,
        });
        var logText = new TextRenderable(_renderer, new TextOptions
        {
            Id = "log-text",
            Content = "",
            WrapMode = WrapMode.Char,
        });
        logArea.Add(logText);
        _renderer.Root.Add(logArea);

        RenderFrame();
        int heightBefore = logText.Height;

        // Add 50 lines (like the console demo does incrementally)
        logText.ContentText = string.Join("\n", Enumerable.Range(0, 50).Select(i => $"Line {i}"));
        RenderFrame();

        // logArea=40, inner=38 (borders). Text should fill the parent, not stay at 1.
        Assert.Equal(1, heightBefore);
        Assert.Equal(38, logText.Height);
    }

    [Fact]
    public void TextRenderable_MaxScrollY_IsCorrectAfterDynamicUpdate()
    {
        // The viewport height determines MaxScrollY.
        // With 50 lines of content in a 38-tall viewport, MaxScrollY should be positive.
        var logArea = new BoxRenderable(_renderer, new BoxOptions
        {
            Id = "log-area",
            FlexGrow = 1,
            Border = true,
            FlexDirection = FlexDirectionValue.Column,
            Overflow = OverflowValue.Hidden,
        });
        var logText = new TextRenderable(_renderer, new TextOptions
        {
            Id = "log-text",
            Content = "",
            WrapMode = WrapMode.Char,
        });
        logArea.Add(logText);
        _renderer.Root.Add(logArea);

        RenderFrame();

        logText.ContentText = string.Join("\n", Enumerable.Range(0, 50).Select(i => $"Line {i}"));
        RenderFrame();

        // 50 virtual lines - 38 viewport height = 12
        Assert.True(logText.MaxScrollY > 0,
            $"MaxScrollY should be positive for overflow content, got {logText.MaxScrollY}");
        Assert.Equal(12, logText.MaxScrollY);
    }

    [Fact]
    public void TextRenderable_ConsoleLayout_MatchesDemo()
    {
        // Full console demo layout: header(3) + logArea(flex) + buttonRow(3) + footer(3)
        var header = new BoxRenderable(_renderer, new BoxOptions
        {
            Id = "header",
            Height = DimensionValue.Point(3),
            Border = true,
        });
        var logArea = new BoxRenderable(_renderer, new BoxOptions
        {
            Id = "log-area",
            FlexGrow = 1,
            Border = true,
            FlexDirection = FlexDirectionValue.Column,
            Overflow = OverflowValue.Hidden,
        });
        var logText = new TextRenderable(_renderer, new TextOptions
        {
            Id = "log-text",
            Content = "",
            WrapMode = WrapMode.Char,
        });
        logArea.Add(logText);
        var buttonRow = new BoxRenderable(_renderer, new BoxOptions
        {
            Id = "button-row",
            Height = DimensionValue.Point(3),
        });
        var footer = new BoxRenderable(_renderer, new BoxOptions
        {
            Id = "footer",
            Height = DimensionValue.Point(3),
            Border = true,
        });
        _renderer.Root.Add(header);
        _renderer.Root.Add(logArea);
        _renderer.Root.Add(buttonRow);
        _renderer.Root.Add(footer);

        // Add 50 lines of log content
        logText.ContentText = string.Join("\n", Enumerable.Range(0, 50).Select(i => $"Line {i}"));
        RenderFrame();

        // Root=40, header=3, buttonRow=3, footer=3 → logArea=31, inner=29
        Assert.Equal(31, logArea.Height);
        Assert.Equal(29, logText.Height);
        Assert.True(logText.MaxScrollY > 0,
            $"MaxScrollY should be positive, got {logText.MaxScrollY}");
    }

    [Fact]
    public void TextRenderable_ScrollSetBeforeLayout_IsCorrectedOnResize()
    {
        // Regression: setting ScrollY = MaxScrollY before the first layout used
        // _heightValue = 0, producing an oversized scroll offset that hid all content.
        // OnResize must clamp the scroll position to the valid range.
        var logArea = new BoxRenderable(_renderer, new BoxOptions
        {
            Id = "log-area",
            FlexGrow = 1,
            Border = true,
            FlexDirection = FlexDirectionValue.Column,
            Overflow = OverflowValue.Hidden,
        });
        var logText = new TextRenderable(_renderer, new TextOptions
        {
            Id = "log-text",
            Content = "",
            WrapMode = WrapMode.Char,
        });
        logArea.Add(logText);
        _renderer.Root.Add(logArea);

        // Simulate the ConsoleDemo pattern: set content + scroll BEFORE first layout
        logText.ContentText = "Line 1\nLine 2";
        logText.ScrollY = logText.MaxScrollY;

        RenderFrame();

        // After layout, OnResize should have clamped scroll to valid range.
        // With 2 lines of content, height=2 (content size, smaller than viewport).
        // MaxScrollY = 0, so ScrollY should be clamped to 0.
        Assert.Equal(2, logText.Height);
        Assert.Equal(0, logText.MaxScrollY);
        Assert.Equal(0, logText.ScrollY);
    }

    [Fact]
    public void TextRenderable_FlexShrink_DoesNotCollapseSiblingRows()
    {
        var panel = new BoxRenderable(_renderer, new BoxOptions
        {
            Id = "panel",
            Height = DimensionValue.Point(2),
            FlexDirection = FlexDirectionValue.Column,
        });

        var line1 = new TextRenderable(_renderer, new TextOptions { Id = "line-1", Content = "A" });
        var line2 = new TextRenderable(_renderer, new TextOptions { Id = "line-2", Content = "B" });
        var line3 = new TextRenderable(_renderer, new TextOptions { Id = "line-3", Content = "C" });

        panel.Add(line1);
        panel.Add(line2);
        panel.Add(line3);
        _renderer.Root.Add(panel);

        RenderFrame();

        Assert.True(line2.ScreenY > line1.ScreenY, $"Expected line2 below line1, got {line1.ScreenY} and {line2.ScreenY}");
        Assert.True(line3.ScreenY > line2.ScreenY, $"Expected line3 below line2, got {line2.ScreenY} and {line3.ScreenY}");
    }
}
