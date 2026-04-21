using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using OpenTui.Core.Managed.Unicode;

namespace OpenTui.Core.Managed;

/// <summary>
/// Pure C# implementation of the Zig <c>OptimizedBuffer</c>.
/// Uses Structure-of-Arrays layout for cache-friendly rendering.
/// </summary>
public sealed class ManagedBuffer : IDisposable
{
    #region Constants

    /// <summary>Default space codepoint.</summary>
    public const uint DefaultSpaceChar = 32;

    private const uint MaxUnicodeCodepoint = 0x10FFFF;
    private const uint BlockChar = 0x2588;

    // Grapheme encoding helpers (mirrors ManagedGraphemePool constants)
    private const uint CharFlagGrapheme = ManagedGraphemePool.CharFlagGrapheme;
    private const uint CharFlagContinuation = ManagedGraphemePool.CharFlagContinuation;
    private const uint GraphemeIdMask = ManagedGraphemePool.GraphemeIdMask;
    private const int CharExtRightShift = ManagedGraphemePool.CharExtRightShift;
    private const int CharExtLeftShift = ManagedGraphemePool.CharExtLeftShift;
    private const uint CharExtMask = ManagedGraphemePool.CharExtMask;

    #endregion

    #region SoA Cell Grid

    private uint[] _chars;
    private Rgba[] _fg;
    private Rgba[] _bg;
    private uint[] _attributes;

    #endregion

    #region State

    private readonly List<ClipRect> _scissorStack = [];
    private readonly List<float> _opacityStack = [];
    private readonly GraphemeTracker _graphemeTracker = new();
    private readonly LinkTracker _linkTracker = new();
    private readonly WidthMethod _widthMethod;
    private bool _disposed;

    #endregion

    #region Construction

    private ManagedBuffer(uint width, uint height, WidthMethod widthMethod, bool respectAlpha, string id)
    {
        if (width == 0 || height == 0)
            throw new ArgumentException("Buffer dimensions must be > 0.");

        Width = width;
        Height = height;
        RespectAlpha = respectAlpha;
        Id = id;
        _widthMethod = widthMethod;

        int size = (int)(width * height);
        _chars = new uint[size];
        _fg = new Rgba[size];
        _bg = new Rgba[size];
        _attributes = new uint[size];

        // Default: space char, white fg, transparent bg
        _chars.AsSpan().Fill(DefaultSpaceChar);
        _fg.AsSpan().Fill(Rgba.White);
    }

    /// <summary>Creates a new managed buffer with the specified dimensions.</summary>
    public static ManagedBuffer Create(
        uint width,
        uint height,
        WidthMethod widthMethod = WidthMethod.Unicode,
        bool respectAlpha = false,
        string? id = null)
    {
        return new ManagedBuffer(width, height, widthMethod, respectAlpha, id ?? "unnamed buffer");
    }

    /// <summary>Resizes the buffer to new dimensions, clearing all content.</summary>
    public void Resize(uint width, uint height)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (width == 0 || height == 0)
            throw new ArgumentException("Buffer dimensions must be > 0.");
        if (Width == width && Height == height) return;

        int size = (int)(width * height);
        _chars = new uint[size];
        _fg = new Rgba[size];
        _bg = new Rgba[size];
        _attributes = new uint[size];

        Width = width;
        Height = height;

