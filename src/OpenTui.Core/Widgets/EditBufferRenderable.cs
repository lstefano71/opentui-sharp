using Facebook.Yoga;

namespace OpenTui.Core;

/// <summary>
/// Options for EditBufferRenderable.
/// Matches TypeScript EditBufferOptions.
/// </summary>
public class EditBufferOptions : RenderableOptions
{
    public Rgba? TextColor { get; init; }
    public Rgba? BackgroundColor { get; init; }
    public Rgba? SelectionBg { get; init; }
    public Rgba? SelectionFg { get; init; }
    public bool Selectable { get; init; } = true;
    public TextAttributes Attributes { get; init; }
    public byte WrapMode { get; init; } = 2; // 0=none, 1=char, 2=word
    public float ScrollMargin { get; init; } = 0.2f;
    public float ScrollSpeed { get; init; } = 16;
    public bool ShowCursor { get; init; } = true;
    public Rgba? CursorColor { get; init; }
    public CursorStyle CursorStyle { get; init; } = CursorStyle.BlinkingBlock;
    public string? TabIndicator { get; init; }
    public Rgba? TabIndicatorColor { get; init; }
    public SyntaxStyle? SyntaxStyle { get; init; }
    public Action<(int Line, int VisualColumn)>? OnCursorChange { get; init; }
    public Action? OnContentChange { get; init; }
}

/// <summary>
/// Abstract base for editor-backed renderables (Textarea, Input, Code).
/// Owns an EditBuffer + EditorView for text editing and display.
/// Matches TypeScript EditBufferRenderable from EditBufferRenderable.ts.
/// </summary>
public abstract class EditBufferRenderable : Renderable, ILineInfoProvider
{
    protected Rgba _ebTextColor;
    protected Rgba _ebBackgroundColor;
    protected TextAttributes _defaultAttributes;
    protected Rgba? _selectionBg;
    protected Rgba? _selectionFg;
    protected byte _wrapMode;
    protected float _scrollMargin;
    protected float _scrollSpeed;
    protected bool _showCursor;
    protected Rgba _cursorColor;
    protected CursorStyle _cursorStyle;
    protected string? _tabIndicator;
    protected Rgba? _tabIndicatorColor;

    private LineInfo? _cachedLineInfo;
    private bool _lineInfoDirty = true;
    private uint? _selectionAnchorOffset;

    public EditBuffer EditBuffer { get; }
    public EditorView EditorView { get; }

    protected EditBufferRenderable(IRenderContext ctx, EditBufferOptions options)
        : base(ctx, options)
    {
        _ebTextColor = options.TextColor ?? Rgba.FromInts(255, 255, 255);
        _ebBackgroundColor = options.BackgroundColor ?? Rgba.Transparent;
        _defaultAttributes = options.Attributes;
        _selectionBg = options.SelectionBg;
        _selectionFg = options.SelectionFg;
        _wrapMode = options.WrapMode;
        _scrollMargin = options.ScrollMargin;
        _scrollSpeed = options.ScrollSpeed;
        Selectable = options.Selectable;
        _showCursor = options.ShowCursor;
        _cursorColor = options.CursorColor ?? Rgba.FromInts(255, 255, 255);
        _cursorStyle = options.CursorStyle;
        _tabIndicator = options.TabIndicator;
        _tabIndicatorColor = options.TabIndicatorColor;

        Focusable = true;

        EditBuffer = EditBuffer.Create((byte)ctx.WidthMethod);
        uint w = _widthValue > 0 ? (uint)_widthValue : 80u;
        uint h = _heightValue > 0 ? (uint)_heightValue : 24u;
        EditorView = EditorView.Create(EditBuffer, w, h);

        EditorView.SetWrapMode(_wrapMode);
        EditorView.SetScrollMargin(_scrollMargin);
        if (!string.IsNullOrEmpty(_tabIndicator))
            EditorView.SetTabIndicator(ToCodepoint(_tabIndicator));
        if (_tabIndicatorColor is { } tabIndicatorColor)
            EditorView.SetTabIndicatorColor(tabIndicatorColor);

        EditBuffer.SetForeground(_ebTextColor);
        EditBuffer.SetBackground(_ebBackgroundColor);
        EditBuffer.SetAttributes(_defaultAttributes);

        SetupMeasureFunc();
    }

