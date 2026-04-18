using Facebook.Yoga;

namespace OpenTui.Core;

/// <summary>
/// Options for ScrollBox renderable.
/// Matches TypeScript ScrollBoxOptions.
/// </summary>
public class ScrollBoxOptions : BoxOptions
{
    public BoxOptions? RootOptions { get; init; }
    public BoxOptions? WrapperOptions { get; init; }
    public BoxOptions? ViewportOptions { get; init; }
    public BoxOptions? ContentOptions { get; init; }
    public ScrollBarOptions? ScrollbarOptions { get; init; }
    public ScrollBarOptions? VerticalScrollbarOptions { get; init; }
    public ScrollBarOptions? HorizontalScrollbarOptions { get; init; }
    public bool StickyScroll { get; init; }
    public string? StickyStart { get; init; } // "bottom"|"top"|"left"|"right"
    public bool ScrollX { get; init; }
    public bool ScrollY { get; init; } = true;
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
    private string? _stickyStart;
    private bool _hasManualScroll;
    private bool _viewportCulling;

    // Fractional scroll accumulators for sub-pixel mouse scrolling
    private float _scrollAccumulatorX;
    private float _scrollAccumulatorY;

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
            _horizontalScrollBar = new ScrollBarRenderable(ctx, new ScrollBarOptions
            {
                Orientation = SliderOrientation.Horizontal,
                OnChange = OnHorizontalScroll,
            });
            _horizontalScrollBar.HeightDimension = DimensionValue.Point(1);
            _wrapper.Add(_horizontalScrollBar);
        }

        base.Add(_wrapper);

        // Vertical scrollbar
        if (_scrollY)
        {
            _verticalScrollBar = new ScrollBarRenderable(ctx, new ScrollBarOptions
            {
                Orientation = SliderOrientation.Vertical,
                OnChange = OnVerticalScroll,
            });
            _verticalScrollBar.WidthDimension = DimensionValue.Point(1);
            base.Add(_verticalScrollBar);
        }
    }

    #region Properties

    public float ScrollTop
    {
        get => _verticalScrollBar?.ScrollPosition ?? 0;
        set { if (_verticalScrollBar != null) _verticalScrollBar.ScrollPosition = value; }
    }

    public float ScrollLeft
    {
        get => _horizontalScrollBar?.ScrollPosition ?? 0;
        set { if (_horizontalScrollBar != null) _horizontalScrollBar.ScrollPosition = value; }
    }

    public float ScrollHeight => _verticalScrollBar?.ScrollSize ?? 0;
    public float ScrollWidth => _horizontalScrollBar?.ScrollSize ?? 0;
    public float MaxScrollTop => Math.Max(0, ScrollHeight - _viewport.Height);
    public float MaxScrollLeft => Math.Max(0, ScrollWidth - _viewport.Width);
    public int ViewportHeight => _viewport.Height;
    public int ViewportWidth => _viewport.Width;

    public bool StickyScroll
    {
        get => _stickyScroll;
        set => _stickyScroll = value;
    }

    public string? StickyStart
    {
        get => _stickyStart;
        set
        {
            _stickyStart = value;
            if (_stickyScroll && !string.IsNullOrEmpty(value))
                ApplyStickyStart(value);
        }
    }

    public bool ViewportCulling
    {
        get => _viewportCulling;
        set => _viewportCulling = value;
    }

    #endregion

    #region Child Management (delegates to content)

    public override int Add(Renderable child, int? index = null)
    {
        return _content.Add(child, index);
    }

    public override void Remove(string id)
    {
        _content.Remove(id);
    }

    public new Renderable[] GetChildren()
    {
        return _content.GetChildren().ToArray();
    }

    #endregion

    #region Scroll Operations

    public void ScrollBy(float deltaX, float deltaY, ScrollUnit unit = ScrollUnit.Absolute)
    {
        _verticalScrollBar?.ScrollBy(deltaY, unit);
        _horizontalScrollBar?.ScrollBy(deltaX, unit);
        _hasManualScroll = true;
    }

    public void ScrollTo(float? x = null, float? y = null)
    {
        if (x.HasValue) ScrollLeft = x.Value;
        if (y.HasValue) ScrollTop = y.Value;
        _hasManualScroll = true;
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
    }

    private void OnHorizontalScroll(float position)
    {
        _content.TranslateX = -position;
    }

    #endregion

    #region Mouse

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

        if (MaxScrollTop > 1 || MaxScrollLeft > 1)
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

    protected override void HandleKeyPress(KeyEvent key)
    {
        // Delegate to scrollbar key handlers
        _verticalScrollBar?.HandleKeyPressInternal(key);
        if (!key.IsPropagationStopped)
            _horizontalScrollBar?.HandleKeyPressInternal(key);
        if (key.IsPropagationStopped)
            _hasManualScroll = true;
        else
            base.HandleKeyPress(key);
    }

    #endregion

    #region Update

    protected override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);
        RecalculateBarProps();
    }

    private void RecalculateBarProps()
    {
        // Update scrollbar dimensions based on content vs viewport size
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

        // Sticky scroll: if enabled and no manual scroll, snap to edge
        if (_stickyScroll && !_hasManualScroll && _stickyStart != null)
        {
            ApplyStickyStart(_stickyStart);
        }
    }

    private void ApplyStickyStart(string direction)
    {
        switch (direction)
        {
            case "bottom":
                if (_verticalScrollBar != null)
                    _verticalScrollBar.ScrollPosition = _verticalScrollBar.ScrollSize;
                break;
            case "top":
                if (_verticalScrollBar != null)
                    _verticalScrollBar.ScrollPosition = 0;
                break;
            case "right":
                if (_horizontalScrollBar != null)
                    _horizontalScrollBar.ScrollPosition = _horizontalScrollBar.ScrollSize;
                break;
            case "left":
                if (_horizontalScrollBar != null)
                    _horizontalScrollBar.ScrollPosition = 0;
                break;
        }
    }

    #endregion

    #region Dispose

    protected override void DestroySelf()
    {
        base.DestroySelf();
    }

    #endregion
}
