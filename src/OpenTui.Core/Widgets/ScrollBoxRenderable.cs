using Facebook.Yoga;

namespace OpenTui.Core;

/// <summary>
/// Options for ScrollBox renderable.
/// Matches TypeScript ScrollBoxOptions.
/// </summary>
public class ScrollBoxOptions : BoxOptions
{
    /// <summary>
    /// Gets or sets the root options.
    /// </summary>
    public BoxOptions? RootOptions { get; init; }
    /// <summary>
    /// Gets or sets the wrapper options.
    /// </summary>
    public BoxOptions? WrapperOptions { get; init; }
    /// <summary>
    /// Gets or sets the viewport options.
    /// </summary>
    public BoxOptions? ViewportOptions { get; init; }
    /// <summary>
    /// Gets or sets the content options.
    /// </summary>
    public BoxOptions? ContentOptions { get; init; }
    /// <summary>
    /// Gets or sets the scrollbar options.
    /// </summary>
    public ScrollBarOptions? ScrollbarOptions { get; init; }
    /// <summary>
    /// Gets or sets the vertical scrollbar options.
    /// </summary>
    public ScrollBarOptions? VerticalScrollbarOptions { get; init; }
    /// <summary>
    /// Gets or sets the horizontal scrollbar options.
    /// </summary>
    public ScrollBarOptions? HorizontalScrollbarOptions { get; init; }
    /// <summary>
    /// Gets or sets the sticky scroll.
    /// </summary>
    public bool StickyScroll { get; init; }
    /// <summary>
    /// Gets or sets the sticky start.
    /// </summary>
    public string? StickyStart { get; init; } // "bottom"|"top"|"left"|"right"
    /// <summary>
    /// Gets or sets the scroll x.
    /// </summary>
    public bool ScrollX { get; init; }
    /// <summary>
    /// Gets or sets the scroll y.
    /// </summary>
    public bool ScrollY { get; init; } = true;
    /// <summary>
    /// Gets or sets the viewport culling.
    /// </summary>
    public bool ViewportCulling { get; init; } = true;
}

/// <summary>
/// Scrollable container with viewport clipping and optional scrollbars.
/// Composite structure: root → wrapper (viewport + hScrollbar) + vScrollbar.
/// Matches TypeScript ScrollBoxRenderable from ScrollBox.ts.
/// </summary>
public class ScrollBoxRenderable : BoxRenderable
{
    private readonly BoxRenderable _wrapper;
    private readonly BoxRenderable _viewport;
    private readonly BoxRenderable _content;
    private readonly ScrollBarRenderable? _verticalScrollBar;
    private readonly ScrollBarRenderable? _horizontalScrollBar;

    private bool _scrollX;
    private bool _scrollY;
    private bool _stickyScroll;
    private bool _stickyScrollTop;
    private bool _stickyScrollBottom;
    private bool _stickyScrollLeft;
    private bool _stickyScrollRight;
    private string? _stickyStart;
    private bool _hasManualScroll;
    private bool _isApplyingStickyScroll;
    private bool _viewportCulling;

    // Fractional scroll accumulators for sub-pixel mouse scrolling
    private float _scrollAccumulatorX;
    private float _scrollAccumulatorY;

