using Facebook.Yoga;

namespace OpenTui.Core;

/// <summary>
/// Abstract base renderable — the core abstraction tying layout (Yoga node),
/// events (EventEmitter), child management, and rendering together.
/// Matches TypeScript BaseRenderable + Renderable classes from Renderable.ts.
///
/// Thread safety: Renderable and the render tree are single-threaded (like TS/Node.js).
/// All operations must happen on the render thread.
/// </summary>
public abstract class Renderable : EventEmitter
{
    #region Static

    private static int s_nextNum = 1;
    private static readonly Dictionary<int, Renderable> s_renderablesByNumber = [];

    /// <summary>Look up a renderable by its auto-assigned number (used by hit grid).</summary>
    public static Renderable? GetByNumber(int num) =>
        s_renderablesByNumber.TryGetValue(num, out var r) ? r : null;

    #endregion

    #region Fields — identity & base state

    private string _id;
    /// <summary>
    /// Gets the num.
    /// </summary>
    public int Num { get; }
    /// <summary>
    /// Stores the dirty.
    /// </summary>
    protected bool _dirty;
    /// <summary>
    /// Stores the visible.
    /// </summary>
    protected bool _visible = true;
    /// <summary>
    /// Stores the is destroyed.
    /// </summary>
    protected bool _isDestroyed;

    #endregion

    #region Fields — layout

    /// <summary>
    /// Stores the translate x.
    /// </summary>
    protected float _translateX;
    /// <summary>
    /// Stores the translate y.
    /// </summary>
    protected float _translateY;
    /// <summary>
    /// Stores the x.
    /// </summary>
    protected float _x;
    /// <summary>
    /// Stores the y.
    /// </summary>
    protected float _y;
    /// <summary>
    /// Stores the screen x.
    /// </summary>
    protected float _screenX;
    /// <summary>
    /// Stores the screen y.
    /// </summary>
    protected float _screenY;
    /// <summary>
    /// Stores the width.
    /// </summary>
    protected DimensionValue _width;
    /// <summary>
    /// Stores the height.
    /// </summary>
    protected DimensionValue _height;
    /// <summary>
    /// Stores the width value.
    /// </summary>
    protected int _widthValue;
    /// <summary>
    /// Stores the height value.
    /// </summary>
    protected int _heightValue;
    private int _zIndex;
    private float _flexShrink = 1f;
    /// <summary>
    /// Stores the position type.
    /// </summary>
    protected PositionValue _positionType = PositionValue.Relative;
    /// <summary>
    /// Stores the overflow.
    /// </summary>
    protected OverflowValue _overflow = OverflowValue.Visible;
    /// <summary>
    /// Stores the position.
    /// </summary>
    protected (DimensionValue? Top, DimensionValue? Right, DimensionValue? Bottom, DimensionValue? Left) _position;
    /// <summary>
    /// Stores the opacity.
    /// </summary>
    protected float _opacity = 1.0f;

    /// <summary>
    /// Stores the yoga node.
    /// </summary>
    protected Node YogaNode;

    #endregion

    #region Fields — children

    private readonly Dictionary<string, Renderable> _renderableMapById = [];
    /// <summary>
    /// Stores the children in layout order.
    /// </summary>
    protected readonly List<Renderable> _childrenInLayoutOrder = [];
    /// <summary>
    /// Stores the children in z index order.
    /// </summary>
    protected readonly List<Renderable> _childrenInZIndexOrder = [];
    private bool _needsZIndexSort;
    private Renderable? _parent;

    private bool _childrenPrimarySortDirty = true;
    private List<Renderable> _childrenSortedByPrimaryAxis = [];
    private readonly HashSet<Renderable> _shouldUpdateBefore = [];

    #endregion

    #region Fields — state

    /// <summary>
    /// Gets or sets the selectable.
    /// </summary>
    public bool Selectable { get; set; }
    /// <summary>
    /// Stores the focusable.
    /// </summary>
    protected bool _focusable;
    /// <summary>
    /// Stores the focused.
    /// </summary>
    protected bool _focused;
    /// <summary>
    /// Stores the has focused descendant.
    /// </summary>
    protected bool _hasFocusedDescendant;
    private bool _live;
    /// <summary>
    /// Stores the live count.
    /// </summary>
    protected int _liveCount;
    /// <summary>
    /// Stores the buffered.
    /// </summary>
    protected bool _buffered;
    /// <summary>
    /// Stores the frame buffer.
    /// </summary>
    protected OptimizedBuffer? _frameBuffer;
    private int _lastLayoutFrame = -1;

    #endregion

    #region Fields — event handlers

    /// <summary>
    /// Stores the keypress handler.
    /// </summary>
    protected Action<KeyEvent>? _keypressHandler;
    /// <summary>
    /// Stores the paste handler.
    /// </summary>
    protected Action<PasteEvent>? _pasteHandler;
    private IDisposable? _keypressSubscription;
    private IDisposable? _pasteSubscription;
    private Action? _sizeChangeListener;
    private Action<UiMouseEvent>? _mouseListener;
    private readonly Dictionary<string, Action<UiMouseEvent>> _mouseListeners = [];
    private Action<PasteEvent>? _pasteListener;
    private Action<KeyEvent>? _keyDownListener;

    /// <summary>
    /// Gets or sets the on lifecycle pass.
    /// </summary>
    public Action? OnLifecyclePass { get; set; }
    /// <summary>
    /// Gets or sets the render before hook.
    /// </summary>
    public Action<OptimizedBuffer, float>? RenderBeforeHook { get; set; }
    /// <summary>
    /// Gets or sets the render after hook.
    /// </summary>
    public Action<OptimizedBuffer, float>? RenderAfterHook { get; set; }

    #endregion

    #region Context

    /// <summary>
    /// Stores the ctx.
    /// </summary>
    protected readonly IRenderContext _ctx;

    /// <summary>
    /// Gets the ctx.
    /// </summary>
    public IRenderContext Ctx => _ctx;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the Renderable class.
    /// </summary>
    /// <param name="ctx">The render context.</param>
    /// <param name="options">The configuration options.</param>
    protected Renderable(IRenderContext ctx, RenderableOptions options)
    {
        _ctx = ctx;
        Num = Interlocked.Increment(ref s_nextNum);
        _id = options.Id ?? $"renderable-{Num}";
        s_renderablesByNumber[Num] = this;

        RenderBeforeHook = options.RenderBefore;
        RenderAfterHook = options.RenderAfter;

        _width = options.Width ?? DimensionValue.Auto;
        _height = options.Height ?? DimensionValue.Auto;
        _position = (options.Top, options.Right, options.Bottom, options.Left);

        if (_width.IsPoint) _widthValue = (int)_width.Value;
        if (_height.IsPoint) _heightValue = (int)_height.Value;

        _zIndex = options.ZIndex ?? 0;
        _visible = options.Visible != false;
        _buffered = options.Buffered ?? false;
        _live = options.Live ?? false;
        _liveCount = _live && _visible ? 1 : 0;
        _opacity = options.Opacity is { } op ? Math.Clamp(op, 0f, 1f) : 1.0f;

        YogaNode = new Node(LayoutConfig.Shared);
        YGNodeStyleAPI.YGNodeStyleSetDisplay(YogaNode, _visible ? YGDisplay.Flex : YGDisplay.None);
        SetupYogaProperties(options);
        ApplyEventOptions(options);

        if (_buffered) CreateFrameBuffer();
    }

