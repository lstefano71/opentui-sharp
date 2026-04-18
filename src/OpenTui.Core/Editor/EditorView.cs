using System.Runtime.InteropServices;
using OpenTui.Core.Native;
using OpenTui.Native;

namespace OpenTui.Core;

/// <summary>
/// Managed wrapper around the native editor view, providing viewport management,
/// cursor movement, selection, and scrolling over an <see cref="EditBuffer"/>.
/// </summary>
public sealed class EditorView : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeLineInfo
    {
        public nint StartColsPtr;
        public uint StartColsLen;
        public nint WidthColsPtr;
        public uint WidthColsLen;
        public nint SourcesPtr;
        public uint SourcesLen;
        public nint WrapsPtr;
        public uint WrapsLen;
        public uint WidthColsMax;
    }

    private nint _handle;
    private bool _disposed;

    internal nint Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _handle;
        }
    }

    private EditorView(nint handle)
    {
        _handle = handle;
    }

    /// <summary>Creates a new editor view for the given edit buffer.</summary>
    public static EditorView Create(
        EditBuffer editBuffer,
        uint viewportWidth,
        uint viewportHeight)
    {
        var handle = OpenTuiNative.CreateEditorView(
            editBuffer.Handle, viewportWidth, viewportHeight);
        if (handle == nint.Zero)
            throw new InvalidOperationException("Failed to create native editor view.");
        return new EditorView(handle);
    }

    #region Viewport

    /// <summary>Sets the viewport position and size.</summary>
    public void SetViewport(uint x, uint y, uint w, uint h, bool clamp = false) =>
        OpenTuiNative.EditorViewSetViewport(Handle, x, y, w, h, clamp);

    /// <summary>Gets the current viewport position and size.</summary>
    public unsafe ViewportBounds GetViewport()
    {
        uint outX, outY, outW, outH;
        OpenTuiNative.EditorViewGetViewport(Handle, (nint)(&outX), (nint)(&outY), (nint)(&outW), (nint)(&outH));
        return new ViewportBounds((int)outX, (int)outY, (int)outW, (int)outH);
    }

    /// <summary>Sets the viewport size in columns and rows.</summary>
    public void SetViewportSize(uint w, uint h) =>
        OpenTuiNative.EditorViewSetViewportSize(Handle, w, h);

    /// <summary>Sets the scroll margin as a fraction of viewport height.</summary>
    public void SetScrollMargin(float margin) =>
        OpenTuiNative.EditorViewSetScrollMargin(Handle, margin);

    #endregion

    #region Wrap Mode

    /// <summary>Sets the line wrap mode.</summary>
    public void SetWrapMode(byte mode) =>
        OpenTuiNative.EditorViewSetWrapMode(Handle, mode);

    /// <summary>Gets the number of virtual (wrapped) lines visible in the viewport.</summary>
    public uint GetVirtualLineCount() => OpenTuiNative.EditorViewGetVirtualLineCount(Handle);

    /// <summary>Gets the total number of virtual (wrapped) lines in the document.</summary>
    public uint GetTotalVirtualLineCount() => OpenTuiNative.EditorViewGetTotalVirtualLineCount(Handle);

    #endregion

    #region Cursor

    /// <summary>Gets the current visual cursor position.</summary>
    public VisualCursor GetVisualCursor()
    {
        VisualCursor cursor = default;
        unsafe
        {
            OpenTuiNative.EditorViewGetVisualCursor(Handle, (nint)(&cursor));
        }
        return cursor;
    }

    /// <summary>Gets both the logical and visual cursor positions.</summary>
    public (LogicalCursor Logical, VisualCursor Visual) GetCursor()
    {
        LogicalCursor logical = default;
        VisualCursor visual = default;
        unsafe
        {
            OpenTuiNative.EditorViewGetCursor(Handle, (nint)(&logical), (nint)(&visual));
        }
        return (logical, visual);
    }

    /// <summary>Sets the cursor position by character offset.</summary>
    public void SetCursorByOffset(uint offset) =>
        OpenTuiNative.EditorViewSetCursorByOffset(Handle, offset);

    #endregion

    #region Visual Movement

    /// <summary>Moves the cursor up one visual line.</summary>
    public void MoveUpVisual() => OpenTuiNative.EditorViewMoveUpVisual(Handle);

    /// <summary>Moves the cursor down one visual line.</summary>
    public void MoveDownVisual() => OpenTuiNative.EditorViewMoveDownVisual(Handle);

    #endregion

    #region Word / Line Boundaries

    /// <summary>Gets the previous word boundary cursor position.</summary>
    public VisualCursor GetPrevWordBoundary()
    {
        VisualCursor cursor = default;
        unsafe
        {
            OpenTuiNative.EditorViewGetPrevWordBoundary(Handle, (nint)(&cursor));
        }
        return cursor;
    }

    /// <summary>Gets the next word boundary cursor position.</summary>
    public VisualCursor GetNextWordBoundary()
    {
        VisualCursor cursor = default;
        unsafe
        {
            OpenTuiNative.EditorViewGetNextWordBoundary(Handle, (nint)(&cursor));
        }
        return cursor;
    }

    /// <summary>Gets the visual start-of-line cursor position.</summary>
    public VisualCursor GetVisualSOL()
    {
        VisualCursor cursor = default;
        unsafe
        {
            OpenTuiNative.EditorViewGetVisualSOL(Handle, (nint)(&cursor));
        }
        return cursor;
    }

    /// <summary>Gets the visual end-of-line cursor position.</summary>
    public VisualCursor GetVisualEOL()
    {
        VisualCursor cursor = default;
        unsafe
        {
            OpenTuiNative.EditorViewGetVisualEOL(Handle, (nint)(&cursor));
        }
        return cursor;
    }

    /// <summary>Gets the end-of-line cursor position.</summary>
    public VisualCursor GetEOL()
    {
        VisualCursor cursor = default;
        unsafe
        {
            OpenTuiNative.EditorViewGetEOL(Handle, (nint)(&cursor));
        }
        return cursor;
    }

    #endregion

    #region Selection

    /// <summary>Sets the selection range by character offsets with optional selection colors.</summary>
    public void SetSelection(uint start, uint end, Rgba? selFg = null, Rgba? selBg = null) =>
        RgbaMarshalling.WithColorPtrs(selBg, selFg, (bgPtr, fgPtr) =>
            OpenTuiNative.EditorViewSetSelection(Handle, start, end, bgPtr, fgPtr));

    /// <summary>Resets (clears) the current selection.</summary>
    public void ResetSelection() => OpenTuiNative.EditorViewResetSelection(Handle);

    /// <summary>Gets the current selection as a packed 64-bit value.</summary>
    public ulong GetSelection() => OpenTuiNative.EditorViewGetSelection(Handle);

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
    public string GetSelectedText() =>
        Utf8String.GetString((buf, len) => OpenTuiNative.EditorViewGetSelectedTextBytes(Handle, buf, len));

    /// <summary>Deletes the currently selected text.</summary>
    public void DeleteSelectedText() => OpenTuiNative.EditorViewDeleteSelectedText(Handle);

    /// <summary>Sets a local (visual coordinate) selection.</summary>
    public bool SetLocalSelection(int sx, int sy, int ex, int ey, Rgba? selFg = null, Rgba? selBg = null, bool extend = false, bool visual = false)
    {
        bool result = false;
        RgbaMarshalling.WithColorPtrs(selBg, selFg, (bgPtr, fgPtr) =>
            result = OpenTuiNative.EditorViewSetLocalSelection(Handle, sx, sy, ex, ey, bgPtr, fgPtr, extend, visual));
        return result;
    }

    /// <summary>Resets the local (visual coordinate) selection.</summary>
    public void ResetLocalSelection() => OpenTuiNative.EditorViewResetLocalSelection(Handle);

    /// <summary>Updates the local (visual coordinate) selection extent.</summary>
    public bool UpdateLocalSelection(int sx, int sy, int ex, int ey, Rgba? selFg = null, Rgba? selBg = null, bool extend = false, bool visual = false)
    {
        bool result = false;
        RgbaMarshalling.WithColorPtrs(selBg, selFg, (bgPtr, fgPtr) =>
            result = OpenTuiNative.EditorViewUpdateLocalSelection(Handle, sx, sy, ex, ey, bgPtr, fgPtr, extend, visual));
        return result;
    }

    /// <summary>Updates the end offset of the current selection.</summary>
    public void UpdateSelection(uint newEnd, Rgba? selFg = null, Rgba? selBg = null) =>
        RgbaMarshalling.WithColorPtrs(selBg, selFg, (bgPtr, fgPtr) =>
            OpenTuiNative.EditorViewUpdateSelection(Handle, newEnd, bgPtr, fgPtr));

    #endregion

    #region Text Access

    /// <summary>Gets the editor text content.</summary>
    public string GetText() =>
        Utf8String.GetString((buf, len) => OpenTuiNative.EditorViewGetText(Handle, buf, len));

    /// <summary>Gets the underlying text buffer view handle.</summary>
    public nint GetTextBufferView() => OpenTuiNative.EditorViewGetTextBufferView(Handle);

    #endregion

    #region Placeholder & Tab Indicators

    /// <summary>Sets the placeholder styled text from native styled chunks.</summary>
    internal unsafe void SetPlaceholderStyledText(ReadOnlySpan<NativeStyledChunk> chunks)
    {
        if (chunks.IsEmpty)
        {
            OpenTuiNative.EditorViewSetPlaceholderStyledText(Handle, 0, 0);
            return;
        }

        fixed (NativeStyledChunk* ptr = chunks)
        {
            OpenTuiNative.EditorViewSetPlaceholderStyledText(Handle, (nint)ptr, (nuint)chunks.Length);
        }
    }

    /// <summary>Sets the Unicode codepoint used to display tab indicators.</summary>
    public void SetTabIndicator(uint codepoint) =>
        OpenTuiNative.EditorViewSetTabIndicator(Handle, codepoint);

    /// <summary>Sets the color for tab indicator characters.</summary>
    public void SetTabIndicatorColor(Rgba color) =>
        RgbaMarshalling.WithColorPtr(color, ptr => OpenTuiNative.EditorViewSetTabIndicatorColor(Handle, ptr));

    #endregion

    #region Line Info

    /// <summary>Gets line information directly into the output struct.</summary>
    public void GetLineInfoDirect(nint outInfo) =>
        OpenTuiNative.EditorViewGetLineInfoDirect(Handle, outInfo);

    /// <summary>Gets logical line information directly into the output struct.</summary>
    public void GetLogicalLineInfoDirect(nint outInfo) =>
        OpenTuiNative.EditorViewGetLogicalLineInfoDirect(Handle, outInfo);

    /// <summary>Gets viewport-relative line layout information as a managed <see cref="LineInfo"/>.</summary>
    public unsafe LineInfo GetLineInfo()
    {
        GetVirtualLineCount();

        NativeLineInfo info = default;
        OpenTuiNative.EditorViewGetLineInfoDirect(Handle, (nint)(&info));

        return MarshalLineInfo(in info);
    }

    /// <summary>Gets full-document logical line layout information as a managed <see cref="LineInfo"/>.</summary>
    public unsafe LineInfo GetLogicalLineInfo()
    {
        GetVirtualLineCount();

        NativeLineInfo info = default;
        OpenTuiNative.EditorViewGetLogicalLineInfoDirect(Handle, (nint)(&info));

        return MarshalLineInfo(in info);
    }

    private static unsafe LineInfo MarshalLineInfo(in NativeLineInfo info)
    {
        var startCols = new uint[info.StartColsLen];
        var widthCols = new uint[info.WidthColsLen];
        var sources = new uint[info.SourcesLen];
        var wraps = new uint[info.WrapsLen];

        for (int i = 0; i < (int)info.StartColsLen; i++)
            startCols[i] = ((uint*)info.StartColsPtr)[i];
        for (int i = 0; i < (int)info.WidthColsLen; i++)
            widthCols[i] = ((uint*)info.WidthColsPtr)[i];
        for (int i = 0; i < (int)info.SourcesLen; i++)
            sources[i] = ((uint*)info.SourcesPtr)[i];
        for (int i = 0; i < (int)info.WrapsLen; i++)
            wraps[i] = ((uint*)info.WrapsPtr)[i];

        return new LineInfo
        {
            LineStartCols = startCols,
            LineWidthCols = widthCols,
            LineSources = sources,
            LineWraps = wraps,
            LineWidthColsMax = info.WidthColsMax,
        };
    }

    #endregion

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            OpenTuiNative.EditorViewDestroy(_handle);
            _handle = nint.Zero;
        }
    }
}