    /// <summary>
    /// Initializes a new instance of the ScrollBoxRenderable class.
    /// </summary>
    /// <param name="ctx">The render context.</param>
    /// <param name="options">The configuration options.</param>
    public ScrollBoxRenderable(IRenderContext ctx, ScrollBoxOptions? options = null)
        : base(ctx, options ?? new ScrollBoxOptions())
    {
        options ??= new ScrollBoxOptions();
        _scrollX = options.ScrollX;
        _scrollY = options.ScrollY;
        _stickyScroll = options.StickyScroll;
        _stickyStart = options.StickyStart;
        _viewportCulling = options.ViewportCulling;

        // Root layout: row (content area + vertical scrollbar)
        FlexDirection = FlexDirectionValue.Row;

        // Wrapper: column (viewport + horizontal scrollbar)
        _wrapper = new BoxRenderable(ctx, options.WrapperOptions ?? new BoxOptions());
        _wrapper.FlexDirection = FlexDirectionValue.Column;
        _wrapper.FlexGrow = 1;

        // Viewport: clipped area containing content
        _viewport = new BoxRenderable(ctx, options.ViewportOptions ?? new BoxOptions());
        _viewport.FlexGrow = 1;
        _viewport.FlexDirection = FlexDirectionValue.Column;
        _viewport.Overflow = OverflowValue.Hidden;
        _viewport.OnSizeChange = () => RecalculateBarProps();

        // Content: actual child container (scrolls within viewport)
        _content = new BoxRenderable(ctx, options.ContentOptions ?? new BoxOptions());
        _content.FlexShrink = 0;
        _content.AlignSelf = AlignValue.FlexStart;
        _content.OnSizeChange = () => RecalculateBarProps();

        // Match TS: constrain content width/height based on scroll directions
        if (_scrollX)
        {
            _content.MinWidth = DimensionValue.Percent(100);
        }
        else
        {
            _content.MinWidth = DimensionValue.Percent(100);
            _content.MaxWidth = DimensionValue.Percent(100);
        }

        if (_scrollY)
        {
            _content.MinHeight = DimensionValue.Percent(100);
        }
        else
        {
            _content.MinHeight = DimensionValue.Percent(100);
            _content.MaxHeight = DimensionValue.Percent(100);
        }

        // Build internal tree: viewport contains content
        _viewport.Add(_content);
        _wrapper.Add(_viewport);

        // Horizontal scrollbar
        if (_scrollX)
        {
            _horizontalScrollBar = new ScrollBarRenderable(
                ctx,
                CreateScrollBarOptions(
                    SliderOrientation.Horizontal,
                    options.ScrollbarOptions,
                    options.HorizontalScrollbarOptions,
                    OnHorizontalScroll));
            _horizontalScrollBar.HeightDimension = DimensionValue.Point(1);
            _wrapper.Add(_horizontalScrollBar);
        }

        base.Add(_wrapper);

        // Vertical scrollbar
        if (_scrollY)
        {
            _verticalScrollBar = new ScrollBarRenderable(
                ctx,
                CreateScrollBarOptions(
                    SliderOrientation.Vertical,
                    options.ScrollbarOptions,
                    options.VerticalScrollbarOptions,
                    OnVerticalScroll));
            _verticalScrollBar.WidthDimension = DimensionValue.Point(1);
            base.Add(_verticalScrollBar);
        }

        RecalculateBarProps();
    }

    #region Properties

    /// <summary>
    /// Gets or sets the scroll top.
    /// </summary>
    public float ScrollTop
    {
        get => _verticalScrollBar?.ScrollPosition ?? 0;
        set
        {
            if (_verticalScrollBar == null)
                return;

            _verticalScrollBar.ScrollPosition = value;
            OnVerticalScroll(_verticalScrollBar.ScrollPosition);
        }
    }

    /// <summary>
    /// Gets or sets the scroll left.
    /// </summary>
    public float ScrollLeft
    {
        get => _horizontalScrollBar?.ScrollPosition ?? 0;
        set
        {
            if (_horizontalScrollBar == null)
                return;

            _horizontalScrollBar.ScrollPosition = value;
            OnHorizontalScroll(_horizontalScrollBar.ScrollPosition);
        }
    }

    /// <summary>
    /// Gets the scroll height.
    /// </summary>
    public float ScrollHeight => _verticalScrollBar?.ScrollSize ?? 0;
    /// <summary>
    /// Gets the scroll width.
    /// </summary>
    public float ScrollWidth => _horizontalScrollBar?.ScrollSize ?? 0;
    /// <summary>
    /// Gets the max scroll top.
    /// </summary>
    public float MaxScrollTop => Math.Max(0, ScrollHeight - _viewport.Height);
    /// <summary>
    /// Gets the max scroll left.
    /// </summary>
    public float MaxScrollLeft => Math.Max(0, ScrollWidth - _viewport.Width);
    /// <summary>
    /// Gets the viewport height.
    /// </summary>
    public int ViewportHeight => _viewport.Height;
    /// <summary>
    /// Gets the viewport width.
    /// </summary>
    public int ViewportWidth => _viewport.Width;
    /// <summary>
    /// Gets the vertical scroll bar.
    /// </summary>
    public ScrollBarRenderable? VerticalScrollBar => _verticalScrollBar;
    /// <summary>
    /// Gets the horizontal scroll bar.
    /// </summary>
    public ScrollBarRenderable? HorizontalScrollBar => _horizontalScrollBar;

