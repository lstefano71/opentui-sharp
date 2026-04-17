namespace OpenTui.Cli;

/// <summary>Renders a bordered panel with content text.</summary>
public sealed class CliPanel
{
    private readonly string _content;
    private BorderStyle _border = BorderStyle.Rounded;
    private string? _title;

    public CliPanel(string content) => _content = content;

    public CliPanel SetBorder(BorderStyle style) { _border = style; return this; }
    public CliPanel SetTitle(string title) { _title = title; return this; }

    public void Write() => Write(AnsiConsole.Instance);

    public void Write(ICliConsole console)
    {
        var chars = BorderCharacters.ForStyle(_border);
        var lines = _content.Split('\n');
        int maxLen = lines.Max(l => l.Length);
        int titleNeed = _title is not null ? _title.Length + 5 : 0;
        int innerWidth = Math.Min(Math.Max(maxLen + 2, titleNeed), console.Width - 2);

        // Top
        var top = $"{chars.TopLeft}{new string(chars.Horizontal, innerWidth)}{chars.TopRight}";
        if (_title is not null)
        {
            string t = $" {_title} ";
            if (t.Length < innerWidth - 2)
                top = $"{chars.TopLeft}{chars.Horizontal}{t}{new string(chars.Horizontal, innerWidth - t.Length - 1)}{chars.TopRight}";
        }
        console.WriteLine(top);

        // Content
        foreach (var line in lines)
        {
            string padded = $" {line}".PadRight(innerWidth);
            if (padded.Length > innerWidth)
                padded = padded[..innerWidth];
            console.WriteLine($"{chars.Vertical}{padded}{chars.Vertical}");
        }

        // Bottom
        console.WriteLine($"{chars.BottomLeft}{new string(chars.Horizontal, innerWidth)}{chars.BottomRight}");
    }
}
