namespace OpenTui;

/// <summary>
/// A container widget that can draw a border and hold child widgets.
/// The primary building block for TUI layouts.
/// </summary>
public class Box : Widget
{
    /// <summary>Border style. Null means no border.</summary>
    public BorderStyle? Border { get; set; }

    /// <summary>Optional title displayed in the top border.</summary>
    public string? Title { get; set; }

    /// <summary>Title alignment within the border.</summary>
    public TitleAlignment TitleAlignment { get; set; } = TitleAlignment.Left;

    /// <inheritdoc />
    protected internal override void Draw(nint buffer, int offsetX, int offsetY)
    {
        int w = (int)Layout.LayoutWidth;
        int h = (int)Layout.LayoutHeight;

        if (w <= 0 || h <= 0) return;

        // Fill background if set — will call native bufferFillRect once available
        if (Bg is { } bg)
        {
        }

        // Draw border if specified — will call native bufferDrawBox once available
        if (Border is { } borderStyle)
        {
        }
    }
}
