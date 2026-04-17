namespace OpenTui;

/// <summary>Describes a single expandable/collapsible section inside an <see cref="Accordion"/>.</summary>
/// <param name="Title">Header text for the section.</param>
/// <param name="Content">Widget rendered when the section is expanded.</param>
/// <param name="IsExpanded">Whether the section is initially expanded.</param>
public record AccordionSection(string Title, Widget Content, bool IsExpanded = false);

/// <summary>
/// A widget containing vertically stacked sections that can be independently
/// expanded or collapsed.
/// </summary>
public class Accordion : Widget
{
    /// <summary>The list of accordion sections.</summary>
    public IList<AccordionSection> Sections { get; set; } = new List<AccordionSection>();

    /// <summary>When false only one section may be expanded at a time.</summary>
    public bool AllowMultipleExpanded { get; set; }

    /// <summary>Toggles the expanded state of the section at <paramref name="index"/>.</summary>
    public void Toggle(int index)
    {
        if (index < 0 || index >= Sections.Count) return;

        var section = Sections[index];
        bool newState = !section.IsExpanded;

        if (newState && !AllowMultipleExpanded)
        {
            for (int i = 0; i < Sections.Count; i++)
            {
                if (i != index && Sections[i].IsExpanded)
                    Sections[i] = Sections[i] with { IsExpanded = false };
            }
        }

        Sections[index] = section with { IsExpanded = newState };
    }

    /// <summary>Expands all sections.</summary>
    public void ExpandAll()
    {
        for (int i = 0; i < Sections.Count; i++)
            Sections[i] = Sections[i] with { IsExpanded = true };
    }

    /// <summary>Collapses all sections.</summary>
    public void CollapseAll()
    {
        for (int i = 0; i < Sections.Count; i++)
            Sections[i] = Sections[i] with { IsExpanded = false };
    }

    /// <inheritdoc />
    protected internal override void Draw(NativeBuffer buffer, int offsetX, int offsetY)
    {
        // TODO: wire to native buffer
    }
}