    #region Properties

    public virtual Rgba TextColor
    {
        get => _ebTextColor;
        set
        {
            _ebTextColor = value;
            EditBuffer.SetForeground(value);
            RequestRender();
        }
    }

    public virtual Rgba BackgroundColor
    {
        get => _ebBackgroundColor;
        set
        {
            _ebBackgroundColor = value;
            EditBuffer.SetBackground(value);
            RequestRender();
        }
    }

    public string PlainText => EditBuffer.GetText();
    public int LineCount => CountLogicalLines(PlainText);
    public int VirtualLineCount => (int)EditorView.GetVirtualLineCount();
    public int ScrollY => EditorView.GetViewport().Y;

    public LogicalCursor LogicalCursor => EditBuffer.GetCursorPosition();
    public VisualCursor VisualCursor => EditorView.GetVisualCursor();

    public byte WrapMode
    {
        get => _wrapMode;
        set
        {
            if (_wrapMode == value) return;
            _wrapMode = value;
            EditorView.SetWrapMode(value);
            YGNodeAPI.YGNodeMarkDirty(YogaNode);
            InvalidateLineInfo();
            RequestRender();
        }
    }

    public float ScrollSpeed
    {
        get => _scrollSpeed;
        set => _scrollSpeed = Math.Max(0, value);
    }

    public string? TabIndicator
    {
        get => _tabIndicator;
        set
        {
            if (_tabIndicator == value) return;
            _tabIndicator = value;
            if (!string.IsNullOrEmpty(value))
                EditorView.SetTabIndicator(ToCodepoint(value));
            RequestRender();
        }
    }

    public Rgba? TabIndicatorColor
    {
        get => _tabIndicatorColor;
        set
        {
            _tabIndicatorColor = value;
            if (value is { } color)
                EditorView.SetTabIndicatorColor(color);
            RequestRender();
        }
    }

    #endregion

    #region Text Operations

    public void SetText(string text)
    {
        ClearSelection();
        EditBuffer.SetText(text);
        InvalidateLineInfo();
    }

    public string GetText() => EditBuffer.GetText();

    public virtual void InsertText(string text)
    {
        DeleteSelectionIfPresent();
        EditBuffer.InsertText(text);
        InvalidateLineInfo();
        RequestRender();
    }

    public virtual bool DeleteCharBackward()
    {
        if (DeleteSelectionIfPresent())
            return true;

        ClearSelection();
        EditBuffer.DeleteCharBackward();
        InvalidateLineInfo();
        RequestRender();
        return true;
    }

    public virtual bool DeleteChar()
    {
        if (DeleteSelectionIfPresent())
            return true;

        ClearSelection();
        EditBuffer.DeleteChar();
        InvalidateLineInfo();
        RequestRender();
        return true;
    }

    public virtual bool NewLine()
    {
        ClearSelection();
        EditBuffer.NewLine();
        InvalidateLineInfo();
        RequestRender();
        return true;
    }

    public virtual bool Undo()
    {
        ClearSelection();
        EditBuffer.Undo();
        InvalidateLineInfo();
        RequestRender();
        return true;
    }

    public virtual bool Redo()
    {
        ClearSelection();
        EditBuffer.Redo();
        InvalidateLineInfo();
        RequestRender();
        return true;
    }

    #endregion

    #region Cursor Movement

    public bool MoveCursorLeft(bool select = false)
    {
        if (!select && EditorView.HasSelection())
        {
            var selection = EditorView.GetSelectionRange()!.Value;
            EditBuffer.SetCursorByOffset(selection.Start);
            ClearSelection();
            RequestRender();
            return true;
        }

        UpdateSelectionForMovement(select, beforeMovement: true);
        EditBuffer.MoveCursorLeft();
        UpdateSelectionForMovement(select, beforeMovement: false);
        RequestRender();
        return true;
    }

