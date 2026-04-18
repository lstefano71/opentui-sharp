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
    public SyntaxStyle? SyntaxStyle { get; init; }
    public Action<(int Line, int VisualColumn)>? OnCursorChange { get; init; }
    public Action? OnContentChange { get; init; }
}

/// <summary>
/// Abstract base for editor-backed renderables (Textarea, Input, Code).
/// Owns an EditBuffer + EditorView for text editing and display.
/// Matches TypeScript EditBufferRenderable from EditBufferRenderable.ts.
/// </summary>
public abstract class EditBufferRenderable : Renderable
{
    protected Rgba _ebTextColor;
    protected Rgba _ebBackgroundColor;
    protected TextAttributes _defaultAttributes;
    protected Rgba? _selectionBg;
    protected Rgba? _selectionFg;
    protected byte _wrapMode;
    protected float _scrollMargin;
    protected bool _showCursor;
    protected Rgba _cursorColor;
    protected CursorStyle _cursorStyle;

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
        _showCursor = options.ShowCursor;
        _cursorColor = options.CursorColor ?? Rgba.FromInts(255, 255, 255);
        _cursorStyle = options.CursorStyle;

        Focusable = true;

        // Create native resources
        EditBuffer = EditBuffer.Create((byte)ctx.WidthMethod);
        uint w = _widthValue > 0 ? (uint)_widthValue : 80u;
        uint h = _heightValue > 0 ? (uint)_heightValue : 24u;
        EditorView = EditorView.Create(EditBuffer, w, h);

        EditorView.SetWrapMode(_wrapMode);
        EditorView.SetScrollMargin(_scrollMargin);

        // EditBuffer foreground/background/attributes are set on the underlying
        // TextBuffer via the native API — EditBuffer doesn't expose them directly
        // The EditorView will use them during rendering

