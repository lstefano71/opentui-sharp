namespace OpenTui;

/// <summary>
/// A standalone scrollbar widget that can be placed beside scrollable content
/// to indicate position and provide scroll interaction.
/// </summary>
public class ScrollBar : Widget
{
    /// <summary>Total size of the scrollable content (rows or columns).</summary>
    public int ContentSize { get; set; }

    /// <summary>Size of the visible viewport (rows or columns).</summary>
    public int ViewportSize { get; set; }

    /// <summary>Current scroll position.</summary>
    public int Position { get; set; }

    /// <summary>Whether the scrollbar is vertical (true) or horizontal (false).</summary>
    public bool IsVertical { get; set; } = true;

    /// <summary>Character used for the scrollbar thumb.</summary>
    public char ThumbChar { get; set; } = '█';

    /// <summary>Character used for the scrollbar track.</summary>
    public char TrackChar { get; set; } = '░';

    /// <inheritdoc />
    protected internal override void Draw(NativeBuffer buffer, int offsetX, int offsetY)
    {
        // TODO: wire to native buffer
    }
}