    public bool MoveCursorRight(bool select = false)
    {
        if (!select && EditorView.HasSelection())
        {
            var selection = EditorView.GetSelectionRange()!.Value;
            uint cursorOffset = EditBuffer.GetCursorPosition().Offset;
            uint targetOffset = cursorOffset == selection.Start && selection.End > selection.Start
                ? selection.End - 1
                : selection.End;
            EditBuffer.SetCursorByOffset(targetOffset);
            ClearSelection();
            RequestRender();
            return true;
        }

        UpdateSelectionForMovement(select, beforeMovement: true);
        EditBuffer.MoveCursorRight();
        UpdateSelectionForMovement(select, beforeMovement: false);
        RequestRender();
        return true;
    }

    public bool MoveCursorUp(bool select = false)
    {
        UpdateSelectionForMovement(select, beforeMovement: true);
        EditorView.MoveUpVisual();
        UpdateSelectionForMovement(select, beforeMovement: false);
        RequestRender();
        return true;
    }

    public bool MoveCursorDown(bool select = false)
    {
        UpdateSelectionForMovement(select, beforeMovement: true);
        EditorView.MoveDownVisual();
        UpdateSelectionForMovement(select, beforeMovement: false);
        RequestRender();
        return true;
    }

    public bool GotoLineHome(bool select = false)
    {
        UpdateSelectionForMovement(select, beforeMovement: true);
        var (logical, _) = EditorView.GetCursor();
        if (logical.Col == 0 && logical.Row > 0)
        {
            EditBuffer.SetCursor(logical.Row - 1, 0);
            var prevLineEol = EditBuffer.GetEOL();
            EditBuffer.SetCursor(prevLineEol.Row, prevLineEol.Col);
        }
        else
        {
            EditBuffer.SetCursor(logical.Row, 0);
        }

        UpdateSelectionForMovement(select, beforeMovement: false);
        RequestRender();
        return true;
    }

    public bool GotoLineEnd(bool select = false)
    {
        UpdateSelectionForMovement(select, beforeMovement: true);
        var (logical, _) = EditorView.GetCursor();
        var eol = EditBuffer.GetEOL();
        if (logical.Col == eol.Col && logical.Row < (uint)Math.Max(0, LineCount - 1))
            EditBuffer.SetCursor(logical.Row + 1, 0);
        else
            EditBuffer.SetCursor(eol.Row, eol.Col);

        UpdateSelectionForMovement(select, beforeMovement: false);
        RequestRender();
        return true;
    }

    public bool GotoVisualLineHome(bool select = false)
    {
        UpdateSelectionForMovement(select, beforeMovement: true);
        var sol = EditorView.GetVisualSOL();
        EditBuffer.SetCursor(sol.LogicalRow, sol.LogicalCol);
        UpdateSelectionForMovement(select, beforeMovement: false);
        RequestRender();
        return true;
    }

    public bool GotoVisualLineEnd(bool select = false)
    {
        UpdateSelectionForMovement(select, beforeMovement: true);
        var eol = EditorView.GetVisualEOL();
        EditBuffer.SetCursor(eol.LogicalRow, eol.LogicalCol);
        UpdateSelectionForMovement(select, beforeMovement: false);
        RequestRender();
        return true;
    }

    public bool GotoBufferHome(bool select = false)
    {
        UpdateSelectionForMovement(select, beforeMovement: true);
        EditBuffer.SetCursor(0, 0);
        UpdateSelectionForMovement(select, beforeMovement: false);
        RequestRender();
        return true;
    }

    public bool GotoBufferEnd(bool select = false)
    {
        UpdateSelectionForMovement(select, beforeMovement: true);
        EditBuffer.GotoLine(uint.MaxValue);
        UpdateSelectionForMovement(select, beforeMovement: false);
        RequestRender();
        return true;
    }

    public bool MoveWordForward(bool select = false)
    {
        UpdateSelectionForMovement(select, beforeMovement: true);
        var boundary = EditBuffer.GetNextWordBoundary();
        EditBuffer.SetCursorByOffset(boundary.Offset);
        UpdateSelectionForMovement(select, beforeMovement: false);
        RequestRender();
        return true;
    }

    public bool MoveWordBackward(bool select = false)
    {
        UpdateSelectionForMovement(select, beforeMovement: true);
        var boundary = EditBuffer.GetPrevWordBoundary();
        EditBuffer.SetCursorByOffset(boundary.Offset);
        UpdateSelectionForMovement(select, beforeMovement: false);
        RequestRender();
        return true;
    }

