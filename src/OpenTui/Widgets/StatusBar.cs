namespace OpenTui;

/// <summary>Describes a single section within a <see cref="StatusBar"/>.</summary>
/// <param name="Text">Display text for the section.</param>
/// <param name="Fg">Optional foreground color override.</param>
/// <param name="Bg">Optional background color override.</param>
/// <param name="MinWidth">Minimum column width for this section.</param>
public record StatusSection(string Text, Rgba? Fg = null, Rgba? Bg = null, int MinWidth = 0);

/// <summary>
/// A bottom status bar with left-aligned and right-aligned sections,
/// typically used to display mode, file info, or key hints.
/// </summary>
public class StatusBar : Widget
{
    /// <summary>Sections rendered on the left side of the bar.</summary>
    public IList<StatusSection> LeftSections { get; set; } = new List<StatusSection>();

    /// <summary>Sections rendered on the right side of the bar.</summary>
    public IList<StatusSection> RightSections { get; set; } = new List<StatusSection>();

    /// <summary>Default background color for the entire status bar.</summary>
    public Rgba? DefaultBg { get; set; }

    /// <inheritdoc />
    protected internal override void Draw(nint buffer, int offsetX, int offsetY)
    {
        // TODO: wire to native buffer
    }
}
