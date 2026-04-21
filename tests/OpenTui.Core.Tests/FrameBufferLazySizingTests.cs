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

    private static uint ReadCellChar(OptimizedBuffer buf, uint x, uint y) =>
        buf.GetCharAt(x, y);

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

    private static bool RowContainsCodepoint(OptimizedBuffer buf, uint y, uint cp)
    {
        for (uint x = 0; x < buf.Width; x++)
            if (ReadCellChar(buf, x, y) == cp) return true;
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

    [Fact]
    public void TransparentFrameBuffer_SkipsFullyTransparentCells()
    {
        var underlay = new TextRenderable(_renderer, new TextOptions
        {
            Id = "underlay",
            Position = PositionValue.Absolute,
            Left = 0,
            Top = 0,
            Content = "AAAA",
            Fg = Rgba.White,
            Bg = Rgba.FromInts(0, 0, 0, 255),
            ZIndex = 0,
        });
        _renderer.Root.Add(underlay);

        var overlay = new FrameBufferRenderable(_renderer, new FrameBufferOptions
        {
            Id = "overlay",
            Position = PositionValue.Absolute,
            Left = 0,
            Top = 0,
            Width = 4,
            Height = 1,
            ZIndex = 1,
        });
        _renderer.Root.Add(overlay);

        // Upstream drawFrameBuffer skips cells only when both fg and bg alpha are
        // zero. A transparent clear still leaves space characters with opaque fg,
        // so make the unused cells explicitly empty/transparent to model a sparse
        // overlay correctly.
        for (uint x = 0; x < 4; x++)
            overlay.Buffer!.SetCell(x, 0, 0, Rgba.Transparent, Rgba.Transparent);
        overlay.Buffer!.SetCell(1, 0, 'B', Rgba.White, Rgba.Transparent);
        _renderer.RenderTestFrame();

        Assert.Equal("ABAA", ReadRowText(_renderer.NextRenderBuffer, 0, 0, 4));
    }

    [Fact]
    public void Resize_UsesSamePrivateBufferInstanceAndClearsCells()
    {
        var frameBuffer = new FrameBufferRenderable(_renderer, new FrameBufferOptions
        {
            Id = "resizable",
            Position = PositionValue.Absolute,
            Left = 0,
            Top = 0,
            Width = 4,
            Height = 2,
        });
        _renderer.Root.Add(frameBuffer);

        var initialBuffer = frameBuffer.Buffer;
        Assert.NotNull(initialBuffer);
        initialBuffer!.DrawText("AB", 0, 0, Rgba.White, Rgba.FromInts(0, 0, 0, 255));

        frameBuffer.WidthDimension = DimensionValue.Point(6);
        frameBuffer.HeightDimension = DimensionValue.Point(3);
        _renderer.PresentTestFrame();

        Assert.Same(initialBuffer, frameBuffer.Buffer);
        Assert.Equal((uint)' ', ReadCellChar(frameBuffer.Buffer!, 0, 0));
        Assert.Equal((uint)' ', ReadCellChar(frameBuffer.Buffer!, 1, 0));
        Assert.Equal(6u, frameBuffer.Buffer!.Width);
        Assert.Equal(3u, frameBuffer.Buffer!.Height);
    }
}
