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
    protected internal override void Draw(NativeBuffer buffer, int offsetX, int offsetY)
    {
        int w = (int)Layout.LayoutWidth;
        int h = (int)Layout.LayoutHeight;
        if (w <= 0 || h <= 0) return;

        var barBg = DefaultBg ?? Rgba.FromInts(32, 32, 32);
        buffer.FillRect((uint)offsetX, (uint)offsetY, (uint)w, (uint)h, barBg);

        // Draw left sections
        int xPos = offsetX;
        foreach (var section in LeftSections)
        {
            var sectionBg = section.Bg ?? barBg;
            var sectionFg = section.Fg ?? ResolvedFg;
            string text = $" {section.Text} ";
            int sectionW = Math.Max(text.Length, section.MinWidth);
            buffer.FillRect((uint)xPos, (uint)offsetY, (uint)sectionW, (uint)h, sectionBg);
            buffer.DrawText(text, (uint)xPos, (uint)offsetY, sectionFg, sectionBg);
            xPos += sectionW;
        }

        // Draw right sections (right-aligned)
        int rightX = offsetX + w;
        for (int i = RightSections.Count - 1; i >= 0; i--)
        {
            var section = RightSections[i];
            var sectionBg = section.Bg ?? barBg;
            var sectionFg = section.Fg ?? ResolvedFg;
            string text = $" {section.Text} ";
            int sectionW = Math.Max(text.Length, section.MinWidth);
            rightX -= sectionW;
            buffer.FillRect((uint)rightX, (uint)offsetY, (uint)sectionW, (uint)h, sectionBg);
            buffer.DrawText(text, (uint)rightX, (uint)offsetY, sectionFg, sectionBg);
        }
    }
}
