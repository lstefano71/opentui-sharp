namespace OpenTui;

/// <summary>
/// A TUI table widget that renders column headers and data rows with
/// optional selection highlighting and borders.
/// </summary>
public class Table : Widget
{
    /// <summary>Column header labels.</summary>
    public IList<string> Columns { get; set; } = new List<string>();

    /// <summary>Data rows. Each element is an array of cell values matching <see cref="Columns"/>.</summary>
    public IList<string[]> Rows { get; set; } = new List<string[]>();

    /// <summary>Border style for the table grid. Null means no borders.</summary>
    public BorderStyle? Border { get; set; } = BorderStyle.Single;

    /// <summary>Whether rows can be selected/highlighted by the user.</summary>
    public bool Selectable { get; set; }

    /// <summary>Index of the currently selected row, or -1 for no selection.</summary>
    public int SelectedRow { get; set; } = -1;

    /// <summary>Foreground color for the header row.</summary>
    public Rgba? HeaderFg { get; set; }

    /// <summary>Background color for the selected row.</summary>
    public Rgba? SelectedRowBg { get; set; }

    /// <summary>Raised when a row is selected by the user.</summary>
    public event Action<int>? OnRowSelected;

    /// <inheritdoc />
    protected internal override void Draw(nint buffer, int offsetX, int offsetY)
    {
        // TODO: wire to native buffer
    }
}
