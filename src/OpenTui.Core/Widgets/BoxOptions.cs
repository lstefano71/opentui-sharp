namespace OpenTui.Core;

/// <summary>
/// Options for creating a Box renderable.
/// Extends RenderableOptions with border, background, title, and gap configuration.
/// Matches TypeScript BoxOptions from Box.ts.
/// </summary>
public class BoxOptions : RenderableOptions
{
    public Rgba? BackgroundColor { get; init; }
    public BorderStyle BorderStyle { get; init; } = BorderStyle.Single;
    public bool Border { get; init; }
    public BorderSides? BorderSidesOverride { get; init; }
    public Rgba? BorderColor { get; init; }
    public Rgba? FocusedBorderColor { get; init; }
    public BorderCharacters? CustomBorderChars { get; init; }
    public bool ShouldFill { get; init; } = true;
    public string? Title { get; init; }
    public TitleAlignment TitleAlignment { get; init; } = TitleAlignment.Left;
    public string? BottomTitle { get; init; }
    public TitleAlignment BottomTitleAlignment { get; init; } = TitleAlignment.Left;
    public bool BoxFocusable { get; init; }
}
