using OpenTui.Core.Managed;

namespace OpenTui.Core;

/// <summary>
/// Managed wrapper around <see cref="ManagedEditorView"/>, providing viewport management,
/// cursor movement, selection, and scrolling over an <see cref="EditBuffer"/>.
/// </summary>
public sealed class EditorView : IDisposable
{
    internal readonly ManagedEditorView _managed;
    private bool _disposed;

    private EditorView(ManagedEditorView managed)
    {
        _managed = managed;
    }

    /// <summary>Creates a new editor view for the given edit buffer.</summary>
    public static EditorView Create(
        EditBuffer editBuffer,
        uint viewportWidth,
        uint viewportHeight)
    {
        var managed = ManagedEditorView.Create(
            editBuffer._managed, viewportWidth, viewportHeight);
        return new EditorView(managed);
    }

    #region Viewport

    /// <summary>Sets the viewport position and size.</summary>
    public void SetViewport(uint x, uint y, uint w, uint h, bool clamp = false) =>
        _managed.View.SetViewport(x, y, w, h);

    /// <summary>Gets the current viewport position and size.</summary>
    public ViewportBounds GetViewport() =>
        new((int)_managed.View.ViewportX, (int)_managed.View.ViewportY,
            (int)_managed.View.Width, (int)_managed.View.Height);

    /// <summary>Sets the viewport size in columns and rows.</summary>
    public void SetViewportSize(uint w, uint h) =>
        _managed.Resize(w, h);

    /// <summary>Sets the scroll margin as a fraction of viewport height.</summary>
    public void SetScrollMargin(float margin) =>
        _managed.ScrollMargin = margin;

    #endregion

    #region Wrap Mode

    /// <summary>Sets the line wrap mode.</summary>
    public void SetWrapMode(byte mode) =>
        _managed.WrapMode = (WrapMode)mode;

    /// <summary>Gets the number of virtual (wrapped) lines visible in the viewport.</summary>
    public uint GetVirtualLineCount() => _managed.View.GetVirtualLineCount();

    /// <summary>Gets the total number of virtual (wrapped) lines in the document.</summary>
    public uint GetTotalVirtualLineCount() => _managed.View.GetVirtualLineCount();

    #endregion

    #region Cursor

    /// <summary>Gets the current visual cursor position.</summary>
    public VisualCursor GetVisualCursor()
    {
        var vc = _managed.GetVisualCursor();
        return new VisualCursor(vc.VisualRow, vc.VisualCol, vc.LogicalRow, vc.LogicalCol, vc.Offset);
    }

    /// <summary>Gets both the logical and visual cursor positions.</summary>
    public (LogicalCursor Logical, VisualCursor Visual) GetCursor()
    {
        var vc = _managed.GetVisualCursor();
        var logical = new LogicalCursor(vc.LogicalRow, vc.LogicalCol, vc.Offset);
        var visual = new VisualCursor(vc.VisualRow, vc.VisualCol, vc.LogicalRow, vc.LogicalCol, vc.Offset);
        return (logical, visual);
    }

    /// <summary>Sets the cursor position by character offset.</summary>
    public void SetCursorByOffset(uint offset) =>
        _managed.EditBuffer.SetCursorByOffset(offset);

    #endregion

    #region Visual Movement

    /// <summary>Moves the cursor up one visual line.</summary>
    public void MoveUpVisual() => _managed.MoveUp();

    /// <summary>Moves the cursor down one visual line.</summary>
    public void MoveDownVisual() => _managed.MoveDown();

    #endregion

    #region Word / Line Boundaries

    /// <summary>Gets the previous word boundary cursor position.</summary>
    public VisualCursor GetPrevWordBoundary()
    {
        var (line, col) = _managed.EditBuffer.GetPrevWordBoundary();
        var vc = _managed.LogicalToVisualCursor(line, col);
        return new VisualCursor(vc.VisualRow, vc.VisualCol, vc.LogicalRow, vc.LogicalCol, vc.Offset);
    }

    /// <summary>Gets the next word boundary cursor position.</summary>
    public VisualCursor GetNextWordBoundary()
    {
        var (line, col) = _managed.EditBuffer.GetNextWordBoundary();
        var vc = _managed.LogicalToVisualCursor(line, col);
        return new VisualCursor(vc.VisualRow, vc.VisualCol, vc.LogicalRow, vc.LogicalCol, vc.Offset);
    }

    /// <summary>Gets the visual start-of-line cursor position.</summary>
    public VisualCursor GetVisualSOL()
    {
        var vc = _managed.GetVisualSOL();
        return new VisualCursor(vc.VisualRow, vc.VisualCol, vc.LogicalRow, vc.LogicalCol, vc.Offset);
    }