    public virtual bool DeleteWordForward()
    {
        if (DeleteSelectionIfPresent())
            return true;

        var boundary = EditBuffer.GetNextWordBoundary();
        var cursor = EditBuffer.GetCursorPosition();
        if (boundary.Offset > cursor.Offset)
            EditBuffer.DeleteRange(cursor.Row, cursor.Col, boundary.Row, boundary.Col);

        ClearSelection();
        InvalidateLineInfo();
        RequestRender();
        return true;
    }

    public virtual bool DeleteWordBackward()
    {
        if (DeleteSelectionIfPresent())
            return true;

        var boundary = EditBuffer.GetPrevWordBoundary();
        var cursor = EditBuffer.GetCursorPosition();
        if (boundary.Offset < cursor.Offset)
            EditBuffer.DeleteRange(boundary.Row, boundary.Col, cursor.Row, cursor.Col);

        ClearSelection();
        InvalidateLineInfo();
        RequestRender();
        return true;
    }

    public virtual bool DeleteLine()
    {
        ClearSelection();
        EditBuffer.DeleteLine();
        InvalidateLineInfo();
        RequestRender();
        return true;
    }

    public virtual bool DeleteToLineEnd()
    {
        if (DeleteSelectionIfPresent())
            return true;

        var (cursor, _) = EditorView.GetCursor();
        var eol = EditBuffer.GetEOL();
        if (eol.Col > cursor.Col)
            EditBuffer.DeleteRange(cursor.Row, cursor.Col, eol.Row, eol.Col);

        InvalidateLineInfo();
        RequestRender();
        return true;
    }

    public virtual bool DeleteToLineStart()
    {
        if (DeleteSelectionIfPresent())
            return true;

        var (cursor, _) = EditorView.GetCursor();
        if (cursor.Col > 0)
            EditBuffer.DeleteRange(cursor.Row, 0, cursor.Row, cursor.Col);
        else if (cursor.Row > 0)
            EditBuffer.DeleteCharBackward();

        InvalidateLineInfo();
        RequestRender();
        return true;
    }

    public virtual bool SelectAll()
    {
        UpdateSelectionForMovement(false, beforeMovement: true);
        EditBuffer.SetCursor(0, 0);
        return GotoBufferEnd(select: true);
    }

    #endregion

    #region Yoga Measure

    private void SetupMeasureFunc()
    {
        YGNodeAPI.YGNodeSetMeasureFunc(YogaNode, MeasureFunc);
    }

    private YGSize MeasureFunc(Node node, float width, MeasureMode widthMode,
        float height, MeasureMode heightMode)
    {
        uint constrainedWidth = widthMode == MeasureMode.Undefined || float.IsNaN(width)
            ? 80u
            : (uint)width;

        EditorView.SetViewportSize(constrainedWidth, 10000);
        float measuredHeight = EditorView.GetTotalVirtualLineCount();
        float measuredWidth = constrainedWidth;

        if (widthMode == MeasureMode.AtMost)
            measuredWidth = Math.Min(measuredWidth, width);

        return new YGSize { Width = measuredWidth, Height = measuredHeight };
    }

    #endregion

    #region Rendering

    protected override void RenderSelf(OptimizedBuffer buffer, float deltaTime)
    {
        if (_widthValue == 0 || _heightValue == 0) return;

        var baseX = _buffered ? 0 : (int)_screenX;
        var baseY = _buffered ? 0 : (int)_screenY;

        EditorView.SetViewportSize((uint)_widthValue, (uint)_heightValue);

        buffer.FillRect((uint)baseX, (uint)baseY,
            (uint)_widthValue, (uint)_heightValue, _ebBackgroundColor);

        buffer.DrawEditorView(EditorView.Handle, baseX, baseY);

        if (_showCursor && Focused)
        {
            var vc = EditorView.GetVisualCursor();
            int cx = (int)_screenX + (int)vc.VisualCol + 1;
            int cy = (int)_screenY + (int)vc.VisualRow + 1;
            _ctx?.SetCursorPosition(cx, cy, true);
            _ctx?.SetCursorStyle(new CursorStyleOptions { Style = (byte)_cursorStyle });
            _ctx?.SetCursorColor(_cursorColor);
        }
    }