        Clear(new Rgba(0f, 0f, 0f, 1f));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _chars = [];
            _fg = [];
            _bg = [];
            _attributes = [];
            _scissorStack.Clear();
            _opacityStack.Clear();
            _graphemeTracker.Clear();
            _linkTracker.Clear();
        }
    }

    #endregion

    #region Properties

    /// <summary>Gets the width of the buffer in columns.</summary>
    public uint Width { get; private set; }

    /// <summary>Gets the height of the buffer in rows.</summary>
    public uint Height { get; private set; }

    /// <summary>Gets or sets whether the buffer respects alpha transparency.</summary>
    public bool RespectAlpha { get; set; }

    /// <summary>Gets or sets the backdrop color used when blending over transparent destinations.</summary>
    public Rgba? BlendBackdropColor { get; set; }

    /// <summary>Gets the buffer's identifier string.</summary>
    public string Id { get; }

    /// <summary>Gets the current effective opacity value (product of the opacity stack).</summary>
    public float CurrentOpacity
    {
        get
        {
            if (_opacityStack.Count == 0) return 1f;
            return _opacityStack[^1];
        }
    }

    /// <summary>Gets the real character size accounting for graphemes.</summary>
    public uint RealCharSize
    {
        get
        {
            uint totalCells = Width * Height;
            // Simplified: without grapheme pool integration, just return totalCells * 4
            return totalCells * sizeof(uint);
        }
    }

    #endregion

    #region Cell Type

    /// <summary>Represents a single cell in the buffer.</summary>
    public readonly record struct Cell(uint Char, Rgba Fg, Rgba Bg, uint Attributes);

    /// <summary>Clip rectangle for scissor operations.</summary>
    public readonly record struct ClipRect(int X, int Y, uint Width, uint Height);

    #endregion

    #region Cell Access

    /// <summary>Sets a cell with grapheme span cleanup.</summary>
    public void Set(uint x, uint y, Cell cell)
    {
        SetInternal(x, y, cell, spanCleanup: true);
    }

    /// <summary>Sets a cell without span cleanup (no grapheme tracking).</summary>
    public void SetRaw(uint x, uint y, Cell cell)
    {
        uint? index = ValidateAndIndex(x, y);
        if (index is null) return;
        WriteCellAndLinks(index.Value, cell);
    }

    /// <summary>
    /// Like Set but without span cleanup. Used by the renderer's diff loop
    /// where cells are synced from an authoritative source buffer.
    /// </summary>
    public void SyncCell(uint x, uint y, Cell cell)
    {
        SetInternal(x, y, cell, spanCleanup: false);
    }

    /// <summary>Gets the cell at the specified coordinates, or null if out of bounds.</summary>
    public Cell? Get(uint x, uint y)
    {
        if (x >= Width || y >= Height) return null;
        uint index = y * Width + x;
        return new Cell(_chars[index], _fg[index], _bg[index], _attributes[index]);
    }

    /// <summary>Gets the cell data at the specified position.</summary>
    public Cell GetCell(uint x, uint y)
    {
        if (x >= Width || y >= Height)
            return default;
        int idx = (int)(y * Width + x);
        return new Cell(_chars[idx], _fg[idx], _bg[idx], _attributes[idx]);
    }

    /// <summary>Gets the character codepoint at the specified position.</summary>
    public uint GetCharAt(uint x, uint y)
    {
        if (x >= Width || y >= Height) return 0;
        return _chars[(int)(y * Width + x)];
    }

    /// <summary>Gets the foreground color at the specified position.</summary>
    public Rgba GetFgAt(uint x, uint y)
    {
        if (x >= Width || y >= Height) return default;
        return _fg[(int)(y * Width + x)];
    }

    /// <summary>Gets the background color at the specified position.</summary>
    public Rgba GetBgAt(uint x, uint y)
    {
        if (x >= Width || y >= Height) return default;
        return _bg[(int)(y * Width + x)];
    }

    /// <summary>Gets the attributes at the specified position.</summary>
    public uint GetAttributesAt(uint x, uint y)
    {
        if (x >= Width || y >= Height) return 0;
        return _attributes[(int)(y * Width + x)];
    }

    private void SetInternal(uint x, uint y, Cell cell, bool spanCleanup)
    {
        uint? maybeIndex = ValidateAndIndex(x, y);
        if (maybeIndex is null) return;
        uint index = maybeIndex.Value;

        uint prevChar = _chars[index];
        uint prevLinkId = GetLinkId(_attributes[index]);
        bool trackerReplaced = false;

        if (!spanCleanup)
        {
            uint? oldStartId = IsGraphemeChar(prevChar) ? GraphemeIdFromChar(prevChar) : null;
            uint? newStartId = null;
            if (IsGraphemeChar(cell.Char))
            {
                uint newWidth = CharRightExtent(cell.Char) + 1;
                if (x + newWidth <= Width)
                    newStartId = GraphemeIdFromChar(cell.Char);
            }

            if (oldStartId.HasValue || newStartId.HasValue)
            {
                _graphemeTracker.Replace(oldStartId, newStartId);
                trackerReplaced = true;
            }
        }

        // If overwriting a grapheme span with a different char, clear that span first
        if (spanCleanup)
        {
            if ((IsGraphemeChar(prevChar) || IsContinuationChar(prevChar)) && prevChar != cell.Char)
            {
                uint rowStart = y * Width;
                uint rowEnd = rowStart + Width - 1;
                uint left = CharLeftExtent(prevChar);
                uint right = CharRightExtent(prevChar);
                uint id = GraphemeIdFromChar(prevChar);

                uint? newGraphemeId = null;
                if (IsGraphemeChar(cell.Char))
                {
                    uint newWidth = CharRightExtent(cell.Char) + 1;
                    if (x + newWidth <= Width)
                        newGraphemeId = GraphemeIdFromChar(cell.Char);
                }
                _graphemeTracker.Replace(id, newGraphemeId);
                trackerReplaced = true;

                uint spanStart = index - Math.Min(left, index - rowStart);
                uint spanEnd = index + Math.Min(right, rowEnd - index);

                for (uint si = spanStart; si <= spanEnd; si++)
                {
                    uint spanChar = _chars[si];
                    if (!(IsGraphemeChar(spanChar) || IsContinuationChar(spanChar))) continue;
                    if (GraphemeIdFromChar(spanChar) != id) continue;

                    uint spanLinkId = GetLinkId(_attributes[si]);
                    if (spanLinkId != 0)
                        _linkTracker.RemoveCellRef(spanLinkId);

                    _chars[si] = DefaultSpaceChar;
                    _attributes[si] = 0;
                }
            }
        }

        if (IsGraphemeChar(cell.Char))
        {
            uint rightExt = CharRightExtent(cell.Char);
            uint width = 1 + rightExt;

            if (x + width > Width)
            {
                // Grapheme doesn't fit; fill rest of line with spaces
                uint endOfLine = (y + 1) * Width;
                for (uint i = index; i < endOfLine; i++)
                {
                    uint eolLinkId = GetLinkId(_attributes[i]);
                    if (eolLinkId != 0)
                        _linkTracker.RemoveCellRef(eolLinkId);
                }

                _chars.AsSpan((int)index, (int)(endOfLine - index)).Fill(DefaultSpaceChar);
                _attributes.AsSpan((int)index, (int)(endOfLine - index)).Fill(cell.Attributes);
                _fg.AsSpan((int)index, (int)(endOfLine - index)).Fill(cell.Fg);
                _bg.AsSpan((int)index, (int)(endOfLine - index)).Fill(cell.Bg);

                uint newLinkId = GetLinkId(cell.Attributes);
                if (newLinkId != 0)
                {
                    uint cellsWritten = endOfLine - index;
                    for (uint i = 0; i < cellsWritten; i++)
                        _linkTracker.AddCellRef(newLinkId);
                }
                return;
            }

            _chars[index] = cell.Char;
            _fg[index] = cell.Fg;
            _bg[index] = cell.Bg;
            _attributes[index] = cell.Attributes;

            uint gid = GraphemeIdFromChar(cell.Char);
            bool isSameGraphemeStart = IsGraphemeChar(prevChar) && prevChar == cell.Char;
            if (!trackerReplaced && !isSameGraphemeStart)
                _graphemeTracker.Add(gid);

            uint newLink = GetLinkId(cell.Attributes);
            if (prevLinkId != 0 && prevLinkId != newLink)
                _linkTracker.RemoveCellRef(prevLinkId);
            if (newLink != 0 && newLink != prevLinkId)
                _linkTracker.AddCellRef(newLink);

            if (width > 1)
            {
                uint rowEndIndex = (y * Width) + Width - 1;
                uint maxRight = Math.Min(rightExt, rowEndIndex - index);
                if (maxRight > 0)
                {
                    for (uint ci = 1; ci <= maxRight; ci++)
                    {
                        uint contLinkId = GetLinkId(_attributes[index + ci]);
                        if (contLinkId != 0)
                            _linkTracker.RemoveCellRef(contLinkId);
                    }

                    _fg.AsSpan((int)(index + 1), (int)maxRight).Fill(cell.Fg);
                    _bg.AsSpan((int)(index + 1), (int)maxRight).Fill(cell.Bg);
                    _attributes.AsSpan((int)(index + 1), (int)maxRight).Fill(cell.Attributes);

                    for (uint k = 1; k <= maxRight; k++)
                    {
                        _chars[index + k] = PackContinuation(k, maxRight - k, gid);
                        if (newLink != 0)
                            _linkTracker.AddCellRef(newLink);
                    }
                }
            }
        }
        else
        {
            WriteCellAndLinks(index, cell);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint? ValidateAndIndex(uint x, uint y)
    {
        if (x >= Width || y >= Height) return null;
        if (!IsPointInScissor((int)x, (int)y)) return null;
        return y * Width + x;
    }

    private void WriteCellAndLinks(uint index, Cell cell)
    {
        uint prevLinkId = GetLinkId(_attributes[index]);
        uint newLinkId = GetLinkId(cell.Attributes);

        _chars[index] = cell.Char;
        _fg[index] = cell.Fg;
        _bg[index] = cell.Bg;
        _attributes[index] = cell.Attributes;

        if (prevLinkId != 0 && prevLinkId != newLinkId)
            _linkTracker.RemoveCellRef(prevLinkId);
        if (newLinkId != 0 && newLinkId != prevLinkId)
            _linkTracker.AddCellRef(newLinkId);
    }

    #endregion

    #region Scissor Stack

    /// <summary>Pushes a scissor (clipping) rectangle. Nested rects intersect with parent.</summary>
    public void PushScissorRect(int x, int y, uint width, uint height)
    {
        var rect = new ClipRect(x, y, width, height);

        if (_scissorStack.Count > 0)
        {
            var clipped = ClipRectToScissor(rect.X, rect.Y, rect.Width, rect.Height);
            rect = clipped ?? new ClipRect(0, 0, 0, 0);
        }

        _scissorStack.Add(rect);
    }

    /// <summary>Pops the most recent scissor rectangle.</summary>
    public void PopScissorRect()
    {
        if (_scissorStack.Count > 0)
            _scissorStack.RemoveAt(_scissorStack.Count - 1);
    }

    /// <summary>Clears all scissor rectangles.</summary>
    public void ClearScissorRects() => _scissorStack.Clear();

    /// <summary>Gets the current active scissor rect, or null if none.</summary>
    public ClipRect? GetCurrentScissorRect()
    {
        if (_scissorStack.Count == 0) return null;
        return _scissorStack[^1];
    }

    /// <summary>Tests if a point is within the current scissor rect.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsPointInScissor(int x, int y)
    {
        var scissor = GetCurrentScissorRect();
        if (scissor is null) return true;
        var s = scissor.Value;
        return x >= s.X && x < s.X + (int)s.Width &&
               y >= s.Y && y < s.Y + (int)s.Height;
    }

    private bool IsRectInScissor(int x, int y, uint width, uint height)
    {
        var scissor = GetCurrentScissorRect();
        if (scissor is null) return true;
        var s = scissor.Value;

        int rectEndX = x + (int)width;
        int rectEndY = y + (int)height;
        int scissorEndX = s.X + (int)s.Width;
        int scissorEndY = s.Y + (int)s.Height;

        return !(x >= scissorEndX || rectEndX <= s.X ||
                 y >= scissorEndY || rectEndY <= s.Y);
    }

    private ClipRect? ClipRectToScissor(int x, int y, uint width, uint height)
    {
        var scissor = GetCurrentScissorRect();
        if (scissor is null)
            return new ClipRect(x, y, width, height);

        var s = scissor.Value;
        int rectEndX = x + (int)width;
        int rectEndY = y + (int)height;
        int scissorEndX = s.X + (int)s.Width;
        int scissorEndY = s.Y + (int)s.Height;

        int intersectX = Math.Max(x, s.X);
        int intersectY = Math.Max(y, s.Y);
        int intersectEndX = Math.Min(rectEndX, scissorEndX);
        int intersectEndY = Math.Min(rectEndY, scissorEndY);

        if (intersectX >= intersectEndX || intersectY >= intersectEndY)
            return null;

        return new ClipRect(intersectX, intersectY, (uint)(intersectEndX - intersectX), (uint)(intersectEndY - intersectY));
    }

    #endregion

    #region Opacity Stack

    /// <summary>Pushes an opacity value. Effective opacity = product of all stacked values.</summary>
    public void PushOpacity(float opacity)
    {
        float current = CurrentOpacity;
        float effective = current * Math.Clamp(opacity, 0f, 1f);
        _opacityStack.Add(effective);
    }

    /// <summary>Pops the most recent opacity value.</summary>
    public void PopOpacity()
    {
        if (_opacityStack.Count > 0)
            _opacityStack.RemoveAt(_opacityStack.Count - 1);
    }

    /// <summary>Clears all opacity values, resetting to full opacity.</summary>
    public void ClearOpacity() => _opacityStack.Clear();

    #endregion

    #region Drawing

    /// <summary>Clears the entire buffer with the specified background color.</summary>
    public void Clear(Rgba bg, uint? @char = null)
    {
        uint cellChar = @char ?? DefaultSpaceChar;
        _linkTracker.Clear();
        _graphemeTracker.Clear();
        _chars.AsSpan().Fill(cellChar);
        _attributes.AsSpan().Fill(0);
        _fg.AsSpan().Fill(Rgba.White);
        _bg.AsSpan().Fill(bg);
    }

    /// <summary>Draws UTF-8 text into the buffer with full Unicode / grapheme cluster support.</summary>
    public void DrawText(ReadOnlySpan<byte> utf8Text, uint x, uint y, Rgba fg, Rgba? bg, uint attributes = 0)
    {
        if (x >= Width || y >= Height || utf8Text.Length == 0) return;

        float opacity = CurrentOpacity;
        Rgba bgColor = bg ?? Rgba.Transparent;
        if (IsFullyTransparent(opacity, fg, bgColor)) return;

        uint charX = x;
        int idx = 0;
        while (idx < utf8Text.Length && charX < Width)
        {
            var status = Rune.DecodeFromUtf8(utf8Text[idx..], out Rune rune, out int bytesConsumed);
            if (status != System.Buffers.OperationStatus.Done)
            {
                idx++;
                continue;
            }

            int cp = rune.Value;

            // Stop at newlines
            if (cp == '\n' || cp == '\r') break;

            // Skip control characters (except tab)
            if (cp < 32 && cp != '\t')
            {
                idx += bytesConsumed;
                continue;
            }

            uint displayWidth = TextWidth.CharWidth(rune, 8);

            // Skip zero-width characters (combining marks, ZWJ, etc.)
            if (displayWidth == 0)
            {
                idx += bytesConsumed;
                continue;
            }

            // Tab: advance to next tab stop
            if (cp == '\t')
            {
                uint tabStop = ((charX / 8) + 1) * 8;
                charX = Math.Min(tabStop, Width);
                idx += bytesConsumed;
                continue;
            }

            // Check if character fits
            if (charX + displayWidth > Width) break;

            Rgba actualBg;
            if (bg.HasValue)
            {
                actualBg = bg.Value;
            }
            else
            {
                var existing = Get(charX, y);
                actualBg = existing?.Bg ?? new Rgba(0f, 0f, 0f, 1f);
            }

            uint codepoint = (uint)cp;
            SetCellForText(charX, y, codepoint, fg, actualBg, attributes, opacity);

            if (displayWidth == 2 && charX + 1 < Width)
            {
                // Wide character continuation cell
                SetCellForText(charX + 1, y, CharFlagContinuation | codepoint, fg, actualBg, attributes, opacity);
            }

            charX += displayWidth;
            idx += bytesConsumed;
        }
    }

    /// <summary>Draws a string into the buffer at the specified position.</summary>
    public void DrawText(string text, uint x, uint y, Rgba fg, Rgba? bg = null, TextAttributes attrs = TextAttributes.None)
    {
        if (string.IsNullOrEmpty(text)) return;

        int maxLen = Encoding.UTF8.GetMaxByteCount(text.Length);
        Span<byte> buffer = maxLen <= 1024
            ? stackalloc byte[maxLen]
            : new byte[maxLen];
        int bytesWritten = Encoding.UTF8.GetBytes(text.AsSpan(), buffer);
        DrawText(buffer[..bytesWritten], x, y, fg, bg, (uint)attrs);
    }

    private void SetCellForText(uint charX, uint y, uint codepoint, Rgba fg, Rgba bg, uint attributes, float opacity)
    {
        if (IsRgbaWithAlpha(bg))
        {
            SetCellWithAlphaBlending(charX, y, codepoint, fg, bg, attributes);
        }
        else
        {
            Set(charX, y, new Cell(codepoint, fg, bg, attributes));
        }
    }

    /// <summary>Draws a single character at the specified position.</summary>
    public void DrawChar(uint codepoint, uint x, uint y, Rgba fg, Rgba bg, uint attributes = 0)
    {
        SetCellWithAlphaBlending(x, y, codepoint, fg, bg, attributes);
    }

    /// <summary>Fills a rectangular region with the specified background color.</summary>
    public void FillRect(uint x, uint y, uint width, uint height, Rgba bg)
    {
        if (Width == 0 || Height == 0 || width == 0 || height == 0) return;
        if (x >= Width || y >= Height) return;
        if (!IsRectInScissor((int)x, (int)y, width, height)) return;

        float opacity = CurrentOpacity;
        if (IsFullyTransparent(opacity, Rgba.Transparent, bg)) return;

        uint startX = x;
        uint startY = y;
        uint endX = Math.Min(Width - 1, x + width - 1);
        uint endY = Math.Min(Height - 1, y + height - 1);

        if (startX > endX || startY > endY) return;

        var clipped = ClipRectToScissor((int)startX, (int)startY, endX - startX + 1, endY - startY + 1);
        if (clipped is null) return;
        var cr = clipped.Value;

        uint clippedStartX = Math.Max(startX, (uint)cr.X);
        uint clippedStartY = Math.Max(startY, (uint)cr.Y);
        uint clippedEndX = Math.Min(endX, (uint)(cr.X + (int)cr.Width - 1));
        uint clippedEndY = Math.Min(endY, (uint)(cr.Y + (int)cr.Height - 1));

        bool hasAlpha = IsRgbaWithAlpha(bg) || opacity < 1f;
        bool graphemeAware = _graphemeTracker.HasAny();
        bool linkAware = _linkTracker.HasAny();

        if (graphemeAware || linkAware)
        {
            for (uint fy = clippedStartY; fy <= clippedEndY; fy++)
                for (uint fx = clippedStartX; fx <= clippedEndX; fx++)
                    SetCellWithAlphaBlending(fx, fy, DefaultSpaceChar, Rgba.White, bg, 0);
        }
        else if (hasAlpha)
        {
            for (uint fy = clippedStartY; fy <= clippedEndY; fy++)
                for (uint fx = clippedStartX; fx <= clippedEndX; fx++)
                    SetCellWithAlphaBlendingRaw(fx, fy, DefaultSpaceChar, Rgba.White, bg, 0);
        }
        else
        {
            // Fast path: direct fill for fully opaque backgrounds
            for (uint fy = clippedStartY; fy <= clippedEndY; fy++)
            {
                uint rowStart = fy * Width + clippedStartX;
                int rowWidth = (int)(clippedEndX - clippedStartX + 1);

                _chars.AsSpan((int)rowStart, rowWidth).Fill(DefaultSpaceChar);
                _fg.AsSpan((int)rowStart, rowWidth).Fill(Rgba.White);
                _bg.AsSpan((int)rowStart, rowWidth).Fill(bg);
                _attributes.AsSpan((int)rowStart, rowWidth).Fill(0);
            }
        }
    }

    /// <summary>Sets a cell with alpha blending applied.</summary>
    public void SetCellWithAlphaBlending(uint x, uint y, uint codepoint, Rgba fg, Rgba bg, uint attributes = 0)
    {
        if (!IsPointInScissor((int)x, (int)y)) return;

        float opacity = CurrentOpacity;
        if (IsFullyTransparent(opacity, fg, bg)) return;
        if (IsFullyOpaque(opacity, fg, bg))
        {
            Set(x, y, new Cell(codepoint, fg, bg, attributes));
            return;
        }

        Rgba effectiveFg = new(fg.R, fg.G, fg.B, fg.A * opacity);
        Rgba effectiveBg = new(bg.R, bg.G, bg.B, bg.A * opacity);

        var overlayCell = new Cell(codepoint, effectiveFg, effectiveBg, attributes);
        var destCell = Get(x, y);

        if (destCell.HasValue)
        {
            var blended = BlendCells(overlayCell, destCell.Value);
            Set(x, y, blended);
        }
        else
        {
            Set(x, y, overlayCell);
        }
    }

    private void SetCellWithAlphaBlendingRaw(uint x, uint y, uint codepoint, Rgba fg, Rgba bg, uint attributes)
    {
        if (!IsPointInScissor((int)x, (int)y)) return;

        float opacity = CurrentOpacity;
        if (IsFullyTransparent(opacity, fg, bg)) return;
        if (IsFullyOpaque(opacity, fg, bg))
        {
            var overlayCell = new Cell(codepoint, fg, bg, attributes);
            SetRaw(x, y, overlayCell);
            return;
        }

        Rgba effectiveFg = new(fg.R, fg.G, fg.B, fg.A * opacity);
        Rgba effectiveBg = new(bg.R, bg.G, bg.B, bg.A * opacity);

        var overlay = new Cell(codepoint, effectiveFg, effectiveBg, attributes);
        var dest = Get(x, y);

        if (dest.HasValue)
        {
            var blended = BlendCells(overlay, dest.Value);
            SetRaw(x, y, blended);
        }
        else
        {
            SetRaw(x, y, overlay);
        }
    }

    /// <summary>Draws a region from a source buffer into this buffer.</summary>
    public void DrawFrameBuffer(int x, int y, ManagedBuffer source, uint srcX, uint srcY, uint w, uint h)
    {
        if (Width == 0 || Height == 0 || source.Width == 0 || source.Height == 0) return;

        float opacity = CurrentOpacity;
        if (opacity == 0f) return;

        if (srcX >= source.Width || srcY >= source.Height) return;
        if (w == 0 || h == 0) return;

        uint clampedW = Math.Min(w, source.Width - srcX);
        uint clampedH = Math.Min(h, source.Height - srcY);

        int startDestX = Math.Max(0, x);
        int startDestY = Math.Max(0, y);
        int endDestX = Math.Min((int)Width - 1, x + (int)clampedW - 1);
        int endDestY = Math.Min((int)Height - 1, y + (int)clampedH - 1);

        if (startDestX > endDestX || startDestY > endDestY) return;

        uint destWidth = (uint)(endDestX - startDestX + 1);
        uint destHeight = (uint)(endDestY - startDestY + 1);
        if (!IsRectInScissor(startDestX, startDestY, destWidth, destHeight)) return;

        var clipped = ClipRectToScissor(startDestX, startDestY, destWidth, destHeight);
        if (clipped is null) return;
        var cr = clipped.Value;

        int clippedStartX = Math.Max(startDestX, cr.X);
        int clippedStartY = Math.Max(startDestY, cr.Y);
        int clippedEndX = Math.Min(endDestX, cr.X + (int)cr.Width - 1);
        int clippedEndY = Math.Min(endDestY, cr.Y + (int)cr.Height - 1);

        bool graphemeAware = _graphemeTracker.HasAny() || source._graphemeTracker.HasAny();
        bool linkAware = _linkTracker.HasAny() || source._linkTracker.HasAny();

        if (!graphemeAware && !source.RespectAlpha && !linkAware)
        {
            // Fast path: direct memory copy
            for (int dY = clippedStartY; dY <= clippedEndY; dY++)
            {
                int relY = dY - y;
                uint sY = srcY + (uint)relY;
                if (sY >= source.Height) continue;

                int relX = clippedStartX - x;
                uint sX = srcX + (uint)relX;
                if (sX >= source.Width) continue;

                uint destRow = (uint)dY * Width + (uint)clippedStartX;
                uint srcRow = sY * source.Width + sX;
                int copyWidth = (int)Math.Min((uint)(clippedEndX - clippedStartX + 1), source.Width - sX);

                source._chars.AsSpan((int)srcRow, copyWidth).CopyTo(_chars.AsSpan((int)destRow, copyWidth));
                source._fg.AsSpan((int)srcRow, copyWidth).CopyTo(_fg.AsSpan((int)destRow, copyWidth));
                source._bg.AsSpan((int)srcRow, copyWidth).CopyTo(_bg.AsSpan((int)destRow, copyWidth));
                source._attributes.AsSpan((int)srcRow, copyWidth).CopyTo(_attributes.AsSpan((int)destRow, copyWidth));
            }
            return;
        }

        // Slow path: cell-by-cell with blending
        for (int dY = clippedStartY; dY <= clippedEndY; dY++)
        {
            uint lastDrawnGraphemeId = 0;

            for (int dX = clippedStartX; dX <= clippedEndX; dX++)
            {
                int relX = dX - x;
                int relY = dY - y;
                uint sX = srcX + (uint)relX;
                uint sY = srcY + (uint)relY;

                if (sX >= source.Width || sY >= source.Height) continue;

                uint srcIndex = sY * source.Width + sX;
                uint srcChar = source._chars[srcIndex];
                Rgba srcFg = source._fg[srcIndex];
                Rgba srcBg = source._bg[srcIndex];
                uint srcAttr = source._attributes[srcIndex];

                if (srcBg.A == 0f && srcFg.A == 0f) continue;

                if (graphemeAware)
                {
                    if (IsContinuationChar(srcChar))
                    {
                        uint graphemeId = srcChar & GraphemeIdMask;
                        if (graphemeId != lastDrawnGraphemeId)
                            SetCellWithAlphaBlending((uint)dX, (uint)dY, DefaultSpaceChar, srcFg, srcBg, srcAttr);
                        continue;
                    }

                    if (IsGraphemeChar(srcChar))
                        lastDrawnGraphemeId = srcChar & GraphemeIdMask;

                    SetCellWithAlphaBlending((uint)dX, (uint)dY, srcChar, srcFg, srcBg, srcAttr);
                    continue;
                }

                SetCellWithAlphaBlendingRaw((uint)dX, (uint)dY, srcChar, srcFg, srcBg, srcAttr);
            }
        }
    }

    /// <summary>Renders a text buffer view's visible content into this buffer.</summary>
    public void DrawTextBufferView(ManagedTextBufferView view, int x, int y)
    {
        DrawTextBufferViewInternal(view, x, y);
    }

    /// <summary>Renders an editor view's visible content into this buffer.</summary>
    public void DrawEditorView(ManagedEditorView editorView, int x, int y)
    {
        DrawTextBufferView(editorView.View, x, y);
    }

    private void DrawTextBufferViewInternal(ManagedTextBufferView view, int x, int y)
    {
        float opacity = CurrentOpacity;
        if (opacity == 0f) return;

        view.UpdateVirtualLines();
        var virtualLines = view.GetVirtualLines();
        if (virtualLines.Length == 0) return;

        // Compute visible row range: virtual line i renders at screenY = y + i
        int firstVisible = Math.Max(0, -y);
        int lastPossible = Math.Min(virtualLines.Length, (int)Height - y);
        if (firstVisible >= lastPossible) return;

        uint horizontalOffset = view.ViewportX;
        uint viewportWidth = view.Width;

        var buffer = view.Buffer;
        byte tabWidth = buffer.TabWidth;
        var syntaxStyle = buffer.SyntaxStyle;

        // Selection state
        var selectionRange = view.GetSelectionRange();
        Rgba? selBg = view.SelectionBg;
        Rgba? selFg = view.SelectionFg;

        // Cache per-logical-line char offset for selection hit testing
        uint prevLogicalLine = uint.MaxValue;
        uint lineCharOffset = 0;

        for (int vlineIdx = firstVisible; vlineIdx < lastPossible; vlineIdx++)
        {
            int screenY = y + vlineIdx;
            if (screenY < 0 || screenY >= (int)Height) continue;

            ref readonly var vline = ref virtualLines[vlineIdx];
            uint logicalLine = vline.SourceLine;
            if (logicalLine >= buffer.LineCount) break;

            string lineText = buffer.GetLineText(logicalLine);

            // Compute char offset once per logical line (for selection)
            if (selectionRange.HasValue && logicalLine != prevLogicalLine)
            {
                lineCharOffset = buffer.GetOffset(logicalLine, 0);
                prevLogicalLine = logicalLine;
            }

            // Style spans for syntax highlighting
            ReadOnlySpan<StyleSpan> styleSpans = syntaxStyle != null
                ? buffer.GetStyleSpans(logicalLine)
                : [];
            int spanIdx = 0;

            uint col = 0;       // display column in logical line
            uint charIdx = 0;   // character index in logical line

            foreach (var rune in lineText.EnumerateRunes())
            {
                uint displayWidth = TextWidth.CharWidth(rune, tabWidth);

                // Skip zero-width characters (combining marks, ZWJ, etc.)
                if (displayWidth == 0)
                {
                    charIdx++;
                    continue;
                }

                // Before this virtual line's column range
                if (col + displayWidth <= vline.SourceColOffset)
                {
                    col += displayWidth;
                    charIdx++;
                    continue;
                }

                // Past this virtual line's column range
                if (col >= vline.SourceColOffset + vline.WidthCols)
                    break;

                uint columnInVline = col - vline.SourceColOffset;

                // Apply horizontal scrolling
                if (columnInVline < horizontalOffset)
                {
                    col += displayWidth;
                    charIdx++;
                    continue;
                }

                if (columnInVline >= horizontalOffset + viewportWidth)
                    break;

                int screenX = x + (int)(columnInVline - horizontalOffset);
                if (screenX >= (int)Width) break;

                if (screenX >= 0)
                {
                    Rgba cellFg = Rgba.White;
                    Rgba cellBg = Rgba.Transparent;
                    uint attrs = 0;

                    // ── Syntax highlighting ──
                    if (styleSpans.Length > 0)
                    {
                        while (spanIdx < styleSpans.Length && styleSpans[spanIdx].End <= col)
                            spanIdx++;

                        if (spanIdx < styleSpans.Length)
                        {
                            ref readonly var span = ref styleSpans[spanIdx];
                            if (col >= span.Start && col < span.End)
                            {
                                var style = syntaxStyle!.GetStyleById(span.StyleId);
                                if (style.HasValue)
                                {
                                    if (style.Value.Fg.HasValue) cellFg = style.Value.Fg.Value;
                                    if (style.Value.Bg.HasValue) cellBg = style.Value.Bg.Value;
                                    attrs = (uint)style.Value.Attributes;
                                }
                            }
                        }
                    }

                    // ── Selection overlay ──
                    if (selectionRange.HasValue)
                    {
                        uint charOffset = lineCharOffset + charIdx;
                        var (selStart, selEnd) = selectionRange.Value;
                        if (charOffset >= selStart && charOffset < selEnd)
                        {
                            if (selBg.HasValue || selFg.HasValue)
                            {
                                if (selBg.HasValue) cellBg = selBg.Value;
                                if (selFg.HasValue) cellFg = selFg.Value;
                            }
                            else
                            {
                                // Invert selection: swap fg/bg
                                Rgba newFg = cellBg.A > 0 ? cellBg : Rgba.Black;
                                cellBg = cellFg;
                                cellFg = newFg;
                            }
                        }
                    }

                    uint codepoint = (uint)rune.Value;

                    if (rune.Value == '\t')
                    {
                        // Tab: fill with spaces
                        for (uint t = 0; t < displayWidth; t++)
                        {
                            int tx = screenX + (int)t;
                            if (tx >= (int)Width) break;
                            if (tx >= 0)
                                WriteTextBufferCell((uint)tx, (uint)screenY,
                                    DefaultSpaceChar, cellFg, cellBg, attrs);
                        }
                    }
                    else
                    {
                        WriteTextBufferCell((uint)screenX, (uint)screenY,
                            codepoint, cellFg, cellBg, attrs);

                        // Wide character continuation cell
                        if (displayWidth == 2 && screenX + 1 < (int)Width)
                        {
                            WriteTextBufferCell((uint)(screenX + 1), (uint)screenY,
                                CharFlagContinuation | codepoint, cellFg, cellBg, attrs);
                        }
                    }
                }

                col += displayWidth;
                charIdx++;
            }
        }
    }

    /// <summary>
    /// Writes a cell for text buffer rendering with transparent-background semantics:
    /// spaces with transparent bg preserve the underlying non-space character;
    /// non-space characters with transparent bg preserve the existing background.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteTextBufferCell(uint x, uint y, uint codepoint, Rgba fg, Rgba bg, uint attrs)
    {
        if (bg.A == 0f)
        {
            // Space with transparent bg: preserve underlying non-space character
            if (codepoint == DefaultSpaceChar)
            {
                uint idx = y * Width + x;
                uint destChar = _chars[(int)idx];
                if (destChar > DefaultSpaceChar && destChar <= MaxUnicodeCodepoint)
                    return;
            }

            // Transparent bg: write char/fg/attrs, keep existing background
            uint index = y * Width + x;
            Rgba existingBg = _bg[(int)index];
            SetCellWithAlphaBlending(x, y, codepoint, fg, existingBg, attrs);
        }
        else
        {
            SetCellWithAlphaBlending(x, y, codepoint, fg, bg, attrs);
        }
    }

    #endregion

    #region Color Blending

    /// <summary>
    /// Ported from Zig blendColors: perceptual alpha blend with power curve.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Rgba BlendColors(Rgba overlay, Rgba dest, Rgba? blendBackdrop)
    {
        Rgba d = dest;
        if (d.A == 0f && blendBackdrop.HasValue)
            d = blendBackdrop.Value;

        if (overlay.A == 1f) return overlay;

        if (d.A == 0f)
        {
            float a = overlay.A;
            float r = overlay.R * a;
            float g = overlay.G * a;
            float b = overlay.B * a;
            if (r < 0.01f && g < 0.01f && b < 0.01f)
                return Rgba.Transparent;
            return new Rgba(r, g, b, a);
        }

        float alpha = overlay.A;
        float perceptualAlpha;

        if (alpha > 0.8f)
        {
            float normalizedHighAlpha = (alpha - 0.8f) * 5f;
            float curvedHighAlpha = FastPow(normalizedHighAlpha, 0.2f);
            perceptualAlpha = 0.8f + (curvedHighAlpha * 0.2f);
        }
        else
        {
            perceptualAlpha = FastPow(alpha, 0.9f);
        }

        float oneMinusAlpha = 1f - perceptualAlpha;
        float blendedR = overlay.R * perceptualAlpha + d.R * oneMinusAlpha;
        float blendedG = overlay.G * perceptualAlpha + d.G * oneMinusAlpha;
        float blendedB = overlay.B * perceptualAlpha + d.B * oneMinusAlpha;

        float resultAlpha = alpha + d.A * (1f - alpha);

        return new Rgba(blendedR, blendedG, blendedB, resultAlpha);
    }

    /// <summary>Fast approximate power function matching the Zig implementation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float FastPow(float a, float b)
    {
        if (a <= 0f) return 0f;
        int i = BitConverter.SingleToInt32Bits(a);
        int result = (int)(b * (i - 1065353216) + 1065353216f);
        return BitConverter.Int32BitsToSingle(result);
    }

    /// <summary>
    /// Composites an overlay cell onto a destination cell, handling alpha blending,
    /// char preservation, and link attribution.
    /// </summary>
    internal Cell BlendCells(Cell overlayCell, Cell destCell)
    {
        bool hasBgAlpha = IsRgbaWithAlpha(overlayCell.Bg);
        bool hasFgAlpha = IsRgbaWithAlpha(overlayCell.Fg);

        if (hasBgAlpha || hasFgAlpha)
        {
            Rgba blendedBg = hasBgAlpha
                ? BlendColors(overlayCell.Bg, destCell.Bg, BlendBackdropColor)
                : overlayCell.Bg;

            bool charIsDefaultSpace = overlayCell.Char == DefaultSpaceChar;
            bool destNotZero = destCell.Char != 0;
            bool destNotDefaultSpace = destCell.Char != DefaultSpaceChar;
            bool destWidthIsOne = EncodedCharWidth(destCell.Char) == 1;

            bool preserveChar = charIsDefaultSpace && destNotZero && destNotDefaultSpace && destWidthIsOne;
            uint finalChar = preserveChar ? destCell.Char : overlayCell.Char;

            Rgba finalFg;
            if (preserveChar)
            {
                finalFg = BlendColors(overlayCell.Bg, destCell.Fg, BlendBackdropColor);
            }
            else
            {
                finalFg = hasFgAlpha
                    ? BlendColors(overlayCell.Fg, destCell.Bg, BlendBackdropColor)
                    : overlayCell.Fg;
            }

            // Base attributes from preserved char or overlay; links always from overlay
            uint baseAttrs = preserveChar
                ? GetBaseAttributes(destCell.Attributes)
                : GetBaseAttributes(overlayCell.Attributes);
            uint overlayLinkId = GetLinkId(overlayCell.Attributes);
            uint finalAttributes = SetLinkId(baseAttrs, overlayLinkId);

            // Preserve dest bg alpha when overlay bg is fully transparent
            float finalBgAlpha = overlayCell.Bg.A == 0f ? destCell.Bg.A : overlayCell.Bg.A;

            return new Cell(finalChar, finalFg, new Rgba(blendedBg.R, blendedBg.G, blendedBg.B, finalBgAlpha), finalAttributes);
        }

        return overlayCell;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsRgbaWithAlpha(Rgba color) => color.A < 1f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsFullyOpaque(float opacity, Rgba fg, Rgba bg)
        => opacity == 1f && !IsRgbaWithAlpha(fg) && !IsRgbaWithAlpha(bg);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsFullyTransparent(float opacity, Rgba fg, Rgba bg)
        => opacity == 0f || (fg.A == 0f && bg.A == 0f);

    #endregion

    #region Color Matrix

    /// <summary>
    /// Applies a 4×4 color matrix to cells identified by a packed mask of [x, y, strength, …] triplets.
    /// </summary>
    public void ColorMatrix(ReadOnlySpan<float> matrix, ReadOnlySpan<float> cellMask, float strength, TargetChannel target)
    {
        if (matrix.Length < 16 || cellMask.Length < 3) return;
        if ((byte)target == 0) return;
        if (!float.IsFinite(strength)) return;

        uint width = Width;
        uint height = Height;
        float maxU32F = (float)uint.MaxValue;

        int len = cellMask.Length - (cellMask.Length % 3);
        for (int i = 0; i < len; i += 3)
        {
            float xf = cellMask[i];
            float yf = cellMask[i + 1];

            if (xf < 0f || yf < 0f) continue;
            if (!float.IsFinite(xf) || !float.IsFinite(yf)) continue;
            if (xf > maxU32F || yf > maxU32F) continue;

            uint cx = (uint)xf;
            uint cy = (uint)yf;
            float cellStrength = cellMask[i + 2] * strength;

            if (cx >= width || cy >= height) continue;
            if (!float.IsFinite(cellStrength) || cellStrength == 0f) continue;

            uint index = cy * width + cx;

            if (((byte)target & 1) != 0)
            {
                ref Rgba fgRef = ref _fg[index];
                ApplyMatrixScalar(matrix, ref fgRef, cellStrength);
            }

            if (((byte)target & 2) != 0)
            {
                ref Rgba bgRef = ref _bg[index];
                ApplyMatrixScalar(matrix, ref bgRef, cellStrength);
            }
        }
    }

    /// <summary>
    /// Applies a 4×4 color matrix uniformly to ALL cells, using SIMD where possible.
    /// </summary>
    public void ColorMatrixUniform(ReadOnlySpan<float> matrix, float strength, TargetChannel target)
    {
        if (matrix.Length < 16 || strength == 0f) return;
        if ((byte)target == 0) return;
        if (!float.IsFinite(strength)) return;

        uint size = Width * Height;
        bool processFg = ((byte)target & 1) != 0;
        bool processBg = ((byte)target & 2) != 0;

        if (Sse.IsSupported)
        {
            ColorMatrixUniformSse(matrix, strength, size, processFg, processBg);
        }
        else
        {
            ColorMatrixUniformScalar(matrix, strength, size, processFg, processBg);
        }
    }

    private void ColorMatrixUniformSse(ReadOnlySpan<float> matrix, float strength, uint size, bool processFg, bool processBg)
    {
        // Load matrix rows as SSE vectors
        Vector128<float> row0 = Vector128.Create(matrix[0], matrix[1], matrix[2], matrix[3]);
        Vector128<float> row1 = Vector128.Create(matrix[4], matrix[5], matrix[6], matrix[7]);
        Vector128<float> row2 = Vector128.Create(matrix[8], matrix[9], matrix[10], matrix[11]);
        Vector128<float> row3 = Vector128.Create(matrix[12], matrix[13], matrix[14], matrix[15]);
        Vector128<float> strengthVec = Vector128.Create(strength);

        ref Rgba fgBase = ref MemoryMarshal.GetArrayDataReference(_fg);
        ref Rgba bgBase = ref MemoryMarshal.GetArrayDataReference(_bg);

        for (uint i = 0; i < size; i++)
        {
            if (processFg)
            {
                ref Rgba fg = ref Unsafe.Add(ref fgBase, i);
                Vector128<float> color = Unsafe.As<Rgba, Vector128<float>>(ref fg);
                Vector128<float> transformed = ApplyMatrixSse(row0, row1, row2, row3, color, strengthVec);
                Unsafe.As<Rgba, Vector128<float>>(ref fg) = transformed;
            }

            if (processBg)
            {
                ref Rgba bg = ref Unsafe.Add(ref bgBase, i);
                Vector128<float> color = Unsafe.As<Rgba, Vector128<float>>(ref bg);
                Vector128<float> transformed = ApplyMatrixSse(row0, row1, row2, row3, color, strengthVec);
                Unsafe.As<Rgba, Vector128<float>>(ref bg) = transformed;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> ApplyMatrixSse(
        Vector128<float> row0, Vector128<float> row1, Vector128<float> row2, Vector128<float> row3,
        Vector128<float> color, Vector128<float> strength)
    {
        // Dot product: each output channel = dot(matrix_row, color)
        Vector128<float> r = Sse.Multiply(row0, color);
        Vector128<float> g = Sse.Multiply(row1, color);
        Vector128<float> b = Sse.Multiply(row2, color);
        Vector128<float> a = Sse.Multiply(row3, color);

        // Horizontal sum each via SIMD shuffles
        Vector128<float> newR = HorizontalSum(r);
        Vector128<float> newG = HorizontalSum(g);
        Vector128<float> newB = HorizontalSum(b);
        Vector128<float> newA = HorizontalSum(a);

        // Reconstruct: [newR, newG, newB, newA]
        Vector128<float> newColor = Vector128.Create(
            newR.GetElement(0), newG.GetElement(0), newB.GetElement(0), newA.GetElement(0));

        // Blend: original + (new - original) * strength
        Vector128<float> diff = Sse.Subtract(newColor, color);
        return Sse.Add(color, Sse.Multiply(diff, strength));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> HorizontalSum(Vector128<float> v)
    {
        // v = [a, b, c, d]
        // step1 = [a+b, c+d, a+b, c+d]
        Vector128<float> shuf = Sse.Shuffle(v, v, 0b_01_00_11_10); // [c, d, a, b]
        Vector128<float> sums = Sse.Add(v, shuf); // [a+c, b+d, c+a, d+b]
        Vector128<float> shuf2 = Sse.Shuffle(sums, sums, 0b_10_11_00_01); // [b+d, a+c, d+b, c+a]
        return Sse.Add(sums, shuf2); // [a+b+c+d, ...]
    }

    private void ColorMatrixUniformScalar(ReadOnlySpan<float> matrix, float strength, uint size, bool processFg, bool processBg)
    {
        for (uint i = 0; i < size; i++)
        {
            if (processFg)
                ApplyMatrixScalar(matrix, ref _fg[i], strength);
            if (processBg)
                ApplyMatrixScalar(matrix, ref _bg[i], strength);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyMatrixScalar(ReadOnlySpan<float> matrix, ref Rgba color, float strength)
    {
        float r = color.R, g = color.G, b = color.B, a = color.A;

        float newR = matrix[0] * r + matrix[1] * g + matrix[2] * b + matrix[3] * a;
        float newG = matrix[4] * r + matrix[5] * g + matrix[6] * b + matrix[7] * a;
        float newB = matrix[8] * r + matrix[9] * g + matrix[10] * b + matrix[11] * a;
        float newA = matrix[12] * r + matrix[13] * g + matrix[14] * b + matrix[15] * a;

        color = new Rgba(
            r + (newR - r) * strength,
            g + (newG - g) * strength,
            b + (newB - b) * strength,
            a + (newA - a) * strength);
    }

    #endregion

    #region DrawBox

    /// <summary>
    /// Draws a box with borders, optional fill, and optional title text.
    /// Faithfully ports the Zig drawBox logic including corner/edge/fill/title handling.
    /// </summary>
    public void DrawBox(
        int x, int y, uint w, uint h,
        BorderCharacters borderChars,
        BorderSides sides,
        bool shouldFill,
        Rgba borderColor,
        Rgba? backgroundColor,
        string? title = null,
        TitleAlignment titleAlignment = TitleAlignment.Left,
        string? bottomTitle = null,
        TitleAlignment bottomTitleAlignment = TitleAlignment.Left)
    {
        Rgba bgColor = backgroundColor ?? Rgba.Transparent;
        float opacity = CurrentOpacity;
        if (IsFullyTransparent(opacity, borderColor, bgColor)) return;

        int startX = Math.Max(0, x);
        int startY = Math.Max(0, y);
        int endX = Math.Min((int)Width - 1, x + (int)w - 1);
        int endY = Math.Min((int)Height - 1, y + (int)h - 1);

        if (startX > endX || startY > endY) return;

        uint boxWidth = (uint)(endX - startX + 1);
        uint boxHeight = (uint)(endY - startY + 1);
        if (!IsRectInScissor(startX, startY, boxWidth, boxHeight)) return;

        bool isAtActualLeft = startX == x;
        bool isAtActualRight = endX == x + (int)w - 1;
        bool isAtActualTop = startY == y;
        bool isAtActualBottom = endY == y + (int)h - 1;

        bool hasTop = sides.HasFlag(BorderSides.Top);
        bool hasBottom = sides.HasFlag(BorderSides.Bottom);
        bool hasLeft = sides.HasFlag(BorderSides.Left);
        bool hasRight = sides.HasFlag(BorderSides.Right);

        uint[] cp = borderChars.ToCodePoints();

        var titleLayout = ComputeBoxTitleLayout(title, hasTop, isAtActualTop, startX, endX, w, (byte)titleAlignment);
        var bottomTitleLayout = ComputeBoxTitleLayout(bottomTitle, hasBottom, isAtActualBottom, startX, endX, w, (byte)bottomTitleAlignment);

        // Fill interior
        if (shouldFill)
        {
            if (!hasTop && !hasRight && !hasBottom && !hasLeft)
            {
                FillRect((uint)startX, (uint)startY, boxWidth, boxHeight, bgColor);
            }
            else
            {
                int innerStartX = startX + (hasLeft && isAtActualLeft ? 1 : 0);
                int innerStartY = startY + (hasTop && isAtActualTop ? 1 : 0);
                int innerEndX = endX - (hasRight && isAtActualRight ? 1 : 0);
                int innerEndY = endY - (hasBottom && isAtActualBottom ? 1 : 0);

                if (innerEndX >= innerStartX && innerEndY >= innerStartY)
                {
                    FillRect((uint)innerStartX, (uint)innerStartY,
                        (uint)(innerEndX - innerStartX + 1),
                        (uint)(innerEndY - innerStartY + 1),
                        bgColor);
                }
            }
        }

        // Determine fast path
        bool useTransparentFastPath = CanUseTransparentBorderFastPath(cp, borderColor, bgColor);

        // Special cases for extending vertical borders
        bool leftBorderOnly = hasLeft && isAtActualLeft && !hasTop && !hasBottom;
        bool rightBorderOnly = hasRight && isAtActualRight && !hasTop && !hasBottom;
        bool bottomOnlyWithVert = hasBottom && isAtActualBottom && !hasTop && (hasLeft || hasRight);
        bool topOnlyWithVert = hasTop && isAtActualTop && !hasBottom && (hasLeft || hasRight);

        bool extendVerticalsToTop = leftBorderOnly || rightBorderOnly || bottomOnlyWithVert;
        bool extendVerticalsToBottom = leftBorderOnly || rightBorderOnly || topOnlyWithVert;

        // Draw horizontal borders
        if (hasTop && isAtActualTop)
        {
            for (int drawX = startX; drawX <= endX; drawX++)
            {
                if (startY < 0 || startY >= (int)Height) continue;
                if (titleLayout.ShouldDraw && drawX >= titleLayout.StartX && drawX <= titleLayout.EndX) continue;

                uint ch = cp[4]; // horizontal
                if (drawX == startX && isAtActualLeft)
                    ch = hasLeft ? cp[0] : cp[4]; // topLeft or horizontal
                else if (drawX == endX && isAtActualRight)
                    ch = hasRight ? cp[1] : cp[4]; // topRight or horizontal

                if (useTransparentFastPath)
                {
                    uint index = (uint)startY * Width + (uint)drawX;
                    _chars[index] = ch;
                    _fg[index] = borderColor;
                    _attributes[index] = 0;
                }
                else
                {
                    SetCellWithAlphaBlending((uint)drawX, (uint)startY, ch, borderColor, bgColor, 0);
                }
            }
        }

        if (hasBottom && isAtActualBottom)
        {
            for (int drawX = startX; drawX <= endX; drawX++)
            {
                if (endY < 0 || endY >= (int)Height) continue;
                if (bottomTitleLayout.ShouldDraw && drawX >= bottomTitleLayout.StartX && drawX <= bottomTitleLayout.EndX) continue;

                uint ch = cp[4]; // horizontal
                if (drawX == startX && isAtActualLeft)
                    ch = hasLeft ? cp[2] : cp[4]; // bottomLeft or horizontal
                else if (drawX == endX && isAtActualRight)
                    ch = hasRight ? cp[3] : cp[4]; // bottomRight or horizontal

                if (useTransparentFastPath)
                {
                    uint index = (uint)endY * Width + (uint)drawX;
                    _chars[index] = ch;
                    _fg[index] = borderColor;
                    _attributes[index] = 0;
                }
                else
                {
                    SetCellWithAlphaBlending((uint)drawX, (uint)endY, ch, borderColor, bgColor, 0);
                }
            }
        }

        // Draw vertical borders
        int verticalStartY = extendVerticalsToTop ? startY : startY + (hasTop && isAtActualTop ? 1 : 0);
        int verticalEndY = extendVerticalsToBottom ? endY : endY - (hasBottom && isAtActualBottom ? 1 : 0);

        for (int drawY = verticalStartY; drawY <= verticalEndY; drawY++)
        {
            if (hasLeft && isAtActualLeft && startX >= 0 && startX < (int)Width)
            {
                if (useTransparentFastPath)
                {
                    uint index = (uint)drawY * Width + (uint)startX;
                    _chars[index] = cp[5]; // vertical
                    _fg[index] = borderColor;
                    _attributes[index] = 0;
                }
                else
                {
                    SetCellWithAlphaBlending((uint)startX, (uint)drawY, cp[5], borderColor, bgColor, 0);
                }
            }

            if (hasRight && isAtActualRight && endX >= 0 && endX < (int)Width)
            {
                if (useTransparentFastPath)
                {
                    uint index = (uint)drawY * Width + (uint)endX;
                    _chars[index] = cp[5]; // vertical
                    _fg[index] = borderColor;
                    _attributes[index] = 0;
                }
                else
                {
                    SetCellWithAlphaBlending((uint)endX, (uint)drawY, cp[5], borderColor, bgColor, 0);
                }
            }
        }

        // Draw titles
        if (titleLayout.ShouldDraw && title is not null)
            DrawText(title, (uint)titleLayout.X, (uint)startY, borderColor, bgColor);

        if (bottomTitleLayout.ShouldDraw && bottomTitle is not null)
            DrawText(bottomTitle, (uint)bottomTitleLayout.X, (uint)endY, borderColor, bgColor);
    }

    private bool CanUseTransparentBorderFastPath(uint[] cp, Rgba borderColor, Rgba bgColor)
    {
        return CurrentOpacity == 1f &&
               borderColor.A == 1f &&
               bgColor.A == 0f &&
               !_graphemeTracker.HasAny() &&
               !_linkTracker.HasAny() &&
               IsSingleWidthBorderChar(cp[0]) && // topLeft
               IsSingleWidthBorderChar(cp[1]) && // topRight
               IsSingleWidthBorderChar(cp[2]) && // bottomLeft
               IsSingleWidthBorderChar(cp[3]) && // bottomRight
               IsSingleWidthBorderChar(cp[4]) && // horizontal
               IsSingleWidthBorderChar(cp[5]);   // vertical
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsSingleWidthBorderChar(uint ch)
        => ch <= MaxUnicodeCodepoint && !IsGraphemeChar(ch) && !IsContinuationChar(ch);

    private readonly record struct BoxTitleLayout(bool ShouldDraw, int X, int StartX, int EndX);

    private BoxTitleLayout ComputeBoxTitleLayout(
        string? titleText,
        bool borderSide,
        bool isAtActualSide,
        int startX,
        int endX,
        uint boxWidth,
        byte alignment)
    {
        if (titleText is null || titleText.Length == 0 || !borderSide || !isAtActualSide)
            return new BoxTitleLayout(false, startX, startX, startX);

        // Unicode-aware display width for title
        int titleLength = (int)TextWidth.CalculateTextWidth(titleText.AsSpan(), 8, _widthMethod);
        const int minTitleSpace = 4;

        if ((int)boxWidth < titleLength + minTitleSpace)
            return new BoxTitleLayout(false, startX, startX, startX);

        const int padding = 2;
        int titleX = startX + padding;

        if (alignment == 1) // Center
            titleX = startX + Math.Max(padding, ((int)boxWidth - titleLength) / 2);
        else if (alignment == 2) // Right
            titleX = startX + (int)boxWidth - padding - titleLength;

        titleX = Math.Max(startX + padding, Math.Min(titleX, endX - titleLength));

        return new BoxTitleLayout(true, titleX, titleX, titleX + titleLength - 1);
    }

    #endregion

    #region Grapheme / Link Tracking

    private sealed class GraphemeTracker
    {
        private readonly Dictionary<uint, int> _refCounts = [];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(uint id)
        {
            ref int count = ref CollectionsMarshal.GetValueRefOrAddDefault(_refCounts, id, out _);
            count++;
        }

        public void Remove(uint id)
        {
            if (_refCounts.TryGetValue(id, out int count))
            {
                if (count <= 1) _refCounts.Remove(id);
                else _refCounts[id] = count - 1;
            }
        }

        public void Replace(uint? oldId, uint? newId)
        {
            if (oldId.HasValue) Remove(oldId.Value);
            if (newId.HasValue) Add(newId.Value);
        }

        public bool HasAny() => _refCounts.Count > 0;
        public void Clear() => _refCounts.Clear();
    }

    private sealed class LinkTracker
    {
        private readonly Dictionary<uint, int> _refCounts = [];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddCellRef(uint linkId)
        {
            ref int count = ref CollectionsMarshal.GetValueRefOrAddDefault(_refCounts, linkId, out _);
            count++;
        }

        public void RemoveCellRef(uint linkId)
        {
            if (_refCounts.TryGetValue(linkId, out int count))
            {
                if (count <= 1) _refCounts.Remove(linkId);
                else _refCounts[linkId] = count - 1;
            }
        }

        public bool HasAny() => _refCounts.Count > 0;
        public void Clear() => _refCounts.Clear();
    }

    #endregion

    #region Raw Buffer Access

    /// <summary>Gets the raw character buffer for the renderer.</summary>
    public Span<uint> CharBuffer => _chars.AsSpan();

    /// <summary>Gets the raw foreground color buffer.</summary>
    public Span<Rgba> FgBuffer => _fg.AsSpan();

    /// <summary>Gets the raw background color buffer.</summary>
    public Span<Rgba> BgBuffer => _bg.AsSpan();

    /// <summary>Gets the raw attributes buffer.</summary>
    public Span<uint> AttributesBuffer => _attributes.AsSpan();

    /// <summary>Gets the raw character array (for direct access).</summary>
    public ReadOnlySpan<uint> GetChars() => _chars.AsSpan(0, (int)(Width * Height));

    /// <summary>Gets the raw foreground color array.</summary>
    public ReadOnlySpan<Rgba> GetFgColors() => _fg.AsSpan(0, (int)(Width * Height));

    /// <summary>Gets the raw background color array.</summary>
    public ReadOnlySpan<Rgba> GetBgColors() => _bg.AsSpan(0, (int)(Width * Height));

    /// <summary>Gets the raw attributes array.</summary>
    public ReadOnlySpan<uint> GetAttributes() => _attributes.AsSpan(0, (int)(Width * Height));

    #endregion

    #region Grapheme Encoding Helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsGraphemeChar(uint c) => (c & 0xC000_0000) == 0x8000_0000;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsContinuationChar(uint c) => (c & 0xC000_0000) == 0xC000_0000;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GraphemeIdFromChar(uint c) => c & GraphemeIdMask;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint CharRightExtent(uint c) => (c >> CharExtRightShift) & CharExtMask;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint CharLeftExtent(uint c) => (c >> CharExtLeftShift) & CharExtMask;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint PackContinuation(uint leftExt, uint rightExt, uint id)
        => CharFlagContinuation | (rightExt << CharExtRightShift) | (leftExt << CharExtLeftShift) | id;

    /// <summary>
    /// Gets the display width of an encoded character value (matching Zig encodedCharWidth).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint EncodedCharWidth(uint c)
    {
        if (IsContinuationChar(c))
            return CharLeftExtent(c) + 1 + CharRightExtent(c);
        if (IsGraphemeChar(c))
            return CharRightExtent(c) + 1;
        return 1;
    }

    #endregion

    #region Attribute Helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetLinkId(uint attrs) => attrs >> 8;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetBaseAttributes(uint attrs) => attrs & 0xFF;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint SetLinkId(uint attrs, uint linkId) => (attrs & 0xFF) | (linkId << 8);

    #endregion
}
