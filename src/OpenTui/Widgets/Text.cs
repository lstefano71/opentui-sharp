namespace OpenTui;

/// <summary>
/// A widget that displays styled text content.
/// </summary>
public class Text : Widget
{
    private StyledText _content;

    /// <summary>Creates a Text widget from a plain string.</summary>
    public Text(string text) => _content = text;

    /// <summary>Creates a Text widget from styled text.</summary>
    public Text(StyledText content) => _content = content;

    /// <summary>The text content to display.</summary>
    public StyledText Content
    {
        get => _content;
        set => _content = value;
    }

    /// <summary>Text attributes (bold, italic, etc.).</summary>
    public TextAttribute Attributes { get; set; } = TextAttribute.None;

    /// <inheritdoc />
    protected internal override void Draw(nint buffer, int offsetX, int offsetY)
    {
        // Will use native bufferDrawText once high-level wrappers are available
    }
}
