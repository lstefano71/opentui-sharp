namespace OpenTui.Core;

/// <summary>
/// Options for Select renderable.
/// Matches TypeScript SelectRenderableOptions.
/// </summary>
public class SelectOptions : BoxOptions
{
    public Rgba? TextColor { get; init; }
    public Rgba? FocusedBackgroundColor { get; init; }
    public Rgba? FocusedTextColor { get; init; }
    public Rgba? SelectedBackgroundColor { get; init; }
    public Rgba? SelectedTextColor { get; init; }
    public Rgba? DescriptionColor { get; init; }
    public Rgba? SelectedDescriptionColor { get; init; }
    public SelectOption[]? Options { get; init; }
    public int SelectedIndex { get; init; }
    public bool ShowScrollIndicator { get; init; }
    public bool WrapSelection { get; init; }
    public bool ShowDescription { get; init; } = true;
    public int ItemSpacing { get; init; }
    public int FastScrollStep { get; init; } = 5;
}

/// <summary>A single option in a Select.</summary>
public sealed class SelectOption
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public object? Value { get; init; }
}
