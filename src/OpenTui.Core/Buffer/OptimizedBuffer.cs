using System.Text;
using OpenTui.Core.Managed;
using OpenTui.Core.Managed.Unicode;

namespace OpenTui.Core;

/// <summary>
/// Options for drawing a box with borders, background fill, and optional title text.
/// </summary>
public sealed record BoxDrawOptions
{
    /// <summary>Gets or sets the border chars.</summary>
    public BorderCharacters BorderChars { get; init; } = BorderCharacters.Single;
    /// <summary>Gets or sets the sides.</summary>
    public BorderSides Sides { get; init; } = BorderSides.All;
    /// <summary>Gets or sets a value indicating whether should fill.</summary>
    public bool ShouldFill { get; init; } = true;
    /// <summary>Gets or sets the border color.</summary>
    public Rgba? BorderColor { get; init; }
    /// <summary>Gets or sets the background color.</summary>
    public Rgba? BackgroundColor { get; init; }
    /// <summary>Gets or sets the title.</summary>
    public string? Title { get; init; }
    /// <summary>Gets or sets the title alignment.</summary>
    public TitleAlignment TitleAlignment { get; init; } = TitleAlignment.Left;
    /// <summary>Gets or sets the bottom title.</summary>
    public string? BottomTitle { get; init; }
    /// <summary>Gets or sets the bottom title alignment.</summary>
    public TitleAlignment BottomTitleAlignment { get; init; } = TitleAlignment.Left;
}

/// <summary>
/// Managed buffer for terminal cell rendering. Delegates to <see cref="ManagedBuffer"/>.
/// </summary>
public sealed class OptimizedBuffer : IDisposable
{
    private bool _disposed;
    internal ManagedBuffer Managed;
    private bool _ownsManaged = true;

    private OptimizedBuffer(ManagedBuffer managed)
    {
        Managed = managed;
    }

    /// <summary>
    /// Wraps an existing <see cref="ManagedBuffer"/> without taking ownership.
    /// Dispose is a no-op for the underlying buffer.
    /// </summary>
    internal static OptimizedBuffer WrapExisting(ManagedBuffer buf) =>
        new(buf) { _ownsManaged = false };

    /// <summary>Creates a new optimized buffer with the specified dimensions.</summary>
    public static OptimizedBuffer Create(
        uint width,
        uint height,
        WidthMethod widthMethod = WidthMethod.Unicode,
        bool respectAlpha = false,
        string? id = null)
    {
        var buf = ManagedBuffer.Create(width, height, widthMethod, respectAlpha, id);
        return new OptimizedBuffer(buf);
    }

    #region Properties