    #endregion

    #region Id

    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public string Id
    {
        get => _id;
        set
        {
            if (_parent is not null)
            {
                _parent._renderableMapById.Remove(_id);
                _parent._renderableMapById[value] = this;
            }
            _id = value;
        }
    }

    #endregion

    #region Dirty

    /// <summary>
    /// Gets a value indicating whether is dirty.
    /// </summary>
    public bool IsDirty => _dirty;
    /// <summary>
    /// Performs mark clean.
    /// </summary>
    protected void MarkClean() => _dirty = false;
    /// <summary>
    /// Performs mark dirty.
    /// </summary>
    protected void MarkDirty() => _dirty = true;

    #endregion

    #region Parent

    /// <summary>
    /// Gets or sets the parent.
    /// </summary>
    public Renderable? Parent
    {
        get => _parent;
        internal set => _parent = value;
    }

    #endregion

    #region Visible

    /// <summary>
    /// Gets or sets the visible.
    /// </summary>
    public bool Visible
    {
        get => _visible;
        set
        {
            if (_visible == value) return;

            var wasVisible = _visible;
            _visible = value;
            YGNodeStyleAPI.YGNodeStyleSetDisplay(YogaNode, value ? YGDisplay.Flex : YGDisplay.None);

            if (_live)
            {
                if (!wasVisible && value) PropagateLiveCount(1);
                else if (wasVisible && !value) PropagateLiveCount(-1);
            }

            if (_focused) Blur();
            RequestRender();
        }
    }

    #endregion

    #region Opacity

    /// <summary>
    /// Gets or sets the opacity.
    /// </summary>
    public float Opacity
    {
        get => _opacity;
        set
        {
            var clamped = Math.Clamp(value, 0f, 1f);
            if (_opacity != clamped)
            {
                _opacity = clamped;
                RequestRender();
            }
        }
    }

    #endregion

    #region Focus

    /// <summary>
    /// Gets or sets the focusable.
    /// </summary>
    public bool Focusable
    {
        get => _focusable;
        set => _focusable = value;
    }

    /// <summary>
    /// Gets the focused.
    /// </summary>
    public bool Focused => _focused;
    /// <summary>
    /// Gets a value indicating whether has focused descendant.
    /// </summary>
    public bool HasFocusedDescendant => _hasFocusedDescendant;

    /// <summary>
    /// Gives this instance input focus.
    /// </summary>
    public virtual void Focus()
    {
        if (_isDestroyed || _focused || !_focusable) return;

        _ctx.FocusRenderable(this);
        _focused = true;
        RequestRender();

        _keypressHandler = key =>
        {
            if (_isDestroyed) return;
            _keyDownListener?.Invoke(key);
            if (_isDestroyed) return;
            if (!key.IsDefaultPrevented)
                HandleKeyPress(key);
        };

        _pasteHandler = evt =>
        {
            if (_isDestroyed) return;
            _pasteListener?.Invoke(evt);
            if (_isDestroyed) return;
            if (!evt.IsDefaultPrevented)
                HandlePaste(evt);
        };

        _keypressSubscription = _ctx.InternalKeyInput.OnRenderable<KeyEvent>(KeyHandlerEvents.Keypress, _keypressHandler);
        _pasteSubscription = _ctx.InternalKeyInput.OnRenderable<PasteEvent>(KeyHandlerEvents.Paste, _pasteHandler);

        PropagateFocusChange(true);
        Emit(RenderableEventNames.Focused);
    }

    /// <summary>
    /// Removes input focus from this instance.
    /// </summary>
    public virtual void Blur()
    {
        if (!_focused || !_focusable) return;

        _ctx.BlurRenderable(this);
        _focused = false;
        RequestRender();

        _keypressSubscription?.Dispose();
        _keypressSubscription = null;
        _keypressHandler = null;

        _pasteSubscription?.Dispose();
        _pasteSubscription = null;
        _pasteHandler = null;

        PropagateFocusChange(false);
        Emit(RenderableEventNames.Blurred);
    }

    /// <summary>
    /// Performs propagate focus change.
    /// </summary>
    /// <param name="hasFocus">The has focus.</param>
    protected void PropagateFocusChange(bool hasFocus)
    {
        var parent = _parent;
        while (parent is not null)
        {
            if (parent._hasFocusedDescendant != hasFocus)
            {
                parent._hasFocusedDescendant = hasFocus;
                parent.MarkDirty();
            }
            parent = parent._parent;
        }
        RequestRender();
    }

    /// <summary>Override in subclasses to handle key events when focused.</summary>
    protected virtual void HandleKeyPress(KeyEvent key) { }

    /// <summary>Override in subclasses to handle paste events when focused.</summary>
    protected virtual void HandlePaste(PasteEvent evt) { }

    #endregion

    #region Live

    /// <summary>
    /// Gets or sets the live.
    /// </summary>
    public bool Live
    {
        get => _live;
        set
        {
            if (_live == value) return;
            _live = value;
            if (_visible) PropagateLiveCount(value ? 1 : -1);
        }
    }

    /// <summary>
    /// Gets the live count.
    /// </summary>
    public int LiveCount => _liveCount;

    /// <summary>
    /// Performs propagate live count.
    /// </summary>
    /// <param name="delta">The delta.</param>
    protected virtual void PropagateLiveCount(int delta)
    {
        _liveCount += delta;
        _parent?.PropagateLiveCount(delta);
    }

    #endregion

    #region Selection hooks (virtual)

    /// <summary>
    /// Performs has selection.
    /// </summary>
    /// <returns>true if has selection; otherwise, false.</returns>
    public virtual bool HasSelection() => false;
    /// <summary>
    /// Handles the selection changed.
    /// </summary>
    /// <param name="selection">The selection.</param>
    /// <returns>true if on selection changed; otherwise, false.</returns>
    public virtual bool OnSelectionChanged(Selection? selection) => false;
    /// <summary>
    /// Gets a selected text.
    /// </summary>
    /// <returns>The selected text.</returns>
    public virtual string GetSelectedText() => "";
    /// <summary>
    /// Performs should start selection.
    /// </summary>
    /// <param name="x">The horizontal position.</param>
    /// <param name="y">The vertical position.</param>
    /// <returns>true if should start selection; otherwise, false.</returns>
    public virtual bool ShouldStartSelection(int x, int y) => false;

    #endregion

    #region Position / Layout Properties

    /// <summary>
    /// Gets or sets the translate x.
    /// </summary>
    public float TranslateX
    {
        get => _translateX;
        set
        {
            if (_translateX == value) return;
            _translateX = value;
            var parentScreenX = _parent?._screenX ?? 0;
            _screenX = parentScreenX + _x + _translateX;
            if (_parent is not null) _parent._childrenPrimarySortDirty = true;
            RequestRender();
        }
    }

