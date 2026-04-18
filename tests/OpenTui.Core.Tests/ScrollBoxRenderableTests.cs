using Facebook.Yoga;
using OpenTui.Core;
using Xunit;

namespace OpenTui.Core.Tests;

/// <summary>
/// Tests for ScrollBoxRenderable — scrolling, resize, and content constraints.
/// Regression tests for: arrow-key scrolling, resize layout, content change layout.
/// </summary>
public sealed class ScrollBoxRenderableTests : IDisposable
{
    private readonly CliRenderer _renderer;

    public ScrollBoxRenderableTests()
    {
        _renderer = CliRenderer.Create(new CliRendererConfig
        {
            Testing = true,
            Width = 80,
            Height = 24,
        });
    }

    public void Dispose() => _renderer.Dispose();

    /// <summary>Helper: render a frame so Yoga layout is computed and all renderables update.</summary>
    private void RenderFrame()
    {
        _renderer.RenderTestFrame();
    }

    #region Scroll Operations

    [Fact]
    public void ScrollBy_MovesContentAfterFirstRender()
    {
        // Regression: scrolling did nothing because scrollbar dimensions were stale
        // and scroll callbacks used Yoga Top instead of TranslateY.
        var scrollBox = new ScrollBoxRenderable(_renderer, new ScrollBoxOptions
        {
            Id = "scroll",
            ScrollY = true,
            ScrollX = false,
            FlexGrow = 1,
        });

        var content = new BoxRenderable(_renderer, new BoxOptions
        {
            Id = "tall-content",
            Height = DimensionValue.Point(100), // taller than viewport
        });
        scrollBox.Add(content);
        _renderer.Root.Add(scrollBox);

        // First render — layout is computed, scrollbar dimensions set via OnSizeChange
        RenderFrame();

        // Now scroll down — should NOT be clamped to 0
        scrollBox.ScrollBy(0, 5);

        Assert.True(scrollBox.ScrollTop > 0, "ScrollTop should be > 0 after scrolling down");
    }

    [Fact]
    public void ScrollBy_ClampsToMaxScroll()
    {
        var scrollBox = new ScrollBoxRenderable(_renderer, new ScrollBoxOptions
        {
            Id = "scroll-clamp",
            ScrollY = true,
            ScrollX = false,
            FlexGrow = 1,
        });

        var content = new BoxRenderable(_renderer, new BoxOptions
        {
            Id = "tall-content",
            Height = DimensionValue.Point(50),
        });
        scrollBox.Add(content);
        _renderer.Root.Add(scrollBox);

        RenderFrame();

        // Try to scroll way past the end
        scrollBox.ScrollBy(0, 9999);

        // ScrollTop should be clamped to content - viewport, not 9999
        float scrollHeight = scrollBox.ScrollHeight;
        float expected = Math.Max(0, scrollHeight - _renderer.Height);
        Assert.True(scrollBox.ScrollTop <= expected + 1,
            $"ScrollTop {scrollBox.ScrollTop} should be clamped near {expected}");
    }

    [Fact]
    public void ScrollBy_UsesTranslateY_NotYogaTop()
    {
        // Regression: scroll callbacks used Yoga Top which dirtied layout and caused issues.
        // Now they should use TranslateY.
        var scrollBox = new ScrollBoxRenderable(_renderer, new ScrollBoxOptions
        {
            Id = "scroll-translate",
            ScrollY = true,
            ScrollX = false,
            FlexGrow = 1,
        });

        var tallBox = new BoxRenderable(_renderer, new BoxOptions
        {
            Id = "tall",
            Height = DimensionValue.Point(100),
        });
        scrollBox.Add(tallBox);
        _renderer.Root.Add(scrollBox);

        RenderFrame();

        // Scroll down
        scrollBox.ScrollBy(0, 10);
        RenderFrame();

        // The internal content's TranslateY should be negative (shifted up)
        // We verify indirectly: ScrollTop should be 10, and a second render
        // should not reset it (would happen if Yoga Top was used and layout recalculated)
        var scrollTopAfterScroll = scrollBox.ScrollTop;
        Assert.Equal(10, scrollTopAfterScroll);

        RenderFrame();
        Assert.Equal(scrollTopAfterScroll, scrollBox.ScrollTop);
    }

    #endregion

    #region Content Constraints