    /// <summary>Gets the width of the buffer in columns.</summary>
    public uint Width
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return Managed.Width;
        }
    }

    /// <summary>Gets the height of the buffer in rows.</summary>
    public uint Height
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return Managed.Height;
        }
    }

    /// <summary>Gets or sets whether the buffer respects alpha transparency.</summary>
    public bool RespectAlpha
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return Managed.RespectAlpha;
        }
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            Managed.RespectAlpha = value;
        }
    }

    /// <summary>Gets the buffer's identifier string.</summary>
    public string Id
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return Managed.Id;
        }
    }

    /// <summary>Gets the real character size accounting for wide/combining characters.</summary>
    public uint RealCharSize
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return Managed.RealCharSize;
        }
    }

    /// <summary>Gets the current effective opacity value.</summary>
    public float CurrentOpacity
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return Managed.CurrentOpacity;
        }
    }

    #endregion

    #region Safe Cell Accessors

    /// <summary>Gets the char codepoint at (x, y).</summary>
    public uint GetCharAt(uint x, uint y) => Managed.GetCharAt(x, y);

    /// <summary>Gets the foreground color at (x, y).</summary>
    public Rgba GetFgAt(uint x, uint y) => Managed.GetFgAt(x, y);

    /// <summary>Gets the background color at (x, y).</summary>
    public Rgba GetBgAt(uint x, uint y) => Managed.GetBgAt(x, y);

    /// <summary>Gets the attributes at (x, y).</summary>
    public uint GetAttributesAt(uint x, uint y) => Managed.GetAttributesAt(x, y);

    /// <summary>Gets all char codepoints as a read-only span.</summary>
    public ReadOnlySpan<uint> GetChars() => Managed.GetChars();

    /// <summary>Gets all foreground colors as a read-only span.</summary>
    public ReadOnlySpan<Rgba> GetFgColors() => Managed.GetFgColors();

    /// <summary>Gets all background colors as a read-only span.</summary>
    public ReadOnlySpan<Rgba> GetBgColors() => Managed.GetBgColors();

    /// <summary>Gets all attributes as a read-only span.</summary>
    public ReadOnlySpan<uint> GetAttributes() => Managed.GetAttributes();

    /// <summary>Returns the buffer content as resolved text, with grapheme clusters expanded.</summary>
    public string GetResolvedText(bool addLineBreaks = false) => Managed.GetResolvedText(addLineBreaks);

    #endregion

    #region Drawing

    /// <summary>Clears the entire buffer, filling with the specified background color (default: transparent).</summary>
    public void Clear(Rgba? bgColor = null)
    {
        Managed.Clear(bgColor ?? Rgba.Transparent);
    }

    /// <summary>Draws a UTF-8 text string into the buffer at the given position with styling.</summary>
    public void DrawText(string text, uint x, uint y, Rgba fg, Rgba? bg = null, TextAttributes attrs = TextAttributes.None)
    {
        Managed.DrawText(text, x, y, fg, bg, attrs);
    }

    /// <summary>Sets a single cell in the buffer.</summary>
    public void SetCell(uint x, uint y, uint codepoint, Rgba fg, Rgba bg, TextAttributes attrs = TextAttributes.None) =>
        Managed.Set(x, y, new ManagedBuffer.Cell(codepoint, fg, bg, (uint)attrs));

    /// <summary>Sets a single cell with alpha blending applied.</summary>
    public void SetCellWithAlphaBlending(uint x, uint y, uint codepoint, Rgba fg, Rgba bg, TextAttributes attrs = TextAttributes.None) =>
        Managed.SetCellWithAlphaBlending(x, y, codepoint, fg, bg, (uint)attrs);

    /// <summary>Fills a rectangular region with the specified color.</summary>
    public void FillRect(uint x, uint y, uint w, uint h, Rgba color) =>
        Managed.FillRect(x, y, w, h, color);

    /// <summary>Draws a single character at the specified cell position with styling.</summary>
    public void DrawChar(uint codepoint, uint x, uint y, Rgba fg, Rgba bg, TextAttributes attrs = TextAttributes.None) =>
        Managed.DrawChar(codepoint, x, y, fg, bg, (uint)attrs);

    /// <summary>Draws a box using a <see cref="BoxDrawOptions"/> record.</summary>
    public void DrawBox(int x, int y, uint w, uint h, BoxDrawOptions? options = null)
    {
        var opts = options ?? new BoxDrawOptions();
        Managed.DrawBox(x, y, w, h,
            opts.BorderChars, opts.Sides, opts.ShouldFill,
            opts.BorderColor ?? Rgba.White, opts.BackgroundColor,
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
        Managed.DrawBox(x, y, w, h,
            borderChars ?? BorderCharacters.Single, sides, shouldFill,
            borderColor ?? Rgba.White, backgroundColor,
            title, titleAlignment, bottomTitle, bottomTitleAlignment);
    }

    /// <summary>Draws a region from a source buffer into this buffer at the specified position.</summary>
    public void DrawFrameBuffer(int x, int y, OptimizedBuffer source, uint srcX, uint srcY, uint w, uint h) =>
        Managed.DrawFrameBuffer(x, y, source.Managed, srcX, srcY, w, h);

    /// <summary>Draws a text buffer view into this buffer.</summary>
    public void DrawTextBufferView(TextBufferView view, int x, int y)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Managed.DrawTextBufferView(view._managed, x, y);
    }

    /// <summary>Draws an editor view into this buffer.</summary>
    public void DrawEditorView(EditorView editorView, int x, int y)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Managed.DrawEditorView(editorView._managed, x, y);
    }

    /// <summary>Draws a border grid using precomputed column and row boundary offsets.</summary>
    public void DrawGrid(
        int[] columnOffsets,
        int[] rowOffsets,
        BorderCharacters borderChars,
        Rgba borderFg,
        Rgba borderBg,
        bool drawInner,
        bool drawOuter)
    {
        Managed.DrawGrid(columnOffsets, rowOffsets, borderChars, borderFg, borderBg, drawInner, drawOuter);
    }

    #endregion

    #region Color Matrix

    /// <summary>
    /// Applies a color matrix transformation to the buffer within a packed per-cell mask.
    /// The mask format is [x, y, strength, x, y, strength, ...].
    /// </summary>
    public void ColorMatrix(float[] matrix, float[] cellMask, float opacity = 1f, TargetChannel channel = TargetChannel.Both)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(cellMask);
        if (matrix.Length < 16)
            throw new ArgumentException("Color matrix must contain at least 16 elements.", nameof(matrix));
        Managed.ColorMatrix(matrix, cellMask, opacity, channel);
    }

    /// <summary>
    /// Applies a color matrix transformation to whole cells identified by x/y pairs.
    /// </summary>
    public void ColorMatrix(float[] matrix, uint[] region, float opacity = 1f, TargetChannel channel = TargetChannel.Both)
    {
        if (matrix.Length == 0 || region.Length == 0) return;
        var cellMask = new float[(region.Length / 2) * 3];
        int destIndex = 0;
        for (int i = 0; i + 1 < region.Length; i += 2)
        {
            cellMask[destIndex++] = region[i];
            cellMask[destIndex++] = region[i + 1];
            cellMask[destIndex++] = 1f;
        }
        Managed.ColorMatrix(matrix, cellMask, opacity, channel);
    }

    /// <summary>Applies a uniform color matrix transformation to the entire buffer.</summary>
    public void ColorMatrixUniform(float[] matrix, float opacity = 1f, TargetChannel channel = TargetChannel.Both)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        if (matrix.Length < 16)
            throw new ArgumentException("Color matrix must contain at least 16 elements.", nameof(matrix));
        Managed.ColorMatrixUniform(matrix, opacity, channel);
    }

    #endregion

    #region Scissor / Clipping

    /// <summary>Pushes a scissor (clipping) rectangle onto the buffer's clip stack.</summary>
    public void PushScissorRect(int x, int y, uint w, uint h) =>
        Managed.PushScissorRect(x, y, w, h);

    /// <summary>Pops the most recent scissor rectangle from the buffer's clip stack.</summary>
    public void PopScissorRect() =>
        Managed.PopScissorRect();

    /// <summary>Clears all scissor rectangles from the buffer's clip stack.</summary>
    public void ClearScissorRects() =>
        Managed.ClearScissorRects();

    #endregion

    #region Opacity Stack

    /// <summary>Pushes an opacity value onto the buffer's opacity stack.</summary>
    public void PushOpacity(float opacity) =>
        Managed.PushOpacity(opacity);

    /// <summary>Pops the most recent opacity value from the buffer's opacity stack.</summary>
    public void PopOpacity() =>
        Managed.PopOpacity();

    /// <summary>Clears the entire opacity stack, resetting to full opacity.</summary>
    public void ClearOpacity() =>
        Managed.ClearOpacity();

    #endregion

    #region Resize

    /// <summary>Resizes the buffer to new dimensions.</summary>
    public void Resize(uint w, uint h) =>
        Managed.Resize(w, h);

    #endregion

    #region IDisposable

    /// <summary>Releases all resources held by the buffer.</summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            if (_ownsManaged)
            {
                Managed.Dispose();
            }
        }
    }

    #endregion
}