    /// <summary>
    /// Gets or sets the translate y.
    /// </summary>
    public float TranslateY
    {
        get => _translateY;
        set
        {
            if (_translateY == value) return;
            _translateY = value;
            var parentScreenY = _parent?._screenY ?? 0;
            _screenY = parentScreenY + _y + _translateY;
            if (_parent is not null) _parent._childrenPrimarySortDirty = true;
            RequestRender();
        }
    }

    /// <summary>
    /// Gets the screen x.
    /// </summary>
    public float ScreenX
    {
        get
        {
            var parentScreenX = _parent?._screenX ?? 0;
            return parentScreenX + _x + _translateX;
        }
    }

    /// <summary>
    /// Gets the screen y.
    /// </summary>
    public float ScreenY
    {
        get
        {
            var parentScreenY = _parent?._screenY ?? 0;
            return parentScreenY + _y + _translateY;
        }
    }

    /// <summary>
    /// Gets or sets the x.
    /// </summary>
    public int X
    {
        get => _parent is not null
            ? (int)(_parent.X + _x + _translateX)
            : (int)(_x + _translateX);
        set => Left = DimensionValue.Point(value);
    }

    /// <summary>
    /// Gets or sets the y.
    /// </summary>
    public int Y
    {
        get => _parent is not null
            ? (int)(_parent.Y + _y + _translateY)
            : (int)(_y + _translateY);
        set => Top = DimensionValue.Point(value);
    }

    /// <summary>Computed width in cells (from Yoga layout).</summary>
    public int Width => _widthValue;

    /// <summary>Computed height in cells (from Yoga layout).</summary>
    public int Height => _heightValue;

    #endregion

    #region Dimension Setters

    /// <summary>
    /// Gets or sets the width dimension.
    /// </summary>
    public DimensionValue WidthDimension
    {
        get => _width;
        set
        {
            if (_width.Equals(value)) return;
            _width = value;
            SetYogaDimension(
                YGNodeStyleAPI.YGNodeStyleSetWidth,
                YGNodeStyleAPI.YGNodeStyleSetWidthPercent,
                YGNodeStyleAPI.YGNodeStyleSetWidthAuto,
                value);

            if (value.IsPoint && _flexShrink == 1f)
            {
                _flexShrink = 0f;
                YGNodeStyleAPI.YGNodeStyleSetFlexShrink(YogaNode, 0f);
            }
            RequestRender();
        }
    }

    /// <summary>
    /// Gets or sets the height dimension.
    /// </summary>
    public DimensionValue HeightDimension
    {
        get => _height;
        set
        {
            if (_height.Equals(value)) return;
            _height = value;
            SetYogaDimension(
                YGNodeStyleAPI.YGNodeStyleSetHeight,
                YGNodeStyleAPI.YGNodeStyleSetHeightPercent,
                YGNodeStyleAPI.YGNodeStyleSetHeightAuto,
                value);

            if (value.IsPoint && _flexShrink == 1f)
            {
                _flexShrink = 0f;
                YGNodeStyleAPI.YGNodeStyleSetFlexShrink(YogaNode, 0f);
            }
            RequestRender();
        }
    }

    #endregion

    #region Z-Index

    /// <summary>
    /// Gets or sets the z index.
    /// </summary>
    public int ZIndex
    {
        get => _zIndex;
        set
        {
            if (_zIndex != value)
            {
                _zIndex = value;
                _parent?.RequestZIndexSort();
                RequestRender();
            }
        }
    }

    private void RequestZIndexSort() => _needsZIndexSort = true;

    private void EnsureZIndexSorted()
    {
        if (!_needsZIndexSort) return;
        // Stable sort by (zIndex, layoutOrderIndex)
        _childrenInZIndexOrder.Sort((a, b) =>
        {
            int cmp = a._zIndex.CompareTo(b._zIndex);
            if (cmp != 0) return cmp;
            return _childrenInLayoutOrder.IndexOf(a).CompareTo(_childrenInLayoutOrder.IndexOf(b));
        });
        _needsZIndexSort = false;
    }

    #endregion

    #region Position (top/right/bottom/left)

    /// <summary>
    /// Gets or sets the top.
    /// </summary>
    public DimensionValue? Top
    {
        get => _position.Top;
        set { _position.Top = value; UpdateYogaPosition(top: value); }
    }

    /// <summary>
    /// Gets or sets the right.
    /// </summary>
    public DimensionValue? Right
    {
        get => _position.Right;
        set { _position.Right = value; UpdateYogaPosition(right: value); }
    }

    /// <summary>
    /// Gets or sets the bottom.
    /// </summary>
    public DimensionValue? Bottom
    {
        get => _position.Bottom;
        set { _position.Bottom = value; UpdateYogaPosition(bottom: value); }
    }

    /// <summary>
    /// Gets or sets the left.
    /// </summary>
    public DimensionValue? Left
    {
        get => _position.Left;
        set { _position.Left = value; UpdateYogaPosition(left: value); }
    }

    /// <summary>
    /// Gets or sets the position type.
    /// </summary>
    public PositionValue PositionType
    {
        set
        {
            if (_positionType == value) return;
            _positionType = value;
            YGNodeStyleAPI.YGNodeStyleSetPositionType(YogaNode, value.ToYoga());
            RequestRender();
        }
    }

    /// <summary>
    /// Gets or sets the overflow.
    /// </summary>
    public OverflowValue Overflow
    {
        get => _overflow;
        set
        {
            if (_overflow == value) return;
            _overflow = value;
            YGNodeStyleAPI.YGNodeStyleSetOverflow(YogaNode, value.ToYoga());
            RequestRender();
        }
    }

    #endregion

    #region Flex Setters

    /// <summary>
    /// Gets or sets the flex grow.
    /// </summary>
    public float FlexGrow { set { YGNodeStyleAPI.YGNodeStyleSetFlexGrow(YogaNode, value); RequestRender(); } }

    /// <summary>
    /// Gets or sets the flex shrink.
    /// </summary>
    public float FlexShrink
    {
        set { _flexShrink = value; YGNodeStyleAPI.YGNodeStyleSetFlexShrink(YogaNode, value); RequestRender(); }
    }

    /// <summary>
    /// Gets or sets the flex direction.
    /// </summary>
    public FlexDirectionValue FlexDirection
    {
        set { YGNodeStyleAPI.YGNodeStyleSetFlexDirection(YogaNode, value.ToYoga()); RequestRender(); }
    }

    /// <summary>
    /// Gets or sets the flex wrap.
    /// </summary>
    public WrapValue FlexWrap { set { YGNodeStyleAPI.YGNodeStyleSetFlexWrap(YogaNode, value.ToYoga()); RequestRender(); } }
    /// <summary>
    /// Gets or sets the align items.
    /// </summary>
    public AlignValue AlignItems { set { YGNodeStyleAPI.YGNodeStyleSetAlignItems(YogaNode, value.ToYogaAlign()); RequestRender(); } }
    /// <summary>
    /// Gets or sets the justify content.
    /// </summary>
    public JustifyValue JustifyContent { set { YGNodeStyleAPI.YGNodeStyleSetJustifyContent(YogaNode, value.ToYoga()); RequestRender(); } }
    /// <summary>
    /// Gets or sets the align self.
    /// </summary>
    public AlignValue AlignSelf { set { YGNodeStyleAPI.YGNodeStyleSetAlignSelf(YogaNode, value.ToYogaAlign()); RequestRender(); } }

