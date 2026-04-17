namespace OpenTui.Cli;

/// <summary>Renders a horizontal rule with optional title text.</summary>
public sealed class CliRule
{
    private readonly string? _title;
    private Rgba? _color;
    private char _ruleChar = '─';

    public CliRule(string? title = null) => _title = title;

    /// <summary>Sets the rule color.</summary>
    public CliRule SetColor(Rgba color) { _color = color; return this; }

    /// <summary>Sets the character used for the rule line.</summary>
    public CliRule SetChar(char c) { _ruleChar = c; return this; }

    /// <summary>Writes the rule to the default console.</summary>
    public void Write() => Write(AnsiConsole.Instance);

    /// <summary>Writes the rule to the specified console.</summary>
    public void Write(ICliConsole console)
    {
        int width = console.Width;

        if (_title is null)
        {
            var line = new string(_ruleChar, width);
            WriteStyled(console, line);
            return;
        }

        string titleStr = $" {_title} ";
        int remaining = width - titleStr.Length;
        if (remaining < 4)
        {
            WriteStyled(console, titleStr);
            return;
        }

        int leftLen = remaining / 2;
        int rightLen = remaining - leftLen;
        string line2 = new string(_ruleChar, leftLen) + titleStr + new string(_ruleChar, rightLen);
        WriteStyled(console, line2);
    }

    private void WriteStyled(ICliConsole console, string text)
    {
        if (_color is { } color)
            console.WriteLine(new StyledText(new StyledChunk(text, Fg: color)));
        else
            console.WriteLine(text);
    }
}
