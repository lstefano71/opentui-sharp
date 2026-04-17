namespace OpenTui;

/// <summary>
/// A raw pixel drawing surface that stores an RGBA color per cell.
/// Useful for custom rendering, charts, or image display.
/// </summary>
public class FrameBuffer : Widget
{
    private Rgba[,]? _pixels;

    /// <summary>Width of the pixel buffer.</summary>
    public int PixelWidth { get; private set; }

    /// <summary>Height of the pixel buffer.</summary>
    public int PixelHeight { get; private set; }

    /// <summary>Resizes (and clears) the pixel buffer to the given dimensions.</summary>
    public void Resize(int width, int height)
    {
        PixelWidth = width;
        PixelHeight = height;
        _pixels = new Rgba[width, height];
    }

    /// <summary>Sets the color of a single pixel. Out-of-bounds writes are silently ignored.</summary>
    public void SetPixel(int x, int y, Rgba color)
    {
        if (_pixels is not null && x >= 0 && x < PixelWidth && y >= 0 && y < PixelHeight)
            _pixels[x, y] = color;
    }

    /// <summary>Gets the color of a single pixel. Returns <see cref="Rgba.Transparent"/> if out of bounds.</summary>
    public Rgba GetPixel(int x, int y) =>
        _pixels is not null && x >= 0 && x < PixelWidth && y >= 0 && y < PixelHeight
            ? _pixels[x, y]
            : Rgba.Transparent;

    /// <summary>Fills the entire buffer with the specified color, or <see cref="Rgba.Transparent"/> if null.</summary>
    public void Clear(Rgba? color = null)
    {
        if (_pixels is null) return;

        var fill = color ?? Rgba.Transparent;
        for (int x = 0; x < PixelWidth; x++)
        {
            for (int y = 0; y < PixelHeight; y++)
            {
                _pixels[x, y] = fill;
            }
        }
    }

    /// <inheritdoc />
    protected internal override void Draw(nint buffer, int offsetX, int offsetY)
    {
        // TODO: wire to native buffer
    }
}