    /// <summary>
    /// Gets or sets the flex basis.
    /// </summary>
    public DimensionValue FlexBasis
    {
        set
        {
            SetYogaDimension(
                YGNodeStyleAPI.YGNodeStyleSetFlexBasis,
                YGNodeStyleAPI.YGNodeStyleSetFlexBasisPercent,
                YGNodeStyleAPI.YGNodeStyleSetFlexBasisAuto,
                value);
            RequestRender();
        }
    }

    #endregion

    #region Size constraint setters

    /// <summary>
    /// Gets or sets the min width.
    /// </summary>
    public DimensionValue MinWidth { set { SetYogaSizeConstraint(YGNodeStyleAPI.YGNodeStyleSetMinWidth, YGNodeStyleAPI.YGNodeStyleSetMinWidthPercent, value); RequestRender(); } }
    /// <summary>
    /// Gets or sets the min height.
    /// </summary>
    public DimensionValue MinHeight { set { SetYogaSizeConstraint(YGNodeStyleAPI.YGNodeStyleSetMinHeight, YGNodeStyleAPI.YGNodeStyleSetMinHeightPercent, value); RequestRender(); } }
    /// <summary>
    /// Gets or sets the max width.
    /// </summary>
    public DimensionValue MaxWidth { set { SetYogaSizeConstraint(YGNodeStyleAPI.YGNodeStyleSetMaxWidth, YGNodeStyleAPI.YGNodeStyleSetMaxWidthPercent, value); RequestRender(); } }
    /// <summary>
    /// Gets or sets the max height.
    /// </summary>
    public DimensionValue MaxHeight { set { SetYogaSizeConstraint(YGNodeStyleAPI.YGNodeStyleSetMaxHeight, YGNodeStyleAPI.YGNodeStyleSetMaxHeightPercent, value); RequestRender(); } }

    #endregion

    #region Margin/Padding setters

    /// <summary>
    /// Gets or sets the margin.
    /// </summary>
    public DimensionValue Margin { set { SetYogaMargin(YGEdge.All, value); RequestRender(); } }
    /// <summary>
    /// Gets or sets the margin x.
    /// </summary>
    public DimensionValue MarginX { set { SetYogaMargin(YGEdge.Horizontal, value); RequestRender(); } }
    /// <summary>
    /// Gets or sets the margin y.
    /// </summary>
    public DimensionValue MarginY { set { SetYogaMargin(YGEdge.Vertical, value); RequestRender(); } }
    /// <summary>
    /// Gets or sets the margin top.
    /// </summary>
    public DimensionValue MarginTop { set { SetYogaMargin(YGEdge.Top, value); RequestRender(); } }
    /// <summary>
    /// Gets or sets the margin right.
    /// </summary>
    public DimensionValue MarginRight { set { SetYogaMargin(YGEdge.Right, value); RequestRender(); } }
    /// <summary>
    /// Gets or sets the margin bottom.
    /// </summary>
    public DimensionValue MarginBottom { set { SetYogaMargin(YGEdge.Bottom, value); RequestRender(); } }
    /// <summary>
    /// Gets or sets the margin left.
    /// </summary>
    public DimensionValue MarginLeft { set { SetYogaMargin(YGEdge.Left, value); RequestRender(); } }

    /// <summary>
    /// Gets or sets the padding.
    /// </summary>
    public DimensionValue Padding { set { SetYogaPadding(YGEdge.All, value); RequestRender(); } }
    /// <summary>
    /// Gets or sets the padding x.
    /// </summary>
    public DimensionValue PaddingX { set { SetYogaPadding(YGEdge.Horizontal, value); RequestRender(); } }
    /// <summary>
    /// Gets or sets the padding y.
    /// </summary>
    public DimensionValue PaddingY { set { SetYogaPadding(YGEdge.Vertical, value); RequestRender(); } }
    /// <summary>
    /// Gets or sets the padding top.
    /// </summary>
    public DimensionValue PaddingTop { set { SetYogaPadding(YGEdge.Top, value); RequestRender(); } }
    /// <summary>
    /// Gets or sets the padding right.
    /// </summary>
    public DimensionValue PaddingRight { set { SetYogaPadding(YGEdge.Right, value); RequestRender(); } }
    /// <summary>
    /// Gets or sets the padding bottom.
    /// </summary>
    public DimensionValue PaddingBottom { set { SetYogaPadding(YGEdge.Bottom, value); RequestRender(); } }
    /// <summary>
    /// Gets or sets the padding left.
    /// </summary>
    public DimensionValue PaddingLeft { set { SetYogaPadding(YGEdge.Left, value); RequestRender(); } }

    #endregion

    #region Layout Node

    /// <summary>
    /// Gets a layout node.
    /// </summary>
    /// <returns>The layout node.</returns>
    public Node GetLayoutNode() => YogaNode;

    /// <summary>
    /// Gets the primary axis.
    /// </summary>
    public string PrimaryAxis
    {
        get
        {
            var dir = YGNodeStyleAPI.YGNodeStyleGetFlexDirection(YogaNode);
            return dir is YGFlexDirection.Row or YGFlexDirection.RowReverse ? "row" : "column";
        }
    }

    #endregion

    #region Update from layout

    /// <summary>
    /// Updates the from layout.
    /// </summary>
    public void UpdateFromLayout()
    {
        var frameId = _ctx.FrameId;
        if (_lastLayoutFrame == frameId) return;
        _lastLayoutFrame = frameId;

        var left = YGNodeLayoutAPI.YGNodeLayoutGetLeft(YogaNode);
        var top = YGNodeLayoutAPI.YGNodeLayoutGetTop(YogaNode);
        var w = YGNodeLayoutAPI.YGNodeLayoutGetWidth(YogaNode);
        var h = YGNodeLayoutAPI.YGNodeLayoutGetHeight(YogaNode);

        // Yoga.Net workaround: YGNodeStyleSetBorder does not properly reduce
        // the layout width for stretched children — their LayoutGetWidth returns
        // the parent's border-box width instead of the content-box width.
        // Only applies to auto-width, non-absolute children whose parent has border.
        if (_parent is not null && _positionType != PositionValue.Absolute)
        {
            var styleW = YGNodeStyleAPI.YGNodeStyleGetWidth(YogaNode);
            if (styleW.Unit is Unit.Auto or Unit.Undefined)
            {
                var pNode = _parent.YogaNode;
                float pBorderH = YGNodeLayoutAPI.YGNodeLayoutGetBorder(pNode, YGEdge.Left)
                               + YGNodeLayoutAPI.YGNodeLayoutGetBorder(pNode, YGEdge.Right);
                if (pBorderH > 0)
                {
                    float pPadH = YGNodeLayoutAPI.YGNodeLayoutGetPadding(pNode, YGEdge.Left)
                                + YGNodeLayoutAPI.YGNodeLayoutGetPadding(pNode, YGEdge.Right);
                    float maxW = YGNodeLayoutAPI.YGNodeLayoutGetWidth(pNode) - pBorderH - pPadH;
                    if (w > maxW) w = maxW;
                }
            }
        }

        var oldX = _x;
        var oldY = _y;
        var oldWidth = _widthValue;
        var oldHeight = _heightValue;

        _x = left;
        _y = top;
        var parentScreenX = _parent?._screenX ?? 0;
        var parentScreenY = _parent?._screenY ?? 0;
        _screenX = parentScreenX + _x + _translateX;
        _screenY = parentScreenY + _y + _translateY;

        var newWidth = (int)Math.Max(w, 1);
        var newHeight = (int)Math.Max(h, 1);
        var sizeChanged = oldWidth != newWidth || oldHeight != newHeight;

        _widthValue = newWidth;
        _heightValue = newHeight;

        if (sizeChanged) OnLayoutResize(newWidth, newHeight);

        if (oldX != _x || oldY != _y)
        {
            if (_parent is not null) _parent._childrenPrimarySortDirty = true;
        }
    }

