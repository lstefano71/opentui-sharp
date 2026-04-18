using System.Runtime.InteropServices;
using System.Text;
using OpenTui.Core.Native;
using OpenTui.Native;

namespace OpenTui.Core;

/// <summary>
/// Options for drawing a box with borders, background fill, and optional title text.
/// </summary>
public sealed record BoxDrawOptions
{
    public BorderCharacters BorderChars { get; init; } = BorderCharacters.Single;
    public BorderSides Sides { get; init; } = BorderSides.All;
    public bool ShouldFill { get; init; } = true;
    public Rgba? BorderColor { get; init; }
    public Rgba? BackgroundColor { get; init; }
    public string? Title { get; init; }
    public TitleAlignment TitleAlignment { get; init; } = TitleAlignment.Left;
    public string? BottomTitle { get; init; }
    public TitleAlignment BottomTitleAlignment { get; init; } = TitleAlignment.Left;
}

/// <summary>
/// Safe managed wrapper around the native OpenTUI optimized buffer.
/// Wraps all P/Invoke methods in the Buffer region of <see cref="OpenTuiNative"/>.
/// </summary>
public sealed class OptimizedBuffer : IDisposable
{
    private nint _handle;
    private bool _disposed;
    private bool _ownsHandle = true;

    private OptimizedBuffer(nint handle)
    {
        _handle = handle;
    }

    /// <summary>
    /// Wraps an existing native buffer handle (e.g. from NativeRenderer.GetNextBuffer).
    /// The wrapper does NOT own the handle — Dispose is a no-op.
    /// </summary>
    internal static OptimizedBuffer WrapExisting(nint handle) =>
        new(handle) { _ownsHandle = false };

    /// <summary>Creates a new optimized buffer with the specified dimensions.</summary>
    public static OptimizedBuffer Create(
        uint width,
        uint height,
        WidthMethod widthMethod = WidthMethod.Unicode,
        bool respectAlpha = false,
        string? id = null)
    {
        var idBytes = id is null ? [] : Encoding.UTF8.GetBytes(id);
        nint handle;
        unsafe
        {
            fixed (byte* idPtr = idBytes)
            {
                handle = OpenTuiNative.CreateOptimizedBuffer(
                    width, height, respectAlpha, (byte)widthMethod,
                    idBytes.Length > 0 ? (nint)idPtr : nint.Zero, (nuint)idBytes.Length);
            }
        }
        if (handle == nint.Zero)
            throw new InvalidOperationException("Failed to create native optimized buffer.");
        return new OptimizedBuffer(handle);
    }

