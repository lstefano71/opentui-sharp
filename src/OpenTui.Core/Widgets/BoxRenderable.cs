using Facebook.Yoga;

namespace OpenTui.Core;

/// <summary>
/// Container renderable with optional border, background fill, and title.
/// Matches TypeScript BoxRenderable from Box.ts.
///
/// The actual pixel drawing is delegated entirely to <see cref="OptimizedBuffer.DrawBox"/>.
/// This class decides *what* to draw: which colors, which sides, whether to fill.
/// </summary>
public class BoxRenderable : Renderable
{
    private Rgba _backgroundColor;
    private BorderStyle _borderStyle;
    private bool _hasBorder;
    private BorderSides _borderSides;
    private Rgba _borderColor;
    private Rgba _focusedBorderColor;
    private BorderCharacters? _customBorderChars;
    private bool _shouldFill;
    private string? _title;
    private TitleAlignment _titleAlignment;
    private string? _bottomTitle;
    private TitleAlignment _bottomTitleAlignment;

    public BoxRenderable(IRenderContext ctx, BoxOptions? options = null)
        : base(ctx, options ?? new BoxOptions())
    {
        options ??= new BoxOptions();

        _backgroundColor = options.BackgroundColor ?? Rgba.Transparent;
        _borderStyle = options.BorderStyle;
        _shouldFill = options.ShouldFill;
        _title = options.Title;
        _titleAlignment = options.TitleAlignment;
        _bottomTitle = options.BottomTitle;
        _bottomTitleAlignment = options.BottomTitleAlignment;

        _borderColor = options.BorderColor ?? Rgba.FromInts(255, 255, 255);
        _focusedBorderColor = options.FocusedBorderColor ?? Rgba.FromHex("#00AAFF");
        _customBorderChars = options.CustomBorderChars;

        if (options.BoxFocusable)
            Focusable = true;

        // Auto-enable border if border-related props are set
        _hasBorder = options.Border
            || options.CustomBorderChars is not null
            || options.BorderColor is not null
            || options.FocusedBorderColor is not null;

        _borderSides = options.BorderSidesOverride ?? (_hasBorder ? BorderSides.All : BorderSides.None);

        ApplyYogaBorders();
    }

    #region Properties

    public Rgba BackgroundColor
    {
        get => _backgroundColor;
        set { _backgroundColor = value; RequestRender(); }
    }

    public bool HasBorder => _hasBorder;

    public bool Border
    {
        get => _hasBorder;
        set
        {
            _hasBorder = value;
            _borderSides = value ? BorderSides.All : BorderSides.None;
            ApplyYogaBorders();
            RequestRender();
        }
    }

    public BorderSides ActiveBorderSides
    {
        get => _borderSides;
        set
        {
            _borderSides = value;
            _hasBorder = value != BorderSides.None;
            ApplyYogaBorders();
            RequestRender();
        }
    }

    public BorderStyle BorderStyle
    {
        get => _borderStyle;
        set
        {
            _borderStyle = value;
            InitializeBorder();
            RequestRender();
        }
    }

    public Rgba BorderColor
    {
        get => _borderColor;
        set
        {
            _borderColor = value;
            InitializeBorder();
            RequestRender();
        }
    }

    public Rgba FocusedBorderColor
    {
        get => _focusedBorderColor;
        set
        {
            _focusedBorderColor = value;
            InitializeBorder();
            RequestRender();
        }
    }

    public BorderCharacters? CustomBorderChars
    {
        get => _customBorderChars;
        set
        {
            _customBorderChars = value;
            if (value is not null) InitializeBorder();
            RequestRender();
        }
    }

    public bool ShouldFill
    {
        get => _shouldFill;
        set { _shouldFill = value; RequestRender(); }
    }

    public string? Title
    {
        get => _title;
        set { _title = value; RequestRender(); }
    }

    public TitleAlignment TitleAlignment
    {
        get => _titleAlignment;
        set { _titleAlignment = value; RequestRender(); }
    }

