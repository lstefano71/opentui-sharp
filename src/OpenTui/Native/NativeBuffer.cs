using System.Runtime.CompilerServices;
using CoreBuffer = OpenTui.Core.OptimizedBuffer;
using CoreBorderChars = OpenTui.Core.BorderCharacters;
using CoreBorderSides = OpenTui.Core.BorderSides;
using CoreTitleAlignment = OpenTui.Core.TitleAlignment;

namespace OpenTui;

/// <summary>High-level wrapper around the native OpenTUI optimized buffer.</summary>
public sealed class NativeBuffer : IDisposable
{
    internal readonly CoreBuffer _core;
    private readonly bool _ownsCore;
    private bool _disposed;

    /// <summary>Creates a new owned optimized buffer.</summary>
    public NativeBuffer(uint width, uint height, bool respectAlpha = false, byte widthMethod = 0, string? id = null)
    {
        _core = CoreBuffer.Create(width, height, id: id);
        _core.RespectAlpha = respectAlpha;
        _ownsCore = true;
    }

    /// <summary>Creates a borrowed (non-owning) buffer wrapper. The caller must not dispose.</summary>
    internal NativeBuffer(CoreBuffer core)
    {
        _core = core;
        _ownsCore = false;
    }

    /// <summary>Gets the width of the buffer in columns.</summary>
    public uint Width => _core.Width;

    /// <summary>Gets the height of the buffer in rows.</summary>
    public uint Height => _core.Height;

    /// <summary>Gets whether the buffer respects alpha transparency.</summary>
    public bool RespectAlpha
    {
        get => _core.RespectAlpha;
        set => _core.RespectAlpha = value;
    }

    /// <summary>Gets the real character size accounting for wide/combining characters.</summary>
    public uint RealCharSize => _core.RealCharSize;

    /// <summary>Gets the current effective opacity value.</summary>
    public float CurrentOpacity => _core.CurrentOpacity;

    /// <summary>Clears the entire buffer, filling with the specified background color (default: transparent).</summary>
    public void Clear(Rgba? bgColor = null) =>
        _core.Clear(ToCore(bgColor ?? Rgba.Transparent));

    /// <summary>Sets a single cell in the buffer.</summary>
    public void SetCell(uint x, uint y, uint codepoint, Rgba fg, Rgba bg, TextAttribute attrs = TextAttribute.None) =>
        _core.SetCell(x, y, codepoint, ToCore(fg), ToCore(bg), (OpenTui.Core.TextAttributes)(uint)attrs);

    /// <summary>Sets a single cell with alpha blending applied.</summary>
    public void SetCellWithAlphaBlending(uint x, uint y, uint codepoint, Rgba fg, Rgba bg, TextAttribute attrs = TextAttribute.None) =>
        _core.SetCellWithAlphaBlending(x, y, codepoint, ToCore(fg), ToCore(bg), (OpenTui.Core.TextAttributes)(uint)attrs);

    /// <summary>Draws a single character at the specified cell position with styling.</summary>
    public void DrawChar(uint x, uint y, uint codepoint, Rgba fg, Rgba bg, TextAttribute attrs = TextAttribute.None) =>
        _core.DrawChar(codepoint, x, y, ToCore(fg), ToCore(bg), (OpenTui.Core.TextAttributes)(uint)attrs);

    /// <summary>Fills a rectangular region with the specified color.</summary>
    public void FillRect(uint x, uint y, uint w, uint h, Rgba color) =>
        _core.FillRect(x, y, w, h, ToCore(color));

    /// <summary>Draws a UTF-8 text string into the buffer at the given position with styling.</summary>
    public void DrawText(string text, uint x, uint y, Rgba fg, Rgba? bg = null, TextAttribute attrs = TextAttribute.None) =>
        _core.DrawText(text, x, y, ToCore(fg), bg.HasValue ? ToCore(bg.Value) : null, (OpenTui.Core.TextAttributes)(uint)attrs);

    /// <summary>Draws a box with optional border, background fill, and title text.</summary>
    public void DrawBox(int x, int y, uint w, uint h, BorderCharacters? borderChars = null,
        bool borderTop = true, bool borderRight = true, bool borderBottom = true, bool borderLeft = true,
        bool shouldFill = true, Rgba? borderColor = null, Rgba? backgroundColor = null,
        string? title = null, TitleAlignment titleAlignment = TitleAlignment.Left,
        string? bottomTitle = null, TitleAlignment bottomTitleAlignment = TitleAlignment.Left)
    {
        var chars = borderChars ?? BorderCharacters.Single;
        var coreBorderChars = new CoreBorderChars
        {
            TopLeft = chars.TopLeft, TopRight = chars.TopRight,
            BottomLeft = chars.BottomLeft, BottomRight = chars.BottomRight,
            Horizontal = chars.Horizontal, Vertical = chars.Vertical,
            TopT = chars.TopT, BottomT = chars.BottomT,
            LeftT = chars.LeftT, RightT = chars.RightT,
            Cross = chars.Cross,
        };

        var sides = CoreBorderSides.None;
        if (borderTop) sides |= CoreBorderSides.Top;
        if (borderRight) sides |= CoreBorderSides.Right;
        if (borderBottom) sides |= CoreBorderSides.Bottom;
        if (borderLeft) sides |= CoreBorderSides.Left;

        _core.DrawBox(x, y, w, h, coreBorderChars, sides, shouldFill,
            borderColor.HasValue ? ToCore(borderColor.Value) : null,
            backgroundColor.HasValue ? ToCore(backgroundColor.Value) : null,
            title, (CoreTitleAlignment)titleAlignment,
            bottomTitle, (CoreTitleAlignment)bottomTitleAlignment);
    }

