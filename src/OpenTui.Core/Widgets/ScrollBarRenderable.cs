using Facebook.Yoga;

namespace OpenTui.Core;

/// <summary>Arrow direction for scroll bar arrows.</summary>
public enum ArrowDirection
{
    /// <summary>
    /// Represents the up direction.
    /// </summary>
    Up,

    /// <summary>
    /// Represents the down direction.
    /// </summary>
    Down,

    /// <summary>
    /// Represents the left direction.
    /// </summary>
    Left,

    /// <summary>
    /// Represents the right direction.
    /// </summary>
    Right,
}

/// <summary>Options for ArrowRenderable.</summary>
public class ArrowOptions : RenderableOptions
{
    /// <summary>
    /// Gets or sets the direction.
    /// </summary>
    public required ArrowDirection Direction { get; init; }
    /// <summary>
    /// Gets or sets the foreground color.
    /// </summary>
    public Rgba? ForegroundColor { get; init; }
    /// <summary>
    /// Gets or sets the background color.
    /// </summary>
    public Rgba? BackgroundColor { get; init; }
    /// <summary>
    /// Gets or sets the attributes.
    /// </summary>
    public TextAttributes Attributes { get; init; }
    /// <summary>
    /// Gets or sets the arrow char.
    /// </summary>
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

    /// <summary>
    /// Initializes a new instance of the ArrowRenderable class.
    /// </summary>
    /// <param name="ctx">The render context.</param>
    /// <param name="options">The configuration options.</param>
    public ArrowRenderable(IRenderContext ctx, ArrowOptions options)
        : base(ctx, options)
    {
        _direction = options.Direction;
        _arrowChar = options.ArrowChar ?? DefaultChars[_direction];
        _foregroundColor = options.ForegroundColor ?? Rgba.FromInts(255, 255, 255);
        WidthDimension = DimensionValue.Point(1);
        HeightDimension = DimensionValue.Point(1);
    }

    /// <inheritdoc />
    protected override void RenderSelf(OptimizedBuffer buffer, float deltaTime)
    {
        buffer.DrawText(_arrowChar, (uint)_screenX, (uint)_screenY, _foregroundColor);
    }
}

/// <summary>Scroll unit types for scrollBy.</summary>
public enum ScrollUnit
{
    /// <summary>
    /// Scrolls to an absolute position.
    /// </summary>
    Absolute,

    /// <summary>
    /// Scrolls by viewport-sized increments.
    /// </summary>
    Viewport,

    /// <summary>
    /// Scrolls relative to the full content size.
    /// </summary>
    Content,

    /// <summary>
    /// Scrolls by a single step increment.
    /// </summary>
    Step,
}

/// <summary>
/// Options for ScrollBar renderable.
/// Matches TypeScript ScrollBarOptions.
/// </summary>
public class ScrollBarOptions : RenderableOptions
{
    /// <summary>
    /// Gets or sets the orientation.
    /// </summary>
    public required SliderOrientation Orientation { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether show arrows.
    /// </summary>
    public bool ShowArrows { get; init; }
    /// <summary>
    /// Gets or sets the arrow options.
    /// </summary>
    public ArrowOptions? ArrowOptions { get; init; }
    /// <summary>
    /// Gets or sets the track options.
    /// </summary>
    public SliderOptions? TrackOptions { get; init; }
    /// <summary>
    /// Gets or sets the on change.
    /// </summary>
    public Action<float>? OnChange { get; init; }
}

/// <summary>
/// Composite scrollbar: optional arrows + slider track.
/// Matches TypeScript ScrollBarRenderable from ScrollBar.ts.
/// </summary>
public class ScrollBarRenderable : Renderable
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

    /// <summary>
    /// Initializes a new instance of the ScrollBarRenderable class.
    /// </summary>
    /// <param name="ctx">The render context.</param>
    /// <param name="options">The configuration options.</param>
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
            BackgroundColor = options.TrackOptions?.BackgroundColor,
            ForegroundColor = options.TrackOptions?.ForegroundColor,
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

    /// <summary>
    /// Gets or sets visibility. Setting this marks the scrollbar as manually controlled,
    /// preventing auto-visibility from overriding the value.
    /// </summary>
    public override bool Visible
    {
        get => base.Visible;
        set
        {
            _manualVisibility = true;
            base.Visible = value;
        }
    }

    /// <summary>
    /// Resets manual visibility control, allowing auto-hide behavior to resume.
    /// </summary>
    public void ResetVisibilityControl()
    {
        _manualVisibility = false;
        RecalculateVisibility();
    }

    /// <summary>
    /// Gets or sets the scroll size.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the scroll position.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the viewport size.
    /// </summary>
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

    /// <summary>
    /// Gets or sets a value indicating whether show arrows.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the scroll step.
    /// </summary>
    public float? ScrollStep
    {
        get => _scrollStep;
        set => _scrollStep = value;
    }

    /// <summary>
    /// Gets the slider.
    /// </summary>
    public SliderRenderable Slider => _slider;

    #endregion

    #region Scroll Operations

    /// <summary>
    /// Performs scroll by.
    /// </summary>
    /// <param name="delta">The delta.</param>
    /// <param name="unit">The unit.</param>
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

    /// <inheritdoc />
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
        base.Visible = _scrollSize > _viewportSize;
    }

    #endregion
}