    /// <summary>
    /// Gets or sets the sticky scroll.
    /// </summary>
    public bool StickyScroll
    {
        get => _stickyScroll;
        set
        {
            _stickyScroll = value;
            UpdateStickyState();
        }
    }

    /// <summary>
    /// Gets or sets the sticky start.
    /// </summary>
    public string? StickyStart
    {
        get => _stickyStart;
        set
        {
            _stickyStart = value;
            UpdateStickyState();
        }
    }

    /// <summary>
    /// Gets or sets the viewport culling.
    /// </summary>
    public bool ViewportCulling
    {
        get => _viewportCulling;
        set => _viewportCulling = value;
    }

    #endregion

    #region Child Management (delegates to content)

    /// <inheritdoc />
    public override int Add(Renderable child, int? index = null)
    {
        return _content.Add(child, index);
    }

    /// <inheritdoc />
    public override void Remove(string id)
    {
        _content.Remove(id);
    }

    /// <summary>
    /// Gets a children.
    /// </summary>
    /// <returns>The children.</returns>
    public new Renderable[] GetChildren()
    {
        return _content.GetChildren().ToArray();
    }

    #endregion

    #region Scroll Operations

    /// <summary>
    /// Performs scroll by.
    /// </summary>
    /// <param name="deltaX">The delta x.</param>
    /// <param name="deltaY">The delta y.</param>
    /// <param name="unit">The unit.</param>
    public void ScrollBy(float deltaX, float deltaY, ScrollUnit unit = ScrollUnit.Absolute)
    {
        _verticalScrollBar?.ScrollBy(deltaY, unit);
        _horizontalScrollBar?.ScrollBy(deltaX, unit);
    }

    /// <summary>
    /// Performs scroll to.
    /// </summary>
    /// <param name="x">The horizontal position.</param>
    /// <param name="y">The vertical position.</param>
    public void ScrollTo(float? x = null, float? y = null)
    {
        if (x.HasValue) ScrollLeft = x.Value;
        if (y.HasValue) ScrollTop = y.Value;
    }

    /// <summary>Scroll a child renderable into view.</summary>
    public void ScrollChildIntoView(string childId)
    {
        // Find the child by walking the content's children
        var child = _content.FindDescendantById(childId);
        if (child == null) return;

        // Calculate the delta needed to bring the child into the viewport
        // This requires layout coordinates which are available after a layout pass
        // Simplified version: use child's screen coordinates relative to viewport
        // Full implementation would match TS getNearestDelta logic
    }

    #endregion

    #region Internal Scroll Callbacks

    private void OnVerticalScroll(float position)
    {
        // Use TranslateY (not Yoga Top) to shift content without triggering layout recalc
        _content.TranslateY = -position;

        if (!_isApplyingStickyScroll)
        {
            float maxScrollTop = Math.Max(0, ScrollHeight - _viewport.Height);
            if (!IsAtStickyPosition() && maxScrollTop > 1)
                _hasManualScroll = true;
        }

        UpdateStickyState();
    }

    private void OnHorizontalScroll(float position)
    {
        _content.TranslateX = -position;

        if (!_isApplyingStickyScroll)
        {
            float maxScrollLeft = Math.Max(0, ScrollWidth - _viewport.Width);
            if (!IsAtStickyPosition() && maxScrollLeft > 1)
                _hasManualScroll = true;
        }

        UpdateStickyState();
    }

    #endregion

    #region Mouse