    /// <summary>Draws a region from a source buffer into this buffer at the specified position.</summary>
    public void DrawFrameBuffer(int x, int y, NativeBuffer source, uint srcX, uint srcY, uint w, uint h) =>
        _core.DrawFrameBuffer(x, y, source._core, srcX, srcY, w, h);

    /// <summary>Draws a text buffer view into this buffer.</summary>
    [Obsolete("Use the Core layer TextBufferView directly.")]
    public void DrawTextBufferView(nint textBufferView, int x, int y) =>
        throw new NotSupportedException("Direct nint-based DrawTextBufferView is not supported in managed mode.");

    /// <summary>Draws an editor view into this buffer.</summary>
    [Obsolete("Use the Core layer EditorView directly.")]
    public void DrawEditorView(nint editorView, int x, int y) =>
        throw new NotSupportedException("Direct nint-based DrawEditorView is not supported in managed mode.");

    /// <summary>Pushes a scissor (clipping) rectangle onto the buffer's clip stack.</summary>
    public void PushScissor(int x, int y, uint w, uint h) =>
        _core.PushScissorRect(x, y, w, h);

    /// <summary>Pops the most recent scissor rectangle from the buffer's clip stack.</summary>
    public void PopScissor() =>
        _core.PopScissorRect();

    /// <summary>Clears all scissor rectangles from the buffer's clip stack.</summary>
    public void ClearScissors() =>
        _core.ClearScissorRects();

    /// <summary>Pushes an opacity value onto the buffer's opacity stack.</summary>
    public void PushOpacity(float opacity) =>
        _core.PushOpacity(opacity);

    /// <summary>Pops the most recent opacity value from the buffer's opacity stack.</summary>
    public void PopOpacity() =>
        _core.PopOpacity();

    /// <summary>Clears the entire opacity stack, resetting to full opacity.</summary>
    public void ClearOpacity() =>
        _core.ClearOpacity();

    /// <summary>Resizes the buffer to new dimensions.</summary>
    public void Resize(uint w, uint h) =>
        _core.Resize(w, h);

    /// <summary>Gets the character codepoint at the specified cell position.</summary>
    public uint GetCharAt(uint x, uint y) => _core.GetCharAt(x, y);

    /// <summary>Gets the foreground color at the specified cell position.</summary>
    public Rgba GetFgAt(uint x, uint y) => FromCore(_core.GetFgAt(x, y));

    /// <summary>Gets the background color at the specified cell position.</summary>
    public Rgba GetBgAt(uint x, uint y) => FromCore(_core.GetBgAt(x, y));

    /// <summary>Gets the cell attributes at the specified cell position.</summary>
    public uint GetAttributesAt(uint x, uint y) => _core.GetAttributesAt(x, y);

    /// <summary>Gets a pointer to the buffer's character data array.</summary>
    [Obsolete("Use GetCharAt() instead. Raw pointer access is not supported in managed mode.")]
    public nint GetCharPtr() =>
        throw new NotSupportedException("Raw pointer access is not supported in managed mode. Use GetCharAt() instead.");

    /// <summary>Gets a pointer to the buffer's foreground color data array.</summary>
    [Obsolete("Use GetFgAt() instead. Raw pointer access is not supported in managed mode.")]
    public nint GetFgPtr() =>
        throw new NotSupportedException("Raw pointer access is not supported in managed mode. Use GetFgAt() instead.");

    /// <summary>Gets a pointer to the buffer's background color data array.</summary>
    [Obsolete("Use GetBgAt() instead. Raw pointer access is not supported in managed mode.")]
    public nint GetBgPtr() =>
        throw new NotSupportedException("Raw pointer access is not supported in managed mode. Use GetBgAt() instead.");

    /// <summary>Gets a pointer to the buffer's cell attributes array.</summary>
    [Obsolete("Use GetAttributesAt() instead. Raw pointer access is not supported in managed mode.")]
    public nint GetAttributesPtr() =>
        throw new NotSupportedException("Raw pointer access is not supported in managed mode. Use GetAttributesAt() instead.");

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed && _ownsCore)
        {
            _disposed = true;
            _core.Dispose();
        }
    }

    // Type conversion helpers between OpenTui.Rgba and OpenTui.Core.Rgba.
    // Both are LayoutKind.Sequential readonly record structs with identical fields (R, G, B, A).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static OpenTui.Core.Rgba ToCore(Rgba c) => new(c.R, c.G, c.B, c.A);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Rgba FromCore(OpenTui.Core.Rgba c) => new(c.R, c.G, c.B, c.A);
}
