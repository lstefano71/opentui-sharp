using Facebook.Yoga;

namespace OpenTui.Core;

/// <summary>Slider orientation.</summary>
public enum SliderOrientation
{
    /// <summary>
    /// Represents the Horizontal option.
    /// </summary>
    Horizontal,
    /// <summary>
    /// Represents the Vertical option.
    /// </summary>
    Vertical
}

/// <summary>
/// Options for Slider renderable.
/// Matches TypeScript SliderOptions.
/// </summary>
public class SliderOptions : RenderableOptions
{
    /// <summary>
    /// Gets or sets the orientation.
    /// </summary>
    public required SliderOrientation Orientation { get; init; }
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public float Value { get; init; }
    /// <summary>
    /// Gets or sets the min.
    /// </summary>
    public float Min { get; init; }
    /// <summary>
    /// Gets or sets the max.
    /// </summary>
    public float Max { get; init; } = 100;
    /// <summary>
    /// Gets or sets the view port size.
    /// </summary>
    public float? ViewPortSize { get; init; }
    /// <summary>
    /// Gets or sets the background color.
    /// </summary>
    public Rgba? BackgroundColor { get; init; }
    /// <summary>
    /// Gets or sets the foreground color.
    /// </summary>
    public Rgba? ForegroundColor { get; init; }
    /// <summary>
    /// Gets or sets the on change.
    /// </summary>
    public Action<float>? OnChange { get; init; }
}

/// <summary>
/// Slider widget with sub-cell rendering at 2× virtual resolution.
/// Matches TypeScript SliderRenderable from Slider.ts.
/// </summary>
public class SliderRenderable : Renderable
{
    /// <summary>
    /// Represents an Events.
    /// </summary>
    public static class Events
    {
        /// <summary>
        /// Stores the change.
        /// </summary>
        public const string Change = "change";
    }

    private readonly SliderOrientation _orientation;
    private float _value;
    private float _min;
    private float _max;
    private float _viewPortSize;
    private Rgba _backgroundColor;
    private Rgba _foregroundColor;
    private Action<float>? _onChange;
    private bool _isDragging;
    private int _dragOffsetVirtual;

    /// <summary>
    /// Initializes a new instance of the SliderRenderable class.
    /// </summary>
    /// <param name="ctx">The render context.</param>
    /// <param name="options">The configuration options.</param>
    public SliderRenderable(IRenderContext ctx, SliderOptions options)
        : base(ctx, options)
    {
        _orientation = options.Orientation;
        _min = options.Min;
        _max = options.Max;
        _value = Math.Clamp(options.Value, _min, _max);
        _viewPortSize = options.ViewPortSize ?? Math.Max(1, (_max - _min) * 0.1f);
        _backgroundColor = options.BackgroundColor ?? Rgba.FromHex("#252527");
        _foregroundColor = options.ForegroundColor ?? Rgba.FromHex("#9a9ea3");
        _onChange = options.OnChange;

        // Slider doesn't shrink by default
        FlexShrink = 0;
        Focusable = true;
    }

    #region Properties

    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public float Value
    {
        get => _value;
        set
        {
            float clamped = Math.Clamp(value, _min, _max);
            if (Math.Abs(clamped - _value) < 0.0001f) return;
            _value = clamped;
            _onChange?.Invoke(_value);
            Emit<float>(Events.Change, _value);
            RequestRender();
        }
    }

    /// <summary>
    /// Gets or sets the min.
    /// </summary>
    public float Min
    {
        get => _min;
        set { _min = value; if (_value < _min) Value = _min; RequestRender(); }
    }

    /// <summary>
    /// Gets or sets the max.
    /// </summary>
    public float Max
    {
        get => _max;
        set { _max = value; if (_value > _max) Value = _max; RequestRender(); }
    }

