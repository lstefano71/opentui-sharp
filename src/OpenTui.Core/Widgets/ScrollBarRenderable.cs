using Facebook.Yoga;

namespace OpenTui.Core;

/// <summary>Arrow direction for scroll bar arrows.</summary>
public enum ArrowDirection { Up, Down, Left, Right }

/// <summary>Options for ArrowRenderable.</summary>
public class ArrowOptions : RenderableOptions
{
    public required ArrowDirection Direction { get; init; }
    public Rgba? ForegroundColor { get; init; }
    public Rgba? BackgroundColor { get; init; }
    public TextAttributes Attributes { get; init; }
    public string? ArrowChar { get; init; }
}

/// <summary>Single-character arrow indicator.</summary>
public class ArrowRenderable : Renderable
{
    private static readonly Dictionary<ArrowDirection, string> DefaultChars = new()
    {
        [ArrowDirection.Up] = "▲",
        [ArrowDirection.Down] = "▼",
        [ArrowDirection.Left] = "◀",
        [ArrowDirection.Right] = "▶",
    };

    private readonly ArrowDirection _direction;
    private readonly string _arrowChar;
    private readonly Rgba _foregroundColor;

    public ArrowRenderable(IRenderContext ctx, ArrowOptions options)
        : base(ctx, options)
    {
        _direction = options.Direction;
        _arrowChar = options.ArrowChar ?? DefaultChars[_direction];
        _foregroundColor = options.ForegroundColor ?? Rgba.FromInts(255, 255, 255);
        WidthDimension = DimensionValue.Point(1);
        HeightDimension = DimensionValue.Point(1);
    }

    protected override void RenderSelf(OptimizedBuffer buffer, float deltaTime)
    {
        buffer.DrawText(_arrowChar, (uint)_screenX, (uint)_screenY, _foregroundColor);
    }
}

/// <summary>Scroll unit types for scrollBy.</summary>
public enum ScrollUnit { Absolute, Viewport, Content, Step }

/// <summary>
/// Options for ScrollBar renderable.
/// Matches TypeScript ScrollBarOptions.
/// </summary>
public class ScrollBarOptions : RenderableOptions
{
    public required SliderOrientation Orientation { get; init; }
    public bool ShowArrows { get; init; }
    public ArrowOptions? ArrowOptions { get; init; }
    public SliderOptions? TrackOptions { get; init; }
    public Action<float>? OnChange { get; init; }
}

/// <summary>
/// Composite scrollbar: optional arrows + slider track.
/// Matches TypeScript ScrollBarRenderable from ScrollBar.ts.
/// </summary>
public class ScrollBarRenderable : Renderable
{
    public static class Events
    {
        public const string Change = "change";
    }

    private readonly SliderOrientation _orientation;
    private float _scrollSize;
    private float _scrollPosition;
    private float _viewportSize;
    private bool _showArrows;
    private bool _manualVisibility;
    private float? _scrollStep;
    private Action<float>? _onChange;

    private readonly ArrowRenderable _startArrow;
    private readonly SliderRenderable _slider;
    private readonly ArrowRenderable _endArrow;

    public ScrollBarRenderable(IRenderContext ctx, ScrollBarOptions options)
        : base(ctx, options)
    {
        _orientation = options.Orientation;
        _showArrows = options.ShowArrows;
        _onChange = options.OnChange;

        // Layout direction matches orientation
        FlexDirection = _orientation == SliderOrientation.Vertical
            ? FlexDirectionValue.Column
            : FlexDirectionValue.Row;

        // Create arrows
        var startDir = _orientation == SliderOrientation.Vertical
            ? ArrowDirection.Up : ArrowDirection.Left;
        var endDir = _orientation == SliderOrientation.Vertical
            ? ArrowDirection.Down : ArrowDirection.Right;

        _startArrow = new ArrowRenderable(ctx, new ArrowOptions
        {
            Direction = startDir,
            ForegroundColor = options.ArrowOptions?.ForegroundColor,
        });

        _slider = new SliderRenderable(ctx, new SliderOptions
        {
            Orientation = _orientation,
            Min = 0,
            Max = 0,
            OnChange = OnSliderChange,
        });
        _slider.FlexGrow = 1;
        _slider.FlexShrink = 1;

        _endArrow = new ArrowRenderable(ctx, new ArrowOptions
        {
            Direction = endDir,
            ForegroundColor = options.ArrowOptions?.ForegroundColor,
        });

        // Add children
        Add(_startArrow);
        Add(_slider);
        Add(_endArrow);

        if (!_showArrows)
        {
            _startArrow.Visible = false;
            _endArrow.Visible = false;
        }

        Focusable = true;
    }