    [Fact]
    public void Content_HasMinHeight100Percent_WhenScrollYEnabled()
    {
        // Regression: content lacked minHeight: 100% constraint matching TS reference.
        // This ensures the content is at least as tall as the viewport.
        var scrollBox = new ScrollBoxRenderable(_renderer, new ScrollBoxOptions
        {
            Id = "scroll-min",
            ScrollY = true,
            ScrollX = false,
            FlexGrow = 1,
        });

        // Add a small child — content should still fill viewport
        var smallBox = new BoxRenderable(_renderer, new BoxOptions
        {
            Id = "small",
            Height = DimensionValue.Point(2),
        });
        scrollBox.Add(smallBox);
        _renderer.Root.Add(scrollBox);

        RenderFrame();

        // ScrollHeight (content.Height) should be at least viewport height
        Assert.True(scrollBox.ScrollHeight >= _renderer.Height - 1,
            $"Content height {scrollBox.ScrollHeight} should be >= viewport height");
    }

    #endregion

    #region Code Content Change (layout dirty)

    [Fact]
    public void CodeRenderable_ContentChange_MarksYogaDirty()
    {
        // Regression: changing CodeRenderable.Content didn't mark Yoga dirty,
        // causing stale dimensions and text spilling into borders.
        var code = new CodeRenderable(_renderer, new CodeOptions
        {
            Id = "code",
            Content = "line1\nline2\nline3",
            FlexGrow = 1,
        });
        _renderer.Root.Add(code);

        // Render twice to fully stabilize layout
        RenderFrame();
        RenderFrame();

        // After stabilization, the node should be clean
        Assert.False(code.GetLayoutNode().IsDirty(),
            "Yoga node should be clean after render");

        // Change content — this MUST mark the Yoga node dirty so layout recalculates
        code.Content = string.Join("\n", Enumerable.Range(1, 50).Select(i => $"line {i}"));
        Assert.True(code.GetLayoutNode().IsDirty(),
            "Yoga node should be dirty after content change");
    }

    [Fact]
    public void TextRenderable_ContentChange_MarksYogaDirty()
    {
        var text = new TextRenderable(_renderer, new TextOptions
        {
            Id = "text",
            Content = "short",
        });
        _renderer.Root.Add(text);

        RenderFrame();

        text.ContentText = "a much longer text that will need more space to display";
        Assert.True(text.GetLayoutNode().IsDirty(),
            "Yoga node should be dirty after text content change");
    }

    #endregion

    #region CodeRenderable in ScrollBox

    [Fact]
    public void ScrollBy_WorksWithCodeRenderableChild()
    {
        // Reproduces CodeDemo: ScrollBox containing a CodeRenderable with many lines.
        // Scrolling with arrows should move the content.
        var scrollBox = new ScrollBoxRenderable(_renderer, new ScrollBoxOptions
        {
            Id = "scroll",
            ScrollY = true,
            ScrollX = false,
            FlexGrow = 1,
        });

        // Multi-line code content that should be taller than the 24-row viewport
        var lines = string.Join("\n", Enumerable.Range(1, 50).Select(i => $"const line{i} = {i};"));
        var code = new CodeRenderable(_renderer, new CodeOptions
        {
            Id = "code",
            Content = lines,
            FlexGrow = 1,
        });
        scrollBox.Add(code);
        _renderer.Root.Add(scrollBox);

        // Render to compute layout
        RenderFrame();

        // Diagnostic: check dimensions
        var scrollHeight = scrollBox.ScrollHeight;
        var viewportHeight = _renderer.Height;

        Assert.True(scrollHeight > viewportHeight,
            $"ScrollHeight ({scrollHeight}) should exceed viewport ({viewportHeight}) for 50-line content");

        // Now try scrolling — this is what the arrow keys do in CodeDemo
        scrollBox.ScrollBy(0, 1);

        Assert.True(scrollBox.ScrollTop > 0,
            $"ScrollTop should be > 0 after scrolling down, but was {scrollBox.ScrollTop}. " +
            $"ScrollHeight={scrollHeight}, ViewportHeight={viewportHeight}");
    }

    #endregion

    #region Resize

    [Fact]
    public void Resize_UpdatesRootAndTriggersLayout()
    {
        _renderer.Root.Add(new BoxRenderable(_renderer, new BoxOptions
        {
            Id = "fill",
            FlexGrow = 1,
        }));

        RenderFrame();

        _renderer.Resize(120, 40);
        Assert.Equal(120, _renderer.Width);
        Assert.Equal(40, _renderer.Height);

        RenderFrame();

        // Root should match the new size
        Assert.Equal(120, _renderer.Root.Width);
        Assert.Equal(40, _renderer.Root.Height);
    }

    #endregion
}
