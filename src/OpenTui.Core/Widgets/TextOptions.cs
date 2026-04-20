namespace OpenTui.Core;

/// <summary>
/// Options for creating a TextBufferRenderable.
/// Covers text styling, wrapping, truncation, and scrolling.
/// </summary>
public class TextBufferOptions : RenderableOptions
{
    /// <summary>
    /// Gets or sets the fg.
    /// </summary>
    public Rgba? Fg { get; init; }
    /// <summary>
    /// Gets or sets the bg.
    /// </summary>
    public Rgba? Bg { get; init; }
    /// <summary>
    /// Gets or sets the selection bg.
    /// </summary>
    public Rgba? SelectionBg { get; init; }
    /// <summary>
    /// Gets or sets the selection fg.
    /// </summary>
    public Rgba? SelectionFg { get; init; }
    /// <summary>
    /// Gets or sets the selectable.
    /// </summary>
    public bool Selectable { get; init; } = true;
    /// <summary>
    /// Gets or sets the attributes.
    /// </summary>
    public TextAttributes Attributes { get; init; } = TextAttributes.None;
    /// <summary>
    /// Gets or sets the wrap mode.
    /// </summary>
    public WrapMode WrapMode { get; init; } = WrapMode.Word;
    /// <summary>
    /// Gets or sets the truncate.
    /// </summary>
    public bool Truncate { get; init; }
    /// <summary>
    /// Gets or sets the tab indicator.
    /// </summary>
    public string? TabIndicator { get; init; }
    /// <summary>
    /// Gets or sets the tab indicator color.
    /// </summary>
    public Rgba? TabIndicatorColor { get; init; }
}

/// <summary>
/// Options for creating a TextRenderable (extends TextBufferOptions with content).
/// </summary>
public class TextOptions : TextBufferOptions
{
    /// <summary>
    /// Gets or sets the content.
    /// </summary>
    public string? Content { get; init; }
    /// <summary>
    /// Gets or sets the styled content.
    /// </summary>
    public StyledText? StyledContent { get; init; }
}
