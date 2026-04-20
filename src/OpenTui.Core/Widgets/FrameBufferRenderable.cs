namespace OpenTui.Core;

/// <summary>
/// Options for <see cref="FrameBufferRenderable"/>.
/// Unlike upstream, the C# port currently inherits the full LayoutOptions surface,
/// so framebuffers may participate in Yoga sizing as well as fixed point sizing.
/// </summary>
public class FrameBufferOptions : RenderableOptions
{
    /// <summary>
    /// Gets or sets the background color.
    /// </summary>
    public Rgba? BackgroundColor { get; init; }
    /// <summary>
    /// Gets or sets the respect alpha.
    /// </summary>
    public bool RespectAlpha { get; init; } = true;
}

/// <summary>
/// Raw pixel/cell direct-painting surface.
/// Provides a private OptimizedBuffer that the user draws into;
/// RenderSelf blits it onto the main render buffer.
/// The core blit/resize semantics are aligned with the upstream FrameBufferRenderable,
/// but the C# type still supports layout-driven sizing via inherited LayoutOptions.
/// </summary>
public class FrameBufferRenderable : Renderable
{
    private Rgba _backgroundColor;
    private bool _respectAlpha;
    private OptimizedBuffer? _privateBuffer;
    private int _lastWidth;
    private int _lastHeight;

    /// <summary>
    /// Initializes a new instance of the FrameBufferRenderable class.
    /// </summary>
    /// <param name="ctx">The render context.</param>
    /// <param name="options">The configuration options.</param>
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

    /// <summary>
    /// Gets or sets the background color.
    /// </summary>
    public Rgba BackgroundColor
    {
        get => _backgroundColor;
        set { _backgroundColor = value; RequestRender(); }
    }

    /// <summary>
    /// Gets or sets the respect alpha.
    /// </summary>
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
    public void Clear()
    {
        EnsureBuffer();
        _privateBuffer?.Clear(_backgroundColor);
    }

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

        if (_privateBuffer is null)
        {
            _privateBuffer = OptimizedBuffer.Create(
                (uint)_widthValue, (uint)_heightValue, _ctx.WidthMethod, _respectAlpha, $"framebuf-{Id}");
            _privateBuffer.Clear(_backgroundColor);
        }
        else if (_lastWidth != _widthValue || _lastHeight != _heightValue)
        {
            _privateBuffer.Resize((uint)_widthValue, (uint)_heightValue);
        }

        _lastWidth = _widthValue;
        _lastHeight = _heightValue;
    }

    /// <inheritdoc />
    protected override void OnResize(int width, int height)
    {
        EnsureBuffer();
        base.OnResize(width, height);
    }

    /// <inheritdoc />
    protected override void RenderSelf(OptimizedBuffer buffer, float deltaTime)
    {
        if (_privateBuffer == null || _widthValue == 0 || _heightValue == 0) return;

        // Blit private buffer onto main buffer
        buffer.DrawFrameBuffer((int)_screenX, (int)_screenY,
            _privateBuffer, 0, 0, (uint)_widthValue, (uint)_heightValue);
    }

    /// <inheritdoc />
    protected override void DestroySelf()
    {
        _privateBuffer?.Dispose();
        _privateBuffer = null;
        base.DestroySelf();
    }
}