    /// <inheritdoc />
    protected override void OnMouseEvent(UiMouseEvent evt)
    {
        if (evt.Type != MouseEventType.Scroll || evt.Scroll is not { } scroll)
            return;

        string direction = evt.Modifiers.Shift
            ? scroll.Direction switch
            {
                "up" => "left",
                "down" => "right",
                "left" => "down",
                "right" => "up",
                _ => scroll.Direction,
            }
            : scroll.Direction;

        float scrollAmount = Math.Max(0, scroll.Delta);
        bool handled = direction switch
        {
            "up" when _scrollY => ApplyAccumulatedScroll(ref _scrollAccumulatorY, -scrollAmount, vertical: true),
            "down" when _scrollY => ApplyAccumulatedScroll(ref _scrollAccumulatorY, scrollAmount, vertical: true),
            "left" when _scrollX => ApplyAccumulatedScroll(ref _scrollAccumulatorX, -scrollAmount, vertical: false),
            "right" when _scrollX => ApplyAccumulatedScroll(ref _scrollAccumulatorX, scrollAmount, vertical: false),
            _ => false,
        };

        if (!handled)
            return;

        if ((MaxScrollTop > 1 || MaxScrollLeft > 1) && !IsAtStickyPosition())
        {
            _hasManualScroll = true;
            evt.StopPropagation();
        }
    }

    private bool ApplyAccumulatedScroll(ref float accumulator, float delta, bool vertical)
    {
        accumulator += delta;
        int integerScroll = (int)MathF.Truncate(accumulator);

        if (integerScroll == 0)
            return false;

        if (vertical)
            ScrollTop += integerScroll;
        else
            ScrollLeft += integerScroll;

        accumulator -= integerScroll;
        return true;
    }

    #endregion

    #region Keyboard

    /// <inheritdoc />
    protected override void HandleKeyPress(KeyEvent key)
    {
        // Delegate to scrollbar key handlers
        _verticalScrollBar?.HandleKeyPressInternal(key);
        if (!key.IsPropagationStopped)
            _horizontalScrollBar?.HandleKeyPressInternal(key);
        if (!key.IsPropagationStopped)
            base.HandleKeyPress(key);
    }

    #endregion

    #region Update

