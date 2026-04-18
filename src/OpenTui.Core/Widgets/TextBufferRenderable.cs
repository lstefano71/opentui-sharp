using Facebook.Yoga;

namespace OpenTui.Core;

/// <summary>
/// Intermediate renderable that owns a TextBuffer + TextBufferView + SyntaxStyle.
/// Integrates with Yoga layout via a measure function.
/// Matches TypeScript TextBufferRenderable from text-buffer-renderable.ts.
/// </summary>
public class TextBufferRenderable : Renderable
{
    private TextBuffer _textBuffer;
    private TextBufferView _textBufferView;
    private SyntaxStyle _syntaxStyle;

    private Rgba _fg;
    private Rgba _bg;
    private Rgba? _selectionBg;
    private Rgba? _selectionFg;
    private bool _selectable;
    private LocalSelectionBounds? _lastLocalSelection;
    private WrapMode _wrapMode;
    private bool _truncate;

    protected TextBufferRenderable(IRenderContext ctx, TextBufferOptions options)
        : base(ctx, options)
    {
        _fg = options.Fg ?? Rgba.FromInts(255, 255, 255);
        _bg = options.Bg ?? Rgba.Transparent;
        _selectionBg = options.SelectionBg;
        _selectionFg = options.SelectionFg;
        _selectable = options.Selectable;
        base.Selectable = options.Selectable;
        _wrapMode = options.WrapMode;
        _truncate = options.Truncate;

        // Create native handles
        _textBuffer = TextBuffer.Create(ctx.WidthMethod == WidthMethod.Unicode
            ? WidthMethod.Unicode : WidthMethod.Wcwidth);
        _textBufferView = TextBufferView.Create(_textBuffer);
        _syntaxStyle = SyntaxStyle.Create();

        // Apply initial styling
        _textBuffer.SetForeground(_fg);
        _textBuffer.SetBackground(_bg);
        _textBuffer.SetAttributes(options.Attributes);
        _textBuffer.SetSyntaxStyle(_syntaxStyle.Handle);

        _textBufferView.SetWrapMode(_wrapMode);
        if (_truncate) _textBufferView.SetTruncate(true);

        // Text renderables clamp their runtime height to at least one row.
        // Mirror that in Yoga so flex shrink cannot collapse them to 0 and
        // cause later siblings to paint over the same row.
        if (options.MinHeight is null)
            MinHeight = DimensionValue.Point(1);

        // Install Yoga measure function
        YGNodeAPI.YGNodeSetMeasureFunc(YogaNode, MeasureFunc);
    }

    #region Properties

    public TextBuffer TextBuffer => _textBuffer;
    public TextBufferView TextBufferView => _textBufferView;
    public SyntaxStyle SyntaxStyle => _syntaxStyle;

    public Rgba Fg
    {
        get => _fg;
        set { _fg = value; _textBuffer.SetForeground(value); RequestRender(); }
    }

    public Rgba Bg
    {
        get => _bg;
        set { _bg = value; _textBuffer.SetBackground(value); RequestRender(); }
    }

    public Rgba? SelectionBg
    {
        get => _selectionBg;
        set
        {
            _selectionBg = value;
            RefreshLocalSelection();
            RequestRender();
        }
    }

    public Rgba? SelectionFg
    {
        get => _selectionFg;
        set
        {
            _selectionFg = value;
            RefreshLocalSelection();
            RequestRender();
        }
    }

    public bool Selectable
    {
        get => _selectable;
        set
        {
            _selectable = value;
            base.Selectable = value;
        }
    }

    public WrapMode WrapMode
    {
        get => _wrapMode;
        set
        {
            if (_wrapMode == value) return;
            _wrapMode = value;
            _textBufferView.SetWrapMode(value);
            // Mark yoga dirty when wrap mode changes
            YGNodeAPI.YGNodeMarkDirty(YogaNode);
            RequestRender();
        }
    }

    public bool Truncate
    {
        get => _truncate;
        set { _truncate = value; _textBufferView.SetTruncate(value); RequestRender(); }
    }

    /// <summary>Plain text content.</summary>
    public string PlainText => _textBuffer.GetPlainText();

    public uint TextLength => _textBuffer.Length;
    public uint LineCount => _textBuffer.LineCount;

    #endregion

    #region Scroll

    private int _scrollX;
    private int _scrollY;

    public int ScrollX
    {
        get => _scrollX;
        set
        {
            var max = MaxScrollX;
            _scrollX = Math.Clamp(value, 0, Math.Max(0, max));
            UpdateViewportOffset();
        }
    }

    public int ScrollY
    {
        get => _scrollY;
        set
        {
            var max = MaxScrollY;
            _scrollY = Math.Clamp(value, 0, Math.Max(0, max));
            UpdateViewportOffset();
        }
    }

    public int MaxScrollX
    {
        get
        {
            // TODO: compute from line info max column width
            return 0;
        }
    }

    public int MaxScrollY
    {
        get
        {
            int virtualLines = (int)_textBufferView.GetVirtualLineCount();
            return Math.Max(0, virtualLines - _heightValue);
        }
    }

    private void UpdateViewportOffset()
    {
        _textBufferView.SetViewport((uint)Math.Max(0, _scrollX), (uint)Math.Max(0, _scrollY),
            (uint)_widthValue, (uint)_heightValue);
        RequestRender();
    }