    /// <summary>Gets the visual end-of-line cursor position.</summary>
    public VisualCursor GetVisualEOL()
    {
        var vc = _managed.GetVisualEOL();
        return new VisualCursor(vc.VisualRow, vc.VisualCol, vc.LogicalRow, vc.LogicalCol, vc.Offset);
    }

    /// <summary>Gets the end-of-line cursor position.</summary>
    public VisualCursor GetEOL()
    {
        var (line, _) = _managed.EditBuffer.GetPrimaryCursor();
        uint lineWidth = _managed.EditBuffer.GetLineWidth(line);
        var vc = _managed.LogicalToVisualCursor(line, lineWidth);
        return new VisualCursor(vc.VisualRow, vc.VisualCol, vc.LogicalRow, vc.LogicalCol, vc.Offset);
    }

    #endregion

    #region Selection

    /// <summary>Sets the selection range by character offsets with optional selection colors.</summary>
    public void SetSelection(uint start, uint end, Rgba? selBg = null, Rgba? selFg = null) =>
        _managed.View.SetSelection(start, end, selBg, selFg);

    /// <summary>Resets (clears) the current selection.</summary>
    public void ResetSelection() => _managed.View.ResetSelection();

    /// <summary>Gets the current selection as a packed 64-bit value.</summary>
    public ulong GetSelection() => _managed.View.GetSelectionInfo();

    /// <summary>Gets the current selection range, or null when no selection is active.</summary>
    public (uint Start, uint End)? GetSelectionRange()
    {
        const ulong noSelection = 0xffff_ffff_ffff_ffffUL;
        ulong packed = GetSelection();
        if (packed == noSelection)
            return null;

        return ((uint)(packed >> 32), (uint)(packed & 0xffff_ffff));
    }

    /// <summary>Returns true when the editor currently has an active selection.</summary>
    public bool HasSelection() => GetSelectionRange() is not null;

    /// <summary>Gets the currently selected text, or an empty string if nothing is selected.</summary>
    public string GetSelectedText() => _managed.GetSelectedText();

    /// <summary>Deletes the currently selected text.</summary>
    public void DeleteSelectedText() => _managed.DeleteSelection();

    /// <summary>Sets a local (visual coordinate) selection.</summary>
    public bool SetLocalSelection(int sx, int sy, int ex, int ey, Rgba? selFg = null, Rgba? selBg = null, bool extend = false, bool visual = false) =>
        _managed.View.SetLocalSelection(sx, sy, ex, ey, selBg, selFg);

    /// <summary>Resets the local (visual coordinate) selection.</summary>
    public void ResetLocalSelection() => _managed.View.ResetLocalSelection();

    /// <summary>Updates the local (visual coordinate) selection extent.</summary>
    public bool UpdateLocalSelection(int sx, int sy, int ex, int ey, Rgba? selFg = null, Rgba? selBg = null, bool extend = false, bool visual = false) =>
        _managed.View.UpdateLocalSelection(sx, sy, ex, ey, selBg, selFg);

    /// <summary>Updates the end offset of the current selection.</summary>
    public void UpdateSelection(uint newEnd, Rgba? selFg = null, Rgba? selBg = null) =>
        _managed.View.UpdateSelection(newEnd, selBg, selFg);

    #endregion

    #region Text Access

    /// <summary>Gets the editor text content.</summary>
    public string GetText() => _managed.EditBuffer.GetText();

    /// <summary>Gets the underlying managed text buffer view.</summary>
    internal ManagedTextBufferView GetManagedTextBufferView() => _managed.View;

    #endregion

    #region Placeholder & Tab Indicators

    /// <summary>Sets the placeholder text. In managed mode, pass plain text.</summary>
    public void SetPlaceholder(string text) => _managed.SetPlaceholder(text);

    /// <summary>Clears the placeholder text.</summary>
    public void ClearPlaceholder() => _managed.ClearPlaceholder();

    /// <summary>Sets the Unicode codepoint used to display tab indicators.</summary>
    public void SetTabIndicator(uint codepoint) =>
        _managed.View.TabIndicatorCodepoint = codepoint;

    /// <summary>Sets the color for tab indicator characters.</summary>
    public void SetTabIndicatorColor(Rgba color) =>
        _managed.View.TabIndicatorColor = color;

    #endregion

    #region Line Info

    /// <summary>Gets viewport-relative line layout information as a managed <see cref="LineInfo"/>.</summary>
    public LineInfo GetLineInfo() => _managed.View.GetLineInfo();

    /// <summary>Gets full-document logical line layout information as a managed <see cref="LineInfo"/>.</summary>
    public LineInfo GetLogicalLineInfo() => _managed.View.GetLogicalLineInfo();

    #endregion

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _managed.Dispose();
        }
    }
}