    /// <summary>Gets the native buffer handle. For use by other wrappers that need the raw pointer.</summary>
    internal nint Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _handle;
        }
    }

    #region Properties

    /// <summary>Gets the width of the buffer in columns.</summary>
    public uint Width => OpenTuiNative.GetBufferWidth(Handle);

    /// <summary>Gets the height of the buffer in rows.</summary>
    public uint Height => OpenTuiNative.GetBufferHeight(Handle);

    /// <summary>Gets or sets whether the buffer respects alpha transparency.</summary>
    public bool RespectAlpha
    {
        get => OpenTuiNative.BufferGetRespectAlpha(Handle);
        set => OpenTuiNative.BufferSetRespectAlpha(Handle, value);
    }

    /// <summary>Gets the buffer's identifier string.</summary>
    public string Id => Utf8String.GetString(
        (outPtr, maxLen) => OpenTuiNative.BufferGetId(Handle, outPtr, maxLen));

    /// <summary>Gets the real character size accounting for wide/combining characters.</summary>
    public uint RealCharSize => OpenTuiNative.BufferGetRealCharSize(Handle);

    /// <summary>Gets the current effective opacity value.</summary>
    public float CurrentOpacity => OpenTuiNative.BufferGetCurrentOpacity(Handle);

    #endregion

    #region Raw Pointers (advanced)

    /// <summary>Gets a pointer to the buffer's character data array.</summary>
    public nint GetCharPtr() => OpenTuiNative.BufferGetCharPtr(Handle);

    /// <summary>Gets a pointer to the buffer's foreground color data array.</summary>
    public nint GetFgPtr() => OpenTuiNative.BufferGetFgPtr(Handle);

    /// <summary>Gets a pointer to the buffer's background color data array.</summary>
    public nint GetBgPtr() => OpenTuiNative.BufferGetBgPtr(Handle);

    /// <summary>Gets a pointer to the buffer's cell attributes array.</summary>
    public nint GetAttributesPtr() => OpenTuiNative.BufferGetAttributesPtr(Handle);

    #endregion

    #region Drawing

    /// <summary>Clears the entire buffer, filling with the specified background color (default: transparent).</summary>
    public void Clear(Rgba? bgColor = null)
    {
        var color = bgColor ?? Rgba.Transparent;
        RgbaMarshalling.WithColorPtr(color, ptr => OpenTuiNative.BufferClear(Handle, ptr));
    }

    /// <summary>Draws a UTF-8 text string into the buffer at the given position with styling.</summary>
    public void DrawText(string text, uint x, uint y, Rgba fg, Rgba? bg = null, TextAttributes attrs = TextAttributes.None)
    {
        var textBytes = Encoding.UTF8.GetBytes(text);
        unsafe
        {
            fixed (byte* textPtr = textBytes)
            {
                var ptr = textBytes.Length > 0 ? (nint)textPtr : nint.Zero;
                RgbaMarshalling.WithColorPtrs(fg, bg ?? Rgba.Transparent, (fgPtr, bgPtr) =>
                    OpenTuiNative.BufferDrawText(Handle, ptr, (uint)textBytes.Length, x, y, fgPtr, bgPtr, (uint)attrs));
            }
        }
    }

    /// <summary>Sets a single cell in the buffer.</summary>
    public void SetCell(uint x, uint y, uint codepoint, Rgba fg, Rgba bg, TextAttributes attrs = TextAttributes.None) =>
        RgbaMarshalling.WithColorPtrs(fg, bg, (fgPtr, bgPtr) =>
            OpenTuiNative.BufferSetCell(Handle, x, y, codepoint, fgPtr, bgPtr, (uint)attrs));

    /// <summary>Sets a single cell with alpha blending applied.</summary>
    public void SetCellWithAlphaBlending(uint x, uint y, uint codepoint, Rgba fg, Rgba bg, TextAttributes attrs = TextAttributes.None) =>
        RgbaMarshalling.WithColorPtrs(fg, bg, (fgPtr, bgPtr) =>
            OpenTuiNative.BufferSetCellWithAlphaBlending(Handle, x, y, codepoint, fgPtr, bgPtr, (uint)attrs));

    /// <summary>Fills a rectangular region with the specified color.</summary>
    public void FillRect(uint x, uint y, uint w, uint h, Rgba color) =>
        RgbaMarshalling.WithColorPtr(color, ptr =>
            OpenTuiNative.BufferFillRect(Handle, x, y, w, h, ptr));

    /// <summary>Draws a single character at the specified cell position with styling.</summary>
    public void DrawChar(uint codepoint, uint x, uint y, Rgba fg, Rgba bg, TextAttributes attrs = TextAttributes.None) =>
        RgbaMarshalling.WithColorPtrs(fg, bg, (fgPtr, bgPtr) =>
            OpenTuiNative.BufferDrawChar(Handle, codepoint, x, y, fgPtr, bgPtr, (uint)attrs));

    /// <summary>Draws a box using a <see cref="BoxDrawOptions"/> record.</summary>
    public void DrawBox(int x, int y, uint w, uint h, BoxDrawOptions? options = null)
    {
        var opts = options ?? new BoxDrawOptions();
        DrawBoxCore(x, y, w, h, opts.BorderChars, opts.Sides, opts.ShouldFill,
            opts.BorderColor, opts.BackgroundColor,
            opts.Title, opts.TitleAlignment,
            opts.BottomTitle, opts.BottomTitleAlignment);
    }

    /// <summary>Draws a box with individual parameters for borders, background fill, and title text.</summary>
    public void DrawBox(
        int x, int y, uint w, uint h,
        BorderCharacters? borderChars = null,
        BorderSides sides = BorderSides.All,
        bool shouldFill = true,
        Rgba? borderColor = null,
        Rgba? backgroundColor = null,
        string? title = null,
        TitleAlignment titleAlignment = TitleAlignment.Left,
        string? bottomTitle = null,
        TitleAlignment bottomTitleAlignment = TitleAlignment.Left)
    {
        DrawBoxCore(x, y, w, h, borderChars ?? BorderCharacters.Single, sides, shouldFill,
            borderColor, backgroundColor, title, titleAlignment, bottomTitle, bottomTitleAlignment);
    }

    private void DrawBoxCore(
        int x, int y, uint w, uint h,
        BorderCharacters borderChars, BorderSides sides, bool shouldFill,
        Rgba? borderColor, Rgba? backgroundColor,
        string? title, TitleAlignment titleAlignment,
        string? bottomTitle, TitleAlignment bottomTitleAlignment)
    {
        uint[] codePoints = borderChars.ToCodePoints();

        // Pack options bitfield: bits 0-3 border sides, bit 4 fill, bits 5-6 title align, bits 7-8 bottom title align
        uint packed = (uint)sides & 0xF;
        if (shouldFill) packed |= 1u << 4;
        packed |= (uint)titleAlignment << 5;
        packed |= (uint)bottomTitleAlignment << 7;

        byte[]? titleBytes = title is not null ? Encoding.UTF8.GetBytes(title) : null;
        byte[]? bottomTitleBytes = bottomTitle is not null ? Encoding.UTF8.GetBytes(bottomTitle) : null;

        unsafe
        {
            fixed (uint* charsPtr = codePoints)
            fixed (byte* titlePtr = titleBytes)
            fixed (byte* bottomTitlePtr = bottomTitleBytes)
            {
                nint cPtr = (nint)charsPtr;
                var tPtr = titleBytes is { Length: > 0 } ? (nint)titlePtr : nint.Zero;
                var bPtr = bottomTitleBytes is { Length: > 0 } ? (nint)bottomTitlePtr : nint.Zero;

                RgbaMarshalling.WithColorPtrs(borderColor ?? Rgba.White, backgroundColor ?? Rgba.Transparent,
                    (borderPtr, bgPtr) =>
                        OpenTuiNative.BufferDrawBox(Handle, x, y, w, h,
                            cPtr, packed, borderPtr, bgPtr,
                            tPtr, (uint)(titleBytes?.Length ?? 0),
                            bPtr, (uint)(bottomTitleBytes?.Length ?? 0)));
            }
        }
    }

    /// <summary>Draws a region from a source buffer into this buffer at the specified position.</summary>
    public void DrawFrameBuffer(int x, int y, OptimizedBuffer source, uint srcX, uint srcY, uint w, uint h) =>
        OpenTuiNative.DrawFrameBuffer(Handle, x, y, source.Handle, srcX, srcY, w, h);

    /// <summary>Draws a text buffer view into this buffer.</summary>
    public void DrawTextBufferView(nint textBufferView, int x, int y) =>
        OpenTuiNative.BufferDrawTextBufferView(Handle, textBufferView, x, y);

    /// <summary>Draws an editor view into this buffer.</summary>
    public void DrawEditorView(nint editorView, int x, int y) =>
        OpenTuiNative.BufferDrawEditorView(Handle, editorView, x, y);

    /// <summary>Draws a grid with specified dimensions, colors, and border characters.</summary>
    public void DrawGrid(nint gridDef, nint widths, nint heights, Rgba[] colors, BorderCharacters borderChars, Rgba borderColor)
    {
        uint[] codePoints = borderChars.ToCodePoints();
        unsafe
        {
            fixed (uint* charsPtr = codePoints)
            {
                nint cPtr = (nint)charsPtr;
                RgbaMarshalling.WithColorArrayPtr(colors, colorsPtr =>
                    RgbaMarshalling.WithColorPtr(borderColor, borderPtr =>
                        OpenTuiNative.BufferDrawGrid(Handle, gridDef, widths, heights,
                            colorsPtr, (uint)colors.Length,
                            cPtr, (uint)codePoints.Length, borderPtr)));
            }
        }
    }

    #endregion

    #region Color Matrix

    /// <summary>Applies a color matrix transformation to the buffer within a region.</summary>
    public void ColorMatrix(float[] matrix, uint[] region, float opacity = 1f, TargetChannel channel = TargetChannel.Both)
    {
        unsafe
        {
            fixed (float* matrixPtr = matrix)
            fixed (uint* regionPtr = region)
            {
                OpenTuiNative.BufferColorMatrix(Handle,
                    (nint)matrixPtr, (nint)regionPtr, (nuint)region.Length,
                    opacity, (byte)channel);
            }
        }
    }

    /// <summary>Applies a uniform color matrix transformation to the entire buffer.</summary>
    public void ColorMatrixUniform(float[] matrix, float opacity = 1f, TargetChannel channel = TargetChannel.Both)
    {
        unsafe
        {
            fixed (float* matrixPtr = matrix)
            {
                OpenTuiNative.BufferColorMatrixUniform(Handle,
                    (nint)matrixPtr, opacity, (byte)channel);
            }
        }
    }

    #endregion

    #region Grayscale / Supersampling / Packed

    /// <summary>Draws a grayscale pixel buffer using the specified foreground/background colors.</summary>
    public void DrawGrayscaleBuffer(int x, int y, nint data, uint w, uint h, Rgba fg, Rgba bg) =>
        RgbaMarshalling.WithColorPtrs(fg, bg, (fgPtr, bgPtr) =>
            OpenTuiNative.BufferDrawGrayscaleBuffer(Handle, x, y, data, w, h, fgPtr, bgPtr));

    /// <summary>Draws a supersampled grayscale buffer using the specified foreground/background colors.</summary>
    public void DrawGrayscaleBufferSupersampled(int x, int y, nint data, uint w, uint h, Rgba fg, Rgba bg) =>
        RgbaMarshalling.WithColorPtrs(fg, bg, (fgPtr, bgPtr) =>
            OpenTuiNative.BufferDrawGrayscaleBufferSupersampled(Handle, x, y, data, w, h, fgPtr, bgPtr));

    /// <summary>Draws a super-sampled buffer at the specified position.</summary>
    public void DrawSuperSampleBuffer(uint x, uint y, nint data, nuint dataLen, byte sampleFactor, uint width) =>
        OpenTuiNative.BufferDrawSuperSampleBuffer(Handle, x, y, data, dataLen, sampleFactor, width);

    /// <summary>Draws a packed pixel buffer at the specified position and dimensions.</summary>
    public void DrawPackedBuffer(nint data, nuint dataLen, uint x, uint y, uint w, uint h) =>
        OpenTuiNative.BufferDrawPackedBuffer(Handle, data, dataLen, x, y, w, h);

    /// <summary>Writes pre-resolved character data into the buffer.</summary>
    public uint WriteResolvedChars(nint chars, nuint len, bool append) =>
        OpenTuiNative.BufferWriteResolvedChars(Handle, chars, len, append);

    #endregion

    #region Scissor / Clipping

    /// <summary>Pushes a scissor (clipping) rectangle onto the buffer's clip stack.</summary>
    public void PushScissorRect(int x, int y, uint w, uint h) =>
        OpenTuiNative.BufferPushScissorRect(Handle, x, y, w, h);

    /// <summary>Pops the most recent scissor rectangle from the buffer's clip stack.</summary>
    public void PopScissorRect() =>
        OpenTuiNative.BufferPopScissorRect(Handle);

    /// <summary>Clears all scissor rectangles from the buffer's clip stack.</summary>
    public void ClearScissorRects() =>
        OpenTuiNative.BufferClearScissorRects(Handle);

    #endregion

    #region Opacity Stack

    /// <summary>Pushes an opacity value onto the buffer's opacity stack.</summary>
    public void PushOpacity(float opacity) =>
        OpenTuiNative.BufferPushOpacity(Handle, opacity);

    /// <summary>Pops the most recent opacity value from the buffer's opacity stack.</summary>
    public void PopOpacity() =>
        OpenTuiNative.BufferPopOpacity(Handle);

    /// <summary>Clears the entire opacity stack, resetting to full opacity.</summary>
    public void ClearOpacity() =>
        OpenTuiNative.BufferClearOpacity(Handle);

    #endregion

    #region Resize

    /// <summary>Resizes the buffer to new dimensions.</summary>
    public void Resize(uint w, uint h) =>
        OpenTuiNative.BufferResize(Handle, w, h);

    #endregion

    #region IDisposable

    /// <summary>Destroys the native buffer and releases all resources.</summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            if (_ownsHandle && _handle != nint.Zero)
            {
                OpenTuiNative.BufferDestroy(_handle);
            }
            _handle = nint.Zero;
        }
    }

    #endregion
}