    #region Properties

    public float ScrollSize
    {
        get => _scrollSize;
        set
        {
            _scrollSize = value;
            UpdateSliderFromScrollState();
            RecalculateVisibility();
        }
    }

    public float ScrollPosition
    {
        get => _scrollPosition;
        set
        {
            _scrollPosition = MathF.Round(Math.Clamp(value, 0,
                Math.Max(0, _scrollSize - _viewportSize)));
            UpdateSliderFromScrollState();
        }
    }

    public float ViewportSize
    {
        get => _viewportSize;
        set
        {
            _viewportSize = value;
            _slider.ViewPortSize = value;
            UpdateSliderFromScrollState();
            RecalculateVisibility();
        }
    }

    public bool ShowArrows
    {
        get => _showArrows;
        set
        {
            _showArrows = value;
            _startArrow.Visible = value;
            _endArrow.Visible = value;
            RequestRender();
        }
    }

    public float? ScrollStep
    {
        get => _scrollStep;
        set => _scrollStep = value;
    }

    public SliderRenderable Slider => _slider;

    #endregion

    #region Scroll Operations

    public void ScrollBy(float delta, ScrollUnit unit = ScrollUnit.Absolute)
    {
        float multiplier = unit switch
        {
            ScrollUnit.Viewport => _viewportSize,
            ScrollUnit.Content => _scrollSize,
            ScrollUnit.Step => _scrollStep ?? 1,
            _ => 1, // Absolute
        };
        ScrollPosition += multiplier * delta;
        _onChange?.Invoke(_scrollPosition);
        Emit<float>(Events.Change, _scrollPosition);
    }

    #endregion

    #region Keyboard

    protected override void HandleKeyPress(KeyEvent key)
    {
        bool handled = false;
        if (_orientation == SliderOrientation.Vertical)
        {
            handled = key.Name switch
            {
                "up" or "k" => Do(() => ScrollBy(-0.2f, ScrollUnit.Viewport)),
                "down" or "j" => Do(() => ScrollBy(0.2f, ScrollUnit.Viewport)),
                _ => false
            };
        }
        else
        {
            handled = key.Name switch
            {
                "left" or "h" => Do(() => ScrollBy(-0.2f, ScrollUnit.Viewport)),
                "right" or "l" => Do(() => ScrollBy(0.2f, ScrollUnit.Viewport)),
                _ => false
            };
        }

        if (!handled)
        {
            handled = key.Name switch
            {
                "pageup" => Do(() => ScrollBy(-0.5f, ScrollUnit.Viewport)),
                "pagedown" => Do(() => ScrollBy(0.5f, ScrollUnit.Viewport)),
                "home" => Do(() => ScrollBy(-1, ScrollUnit.Content)),
                "end" => Do(() => ScrollBy(1, ScrollUnit.Content)),
                _ => false
            };
        }

        if (handled)
            key.StopPropagation();
        else
            base.HandleKeyPress(key);
    }

    private static bool Do(Action action) { action(); return true; }

    /// <summary>Allow parent widgets (like ScrollBox) to dispatch key events to this scrollbar.</summary>
    internal void HandleKeyPressInternal(KeyEvent key) => HandleKeyPress(key);

    #endregion

    #region Internal

    private void UpdateSliderFromScrollState()
    {
        float scrollRange = Math.Max(0, _scrollSize - _viewportSize);
        _slider.Min = 0;
        _slider.Max = scrollRange;
        _slider.Value = Math.Min(_scrollPosition, scrollRange);
    }

    private void OnSliderChange(float value)
    {
        _scrollPosition = MathF.Round(value);
        _onChange?.Invoke(_scrollPosition);
        Emit<float>(Events.Change, _scrollPosition);
    }

    private void RecalculateVisibility()
    {
        if (_manualVisibility) return;
        Visible = _scrollSize > _viewportSize;
    }

    #endregion
}