        SetupMeasureFunc();
    }

    #region Properties

    public virtual Rgba TextColor
    {
        get => _ebTextColor;
        set { _ebTextColor = value; RequestRender(); }
    }

    public virtual Rgba BackgroundColor
    {
        get => _ebBackgroundColor;
        set { _ebBackgroundColor = value; RequestRender(); }
    }

    public string PlainText => EditBuffer.GetText();
    public int VirtualLineCount => (int)EditorView.GetVirtualLineCount();

    public LogicalCursor LogicalCursor => EditBuffer.GetCursorPosition();
    public VisualCursor VisualCursor => EditorView.GetVisualCursor();

    #endregion

    #region Text Operations (delegated to EditBuffer)

    public void SetText(string text) => EditBuffer.SetText(text);
    public string GetText() => EditBuffer.GetText();
    public virtual void InsertText(string text) { EditBuffer.InsertText(text); RequestRender(); }
    public virtual bool DeleteCharBackward() { EditBuffer.DeleteCharBackward(); RequestRender(); return true; }
    public virtual bool DeleteChar() { EditBuffer.DeleteChar(); RequestRender(); return true; }
    public virtual bool NewLine() { EditBuffer.NewLine(); RequestRender(); return true; }
    public virtual bool Undo() { EditBuffer.Undo(); RequestRender(); return true; }
    public virtual bool Redo() { EditBuffer.Redo(); RequestRender(); return true; }

    #endregion

    #region Cursor Movement (delegated to EditBuffer/EditorView)

    public bool MoveCursorLeft(bool select = false)
    {
        EditBuffer.MoveCursorLeft();
        RequestRender();
        return true;
    }

    public bool MoveCursorRight(bool select = false)
    {
        EditBuffer.MoveCursorRight();
        RequestRender();
        return true;
    }

    public bool MoveCursorUp(bool select = false)
    {
        EditorView.MoveUpVisual();
        RequestRender();
        return true;
    }

    public bool MoveCursorDown(bool select = false)
    {
        EditorView.MoveDownVisual();
        RequestRender();
        return true;
    }

    public bool GotoLineHome(bool select = false)
    {
        var cursor = EditBuffer.GetCursorPosition();
        EditBuffer.SetCursor(cursor.Row, 0);
        RequestRender();
        return true;
    }

    public bool GotoLineEnd(bool select = false)
    {
        var eol = EditBuffer.GetEOL();
        EditBuffer.SetCursor(eol.Row, eol.Col);
        RequestRender();
        return true;
    }

    public bool GotoVisualLineHome(bool select = false)
    {
        var sol = EditorView.GetVisualSOL();
        EditBuffer.SetCursorByOffset(sol.Offset);
        RequestRender();
        return true;
    }

    public bool GotoVisualLineEnd(bool select = false)
    {
        var eol = EditorView.GetVisualEOL();
        EditBuffer.SetCursorByOffset(eol.Offset);
        RequestRender();
        return true;
    }

    public bool GotoBufferHome(bool select = false)
    {
        EditBuffer.SetCursor(0, 0);
        RequestRender();
        return true;
    }

    public bool GotoBufferEnd(bool select = false)
    {
        // Move to end by getting text and setting cursor by offset
        var text = EditBuffer.GetText();
        uint len = (uint)System.Text.Encoding.UTF8.GetByteCount(text);
        EditBuffer.SetCursorByOffset(len);
        RequestRender();
        return true;
    }

    public bool MoveWordForward(bool select = false)
    {
        var boundary = EditorView.GetNextWordBoundary();
        EditBuffer.SetCursorByOffset(boundary.Offset);
        RequestRender();
        return true;
    }

    public bool MoveWordBackward(bool select = false)
    {
        var boundary = EditorView.GetPrevWordBoundary();
        EditBuffer.SetCursorByOffset(boundary.Offset);
        RequestRender();
        return true;
    }

    public virtual bool DeleteWordForward()
    {
        var boundary = EditorView.GetNextWordBoundary();
        var cursor = EditBuffer.GetCursorPosition();
        uint start = EditBuffer.PositionToOffset(cursor.Row, cursor.Col);
        if (boundary.Offset > start)
            EditBuffer.DeleteRange(cursor.Row, cursor.Col, boundary.LogicalRow, boundary.LogicalCol);
        RequestRender();
        return true;
    }

    public virtual bool DeleteWordBackward()
    {
        var boundary = EditorView.GetPrevWordBoundary();
        var cursor = EditBuffer.GetCursorPosition();
        uint cursorOffset = EditBuffer.PositionToOffset(cursor.Row, cursor.Col);
        if (boundary.Offset < cursorOffset)
            EditBuffer.DeleteRange(boundary.LogicalRow, boundary.LogicalCol, cursor.Row, cursor.Col);
        RequestRender();
        return true;
    }

    public virtual bool DeleteLine()
    {
        EditBuffer.DeleteLine();
        RequestRender();
        return true;
    }

    public virtual bool DeleteToLineEnd()
    {
        var cursor = EditBuffer.GetCursorPosition();
        var eol = EditBuffer.GetEOL();
        if (eol.Col > cursor.Col)
            EditBuffer.DeleteRange(cursor.Row, cursor.Col, eol.Row, eol.Col);
        RequestRender();
        return true;
    }

    public virtual bool DeleteToLineStart()
    {
        var cursor = EditBuffer.GetCursorPosition();
        if (cursor.Col > 0)
            EditBuffer.DeleteRange(cursor.Row, 0, cursor.Row, cursor.Col);
        RequestRender();
        return true;
    }

    public virtual bool SelectAll()
    {
        // Select all text from start to end
        string text = EditBuffer.GetText();
        uint len = (uint)System.Text.Encoding.UTF8.GetByteCount(text);
        if (len > 0 && _selectionBg.HasValue)
            EditorView.SetSelection(0, len, _selectionFg ?? _ebTextColor, _selectionBg.Value);
        return true;
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

        // Use the virtual line count as measured height
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

        // Update editor view dimensions if they changed
        EditorView.SetViewportSize((uint)_widthValue, (uint)_heightValue);

        // Draw background
        buffer.FillRect((uint)_screenX, (uint)_screenY,
            (uint)_widthValue, (uint)_heightValue, _ebBackgroundColor);

        // Draw the editor view via native handle
        buffer.DrawEditorView(EditorView.Handle, (int)_screenX, (int)_screenY);

        // Cursor
        if (_showCursor && Focused)
        {
            var vc = EditorView.GetVisualCursor();
            int cx = (int)_screenX + (int)vc.VisualCol;
            int cy = (int)_screenY + (int)vc.VisualRow;
            _ctx?.SetCursorPosition(cx, cy, true);
            _ctx?.SetCursorStyle(new CursorStyleOptions { Style = (byte)_cursorStyle });
            _ctx?.SetCursorColor(_cursorColor);
        }
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
}