    /// <summary>
    /// Handles the layout resize.
    /// </summary>
    /// <param name="width">The width value.</param>
    /// <param name="height">The height value.</param>
    protected virtual void OnLayoutResize(int width, int height)
    {
        if (_visible)
        {
            HandleFrameBufferResize(width, height);
            OnResize(width, height);
            RequestRender();
        }
    }

    /// <summary>
    /// Handles the frame buffer resize.
    /// </summary>
    /// <param name="width">The width value.</param>
    /// <param name="height">The height value.</param>
    protected void HandleFrameBufferResize(int width, int height)
    {
        if (!_buffered) return;
        if (width <= 0 || height <= 0) return;

        if (_frameBuffer is not null)
            _frameBuffer.Resize((uint)width, (uint)height);
        else
            CreateFrameBuffer();
    }

    /// <summary>
    /// Creates a frame buffer.
    /// </summary>
    protected void CreateFrameBuffer()
    {
        var w = _widthValue;
        var h = _heightValue;
        if (w <= 0 || h <= 0) return;

        try
        {
            _frameBuffer = OptimizedBuffer.Create((uint)w, (uint)h, _ctx.WidthMethod, true, $"framebuffer-{_id}");
        }
        catch
        {
            _frameBuffer = null;
        }
    }

    /// <summary>
    /// Handles the resize.
    /// </summary>
    /// <param name="width">The width value.</param>
    /// <param name="height">The height value.</param>
    protected virtual void OnResize(int width, int height)
    {
        _sizeChangeListener?.Invoke();
        Emit(RenderableEventNames.Resize);
    }

    #endregion

    #region Child Management

    private void ReplaceParent(Renderable obj)
    {
        obj._parent?.Remove(obj.Id);
        obj._parent = this;
    }

    /// <summary>
    /// Performs add.
    /// </summary>
    /// <param name="obj">The obj.</param>
    /// <param name="index">The zero-based index.</param>
    /// <returns>The result of add.</returns>
    public virtual int Add(Renderable? obj, int? index = null)
    {
        if (obj is null || obj._isDestroyed) return -1;

        var anchorRenderable = index is { } idx && idx >= 0 && idx < _childrenInLayoutOrder.Count
            ? _childrenInLayoutOrder[idx]
            : null;

        if (anchorRenderable is not null) return InsertBefore(obj, anchorRenderable);

        if (obj._parent == this)
        {
            YGNodeAPI.YGNodeRemoveChild(YogaNode, obj.YogaNode);
            _childrenInLayoutOrder.Remove(obj);
        }
        else
        {
            ReplaceParent(obj);
            _needsZIndexSort = true;
            _renderableMapById[obj.Id] = obj;
            _childrenInZIndexOrder.Add(obj);

            if (obj.OnLifecyclePass is not null)
                _ctx.RegisterLifecyclePass(obj);

            if (obj._liveCount > 0)
                PropagateLiveCount(obj._liveCount);
        }

        var insertedIndex = _childrenInLayoutOrder.Count;
        _childrenInLayoutOrder.Add(obj);
        YGNodeAPI.YGNodeInsertChild(YogaNode, obj.YogaNode, (nuint)insertedIndex);

        _childrenPrimarySortDirty = true;
        _shouldUpdateBefore.Add(obj);
        RequestRender();

        return insertedIndex;
    }

    /// <summary>
    /// Instantiates a VNode and adds the resulting renderable as a child.
    /// </summary>
    /// <param name="vnode">The virtual node to instantiate.</param>
    /// <param name="index">Optional insertion index.</param>
    /// <returns>The index at which the child was inserted.</returns>
    public int Add(VNode vnode, int? index = null)
    {
        var renderable = VNodeRuntime.Instantiate(_ctx, vnode);
        return Add(renderable, index);
    }

    /// <summary>
    /// Performs insert before.
    /// </summary>
    /// <param name="obj">The obj.</param>
    /// <param name="anchor">The anchor.</param>
    /// <returns>The result of insert before.</returns>
    public virtual int InsertBefore(Renderable? obj, Renderable? anchor)
    {
        if (anchor is null) return Add(obj);
        if (obj is null || obj._isDestroyed || anchor._isDestroyed) return -1;
        if (obj == anchor || obj.Id == anchor.Id) return -1;
        if (!_renderableMapById.ContainsKey(anchor.Id)) return -1;

        if (obj._parent == this)
        {
            YGNodeAPI.YGNodeRemoveChild(YogaNode, obj.YogaNode);
            _childrenInLayoutOrder.Remove(obj);
        }
        else
        {
            ReplaceParent(obj);
            _needsZIndexSort = true;
            _renderableMapById[obj.Id] = obj;
            _childrenInZIndexOrder.Add(obj);

            if (obj.OnLifecyclePass is not null)
                _ctx.RegisterLifecyclePass(obj);

            if (obj._liveCount > 0)
                PropagateLiveCount(obj._liveCount);
        }

        _childrenPrimarySortDirty = true;

        var anchorIndex = _childrenInLayoutOrder.IndexOf(anchor);
        var insertedIndex = Math.Max(0, Math.Min(anchorIndex, _childrenInLayoutOrder.Count));

        _childrenInLayoutOrder.Insert(insertedIndex, obj);
        YGNodeAPI.YGNodeInsertChild(YogaNode, obj.YogaNode, (nuint)insertedIndex);

        _shouldUpdateBefore.Add(obj);
        RequestRender();

        return insertedIndex;
    }

    /// <summary>
    /// Gets a renderable.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The renderable.</returns>
    public Renderable? GetRenderable(string id) =>
        _renderableMapById.TryGetValue(id, out var r) ? r : null;

