using Facebook.Yoga;

namespace OpenTui.Core;

/// <summary>Slider orientation.</summary>
public enum SliderOrientation
{
    Horizontal,
    Vertical
}

/// <summary>
/// Options for Slider renderable.
/// Matches TypeScript SliderOptions.
/// </summary>
public class SliderOptions : RenderableOptions
{
    public required SliderOrientation Orientation { get; init; }
    public float Value { get; init; }
    public float Min { get; init; }
    public float Max { get; init; } = 100;
    public float? ViewPortSize { get; init; }
    public Rgba? BackgroundColor { get; init; }
    public Rgba? ForegroundColor { get; init; }
    public Action<float>? OnChange { get; init; }
}

/// <summary>
/// Slider widget with sub-cell rendering at 2× virtual resolution.
/// Matches TypeScript SliderRenderable from Slider.ts.
/// </summary>
public class SliderRenderable : Renderable
{
    public static class Events
    {
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
    }

    #region Properties

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

    public float Min
    {
        get => _min;
        set { _min = value; if (_value < _min) Value = _min; RequestRender(); }
    }

    public float Max
    {
        get => _max;
        set { _max = value; if (_value > _max) Value = _max; RequestRender(); }
    }

    public float ViewPortSize
    {
        get => _viewPortSize;
        set { _viewPortSize = Math.Clamp(value, 0.01f, _max - _min); RequestRender(); }
    }

    public Rgba BackgroundColor
    {
        get => _backgroundColor;
        set { _backgroundColor = value; RequestRender(); }
    }

    public Rgba ForegroundColor
    {
        get => _foregroundColor;
        set { _foregroundColor = value; RequestRender(); }
    }

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

    #endregion

    #region Rendering

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
        int startX = (int)_screenX;
        int startY = (int)_screenY;

        // Track background
        buffer.FillRect((uint)startX, (uint)startY, (uint)_widthValue, (uint)_heightValue, _backgroundColor);

        int virtualThumbStart = GetVirtualThumbStart();
        int virtualThumbSize = GetVirtualThumbSize();
        int virtualThumbEnd = virtualThumbStart + virtualThumbSize;

        int realStartCell = virtualThumbStart / 2;
        int realEndCell = (virtualThumbEnd + 1) / 2 - 1;

        for (int cell = realStartCell; cell <= realEndCell && cell < _widthValue; cell++)
        {
            int cellVirtualStart = cell * 2;
            int cellVirtualEnd = cellVirtualStart + 2;

            int overlapStart = Math.Max(cellVirtualStart, virtualThumbStart);
            int overlapEnd = Math.Min(cellVirtualEnd, virtualThumbEnd);
            int coverage = overlapEnd - overlapStart;

            string ch;
            if (coverage >= 2)
                ch = "█";
            else if (overlapStart == cellVirtualStart)
                ch = "▌"; // left half
            else
                ch = "▐"; // right half

            for (int row = 0; row < _heightValue; row++)
            {
                buffer.DrawText(ch, (uint)(startX + cell), (uint)(startY + row), _foregroundColor);
            }
        }
    }

    private void RenderVertical(OptimizedBuffer buffer)
    {
        int startX = (int)_screenX;
        int startY = (int)_screenY;

        // Track background
        buffer.FillRect((uint)startX, (uint)startY, (uint)_widthValue, (uint)_heightValue, _backgroundColor);

        int virtualThumbStart = GetVirtualThumbStart();
        int virtualThumbSize = GetVirtualThumbSize();
        int virtualThumbEnd = virtualThumbStart + virtualThumbSize;

        int realStartCell = virtualThumbStart / 2;
        int realEndCell = (virtualThumbEnd + 1) / 2 - 1;

        for (int cell = realStartCell; cell <= realEndCell && cell < _heightValue; cell++)
        {
            int cellVirtualStart = cell * 2;
            int cellVirtualEnd = cellVirtualStart + 2;

            int overlapStart = Math.Max(cellVirtualStart, virtualThumbStart);
            int overlapEnd = Math.Min(cellVirtualEnd, virtualThumbEnd);
            int coverage = overlapEnd - overlapStart;

            string ch;
            if (coverage >= 2)
                ch = "█";
            else if (overlapStart == cellVirtualStart)
                ch = "▀"; // top half
            else
                ch = "▄"; // bottom half

            for (int col = 0; col < _widthValue; col++)
            {
                buffer.DrawText(ch, (uint)(startX + col), (uint)(startY + cell), _foregroundColor);
            }
        }
    }

    #endregion

    #region Mouse Handling

    /// <summary>Update value from a direct click position (0-based within the slider).</summary>
    public void UpdateValueFromPosition(float position)
    {
        int trackSize = _orientation == SliderOrientation.Vertical ? _heightValue : _widthValue;
        if (trackSize <= 0) return;

        float ratio = Math.Clamp(position / trackSize, 0, 1);
        Value = _min + ratio * (_max - _min);
    }

    #endregion
}