    #endregion

    #region Text Mutation (marks Yoga dirty)

    /// <summary>Sets plain text content and marks the Yoga node dirty so layout recalculates.</summary>
    protected void SetTextAndDirtyLayout(string text)
    {
        _textBuffer.SetText(text);
        YGNodeAPI.YGNodeMarkDirty(YogaNode);
    }

    /// <summary>Sets styled text content and marks the Yoga node dirty so layout recalculates.</summary>
    protected void SetStyledTextAndDirtyLayout(StyledText styledText)
    {
        _textBuffer.SetStyledText(styledText);
        YGNodeAPI.YGNodeMarkDirty(YogaNode);
    }

    #endregion

    #region Yoga Measure

    private YGSize MeasureFunc(Node node, float availableWidth, MeasureMode widthMode,
        float availableHeight, MeasureMode heightMode)
    {
        // Match TS: if width is undefined/NaN, use 0 (signals max-content to Zig)
        uint effectiveWidth = widthMode == MeasureMode.Undefined || float.IsNaN(availableWidth)
            ? 0 : (uint)Math.Floor(availableWidth);
        // Match TS: use 1 as fallback for undefined/NaN height (not 0)
        uint effectiveHeight = float.IsNaN(availableHeight) ? 1 : (uint)Math.Floor(availableHeight);

        if (_textBufferView.MeasureForDimensions(effectiveWidth, effectiveHeight, out var result))
        {
            float w = Math.Max(1, result.WidthColsMax);
            float h = Math.Max(1, result.LineCount);

            // Match TS: only clamp when widthMode is AtMost and not absolute-positioned.
            // The TS reference clamps BOTH axes together in this case, and never
            // independently clamps height based on heightMode. This is critical for
            // scroll containers: without this, a CodeRenderable inside a ScrollBox
            // reports its height clamped to the viewport, making scrollHeight == viewportHeight
            // and preventing any scrolling.
            if (widthMode == MeasureMode.AtMost && _positionType != PositionValue.Absolute)
            {
                w = Math.Min(effectiveWidth, w);
                h = Math.Min(effectiveHeight, h);
            }

            return new YGSize { Width = w, Height = h };
        }

        return new YGSize { Width = 1, Height = 1 };
    }

    #endregion

    #region Rendering

    protected override void RenderSelf(OptimizedBuffer buffer, float deltaTime)
    {
        buffer.DrawTextBufferView(_textBufferView.Handle, (int)_screenX, (int)_screenY);
    }

    public override bool ShouldStartSelection(int x, int y)
    {
        if (!_selectable)
            return false;

        int localX = x - X;
        int localY = y - Y;
        return localX >= 0 && localX < Width && localY >= 0 && localY < Height;
    }

    public override bool OnSelectionChanged(Selection? selection)
    {
        var localSelection = SelectionHelpers.ConvertGlobalToLocalSelection(selection, X, Y);
        _lastLocalSelection = localSelection;

        bool changed;
        if (localSelection is not { IsActive: true } activeSelection)
        {
            _textBufferView.ResetLocalSelection();
            changed = true;
        }
        else if (selection?.IsStart == true)
        {
            changed = _textBufferView.SetLocalSelection(
                activeSelection.AnchorX,
                activeSelection.AnchorY,
                activeSelection.FocusX,
                activeSelection.FocusY,
                _selectionFg,
                _selectionBg);
        }
        else
        {
            changed = _textBufferView.UpdateLocalSelection(
                activeSelection.AnchorX,
                activeSelection.AnchorY,
                activeSelection.FocusX,
                activeSelection.FocusY,
                _selectionFg,
                _selectionBg);
        }

        if (changed)
            RequestRender();

        return HasSelection();
    }

    public override string GetSelectedText() => _textBufferView.GetSelectedText();

    public override bool HasSelection() => _textBufferView.HasSelection();

    #endregion

    #region Resize

    protected override void OnResize(int width, int height)
    {
        // Set viewport dimensions first — virtual line count depends on width for wrapping.
        _textBufferView.SetViewport(
            (uint)Math.Max(0, _scrollX), (uint)Math.Max(0, _scrollY),
            (uint)width, (uint)height);

        // Clamp scroll to valid range after resize (prevents stale offsets
        // set before the first layout from pointing past content).
        int maxY = Math.Max(0, (int)_textBufferView.GetVirtualLineCount() - height);
        if (_scrollY > maxY)
        {
            _scrollY = maxY;
            _textBufferView.SetViewport(
                (uint)Math.Max(0, _scrollX), (uint)Math.Max(0, _scrollY),
                (uint)width, (uint)height);
        }

        YGNodeAPI.YGNodeMarkDirty(YogaNode);
        base.OnResize(width, height);
    }

    #endregion

    #region Lifecycle

    protected override void DestroySelf()
    {
        _textBufferView.Dispose();
        _textBuffer.Dispose();
        _syntaxStyle.Dispose();
        base.DestroySelf();
    }

    #endregion

    #region Selection Helpers

    private void RefreshLocalSelection()
    {
        if (_lastLocalSelection is not { IsActive: true } localSelection)
            return;

        _textBufferView.SetLocalSelection(
            localSelection.AnchorX,
            localSelection.AnchorY,
            localSelection.FocusX,
            localSelection.FocusY,
            _selectionFg,
            _selectionBg);
    }

    #endregion
}