    /// <summary>
    /// Performs remove.
    /// </summary>
    /// <param name="id">The identifier.</param>
    public virtual void Remove(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (!_renderableMapById.TryGetValue(id, out var obj) || obj is null) return;

        if (obj._liveCount > 0)
            PropagateLiveCount(-obj._liveCount);

        YGNodeAPI.YGNodeRemoveChild(YogaNode, obj.YogaNode);
        RequestRender();

        obj.OnRemove();
        obj._parent = null;
        _ctx.UnregisterLifecyclePass(obj);
        _renderableMapById.Remove(id);

        _childrenInLayoutOrder.RemoveAll(c => c.Id == id);
        _childrenInZIndexOrder.RemoveAll(c => c.Id == id);
        _shouldUpdateBefore.Remove(obj);
        _childrenPrimarySortDirty = true;
    }

    /// <summary>
    /// Handles the remove.
    /// </summary>
    protected virtual void OnRemove() { }

    /// <summary>
    /// Gets a children.
    /// </summary>
    /// <returns>The children.</returns>
    public List<Renderable> GetChildren() => [.. _childrenInLayoutOrder];

    /// <summary>
    /// Gets a children count.
    /// </summary>
    /// <returns>The children count.</returns>
    public int GetChildrenCount() => _childrenInLayoutOrder.Count;

    /// <summary>
    /// Performs find descendant by id.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The descendant by id.</returns>
    public Renderable? FindDescendantById(string id)
    {
        foreach (var child in _childrenInLayoutOrder)
        {
            if (child.Id == id) return child;
            var found = child.FindDescendantById(id);
            if (found is not null) return found;
        }
        return null;
    }

    /// <summary>
    /// Gets a children sorted by primary axis.
    /// </summary>
    /// <returns>The children sorted by primary axis.</returns>
    public List<Renderable> GetChildrenSortedByPrimaryAxis()
    {
        if (!_childrenPrimarySortDirty && _childrenSortedByPrimaryAxis.Count == _childrenInLayoutOrder.Count)
            return _childrenSortedByPrimaryAxis;

        var dir = YGNodeStyleAPI.YGNodeStyleGetFlexDirection(YogaNode);
        bool useX = dir is YGFlexDirection.Row or YGFlexDirection.RowReverse;

        var sorted = new List<Renderable>(_childrenInLayoutOrder);
        sorted.Sort((a, b) =>
        {
            var va = useX ? a.ScreenX : a.ScreenY;
            var vb = useX ? b.ScreenX : b.ScreenY;
            return va.CompareTo(vb);
        });

        _childrenSortedByPrimaryAxis = sorted;
        _childrenPrimarySortDirty = false;
        return _childrenSortedByPrimaryAxis;
    }

    #endregion

    #region Render

    /// <summary>
    /// Performs request render.
    /// </summary>
    public void RequestRender()
    {
        MarkDirty();
        _ctx.RequestRender();
    }

    /// <summary>
    /// Performs render.
    /// </summary>
    /// <param name="buffer">The target buffer.</param>
    /// <param name="deltaTime">The delta time.</param>
    public virtual void Render(OptimizedBuffer buffer, float deltaTime)
    {
        var renderBuffer = _buffered && _frameBuffer is not null ? _frameBuffer : buffer;

        RenderBeforeHook?.Invoke(renderBuffer, deltaTime);
        RenderSelf(renderBuffer, deltaTime);
        RenderAfterHook?.Invoke(renderBuffer, deltaTime);

        var screenX = _screenX;
        var screenY = _screenY;

        MarkClean();
        _ctx.AddToHitGrid((int)screenX, (int)screenY, (uint)_widthValue, (uint)_heightValue, (uint)Num);

        if (_buffered && _frameBuffer is not null)
        {
            buffer.DrawFrameBuffer((int)screenX, (int)screenY, _frameBuffer, 0, 0, (uint)_widthValue, (uint)_heightValue);
        }
    }

    /// <summary>Override in subclasses to do actual drawing.</summary>
    protected virtual void RenderSelf(OptimizedBuffer buffer, float deltaTime) { }

    /// <summary>Override in subclasses for per-frame logic before layout read.</summary>
    protected virtual void OnUpdate(float deltaTime) { }

    #endregion

    #region Update Layout (tree walk)

    /// <summary>
    /// Updates the layout.
    /// </summary>
    /// <param name="deltaTime">The delta time.</param>
    /// <param name="renderList">The render list.</param>
    public virtual void UpdateLayout(float deltaTime, List<RenderCommand> renderList)
    {
        if (!_visible) return;

        OnUpdate(deltaTime);
        if (_isDestroyed) return;

        UpdateFromLayout();

        // Update newly added children before culling. Layout callbacks can
        // add/remove siblings here, so process stable snapshots until drained.
        while (_shouldUpdateBefore.Count > 0)
        {
            var pendingChildren = _shouldUpdateBefore.ToArray();
            _shouldUpdateBefore.Clear();

            foreach (var child in pendingChildren)
            {
                if (!child._isDestroyed && child._parent == this)
                    child.UpdateFromLayout();
            }
        }

        if (_isDestroyed) return;

        if (_opacity < 1.0f)
            renderList.Add(RenderCommand.CreatePushOpacity(_opacity));

        renderList.Add(RenderCommand.CreateRender(this));

        EnsureZIndexSorted();

        var shouldPushScissor = _overflow != OverflowValue.Visible && _widthValue > 0 && _heightValue > 0;
        if (shouldPushScissor)
        {
            var sr = GetScissorRect();
            renderList.Add(RenderCommand.CreatePushScissorRect(sr.x, sr.y, sr.w, sr.h, (int)_screenX, (int)_screenY));
        }

        if (!HasVisibleChildFilter())
        {
            foreach (var child in _childrenInZIndexOrder)
                child.UpdateLayout(deltaTime, renderList);
        }
        else
        {
            foreach (var child in _childrenInZIndexOrder)
            {
                if (!child._isDestroyed) child.UpdateFromLayout();
            }
            var visibleNums = GetVisibleChildren();
            var visibleSet = new HashSet<int>(visibleNums);
            foreach (var child in _childrenInZIndexOrder)
            {
                if (visibleSet.Contains(child.Num))
                    child.UpdateLayout(deltaTime, renderList);
            }
        }

        if (shouldPushScissor)
            renderList.Add(RenderCommand.CreatePopScissorRect());
        if (_opacity < 1.0f)
            renderList.Add(RenderCommand.CreatePopOpacity());
    }

    /// <summary>
    /// Performs has visible child filter.
    /// </summary>
    /// <returns>true if has visible child filter; otherwise, false.</returns>
    protected virtual bool HasVisibleChildFilter() => false;
    /// <summary>
    /// Gets a visible children.
    /// </summary>
    /// <returns>The visible children.</returns>
    protected virtual int[] GetVisibleChildren() =>
        [.. _childrenInZIndexOrder.Select(c => c.Num)];

    /// <summary>
    /// Gets a scissor rect.
    /// </summary>
    /// <returns>The scissor rect.</returns>
    protected virtual (int x, int y, int w, int h) GetScissorRect() =>
        (_buffered ? 0 : (int)_screenX, _buffered ? 0 : (int)_screenY, _widthValue, _heightValue);