    public string? BottomTitle
    {
        get => _bottomTitle;
        set { _bottomTitle = value; RequestRender(); }
    }

    public TitleAlignment BottomTitleAlignment
    {
        get => _bottomTitleAlignment;
        set { _bottomTitleAlignment = value; RequestRender(); }
    }

    #endregion

    #region Gap

    public void SetGap(float value)
    {
        YGNodeStyleAPI.YGNodeStyleSetGap(YogaNode, YGGutter.All, value);
        RequestRender();
    }

    public void SetRowGap(float value)
    {
        YGNodeStyleAPI.YGNodeStyleSetGap(YogaNode, YGGutter.Row, value);
        RequestRender();
    }

    public void SetColumnGap(float value)
    {
        YGNodeStyleAPI.YGNodeStyleSetGap(YogaNode, YGGutter.Column, value);
        RequestRender();
    }

    #endregion

    #region Rendering

    protected override void RenderSelf(OptimizedBuffer buffer, float deltaTime)
    {
        bool hasBorderVisible = _borderSides != BorderSides.None;
        bool hasVisibleFill = _shouldFill && _backgroundColor.A > 0;

        // Layout-only boxes: skip native FFI cost
        if (!hasBorderVisible && !hasVisibleFill) return;

        bool hasFocusWithin = _focusable && (_focused || _hasFocusedDescendant);
        var currentBorderColor = hasFocusWithin ? _focusedBorderColor : _borderColor;

        var borderChars = _customBorderChars ?? BorderCharacters.ForStyle(_borderStyle);
        int baseX = _buffered ? 0 : (int)_screenX;
        int baseY = _buffered ? 0 : (int)_screenY;

        buffer.DrawBox(
            baseX, baseY,
            (uint)_widthValue, (uint)_heightValue,
            borderChars: borderChars,
            sides: _borderSides,
            shouldFill: _shouldFill,
            borderColor: currentBorderColor,
            backgroundColor: _backgroundColor,
            title: _title,
            titleAlignment: _titleAlignment,
            bottomTitle: _bottomTitle,
            bottomTitleAlignment: _bottomTitleAlignment);
    }

    protected override (int x, int y, int w, int h) GetScissorRect()
    {
        var baseX = _buffered ? 0 : (int)_screenX;
        var baseY = _buffered ? 0 : (int)_screenY;
        var w = _widthValue;
        var h = _heightValue;

        // Inset by border widths so children clip inside the border
        int topInset = (_borderSides & BorderSides.Top) != 0 ? 1 : 0;
        int bottomInset = (_borderSides & BorderSides.Bottom) != 0 ? 1 : 0;
        int leftInset = (_borderSides & BorderSides.Left) != 0 ? 1 : 0;
        int rightInset = (_borderSides & BorderSides.Right) != 0 ? 1 : 0;

        return (
            baseX + leftInset,
            baseY + topInset,
            w - leftInset - rightInset,
            h - topInset - bottomInset);
    }

    #endregion

    #region Yoga Border Helpers

    private void InitializeBorder()
    {
        if (!_hasBorder)
        {
            _hasBorder = true;
            _borderSides = BorderSides.All;
            ApplyYogaBorders();
        }
    }

    private void ApplyYogaBorders()
    {
        YGNodeStyleAPI.YGNodeStyleSetBorder(YogaNode, YGEdge.Left,
            (_borderSides & BorderSides.Left) != 0 ? 1f : 0f);
        YGNodeStyleAPI.YGNodeStyleSetBorder(YogaNode, YGEdge.Right,
            (_borderSides & BorderSides.Right) != 0 ? 1f : 0f);
        YGNodeStyleAPI.YGNodeStyleSetBorder(YogaNode, YGEdge.Top,
            (_borderSides & BorderSides.Top) != 0 ? 1f : 0f);
        YGNodeStyleAPI.YGNodeStyleSetBorder(YogaNode, YGEdge.Bottom,
            (_borderSides & BorderSides.Bottom) != 0 ? 1f : 0f);
    }

    #endregion
}
