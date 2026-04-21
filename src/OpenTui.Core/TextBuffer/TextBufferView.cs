using OpenTui.Core.Managed;

namespace OpenTui.Core;

/// <summary>
/// Managed wrapper providing viewport, selection, measurement, and line info
/// over a <see cref="TextBuffer"/>. Delegates to <see cref="ManagedTextBufferView"/>.
/// </summary>
public sealed class TextBufferView : IDisposable
{
    internal ManagedTextBufferView _managed;
    private bool _disposed;

    private TextBufferView(ManagedTextBufferView managed) => _managed = managed;

    /// <summary>Creates a new text buffer view for the given text buffer.</summary>
    public static TextBufferView Create(TextBuffer textBuffer)
    {
        ArgumentNullException.ThrowIfNull(textBuffer);
        var managed = ManagedTextBufferView.Create(textBuffer._managed, 0, 0);
        return new TextBufferView(managed);
    }

    /// <summary>Creates a new text buffer view from an edit buffer's underlying text buffer.</summary>
    /// <remarks>
    /// Requires EditBuffer to have been migrated to managed internals.
    /// Currently throws <see cref="NotSupportedException"/> until the EditBuffer swap is complete.
    /// </remarks>
    public static TextBufferView CreateFrom(EditBuffer editBuffer)
    {
        ArgumentNullException.ThrowIfNull(editBuffer);
        // EditBuffer hasn't been migrated to managed yet.
        throw new NotSupportedException(
            "TextBufferView.CreateFrom(EditBuffer) requires EditBuffer to be migrated to managed internals.");
    }

    #region Viewport

    /// <summary>Sets the viewport position and size.</summary>
    public void SetViewport(uint x, uint y, uint w, uint h) =>
        _managed.SetViewport(x, y, w, h);

    /// <summary>Sets the viewport size in columns and rows.</summary>
    public void SetViewportSize(uint w, uint h) =>
        _managed.SetViewportSize(w, h);

    #endregion

    #region Wrap

    /// <summary>Sets the line wrap mode.</summary>
    public void SetWrapMode(WrapMode mode) =>
        _managed.WrapMode = mode;

    /// <summary>Sets the line wrap mode (raw byte).</summary>
    public void SetWrapMode(byte mode) =>
        _managed.WrapMode = (WrapMode)mode;

    /// <summary>Sets the wrap width for line wrapping.</summary>
    public void SetWrapWidth(uint width) =>
        _managed.SetWrapWidth(width);

    /// <summary>Gets the number of virtual (wrapped) lines.</summary>
    public uint GetVirtualLineCount() => _managed.GetVirtualLineCount();

    #endregion

    #region Selection

    /// <summary>Sets the text selection by character offsets with selection colors.</summary>
    public void SetSelection(uint startOffset, uint endOffset, Rgba? selBg = null, Rgba? selFg = null) =>
        _managed.SetSelection(startOffset, endOffset, selBg, selFg);

    /// <summary>Resets (clears) the current selection.</summary>
    public void ResetSelection() =>
        _managed.ResetSelection();

    /// <summary>Gets the currently selected text as a string.</summary>
    public string GetSelectedText() =>
        _managed.GetSelectedText();

    /// <summary>Gets the current selection info as a packed 64-bit value.</summary>
    public ulong GetSelectionInfo() =>
        _managed.GetSelectionInfo();

    /// <summary>Gets the current selection range, or null when no selection is active.</summary>
    public (uint Start, uint End)? GetSelectionRange()
    {
        const ulong noSelection = 0xffff_ffff_ffff_ffffUL;
        ulong packed = GetSelectionInfo();
        if (packed == noSelection)
            return null;

        return ((uint)(packed >> 32), (uint)(packed & 0xffff_ffff));
    }

    /// <summary>Returns true when the text buffer view currently has an active selection.</summary>
    public bool HasSelection() => GetSelectionRange() is not null;

    /// <summary>Sets a local (visual coordinate) selection with selection colors.</summary>
    public bool SetLocalSelection(int startX, int startY, int endX, int endY, Rgba? selBg = null, Rgba? selFg = null) =>
        _managed.SetLocalSelection(startX, startY, endX, endY, selBg, selFg);

    /// <summary>Resets the local (visual coordinate) selection.</summary>
    public void ResetLocalSelection() =>
        _managed.ResetLocalSelection();

    /// <summary>Updates the local (visual coordinate) selection extent.</summary>
    public bool UpdateLocalSelection(int startX, int startY, int endX, int endY, Rgba? selBg = null, Rgba? selFg = null) =>
        _managed.UpdateLocalSelection(startX, startY, endX, endY, selBg, selFg);

    /// <summary>Updates the end offset of the current selection.</summary>
    public void UpdateSelection(uint newEnd, Rgba? selBg = null, Rgba? selFg = null) =>
        _managed.UpdateSelection(newEnd, selBg, selFg);

    #endregion

    #region Measurement

    /// <summary>Measures text content to fit within the given dimensions.</summary>
    public bool MeasureForDimensions(uint w, uint h, out MeasureResult result) =>
        _managed.MeasureForDimensions(w, h, out result);

    #endregion

    #region Truncation

    /// <summary>Enables or disables line truncation.</summary>
    public void SetTruncate(bool truncate) =>
        _managed.SetTruncate(truncate);

    #endregion

    #region Line Info

    /// <summary>Gets virtual line layout information as a managed <see cref="LineInfo"/>.</summary>
    public LineInfo GetLineInfo() =>
        _managed.GetLineInfo();

    /// <summary>
    /// Gets logical (full-document) line layout information as a managed <see cref="LineInfo"/>.
    /// Unlike <see cref="GetLineInfo"/> which returns viewport-only data,
    /// this returns mapping for all virtual lines in the document.
    /// </summary>
    public LineInfo GetLogicalLineInfo() =>
        _managed.GetLogicalLineInfo();

    #endregion

    #region Text Access

    /// <summary>Gets the plain text visible in the view.</summary>
    public string GetPlainText(int maxLen = 64 * 1024) =>
        _managed.GetPlainText(maxLen);

    #endregion

    #region Tab Indicators

    /// <summary>Sets the Unicode codepoint used to display tab indicators.</summary>
    public void SetTabIndicator(uint codepoint) =>
        _managed.TabIndicatorCodepoint = codepoint;

    /// <summary>Sets the color for tab indicator characters.</summary>
    public void SetTabIndicatorColor(Rgba color) =>
        _managed.TabIndicatorColor = color;

    #endregion

    #region Dispose

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _managed.Dispose();
        }
    }

    #endregion
}
