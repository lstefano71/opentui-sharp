namespace OpenTui.Core;

/// <summary>
/// Options for Select renderable.
/// Matches TypeScript SelectRenderableOptions.
/// </summary>
public class SelectOptions : BoxOptions
{
    /// <summary>
    /// Gets or sets the text color.
    /// </summary>
    public Rgba? TextColor { get; init; }
    /// <summary>
    /// Gets or sets the focused background color.
    /// </summary>
    public Rgba? FocusedBackgroundColor { get; init; }
    /// <summary>
    /// Gets or sets the focused text color.
    /// </summary>
    public Rgba? FocusedTextColor { get; init; }
    /// <summary>
    /// Gets or sets the selected background color.
    /// </summary>
    public Rgba? SelectedBackgroundColor { get; init; }
    /// <summary>
    /// Gets or sets the selected text color.
    /// </summary>
    public Rgba? SelectedTextColor { get; init; }
    /// <summary>
    /// Gets or sets the description color.
    /// </summary>
    public Rgba? DescriptionColor { get; init; }
    /// <summary>
    /// Gets or sets the selected description color.
    /// </summary>
    public Rgba? SelectedDescriptionColor { get; init; }
    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    public SelectOption[]? Options { get; init; }
    /// <summary>
    /// Gets or sets the selected index.
    /// </summary>
    public int SelectedIndex { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether show scroll indicator.
    /// </summary>
    public bool ShowScrollIndicator { get; init; }
    /// <summary>
    /// Gets or sets the wrap selection.
    /// </summary>
    public bool WrapSelection { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether show description.
    /// </summary>
    public bool ShowDescription { get; init; } = true;
    /// <summary>
    /// Gets or sets the item spacing.
    /// </summary>
    public int ItemSpacing { get; init; }
    /// <summary>
    /// Gets or sets the fast scroll step.
    /// </summary>
    public int FastScrollStep { get; init; } = 5;
}

/// <summary>A single option in a Select.</summary>
public sealed class SelectOption
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public required string Name { get; init; }
    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string? Description { get; init; }
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public object? Value { get; init; }
}
