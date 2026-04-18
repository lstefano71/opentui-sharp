namespace OpenTui.Core;

/// <summary>
/// Options for creating a TextBufferRenderable.
/// Covers text styling, wrapping, truncation, and scrolling.
/// </summary>
public class TextBufferOptions : RenderableOptions
{
    public Rgba? Fg { get; init; }
    public Rgba? Bg { get; init; }
    public Rgba? SelectionBg { get; init; }
    public Rgba? SelectionFg { get; init; }
    public bool Selectable { get; init; } = true;
    public TextAttributes Attributes { get; init; } = TextAttributes.None;
    public WrapMode WrapMode { get; init; } = WrapMode.Word;
    public bool Truncate { get; init; }
    public string? TabIndicator { get; init; }
    public Rgba? TabIndicatorColor { get; init; }
}

/// <summary>
/// Options for creating a TextRenderable (extends TextBufferOptions with content).
/// </summary>
public class TextOptions : TextBufferOptions
{
    public string? Content { get; init; }
    public StyledText? StyledContent { get; init; }
}