    #endregion

    #region Selection / Line Info

    public override bool HasSelection() => EditorView.HasSelection();

    public override string GetSelectedText() => EditorView.GetSelectedText();

    public LineInfo? GetCachedLineInfo()
    {
        if (_lineInfoDirty)
        {
            _cachedLineInfo = EditorView.GetLogicalLineInfo();
            _lineInfoDirty = false;
        }

        return _cachedLineInfo;
    }

    protected void InvalidateLineInfo()
    {
        _lineInfoDirty = true;
        Emit(ILineInfoProvider.LineInfoChangeEvent);
    }

    #endregion

    #region Mouse / Paste / Resize

    protected override void HandlePaste(PasteEvent evt)
    {
        InsertText(evt.Text);
    }

    protected override void OnMouseEvent(UiMouseEvent evt)
    {
        if (evt.Type == MouseEventType.Scroll && evt.Scroll is { } scroll)
        {
            var viewport = EditorView.GetViewport();
            int offsetX = viewport.X;
            int offsetY = viewport.Y;

            switch (scroll.Direction)
            {
                case "up":
                    offsetY = Math.Max(0, offsetY - scroll.Delta);
                    break;
                case "down":
                {
                    int totalVirtualLines = (int)EditorView.GetTotalVirtualLineCount();
                    int maxOffsetY = Math.Max(0, totalVirtualLines - viewport.Height);
                    offsetY = Math.Min(offsetY + scroll.Delta, maxOffsetY);
                    break;
                }
                case "left" when _wrapMode == 0:
                    offsetX = Math.Max(0, offsetX - scroll.Delta);
                    break;
                case "right" when _wrapMode == 0:
                    offsetX = Math.Max(0, offsetX + scroll.Delta);
                    break;
            }

            if (offsetX != viewport.X || offsetY != viewport.Y)
            {
                EditorView.SetViewport((uint)offsetX, (uint)offsetY, (uint)viewport.Width, (uint)viewport.Height, clamp: true);
                RequestRender();
            }
        }

        base.OnMouseEvent(evt);
    }

    protected override void OnResize(int width, int height)
    {
        base.OnResize(width, height);
        EditorView.SetViewportSize((uint)width, (uint)height);
        InvalidateLineInfo();
    }

    #endregion

    #region Dispose

    protected override void DestroySelf()
    {
        EditorView.Dispose();
        EditBuffer.Dispose();
        base.DestroySelf();
    }

    #endregion

    #region Helpers

    protected bool DeleteSelectionIfPresent()
    {
        if (!EditorView.HasSelection())
            return false;

        EditorView.DeleteSelectedText();
        _selectionAnchorOffset = null;
        InvalidateLineInfo();
        RequestRender();
        return true;
    }

    protected void ClearSelection()
    {
        _selectionAnchorOffset = null;
        if (EditorView.HasSelection())
            EditorView.ResetSelection();
    }

    private void UpdateSelectionForMovement(bool select, bool beforeMovement)
    {
        if (!select)
        {
            _selectionAnchorOffset = null;
            if (EditorView.HasSelection())
                EditorView.ResetSelection();
            return;
        }

        uint currentOffset = EditBuffer.GetCursorPosition().Offset;
        if (beforeMovement)
        {
            _selectionAnchorOffset ??= currentOffset;
            return;
        }

        if (_selectionAnchorOffset is not { } anchorOffset)
            return;

        currentOffset = EditBuffer.GetCursorPosition().Offset;
        if (currentOffset == anchorOffset)
        {
            EditorView.ResetSelection();
            return;
        }

        EditorView.SetSelection(
            Math.Min(anchorOffset, currentOffset),
            Math.Max(anchorOffset, currentOffset),
            _selectionFg ?? _ebTextColor,
            _selectionBg);
    }

    private static int CountLogicalLines(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 1;

        int count = 1;
        foreach (char ch in text)
            if (ch == '\n')
                count++;

        return count;
    }

    private static uint ToCodepoint(string value)
    {
        foreach (var rune in value.EnumerateRunes())
            return (uint)rune.Value;

        return 0;
    }

    #endregion
}