    /// <summary>
    /// Gets or sets the view port size.
    /// </summary>
    public float ViewPortSize
    {
        get => _viewPortSize;
        set { _viewPortSize = Math.Max(0.01f, Math.Min(value, _max - _min)); RequestRender(); }
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
    /// Gets or sets the foreground color.
    /// </summary>
    public Rgba ForegroundColor
    {
        get => _foregroundColor;
        set { _foregroundColor = value; RequestRender(); }
    }

    /// <summary>
    /// Gets the orientation.
    /// </summary>
    public SliderOrientation Orientation => _orientation;

    #endregion

    #region Virtual Coordinate System (2× resolution)

    private int GetVirtualTrackSize()
    {
        int track = _orientation == SliderOrientation.Vertical ? _heightValue : _widthValue;
        return track * 2;
    }

    private int GetVirtualThumbSize()
    {
        int virtualTrackSize = GetVirtualTrackSize();
        float range = _max - _min;
        if (range == 0) return virtualTrackSize;

        float viewportSize = Math.Max(1, _viewPortSize);
        float contentSize = range + viewportSize;
        if (contentSize <= viewportSize) return virtualTrackSize;

        float thumbRatio = viewportSize / contentSize;
        return Math.Clamp((int)MathF.Floor(virtualTrackSize * thumbRatio), 1, virtualTrackSize);
    }

    private int GetVirtualThumbStart()
    {
        int virtualTrackSize = GetVirtualTrackSize();
        float range = _max - _min;
        if (range == 0) return 0;

        float valueRatio = (_value - _min) / range;
        int thumbSize = GetVirtualThumbSize();
        return (int)MathF.Round(valueRatio * (virtualTrackSize - thumbSize));
    }

    private float GetKeyboardStep()
    {
        float range = _max - _min;
        if (range <= 0) return 0;

        int maxThumbStart = Math.Max(1, GetVirtualTrackSize() - GetVirtualThumbSize());
        return range / maxThumbStart;
    }

    private int CalculateDragOffsetVirtual(UiMouseEvent evt)
    {
        int trackStart = _orientation == SliderOrientation.Vertical ? (int)_screenY : (int)_screenX;
        int mousePos = (_orientation == SliderOrientation.Vertical ? evt.Y : evt.X) - trackStart;
        int trackSize = _orientation == SliderOrientation.Vertical ? _heightValue : _widthValue;
        int virtualMousePos = Math.Max(0, Math.Min(trackSize * 2, mousePos * 2));
        int virtualThumbStart = GetVirtualThumbStart();
        int virtualThumbSize = GetVirtualThumbSize();
        return Math.Max(0, Math.Min(virtualThumbSize, virtualMousePos - virtualThumbStart));
    }

    private void UpdateValueFromMouseDirect(UiMouseEvent evt)
    {
        int trackStart = _orientation == SliderOrientation.Vertical ? (int)_screenY : (int)_screenX;
        int mousePos = _orientation == SliderOrientation.Vertical ? evt.Y : evt.X;
        UpdateValueFromPosition(mousePos - trackStart);
    }

    private void UpdateValueFromMouseWithOffset(UiMouseEvent evt, int offsetVirtual)
    {
        int trackStart = _orientation == SliderOrientation.Vertical ? (int)_screenY : (int)_screenX;
        int trackSize = _orientation == SliderOrientation.Vertical ? _heightValue : _widthValue;
        int mousePos = _orientation == SliderOrientation.Vertical ? evt.Y : evt.X;
        int virtualTrackSize = trackSize * 2;
        int clampedMousePos = Math.Max(0, Math.Min(trackSize, mousePos - trackStart));
        int virtualMousePos = clampedMousePos * 2;
        int virtualThumbSize = GetVirtualThumbSize();
        int maxThumbStart = Math.Max(0, virtualTrackSize - virtualThumbSize);
        int desiredThumbStart = Math.Max(0, Math.Min(maxThumbStart, virtualMousePos - offsetVirtual));
        float ratio = maxThumbStart == 0 ? 0 : (float)desiredThumbStart / maxThumbStart;
        Value = _min + ratio * (_max - _min);
    }

    private (int x, int y, int width, int height) GetThumbRect()
    {
        int virtualThumbSize = GetVirtualThumbSize();
        int virtualThumbStart = GetVirtualThumbStart();
        int realThumbStart = virtualThumbStart / 2;
        int realThumbSize = (int)Math.Ceiling((virtualThumbStart + virtualThumbSize) / 2f) - realThumbStart;

        return _orientation == SliderOrientation.Vertical
            ? ((int)_screenX, (int)_screenY + realThumbStart, _widthValue, Math.Max(1, realThumbSize))
            : ((int)_screenX + realThumbStart, (int)_screenY, Math.Max(1, realThumbSize), _heightValue);
    }

    #endregion

    #region Rendering

    /// <inheritdoc />
    protected override void RenderSelf(OptimizedBuffer buffer, float deltaTime)
    {
        if (_widthValue == 0 || _heightValue == 0) return;

        if (_orientation == SliderOrientation.Horizontal)
            RenderHorizontal(buffer);
        else
            RenderVertical(buffer);
    }

    private void RenderHorizontal(OptimizedBuffer buffer)
    {
        int startX = _buffered ? 0 : (int)_screenX;
        int startY = _buffered ? 0 : (int)_screenY;

        // Track background
        buffer.FillRect((uint)startX, (uint)startY, (uint)_widthValue, (uint)_heightValue, _backgroundColor);

        int virtualThumbStart = GetVirtualThumbStart();
        int virtualThumbSize = GetVirtualThumbSize();
        int virtualThumbEnd = virtualThumbStart + virtualThumbSize;

        int realStartCell = Math.Max(0, virtualThumbStart / 2);
        int realEndCell = Math.Min(_widthValue - 1, (virtualThumbEnd + 1) / 2 - 1);

        for (int cell = realStartCell; cell <= realEndCell; cell++)
        {
            int cellVirtualStart = cell * 2;
            int cellVirtualEnd = cellVirtualStart + 2;

            int overlapStart = Math.Max(cellVirtualStart, virtualThumbStart);
            int overlapEnd = Math.Min(cellVirtualEnd, virtualThumbEnd);
            int coverage = overlapEnd - overlapStart;

            uint codepoint;
            if (coverage >= 2)
                codepoint = '█';
            else if (overlapStart == cellVirtualStart)
                codepoint = '▌'; // left half
            else
                codepoint = '▐'; // right half

            for (int row = 0; row < _heightValue; row++)
            {
                buffer.SetCellWithAlphaBlending(
                    (uint)(startX + cell),
                    (uint)(startY + row),
                    codepoint,
                    _foregroundColor,
                    _backgroundColor);
            }
        }
    }

    private void RenderVertical(OptimizedBuffer buffer)
    {
        int startX = _buffered ? 0 : (int)_screenX;
        int startY = _buffered ? 0 : (int)_screenY;

        // Track background
        buffer.FillRect((uint)startX, (uint)startY, (uint)_widthValue, (uint)_heightValue, _backgroundColor);

        int virtualThumbStart = GetVirtualThumbStart();
        int virtualThumbSize = GetVirtualThumbSize();
        int virtualThumbEnd = virtualThumbStart + virtualThumbSize;

        int realStartCell = Math.Max(0, virtualThumbStart / 2);
        int realEndCell = Math.Min(_heightValue - 1, (virtualThumbEnd + 1) / 2 - 1);

        for (int cell = realStartCell; cell <= realEndCell; cell++)
        {
            int cellVirtualStart = cell * 2;
            int cellVirtualEnd = cellVirtualStart + 2;

            int overlapStart = Math.Max(cellVirtualStart, virtualThumbStart);
            int overlapEnd = Math.Min(cellVirtualEnd, virtualThumbEnd);
            int coverage = overlapEnd - overlapStart;

            uint codepoint;
            if (coverage >= 2)
                codepoint = '█';
            else if (overlapStart == cellVirtualStart)
                codepoint = '▀'; // top half
            else
                codepoint = '▄'; // bottom half

            for (int col = 0; col < _widthValue; col++)
            {
                buffer.SetCellWithAlphaBlending(
                    (uint)(startX + col),
                    (uint)(startY + cell),
                    codepoint,
                    _foregroundColor,
                    _backgroundColor);
            }
        }
    }

    #endregion

    #region Mouse Handling

    /// <inheritdoc />
    protected override void OnMouseEvent(UiMouseEvent evt)
    {
        switch (evt.Type)
        {
            case MouseEventType.Down when evt.Button == (int)MouseButton.Left:
            {
                evt.StopPropagation();
                evt.PreventDefault();
                Focus();

                var thumb = GetThumbRect();
                bool inThumb = evt.X >= thumb.x && evt.X < thumb.x + thumb.width
                    && evt.Y >= thumb.y && evt.Y < thumb.y + thumb.height;

                if (!inThumb)
                    UpdateValueFromMouseDirect(evt);

                _isDragging = true;
                _dragOffsetVirtual = CalculateDragOffsetVirtual(evt);
                break;
            }
            case MouseEventType.Drag when _isDragging:
                evt.StopPropagation();
                UpdateValueFromMouseWithOffset(evt, _dragOffsetVirtual);
                break;
            case MouseEventType.Up when _isDragging:
                evt.StopPropagation();
                UpdateValueFromMouseWithOffset(evt, _dragOffsetVirtual);
                _isDragging = false;
                break;
        }
    }

    /// <summary>Update value from a direct click position (0-based within the slider).</summary>
    public void UpdateValueFromPosition(float position)
    {
        int trackSize = _orientation == SliderOrientation.Vertical ? _heightValue : _widthValue;
        if (trackSize <= 0) return;

        float ratio = Math.Clamp(position / trackSize, 0, 1);
        Value = _min + ratio * (_max - _min);
    }

    #endregion

    #region Keyboard

    /// <inheritdoc />
    protected override void HandleKeyPress(KeyEvent key)
    {
        float step = GetKeyboardStep();
        if (step <= 0)
        {
            base.HandleKeyPress(key);
            return;
        }

        float pageStep = Math.Max(step * 5f, Math.Max(1f, _viewPortSize));
        bool handled = _orientation switch
        {
            SliderOrientation.Horizontal => key.Name switch
            {
                "left" or "h" => Do(() => Value -= key.Shift ? pageStep : step),
                "right" or "l" => Do(() => Value += key.Shift ? pageStep : step),
                _ => false
            },
            SliderOrientation.Vertical => key.Name switch
            {
                "up" or "k" => Do(() => Value -= key.Shift ? pageStep : step),
                "down" or "j" => Do(() => Value += key.Shift ? pageStep : step),
                _ => false
            },
            _ => false
        };

        if (!handled)
        {
            handled = key.Name switch
            {
                "pageup" => Do(() => Value -= pageStep),
                "pagedown" => Do(() => Value += pageStep),
                "home" => Do(() => Value = _min),
                "end" => Do(() => Value = _max),
                _ => false
            };
        }

        if (handled)
            key.StopPropagation();
        else
            base.HandleKeyPress(key);
    }

    private static bool Do(Action action)
    {
        action();
        return true;
    }

    #endregion
}
