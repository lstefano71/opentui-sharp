using System.Runtime.InteropServices;
using OpenTui.Core.Native;
using OpenTui.Native;

namespace OpenTui.Core;

/// <summary>
/// Managed wrapper around the native text buffer view.
/// Provides viewport, selection, measurement, and line info over a <see cref="TextBuffer"/>.
/// </summary>
public sealed class TextBufferView : IDisposable
{
    private nint _handle;
    private bool _disposed;

    private TextBufferView(nint handle) => _handle = handle;

    /// <summary>Creates a new text buffer view for the given text buffer.</summary>
    public static TextBufferView Create(TextBuffer textBuffer)
    {
        ArgumentNullException.ThrowIfNull(textBuffer);
        nint handle = OpenTuiNative.CreateTextBufferView(textBuffer.Handle);
        if (handle == nint.Zero)
            throw new InvalidOperationException("Failed to create native text buffer view.");
        return new TextBufferView(handle);
    }

    /// <summary>Creates a new text buffer view from an edit buffer's underlying text buffer.</summary>
    public static TextBufferView CreateFrom(EditBuffer editBuffer)
    {
        ArgumentNullException.ThrowIfNull(editBuffer);
        nint tbHandle = editBuffer.GetTextBuffer();
        nint handle = OpenTuiNative.CreateTextBufferView(tbHandle);
        if (handle == nint.Zero)
            throw new InvalidOperationException("Failed to create native text buffer view.");
        return new TextBufferView(handle);
    }