    /// <inheritdoc />
    protected override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);
        RecalculateBarProps();
    }

    private void RecalculateBarProps()
    {
        bool wasApplyingStickyScroll = _isApplyingStickyScroll;
        _isApplyingStickyScroll = true;

        try
        {
            if (_verticalScrollBar != null)
            {
                _verticalScrollBar.ScrollSize = _content.Height;
                _verticalScrollBar.ViewportSize = _viewport.Height;
            }

            if (_horizontalScrollBar != null)
            {
                _horizontalScrollBar.ScrollSize = _content.Width;
                _horizontalScrollBar.ViewportSize = _viewport.Width;
            }

            float newMaxScrollTop = Math.Max(0, ScrollHeight - _viewport.Height);
            float newMaxScrollLeft = Math.Max(0, ScrollWidth - _viewport.Width);

            if (_stickyScroll)
            {
                if (!string.IsNullOrEmpty(_stickyStart) && !_hasManualScroll)
                {
                    ApplyStickyStart(_stickyStart);
                }
                else
                {
                    if (_stickyScrollTop)
                        ScrollTop = 0;
                    else if (_stickyScrollBottom && newMaxScrollTop > 0)
                        ScrollTop = newMaxScrollTop;

                    if (_stickyScrollLeft)
                        ScrollLeft = 0;
                    else if (_stickyScrollRight && newMaxScrollLeft > 0)
                        ScrollLeft = newMaxScrollLeft;
                }
            }
        }
        finally
        {
            _isApplyingStickyScroll = wasApplyingStickyScroll;
        }
    }

    private void ApplyStickyStart(string direction)
    {
        bool wasApplyingStickyScroll = _isApplyingStickyScroll;
        _isApplyingStickyScroll = true;

        try
        {
            switch (direction)
            {
                case "bottom":
                    _stickyScrollTop = false;
                    _stickyScrollBottom = true;
                    ScrollTop = Math.Max(0, ScrollHeight - _viewport.Height);
                    break;
                case "top":
                    _stickyScrollTop = true;
                    _stickyScrollBottom = false;
                    ScrollTop = 0;
                    break;
                case "right":
                    _stickyScrollLeft = false;
                    _stickyScrollRight = true;
                    ScrollLeft = Math.Max(0, ScrollWidth - _viewport.Width);
                    break;
                case "left":
                    _stickyScrollLeft = true;
                    _stickyScrollRight = false;
                    ScrollLeft = 0;
                    break;
            }
        }
        finally
        {
            _isApplyingStickyScroll = wasApplyingStickyScroll;
        }
    }

    private void UpdateStickyState()
    {
        if (!_stickyScroll)
            return;

        float maxScrollTop = Math.Max(0, ScrollHeight - _viewport.Height);
        float maxScrollLeft = Math.Max(0, ScrollWidth - _viewport.Width);

        if (ScrollTop <= 0)
        {
            _stickyScrollTop = true;
            _stickyScrollBottom = false;
            if (!_isApplyingStickyScroll &&
                (_stickyStart == "top" || (_stickyStart == "bottom" && maxScrollTop <= 0)))
            {
                _hasManualScroll = false;
            }
        }
        else if (ScrollTop >= maxScrollTop)
        {
            _stickyScrollTop = false;
            _stickyScrollBottom = true;
            if (!_isApplyingStickyScroll && _stickyStart == "bottom")
                _hasManualScroll = false;
        }
        else
        {
            _stickyScrollTop = false;
            _stickyScrollBottom = false;
        }

        if (ScrollLeft <= 0)
        {
            _stickyScrollLeft = true;
            _stickyScrollRight = false;
            if (!_isApplyingStickyScroll &&
                (_stickyStart == "left" || (_stickyStart == "right" && maxScrollLeft <= 0)))
            {
                _hasManualScroll = false;
            }
        }
        else if (ScrollLeft >= maxScrollLeft)
        {
            _stickyScrollLeft = false;
            _stickyScrollRight = true;
            if (!_isApplyingStickyScroll && _stickyStart == "right")
                _hasManualScroll = false;
        }
        else
        {
            _stickyScrollLeft = false;
            _stickyScrollRight = false;
        }
    }

    private bool IsAtStickyPosition()
    {
        if (!_stickyScroll || string.IsNullOrEmpty(_stickyStart))
            return false;

        float maxScrollTop = Math.Max(0, ScrollHeight - _viewport.Height);
        float maxScrollLeft = Math.Max(0, ScrollWidth - _viewport.Width);

        return _stickyStart switch
        {
            "top" => ScrollTop <= 0,
            "bottom" => ScrollTop >= maxScrollTop,
            "left" => ScrollLeft <= 0,
            "right" => ScrollLeft >= maxScrollLeft,
            _ => false,
        };
    }

    private static ScrollBarOptions CreateScrollBarOptions(
        SliderOrientation orientation,
        ScrollBarOptions? sharedOptions,
        ScrollBarOptions? specificOptions,
        Action<float> onChange)
    {
        var arrowOptions = specificOptions?.ArrowOptions ?? sharedOptions?.ArrowOptions;
        var trackOptions = specificOptions?.TrackOptions ?? sharedOptions?.TrackOptions;

        return new ScrollBarOptions
        {
            Orientation = orientation,
            ShowArrows = specificOptions?.ShowArrows ?? sharedOptions?.ShowArrows ?? false,
            ArrowOptions = arrowOptions == null
                ? null
                : new ArrowOptions
                {
                    Direction = orientation == SliderOrientation.Vertical ? ArrowDirection.Up : ArrowDirection.Left,
                    ForegroundColor = arrowOptions.ForegroundColor,
                    BackgroundColor = arrowOptions.BackgroundColor,
                    Attributes = arrowOptions.Attributes,
                    ArrowChar = arrowOptions.ArrowChar,
                },
            TrackOptions = trackOptions == null
                ? null
                : new SliderOptions
                {
                    Orientation = orientation,
                    BackgroundColor = trackOptions.BackgroundColor,
                    ForegroundColor = trackOptions.ForegroundColor,
                },
            OnChange = onChange,
        };
    }

    #endregion

    #region Dispose

    /// <inheritdoc />
    protected override void DestroySelf()
    {
        base.DestroySelf();
    }

    #endregion
}
