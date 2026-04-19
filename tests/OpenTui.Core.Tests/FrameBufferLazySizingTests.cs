using OpenTui.Core;
using Xunit;

namespace OpenTui.Core.Tests;

/// <summary>
/// Regression tests for the FullUnicode background-rendering bug.
///
/// Root cause: a FrameBufferRenderable sized with DimensionValue.Percent(...)
/// cannot allocate its private buffer until Yoga assigns concrete dimensions.
/// Frame callbacks fire BEFORE layout on the first frame, so
/// <c>background.Buffer</c> is null and the user's draw silently no-ops.
/// <c>needsRedraw</c> is then cleared, so the background never paints —
/// until an unrelated MarkDirty (vignette toggle, drag) runs, at which point
/// the buffer exists and the wallpaper suddenly floods the screen.
///
/// Correct fix (matching the upstream TS/Zig reference): use Point dimensions
/// equal to the current terminal size. The private buffer is allocated
/// immediately in the constructor, so the very first frame callback succeeds.
/// On terminal resize the caller updates the dimensions and calls MarkDirty.
/// </summary>
public sealed class FrameBufferLazySizingTests : IDisposable
{
    private readonly CliRenderer _renderer;

    public FrameBufferLazySizingTests()
    {
        _renderer = CliRenderer.Create(new CliRendererConfig
        {
            Testing = true,
            Width = 40,
            Height = 8,
        });
    }

    public void Dispose() => _renderer.Dispose();

    // -------------------------------------------------------------------------
    // Helper
    // -------------------------------------------------------------------------

    private static FrameBufferRenderable AddBackground(
        CliRenderer renderer, DimensionValue w, DimensionValue h)
    {
        var root = new BoxRenderable(renderer, new BoxOptions
        {
            Id = "root",
            Width = DimensionValue.Percent(100),
            Height = DimensionValue.Percent(100),
        });
        renderer.Root.Add(root);

        var bg = new FrameBufferRenderable(renderer, new FrameBufferOptions
        {
            Id = "bg",
            Position = PositionValue.Absolute,
            Left = 0,
            Top = 0,
            Width = w,
            Height = h,
            RespectAlpha = false,
        });
        root.Add(bg);
        return bg;
    }

    private static void FillWithMarker(FrameBufferRenderable bg, char marker)
    {
        var b = bg.Buffer;
        if (b is null) return;
        var color = Rgba.FromInts(0, 17, 34, 255);
        b.Clear(color);
        var line = new string(marker, (int)b.Width);
        b.DrawText(line, 0, 0, Rgba.FromInts(220, 220, 220, 255), color);
    }

    private static unsafe bool RowContainsCodepoint(OptimizedBuffer buf, uint y, uint cp)
    {
        var ptr = (uint*)buf.GetCharPtr();
        for (uint x = 0; x < buf.Width; x++)
            if (ptr[y * buf.Width + x] == cp) return true;
        return false;
    }

    // -------------------------------------------------------------------------
    // Bug documentation: Percent sizing, no Resize hook — buffer is blank
    // -------------------------------------------------------------------------

    [Fact]
    public void PercentSized_WithoutResizeHook_BufferIsBlankOnFirstFrame()
    {
        var bg = AddBackground(_renderer, DimensionValue.Percent(100), DimensionValue.Percent(100));

        bool needsRedraw = true;
        void FrameCallback()
        {
            if (!needsRedraw) return;
            FillWithMarker(bg, '#');
            needsRedraw = false;
        }

        FrameCallback();
        _renderer.RenderTestFrame();

        Assert.False(RowContainsCodepoint(_renderer.NextRenderBuffer, 0, '#'),
            "Percent-sized background with no Resize hook is blank on F1 (documents the bug).");
    }

    // -------------------------------------------------------------------------
    // Correct pattern: Point dimensions — buffer allocated at construction
    // -------------------------------------------------------------------------

    [Fact]
    public void PointSized_BufferAvailableImmediately()
    {
        var bg = AddBackground(_renderer, _renderer.TerminalWidth, _renderer.TerminalHeight);

        Assert.NotNull(bg.Buffer);
        Assert.Equal((uint)_renderer.TerminalWidth, bg.Buffer!.Width);
    }

    [Fact]
    public void PointSized_ContentVisibleOnFirstFrame()
    {
        var bg = AddBackground(_renderer, _renderer.TerminalWidth, _renderer.TerminalHeight);

        bool needsRedraw = true;
        void FrameCallback()
        {
            if (!needsRedraw) return;
            FillWithMarker(bg, '#');
            needsRedraw = false;
        }

        // Frame 1: callback runs BEFORE layout — but Buffer is non-null because
        // the Point dimension was set at construction, so drawing succeeds.
        FrameCallback();
        _renderer.RenderTestFrame();

        Assert.True(RowContainsCodepoint(_renderer.NextRenderBuffer, 0, '#'),
            "Point-sized background should be visible on the first frame.");
    }

    [Fact]
    public void PointSized_ContentPersistsAfterMarkDirty()
    {
        var bg = AddBackground(_renderer, _renderer.TerminalWidth, _renderer.TerminalHeight);

        bool needsRedraw = true;
        void Mark() { needsRedraw = true; }
        void FrameCallback()
        {
            if (!needsRedraw) return;
            FillWithMarker(bg, '#');
            needsRedraw = false;
        }

        // Initial paint
        FrameCallback(); _renderer.RenderTestFrame();

        // Simulate vignette toggle / drag — re-armed MarkDirty
        Mark();
        FrameCallback(); _renderer.RenderTestFrame();

        Assert.True(RowContainsCodepoint(_renderer.NextRenderBuffer, 0, '#'),
            "Background content must persist after a MarkDirty-driven redraw.");
    }
}
