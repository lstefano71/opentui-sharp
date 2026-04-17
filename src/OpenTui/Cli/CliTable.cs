namespace OpenTui.Cli;

/// <summary>
/// Renders a formatted table to the console with borders and alignment.
/// Fluent API inspired by Spectre.Console's Table.
/// </summary>
public sealed class CliTable
{
    private readonly List<ColumnDef> _columns = [];
    private readonly List<string[]> _rows = [];
    private BorderStyle _border = BorderStyle.Single;
    private string? _title;
    private TitleAlignment _titleAlignment = TitleAlignment.Left;

    /// <summary>Border style for the table.</summary>
    public CliTable SetBorder(BorderStyle style) { _border = style; return this; }

    /// <summary>Sets the table title.</summary>
    public CliTable SetTitle(string title, TitleAlignment align = TitleAlignment.Left)
    {
        _title = title;
        _titleAlignment = align;
        return this;
    }

    /// <summary>Adds columns by header name.</summary>
    public CliTable AddColumn(params string[] headers)
    {
        foreach (var h in headers)
            _columns.Add(new ColumnDef(h));
        return this;
    }

    /// <summary>Adds a row of values. Must match column count.</summary>
    public CliTable AddRow(params string[] values)
    {
        _rows.Add(values);
        return this;
    }

    /// <summary>Writes the table to the default console.</summary>
    public void Write() => Write(AnsiConsole.Instance);

    /// <summary>Writes the table to the specified console.</summary>
    public void Write(ICliConsole console)
    {
        var chars = BorderCharacters.ForStyle(_border);
        int[] widths = CalculateColumnWidths(console.Width);

        // Top border
        WriteHorizontalBorder(console, chars, widths, chars.TopLeft, chars.TopT, chars.TopRight, _title);

        // Header row
        WriteRow(console, chars, _columns.Select(c => c.Header).ToArray(), widths, isBold: true);

        // Header separator
        WriteHorizontalBorder(console, chars, widths, chars.LeftT, chars.Cross, chars.RightT);

        // Data rows
        foreach (var row in _rows)
            WriteRow(console, chars, row, widths);

        // Bottom border
        WriteHorizontalBorder(console, chars, widths, chars.BottomLeft, chars.BottomT, chars.BottomRight);
    }

    private int[] CalculateColumnWidths(int maxWidth)
    {
        int colCount = _columns.Count;
        int[] widths = new int[colCount];

        for (int i = 0; i < colCount; i++)
        {
            widths[i] = _columns[i].Header.Length;
            foreach (var row in _rows)
            {
                if (i < row.Length)
                    widths[i] = Math.Max(widths[i], row[i].Length);
            }
        }

        // Ensure table is wide enough for title
        if (_title is not null && colCount > 0)
        {
            int titleWidth = _title.Length + 5; // " title " + borders + margin
            int overhead = (colCount + 1) + colCount * 2;
            int totalWidth = widths.Sum() + overhead;
            if (totalWidth < titleWidth)
                widths[0] += titleWidth - totalWidth;
        }

        // Constrain to available width (borders + separators + padding)
        {
            int overhead = (colCount + 1) * 1 + colCount * 2;
            int totalContent = widths.Sum();
            int available = maxWidth - overhead;

            if (totalContent > available && available > colCount)
            {
                float scale = (float)available / totalContent;
                for (int i = 0; i < colCount; i++)
                    widths[i] = Math.Max(1, (int)(widths[i] * scale));
            }
        }

        return widths;
    }

    private static void WriteHorizontalBorder(ICliConsole console, BorderCharacters chars,
        int[] widths, char left, char mid, char right, string? title = null)
    {
        var line = new System.Text.StringBuilder();
        line.Append(left);
        for (int i = 0; i < widths.Length; i++)
        {
            if (i > 0) line.Append(mid);
            line.Append(chars.Horizontal, widths[i] + 2);
        }
        line.Append(right);

        if (title is not null)
        {
            // Insert title into top border (at offset 2, needs room before right corner)
            string titleStr = $" {title} ";
            if (titleStr.Length < line.Length - 2)
                line.Remove(2, titleStr.Length).Insert(2, titleStr);
        }

        console.WriteLine(line.ToString());
    }

    private static void WriteRow(ICliConsole console, BorderCharacters chars,
        string[] values, int[] widths, bool isBold = false)
    {
        var line = new System.Text.StringBuilder();
        line.Append(chars.Vertical);
        for (int i = 0; i < widths.Length; i++)
        {
            string val = i < values.Length ? values[i] : "";
            if (val.Length > widths[i])
                val = val[..(widths[i] - 1)] + "…";

            line.Append(' ');
            line.Append(val.PadRight(widths[i]));
            line.Append(' ');
            line.Append(chars.Vertical);
        }

        if (isBold)
            console.WriteLine(new StyledText(new StyledChunk(line.ToString(), Attributes: TextAttribute.Bold)));
        else
            console.WriteLine(line.ToString());
    }

    private sealed record ColumnDef(string Header);
}