    /// <summary>Gets the native handle.</summary>
    internal nint Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _handle;
        }
    }

    #region Viewport

    /// <summary>Sets the viewport position and size.</summary>
    public void SetViewport(uint x, uint y, uint w, uint h) =>
        OpenTuiNative.TextBufferViewSetViewport(Handle, x, y, w, h);

    /// <summary>Sets the viewport size in columns and rows.</summary>
    public void SetViewportSize(uint w, uint h) =>
        OpenTuiNative.TextBufferViewSetViewportSize(Handle, w, h);

    #endregion

    #region Wrap

    /// <summary>Sets the line wrap mode.</summary>
    public void SetWrapMode(WrapMode mode) =>
        OpenTuiNative.TextBufferViewSetWrapMode(Handle, (byte)mode);

    /// <summary>Sets the line wrap mode (raw byte).</summary>
    public void SetWrapMode(byte mode) =>
        OpenTuiNative.TextBufferViewSetWrapMode(Handle, mode);

    /// <summary>Sets the wrap width for line wrapping.</summary>
    public void SetWrapWidth(uint width) =>
        OpenTuiNative.TextBufferViewSetWrapWidth(Handle, width);

    /// <summary>Gets the number of virtual (wrapped) lines.</summary>
    public uint GetVirtualLineCount() => OpenTuiNative.TextBufferViewGetVirtualLineCount(Handle);

    #endregion

    #region Selection

    /// <summary>Sets the text selection by character offsets with selection colors.</summary>
    public void SetSelection(uint startOffset, uint endOffset, Rgba? selFg = null, Rgba? selBg = null) =>
        RgbaMarshalling.WithColorPtrs(selFg, selBg, (fgPtr, bgPtr) =>
            OpenTuiNative.TextBufferViewSetSelection(Handle, startOffset, endOffset, fgPtr, bgPtr));

    /// <summary>Resets (clears) the current selection.</summary>
    public void ResetSelection() =>
        OpenTuiNative.TextBufferViewResetSelection(Handle);

    /// <summary>Gets the currently selected text as a string.</summary>
    public string GetSelectedText() =>
        Utf8String.GetString((outBuf, maxLen) =>
            OpenTuiNative.TextBufferViewGetSelectedText(Handle, outBuf, maxLen));

    /// <summary>Gets the current selection info as a packed 64-bit value.</summary>
    public ulong GetSelectionInfo() =>
        OpenTuiNative.TextBufferViewGetSelectionInfo(Handle);

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
    public bool SetLocalSelection(int startX, int startY, int endX, int endY, Rgba? selFg = null, Rgba? selBg = null)
    {
        bool result = false;
        RgbaMarshalling.WithColorPtrs(selFg, selBg, (fgPtr, bgPtr) =>
            result = OpenTuiNative.TextBufferViewSetLocalSelection(Handle, startX, startY, endX, endY, fgPtr, bgPtr));
        return result;
    }

    /// <summary>Resets the local (visual coordinate) selection.</summary>
    public void ResetLocalSelection() =>
        OpenTuiNative.TextBufferViewResetLocalSelection(Handle);

    /// <summary>Updates the local (visual coordinate) selection extent.</summary>
    public bool UpdateLocalSelection(int startX, int startY, int endX, int endY, Rgba? selFg = null, Rgba? selBg = null)
    {
        bool result = false;
        RgbaMarshalling.WithColorPtrs(selFg, selBg, (fgPtr, bgPtr) =>
            result = OpenTuiNative.TextBufferViewUpdateLocalSelection(Handle, startX, startY, endX, endY, fgPtr, bgPtr));
        return result;
    }

    /// <summary>Updates the end offset of the current selection.</summary>
    public void UpdateSelection(uint newEnd, Rgba? selFg = null, Rgba? selBg = null) =>
        RgbaMarshalling.WithColorPtrs(selFg, selBg, (fgPtr, bgPtr) =>
            OpenTuiNative.TextBufferViewUpdateSelection(Handle, newEnd, fgPtr, bgPtr));

    #endregion

    #region Measurement

    /// <summary>Measures text content to fit within the given dimensions.</summary>
    public unsafe bool MeasureForDimensions(uint w, uint h, out MeasureResult result)
    {
        result = default;
        fixed (MeasureResult* p = &result)
        {
            return OpenTuiNative.TextBufferViewMeasureForDimensions(Handle, w, h, (nint)p);
        }
    }

    #endregion

    #region Truncation

    /// <summary>Enables or disables line truncation.</summary>
    public void SetTruncate(bool truncate) =>
        OpenTuiNative.TextBufferViewSetTruncate(Handle, truncate);

    #endregion

    #region Line Info

    /// <summary>Native struct matching Zig ExternalLineInfo layout.</summary>
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

    /// <summary>Gets line information directly into the output struct.</summary>
    public void GetLineInfoDirect(nint outInfo) =>
        OpenTuiNative.TextBufferViewGetLineInfoDirect(Handle, outInfo);

    /// <summary>Gets logical line information directly into the output struct.</summary>
    public void GetLogicalLineInfoDirect(nint outInfo) =>
        OpenTuiNative.TextBufferViewGetLogicalLineInfoDirect(Handle, outInfo);

    /// <summary>Gets virtual line layout information as a managed <see cref="LineInfo"/>.</summary>
    public unsafe LineInfo GetLineInfo()
    {
        // Trigger layout by querying line count first
        GetVirtualLineCount();

        NativeLineInfo info = default;
        OpenTuiNative.TextBufferViewGetLineInfoDirect(Handle, (nint)(&info));

        return MarshalLineInfo(in info);
    }

    /// <summary>
    /// Gets logical (full-document) line layout information as a managed <see cref="LineInfo"/>.
    /// Unlike <see cref="GetLineInfo"/> which returns viewport-only data,
    /// this returns mapping for all virtual lines in the document.
    /// </summary>
    public unsafe LineInfo GetLogicalLineInfo()
    {
        GetVirtualLineCount();

        NativeLineInfo info = default;
        OpenTuiNative.TextBufferViewGetLogicalLineInfoDirect(Handle, (nint)(&info));

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

    #region Text Access

    /// <summary>Gets the plain text visible in the view.</summary>
    public string GetPlainText(int maxLen = 64 * 1024) =>
        Utf8String.GetString(
            (outBuf, maxLength) => OpenTuiNative.TextBufferViewGetPlainText(Handle, outBuf, maxLength),
            maxLen);

    #endregion

    #region Tab Indicators

    /// <summary>Sets the Unicode codepoint used to display tab indicators.</summary>
    public void SetTabIndicator(uint codepoint) =>
        OpenTuiNative.TextBufferViewSetTabIndicator(Handle, codepoint);

    /// <summary>Sets the color for tab indicator characters.</summary>
    public void SetTabIndicatorColor(Rgba color) =>
        RgbaMarshalling.WithColorPtr(color, ptr => OpenTuiNative.TextBufferViewSetTabIndicatorColor(Handle, ptr));

    #endregion

    #region Dispose

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            if (_handle != nint.Zero)
            {
                OpenTuiNative.TextBufferViewDestroy(_handle);
                _handle = nint.Zero;
            }
        }
    }

    #endregion
}
