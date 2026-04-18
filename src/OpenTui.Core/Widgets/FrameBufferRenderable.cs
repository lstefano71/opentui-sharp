using Facebook.Yoga;

namespace OpenTui.Core;

/// <summary>
/// Options for FrameBuffer renderable.
/// Matches TypeScript FrameBufferOptions.
/// </summary>
public class FrameBufferOptions : RenderableOptions
{
    public Rgba? BackgroundColor { get; init; }
    public bool RespectAlpha { get; init; } = true;
}

/// <summary>
/// Raw pixel/cell direct-painting surface.
/// Provides a private OptimizedBuffer that the user draws into;
/// RenderSelf blits it onto the main render buffer.
/// Matches TypeScript FrameBufferRenderable from FrameBuffer.ts.
/// </summary>
public class FrameBufferRenderable : Renderable
{
    private Rgba _backgroundColor;
    private bool _respectAlpha;
    private OptimizedBuffer? _privateBuffer;
    private int _lastWidth;
    private int _lastHeight;

    public FrameBufferRenderable(IRenderContext ctx, FrameBufferOptions? options = null)
        : base(ctx, options ?? new FrameBufferOptions())
    {
        options ??= new FrameBufferOptions();
        _backgroundColor = options.BackgroundColor ?? Rgba.Transparent;
        _respectAlpha = options.RespectAlpha;
    }

    /// <summary>
    /// Gets the drawing surface. The buffer is created/resized to match
    /// the current layout dimensions. Returns null if layout has zero size.
    /// </summary>
    public OptimizedBuffer? Buffer
    {
        get
        {
            EnsureBuffer();
            return _privateBuffer;
        }
    }

    public Rgba BackgroundColor
    {
        get => _backgroundColor;
        set { _backgroundColor = value; RequestRender(); }
    }

    public bool RespectAlpha
    {
        get => _respectAlpha;
        set
        {
            if (_respectAlpha == value)
                return;

            _respectAlpha = value;
            if (_privateBuffer is not null)
                _privateBuffer.RespectAlpha = value;
            RequestRender();
        }
    }

    /// <summary>
    /// Clears the private buffer to the background color.
    /// </summary>
    public void Clear() => _privateBuffer?.Clear(_backgroundColor);

    private void EnsureBuffer()
    {
        if (_widthValue <= 0 || _heightValue <= 0)
        {
            _privateBuffer?.Dispose();
            _privateBuffer = null;
            _lastWidth = 0;
            _lastHeight = 0;
            return;
        }

        if (_privateBuffer == null || _lastWidth != _widthValue || _lastHeight != _heightValue)
        {
            _privateBuffer?.Dispose();
            _privateBuffer = OptimizedBuffer.Create(
                (uint)_widthValue, (uint)_heightValue, _ctx.WidthMethod, _respectAlpha, $"framebuf-{Id}");
            _privateBuffer.Clear(_backgroundColor);
            _lastWidth = _widthValue;
            _lastHeight = _heightValue;
        }
    }

    protected override void OnResize(int width, int height)
    {
        EnsureBuffer();
        base.OnResize(width, height);
    }

    protected override void RenderSelf(OptimizedBuffer buffer, float deltaTime)
    {
        if (_privateBuffer == null || _widthValue == 0 || _heightValue == 0) return;

        // Fill background
        buffer.FillRect((uint)_screenX, (uint)_screenY,
            (uint)_widthValue, (uint)_heightValue, _backgroundColor);

        // Blit private buffer onto main buffer
        buffer.DrawFrameBuffer((int)_screenX, (int)_screenY,
            _privateBuffer, 0, 0, (uint)_widthValue, (uint)_heightValue);
    }

    protected override void DestroySelf()
    {
        _privateBuffer?.Dispose();
        _privateBuffer = null;
        base.DestroySelf();
    }
}