    #endregion

    #region Destroy

    /// <summary>
    /// Gets a value indicating whether is destroyed.
    /// </summary>
    public bool IsDestroyed => _isDestroyed;

    /// <summary>
    /// Performs destroy.
    /// </summary>
    public virtual void Destroy()
    {
        if (_isDestroyed) return;
        _isDestroyed = true;

        _parent?.Remove(_id);

        if (_frameBuffer is not null)
        {
            _frameBuffer.Dispose();
            _frameBuffer = null;
        }

        // Remove children from snapshot to avoid mutation during iteration
        var childSnapshot = _childrenInLayoutOrder.ToArray();
        foreach (var child in childSnapshot)
            Remove(child.Id);

        _childrenInLayoutOrder.Clear();
        _renderableMapById.Clear();
        s_renderablesByNumber.Remove(Num);

        Blur();
        RemoveAllListeners();
        DestroySelf();

        try { YGNodeAPI.YGNodeFree(YogaNode); } catch { /* may already be freed */ }
    }

    /// <summary>
    /// Performs destroy recursively.
    /// </summary>
    public virtual void DestroyRecursively()
    {
        var children = _childrenInLayoutOrder.ToArray();
        foreach (var child in children)
            child.DestroyRecursively();
        Destroy();
    }

    /// <summary>Override in subclasses for custom cleanup.</summary>
    protected virtual void DestroySelf() { }

    #endregion

    #region Mouse Events

    /// <summary>
    /// Performs process mouse event.
    /// </summary>
    /// <param name="evt">The event data.</param>
    public void ProcessMouseEvent(UiMouseEvent evt)
    {
        _mouseListener?.Invoke(evt);
        if (_mouseListeners.TryGetValue(evt.Type.ToString().ToLowerInvariant(), out var handler))
            handler.Invoke(evt);
        OnMouseEvent(evt);

        if (_parent is not null && !evt.IsPropagationStopped)
            _parent.ProcessMouseEvent(evt);
    }

    /// <summary>
    /// Handles the mouse event.
    /// </summary>
    /// <param name="evt">The event data.</param>
    protected virtual void OnMouseEvent(UiMouseEvent evt) { }

    #endregion

    #region Event Property Setters

    /// <summary>
    /// Gets or sets the on mouse.
    /// </summary>
    public Action<UiMouseEvent>? OnMouse { set => _mouseListener = value; }
    /// <summary>
    /// Gets or sets the on mouse down.
    /// </summary>
    public Action<UiMouseEvent>? OnMouseDown { set => SetMouseHandler("down", value); }
    /// <summary>
    /// Gets or sets the on mouse up.
    /// </summary>
    public Action<UiMouseEvent>? OnMouseUp { set => SetMouseHandler("up", value); }
    /// <summary>
    /// Gets or sets the on mouse move.
    /// </summary>
    public Action<UiMouseEvent>? OnMouseMove { set => SetMouseHandler("move", value); }
    /// <summary>
    /// Gets or sets the on mouse drag.
    /// </summary>
    public Action<UiMouseEvent>? OnMouseDrag { set => SetMouseHandler("drag", value); }
    /// <summary>
    /// Gets or sets the on mouse drag end.
    /// </summary>
    public Action<UiMouseEvent>? OnMouseDragEnd { set => SetMouseHandler("drag-end", value); }
    /// <summary>
    /// Gets or sets the on mouse drop.
    /// </summary>
    public Action<UiMouseEvent>? OnMouseDrop { set => SetMouseHandler("drop", value); }
    /// <summary>
    /// Gets or sets the on mouse over.
    /// </summary>
    public Action<UiMouseEvent>? OnMouseOver { set => SetMouseHandler("over", value); }
    /// <summary>
    /// Gets or sets the on mouse out.
    /// </summary>
    public Action<UiMouseEvent>? OnMouseOut { set => SetMouseHandler("out", value); }
    /// <summary>
    /// Gets or sets the on mouse scroll.
    /// </summary>
    public Action<UiMouseEvent>? OnMouseScroll { set => SetMouseHandler("scroll", value); }

    /// <summary>
    /// Gets or sets the on paste handler.
    /// </summary>
    public Action<PasteEvent>? OnPasteHandler { get => _pasteListener; set => _pasteListener = value; }
    /// <summary>
    /// Gets or sets the on key down.
    /// </summary>
    public Action<KeyEvent>? OnKeyDown { get => _keyDownListener; set => _keyDownListener = value; }
    /// <summary>
    /// Gets or sets the on size change.
    /// </summary>
    public Action? OnSizeChange { get => _sizeChangeListener; set => _sizeChangeListener = value; }

    private void SetMouseHandler(string type, Action<UiMouseEvent>? handler)
    {
        if (handler is not null)
            _mouseListeners[type] = handler;
        else
            _mouseListeners.Remove(type);
    }

    #endregion

    #region Yoga Setup Helpers

