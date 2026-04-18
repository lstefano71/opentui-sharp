using OpenTui.Native;

namespace OpenTui;

/// <summary>High-level wrapper around the native OpenTUI optimized buffer.</summary>
public sealed class NativeBuffer : IDisposable
{
    // Owned handle for buffers created by the user; null for borrowed buffers from the renderer.
    private readonly BufferHandle? _ownedHandle;
    private readonly nint _ptr;
    private bool _disposed;

    /// <summary>Creates a new owned optimized buffer.</summary>
    public NativeBuffer(uint width, uint height, bool respectAlpha = false, byte widthMethod = 0, string? id = null)
    {
        var utf8Id = new Utf8String(id);
        nint ptr = nint.Zero;
        utf8Id.WithPtr((idPtr, idLen) =>
            ptr = OpenTuiNative.CreateOptimizedBuffer(width, height, respectAlpha, widthMethod, idPtr, idLen));

        _ownedHandle = new BufferHandle();
        _ownedHandle.SetHandleValue(ptr);
        _ptr = ptr;
    }

    /// <summary>Creates a borrowed (non-owning) buffer wrapper. The caller must not dispose.</summary>
    internal NativeBuffer(nint ptr)
    {
        _ptr = ptr;
        _ownedHandle = null;
    }

    internal nint Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _ownedHandle?.DangerousGetHandle() ?? _ptr;
        }
    }

    /// <summary>Gets the width of the buffer in columns.</summary>
    public uint Width => OpenTuiNative.GetBufferWidth(Handle);

    /// <summary>Gets the height of the buffer in rows.</summary>
    public uint Height => OpenTuiNative.GetBufferHeight(Handle);

    /// <summary>Gets whether the buffer respects alpha transparency.</summary>
    public bool RespectAlpha
    {
        get => OpenTuiNative.BufferGetRespectAlpha(Handle);
        set => OpenTuiNative.BufferSetRespectAlpha(Handle, value);
    }

    /// <summary>Gets the real character size accounting for wide/combining characters.</summary>
    public uint RealCharSize => OpenTuiNative.BufferGetRealCharSize(Handle);

    /// <summary>Gets the current effective opacity value.</summary>
    public float CurrentOpacity => OpenTuiNative.BufferGetCurrentOpacity(Handle);

    /// <summary>Clears the entire buffer, filling with the specified background color (default: transparent).</summary>
    public void Clear(Rgba? bgColor = null)
    {
        var color = bgColor ?? Rgba.Transparent;
        RgbaMarshalling.WithColorPtr(color, ptr => OpenTuiNative.BufferClear(Handle, ptr));
    }

    /// <summary>Sets a single cell in the buffer.</summary>
    public void SetCell(uint x, uint y, uint codepoint, Rgba fg, Rgba bg, TextAttribute attrs = TextAttribute.None) =>
        RgbaMarshalling.WithColorPtrs(fg, bg, (fgPtr, bgPtr) =>
            OpenTuiNative.BufferSetCell(Handle, x, y, codepoint, fgPtr, bgPtr, (uint)attrs));

    /// <summary>Sets a single cell with alpha blending applied.</summary>
    public void SetCellWithAlphaBlending(uint x, uint y, uint codepoint, Rgba fg, Rgba bg, TextAttribute attrs = TextAttribute.None) =>
        RgbaMarshalling.WithColorPtrs(fg, bg, (fgPtr, bgPtr) =>
            OpenTuiNative.BufferSetCellWithAlphaBlending(Handle, x, y, codepoint, fgPtr, bgPtr, (uint)attrs));

    /// <summary>Draws a single character at the specified cell position with styling.</summary>
    public void DrawChar(uint x, uint y, uint codepoint, Rgba fg, Rgba bg, TextAttribute attrs = TextAttribute.None) =>
        RgbaMarshalling.WithColorPtrs(fg, bg, (fgPtr, bgPtr) =>
            OpenTuiNative.BufferDrawChar(Handle, codepoint, x, y, fgPtr, bgPtr, (uint)attrs));

    /// <summary>Fills a rectangular region with the specified color.</summary>
    public void FillRect(uint x, uint y, uint w, uint h, Rgba color) =>
        RgbaMarshalling.WithColorPtr(color, ptr =>
            OpenTuiNative.BufferFillRect(Handle, x, y, w, h, ptr));

    /// <summary>Draws a UTF-8 text string into the buffer at the given position with styling.</summary>
    public void DrawText(string text, uint x, uint y, Rgba fg, Rgba? bg = null, TextAttribute attrs = TextAttribute.None)
    {
        var utf8 = new Utf8String(text);
        utf8.WithPtr((textPtr, textLen) =>
            RgbaMarshalling.WithColorPtrs(fg, bg ?? Rgba.Transparent, (fgPtr, bgPtr) =>
                OpenTuiNative.BufferDrawText(Handle, textPtr, (uint)textLen, x, y, fgPtr, bgPtr, (uint)attrs)));
    }

    /// <summary>Draws a box with optional border, background fill, and title text.</summary>
    public void DrawBox(int x, int y, uint w, uint h, BorderCharacters? borderChars = null,
        bool borderTop = true, bool borderRight = true, bool borderBottom = true, bool borderLeft = true,
        bool shouldFill = true, Rgba? borderColor = null, Rgba? backgroundColor = null,
        string? title = null, TitleAlignment titleAlignment = TitleAlignment.Left,
        string? bottomTitle = null, TitleAlignment bottomTitleAlignment = TitleAlignment.Left)
    {
        var chars = borderChars ?? BorderCharacters.Single;
        uint[] codePoints = chars.ToCodePoints();

        // Pack options bitfield
        uint packed = 0;
        if (borderTop) packed |= 0b1000;
        if (borderRight) packed |= 0b0100;
        if (borderBottom) packed |= 0b0010;
        if (borderLeft) packed |= 0b0001;
        if (shouldFill) packed |= 1u << 4;
        packed |= (uint)titleAlignment << 5;
        packed |= (uint)bottomTitleAlignment << 7;

        Span<float> borderRgba = [
            (borderColor ?? Rgba.White).R, (borderColor ?? Rgba.White).G,
            (borderColor ?? Rgba.White).B, (borderColor ?? Rgba.White).A
        ];
        Span<float> bgRgba = [
            (backgroundColor ?? Rgba.Transparent).R, (backgroundColor ?? Rgba.Transparent).G,
            (backgroundColor ?? Rgba.Transparent).B, (backgroundColor ?? Rgba.Transparent).A
        ];

        byte[]? titleBytes = title != null ? System.Text.Encoding.UTF8.GetBytes(title) : null;
        byte[]? bottomTitleBytes = bottomTitle != null ? System.Text.Encoding.UTF8.GetBytes(bottomTitle) : null;

        unsafe
        {
            fixed (uint* charsPtr = codePoints)
            fixed (float* borderColorPtr = borderRgba)
            fixed (float* bgColorPtr = bgRgba)
            fixed (byte* titlePtr = titleBytes)
            fixed (byte* bottomTitlePtr = bottomTitleBytes)
            {
                OpenTuiNative.BufferDrawBox(Handle, x, y, w, h,
                    (nint)charsPtr, packed,
                    (nint)borderColorPtr, (nint)bgColorPtr,
                    titleBytes != null ? (nint)titlePtr : 0, (uint)(titleBytes?.Length ?? 0),
                    bottomTitleBytes != null ? (nint)bottomTitlePtr : 0, (uint)(bottomTitleBytes?.Length ?? 0));
            }
        }
    }

    /// <summary>Draws a region from a source buffer into this buffer at the specified position.</summary>
    public void DrawFrameBuffer(int x, int y, NativeBuffer source, uint srcX, uint srcY, uint w, uint h) =>
        OpenTuiNative.DrawFrameBuffer(Handle, x, y, source.Handle, srcX, srcY, w, h);

    /// <summary>Draws a text buffer view into this buffer.</summary>
    public void DrawTextBufferView(nint textBufferView, int x, int y) =>
        OpenTuiNative.BufferDrawTextBufferView(Handle, textBufferView, x, y);

    /// <summary>Draws an editor view into this buffer.</summary>
    public void DrawEditorView(nint editorView, int x, int y) =>
        OpenTuiNative.BufferDrawEditorView(Handle, editorView, x, y);

    /// <summary>Pushes a scissor (clipping) rectangle onto the buffer's clip stack.</summary>
    public void PushScissor(int x, int y, uint w, uint h) =>
        OpenTuiNative.BufferPushScissorRect(Handle, x, y, w, h);

    /// <summary>Pops the most recent scissor rectangle from the buffer's clip stack.</summary>
    public void PopScissor() =>
        OpenTuiNative.BufferPopScissorRect(Handle);

    /// <summary>Clears all scissor rectangles from the buffer's clip stack.</summary>
    public void ClearScissors() =>
        OpenTuiNative.BufferClearScissorRects(Handle);

    /// <summary>Pushes an opacity value onto the buffer's opacity stack.</summary>
    public void PushOpacity(float opacity) =>
        OpenTuiNative.BufferPushOpacity(Handle, opacity);

    /// <summary>Pops the most recent opacity value from the buffer's opacity stack.</summary>
    public void PopOpacity() =>
        OpenTuiNative.BufferPopOpacity(Handle);

    /// <summary>Clears the entire opacity stack, resetting to full opacity.</summary>
    public void ClearOpacity() =>
        OpenTuiNative.BufferClearOpacity(Handle);

    /// <summary>Resizes the buffer to new dimensions.</summary>
    public void Resize(uint w, uint h) =>
        OpenTuiNative.BufferResize(Handle, w, h);

    /// <summary>Gets a pointer to the buffer's character data array.</summary>
    public nint GetCharPtr() => OpenTuiNative.BufferGetCharPtr(Handle);

    /// <summary>Gets a pointer to the buffer's foreground color data array.</summary>
    public nint GetFgPtr() => OpenTuiNative.BufferGetFgPtr(Handle);

    /// <summary>Gets a pointer to the buffer's background color data array.</summary>
    public nint GetBgPtr() => OpenTuiNative.BufferGetBgPtr(Handle);

    /// <summary>Gets a pointer to the buffer's cell attributes array.</summary>
    public nint GetAttributesPtr() => OpenTuiNative.BufferGetAttributesPtr(Handle);

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed && _ownedHandle is not null)
        {
            _disposed = true;
            _ownedHandle.Dispose();
        }
    }
}
