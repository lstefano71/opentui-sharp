namespace OpenTui.Core;

/// <summary>
/// Options for creating a Box renderable.
/// Extends RenderableOptions with border, background, title, and gap configuration.
/// Matches TypeScript BoxOptions from Box.ts.
/// </summary>
public class BoxOptions : RenderableOptions
{
    /// <summary>
    /// Gets or sets the background color.
    /// </summary>
    public Rgba? BackgroundColor { get; init; }
    /// <summary>
    /// Gets or sets the border style.
    /// </summary>
    public BorderStyle BorderStyle { get; init; } = BorderStyle.Single;
    /// <summary>
    /// Gets or sets the border.
    /// </summary>
    public bool Border { get; init; }
    /// <summary>
    /// Gets or sets the border sides override.
    /// </summary>
    public BorderSides? BorderSidesOverride { get; init; }
    /// <summary>
    /// Gets or sets the border color.
    /// </summary>
    public Rgba? BorderColor { get; init; }
    /// <summary>
    /// Gets or sets the focused border color.
    /// </summary>
    public Rgba? FocusedBorderColor { get; init; }
    /// <summary>
    /// Gets or sets the custom border chars.
    /// </summary>
    public BorderCharacters? CustomBorderChars { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether should fill.
    /// </summary>
    public bool ShouldFill { get; init; } = true;
    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string? Title { get; init; }
    /// <summary>
    /// Gets or sets the title alignment.
    /// </summary>
    public TitleAlignment TitleAlignment { get; init; } = TitleAlignment.Left;
    /// <summary>
    /// Gets or sets the bottom title.
    /// </summary>
    public string? BottomTitle { get; init; }
    /// <summary>
    /// Gets or sets the bottom title alignment.
    /// </summary>
    public TitleAlignment BottomTitleAlignment { get; init; } = TitleAlignment.Left;
    /// <summary>
    /// Gets or sets the box focusable.
    /// </summary>
    public bool BoxFocusable { get; init; }
}