    private void SetupYogaProperties(RenderableOptions options)
    {
        var node = YogaNode;

        if (options.FlexBasis is { } basis)
            SetYogaDimension(YGNodeStyleAPI.YGNodeStyleSetFlexBasis, YGNodeStyleAPI.YGNodeStyleSetFlexBasisPercent, YGNodeStyleAPI.YGNodeStyleSetFlexBasisAuto, basis);

        if (options.MinWidth is { } minW) SetYogaSizeConstraint(YGNodeStyleAPI.YGNodeStyleSetMinWidth, YGNodeStyleAPI.YGNodeStyleSetMinWidthPercent, minW);
        if (options.MinHeight is { } minH) SetYogaSizeConstraint(YGNodeStyleAPI.YGNodeStyleSetMinHeight, YGNodeStyleAPI.YGNodeStyleSetMinHeightPercent, minH);

        YGNodeStyleAPI.YGNodeStyleSetFlexGrow(node, options.FlexGrow ?? 0f);

        bool hasExplicitSize = (options.Width is { IsPoint: true }) || (options.Height is { IsPoint: true });
        _flexShrink = options.FlexShrink ?? (hasExplicitSize ? 0f : 1f);
        YGNodeStyleAPI.YGNodeStyleSetFlexShrink(node, _flexShrink);

        YGNodeStyleAPI.YGNodeStyleSetFlexDirection(node, (options.FlexDirection ?? FlexDirectionValue.Column).ToYoga());
        YGNodeStyleAPI.YGNodeStyleSetFlexWrap(node, (options.FlexWrap ?? WrapValue.NoWrap).ToYoga());
        YGNodeStyleAPI.YGNodeStyleSetAlignItems(node, (options.AlignItems ?? AlignValue.Stretch).ToYogaAlign());
        YGNodeStyleAPI.YGNodeStyleSetJustifyContent(node, (options.JustifyContent ?? JustifyValue.FlexStart).ToYoga());
        YGNodeStyleAPI.YGNodeStyleSetAlignSelf(node, (options.AlignSelf ?? AlignValue.Auto).ToYogaAlign());

        if (options.Width is { } w)
            SetYogaDimension(YGNodeStyleAPI.YGNodeStyleSetWidth, YGNodeStyleAPI.YGNodeStyleSetWidthPercent, YGNodeStyleAPI.YGNodeStyleSetWidthAuto, w);
        if (options.Height is { } h)
            SetYogaDimension(YGNodeStyleAPI.YGNodeStyleSetHeight, YGNodeStyleAPI.YGNodeStyleSetHeightPercent, YGNodeStyleAPI.YGNodeStyleSetHeightAuto, h);

        _positionType = options.Position ?? PositionValue.Relative;
        if (_positionType != PositionValue.Relative)
            YGNodeStyleAPI.YGNodeStyleSetPositionType(node, _positionType.ToYoga());

        _overflow = options.Overflow ?? OverflowValue.Visible;
        if (_overflow != OverflowValue.Visible)
            YGNodeStyleAPI.YGNodeStyleSetOverflow(node, _overflow.ToYoga());

        if (options.Top is { } top) SetYogaEdgePosition(YGEdge.Top, top);
        if (options.Right is { } right) SetYogaEdgePosition(YGEdge.Right, right);
        if (options.Bottom is { } bottom) SetYogaEdgePosition(YGEdge.Bottom, bottom);
        if (options.Left is { } left) SetYogaEdgePosition(YGEdge.Left, left);

        if (options.MaxWidth is { } maxW) SetYogaSizeConstraint(YGNodeStyleAPI.YGNodeStyleSetMaxWidth, YGNodeStyleAPI.YGNodeStyleSetMaxWidthPercent, maxW);
        if (options.MaxHeight is { } maxH) SetYogaSizeConstraint(YGNodeStyleAPI.YGNodeStyleSetMaxHeight, YGNodeStyleAPI.YGNodeStyleSetMaxHeightPercent, maxH);

        if (options.Gap is { } gap) YGNodeStyleAPI.YGNodeStyleSetGap(node, YGGutter.All, gap);
        if (options.RowGap is { } rowGap) YGNodeStyleAPI.YGNodeStyleSetGap(node, YGGutter.Row, rowGap);
        if (options.ColumnGap is { } colGap) YGNodeStyleAPI.YGNodeStyleSetGap(node, YGGutter.Column, colGap);

        SetupMarginAndPadding(options);
    }

    private void SetupMarginAndPadding(RenderableOptions options)
    {
        if (options.Margin is { } m) SetYogaMargin(YGEdge.All, m);
        if (options.MarginX is { } mx) SetYogaMargin(YGEdge.Horizontal, mx);
        if (options.MarginY is { } my) SetYogaMargin(YGEdge.Vertical, my);
        if (options.MarginTop is { } mt) SetYogaMargin(YGEdge.Top, mt);
        if (options.MarginRight is { } mr) SetYogaMargin(YGEdge.Right, mr);
        if (options.MarginBottom is { } mb) SetYogaMargin(YGEdge.Bottom, mb);
        if (options.MarginLeft is { } ml) SetYogaMargin(YGEdge.Left, ml);

        if (options.Padding is { } p) SetYogaPadding(YGEdge.All, p);
        if (options.PaddingX is { } px) SetYogaPadding(YGEdge.Horizontal, px);
        if (options.PaddingY is { } py) SetYogaPadding(YGEdge.Vertical, py);
        if (options.PaddingTop is { } pt) SetYogaPadding(YGEdge.Top, pt);
        if (options.PaddingRight is { } pr) SetYogaPadding(YGEdge.Right, pr);
        if (options.PaddingBottom is { } pb) SetYogaPadding(YGEdge.Bottom, pb);
        if (options.PaddingLeft is { } pl) SetYogaPadding(YGEdge.Left, pl);
    }

    private void ApplyEventOptions(RenderableOptions options)
    {
        OnMouse = options.OnMouse;
        OnMouseDown = options.OnMouseDown;
        OnMouseUp = options.OnMouseUp;
        OnMouseMove = options.OnMouseMove;
        OnMouseDrag = options.OnMouseDrag;
        OnMouseDragEnd = options.OnMouseDragEnd;
        OnMouseDrop = options.OnMouseDrop;
        OnMouseOver = options.OnMouseOver;
        OnMouseOut = options.OnMouseOut;
        OnMouseScroll = options.OnMouseScroll;
        OnPasteHandler = options.OnPaste;
        OnKeyDown = options.OnKeyDown;
        OnSizeChange = options.OnSizeChange;
    }

    private void SetYogaDimension(Action<Node, float> setPoint, Action<Node, float> setPercent, Action<Node> setAuto, DimensionValue dim)
    {
        if (dim.IsAuto) setAuto(YogaNode);
        else if (dim.IsPercent) setPercent(YogaNode, dim.Value);
        else if (dim.IsPoint) setPoint(YogaNode, dim.Value);
    }

    private void SetYogaSizeConstraint(Action<Node, float> setPoint, Action<Node, float> setPercent, DimensionValue dim)
    {
        if (dim.IsAuto || dim.IsUndefined) setPoint(YogaNode, float.NaN);
        else if (dim.IsPercent) setPercent(YogaNode, dim.Value);
        else if (dim.IsPoint) setPoint(YogaNode, dim.Value);
    }

    private void SetYogaMargin(YGEdge edge, DimensionValue dim)
    {
        if (dim.IsAuto) YGNodeStyleAPI.YGNodeStyleSetMarginAuto(YogaNode, edge);
        else if (dim.IsPercent) YGNodeStyleAPI.YGNodeStyleSetMarginPercent(YogaNode, edge, dim.Value);
        else if (dim.IsPoint) YGNodeStyleAPI.YGNodeStyleSetMargin(YogaNode, edge, dim.Value);
    }

    private void SetYogaPadding(YGEdge edge, DimensionValue dim)
    {
        if (dim.IsPercent) YGNodeStyleAPI.YGNodeStyleSetPaddingPercent(YogaNode, edge, dim.Value);
        else if (dim.IsPoint) YGNodeStyleAPI.YGNodeStyleSetPadding(YogaNode, edge, dim.Value);
    }

    private void SetYogaEdgePosition(YGEdge edge, DimensionValue dim)
    {
        if (dim.IsAuto) YGNodeStyleAPI.YGNodeStyleSetPositionAuto(YogaNode, edge);
        else if (dim.IsPercent) YGNodeStyleAPI.YGNodeStyleSetPositionPercent(YogaNode, edge, dim.Value);
        else if (dim.IsPoint) YGNodeStyleAPI.YGNodeStyleSetPosition(YogaNode, edge, dim.Value);
    }

    private void UpdateYogaPosition(
        DimensionValue? top = null, DimensionValue? right = null,
        DimensionValue? bottom = null, DimensionValue? left = null)
    {
        if (top is { } t) SetYogaEdgePosition(YGEdge.Top, t);
        if (right is { } r) SetYogaEdgePosition(YGEdge.Right, r);
        if (bottom is { } b) SetYogaEdgePosition(YGEdge.Bottom, b);
        if (left is { } l) SetYogaEdgePosition(YGEdge.Left, l);
        RequestRender();
    }

    #endregion
}
