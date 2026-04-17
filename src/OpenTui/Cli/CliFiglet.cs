using System.Text;

namespace OpenTui.Cli;

/// <summary>Renders large ASCII art text using a simple block font.</summary>
public sealed class CliFiglet
{
    private static readonly Dictionary<char, string[]> BlockFont = new()
    {
        ['A'] = ["  █  ", " █ █ ", "█████", "█   █", "█   █"],
        ['B'] = ["████ ", "█   █", "████ ", "█   █", "████ "],
        ['C'] = [" ████", "█    ", "█    ", "█    ", " ████"],
        ['D'] = ["████ ", "█   █", "█   █", "█   █", "████ "],
        ['E'] = ["█████", "█    ", "████ ", "█    ", "█████"],
        ['F'] = ["█████", "█    ", "████ ", "█    ", "█    "],
        ['G'] = [" ████", "█    ", "█  ██", "█   █", " ████"],
        ['H'] = ["█   █", "█   █", "█████", "█   █", "█   █"],
        ['I'] = ["█████", "  █  ", "  █  ", "  █  ", "█████"],
        ['J'] = ["█████", "   █ ", "   █ ", "█  █ ", " ██  "],
        ['K'] = ["█  █ ", "█ █  ", "██   ", "█ █  ", "█  █ "],
        ['L'] = ["█    ", "█    ", "█    ", "█    ", "█████"],
        ['M'] = ["█   █", "██ ██", "█ █ █", "█   █", "█   █"],
        ['N'] = ["█   █", "██  █", "█ █ █", "█  ██", "█   █"],
        ['O'] = [" ███ ", "█   █", "█   █", "█   █", " ███ "],
        ['P'] = ["████ ", "█   █", "████ ", "█    ", "█    "],
        ['Q'] = [" ███ ", "█   █", "█ █ █", "█  █ ", " ██ █"],
        ['R'] = ["████ ", "█   █", "████ ", "█ █  ", "█  █ "],
        ['S'] = [" ████", "█    ", " ███ ", "    █", "████ "],
        ['T'] = ["█████", "  █  ", "  █  ", "  █  ", "  █  "],
        ['U'] = ["█   █", "█   █", "█   █", "█   █", " ███ "],
        ['V'] = ["█   █", "█   █", "█   █", " █ █ ", "  █  "],
        ['W'] = ["█   █", "█   █", "█ █ █", "██ ██", "█   █"],
        ['X'] = ["█   █", " █ █ ", "  █  ", " █ █ ", "█   █"],
        ['Y'] = ["█   █", " █ █ ", "  █  ", "  █  ", "  █  "],
        ['Z'] = ["█████", "   █ ", "  █  ", " █   ", "█████"],
        [' '] = ["     ", "     ", "     ", "     ", "     "],
        ['!'] = ["  █  ", "  █  ", "  █  ", "     ", "  █  "],
        ['0'] = [" ███ ", "█   █", "█   █", "█   █", " ███ "],
        ['1'] = ["  █  ", " ██  ", "  █  ", "  █  ", " ███ "],
        ['2'] = [" ███ ", "    █", " ███ ", "█    ", "█████"],
        ['3'] = ["████ ", "    █", " ███ ", "    █", "████ "],
        ['4'] = ["█   █", "█   █", "█████", "    █", "    █"],
        ['5'] = ["█████", "█    ", "████ ", "    █", "████ "],
        ['6'] = [" ████", "█    ", "████ ", "█   █", " ███ "],
        ['7'] = ["█████", "   █ ", "  █  ", " █   ", "█    "],
        ['8'] = [" ███ ", "█   █", " ███ ", "█   █", " ███ "],
        ['9'] = [" ███ ", "█   █", " ████", "    █", "████ "],
    };

    private readonly string _text;
    private ICliConsole _console = AnsiConsole.Instance;

    public CliFiglet(string text) => _text = text.ToUpperInvariant();
    public Rgba? Fg { get; set; }
    public CliFiglet UseConsole(ICliConsole console) { _console = console; return this; }

    public void Write()
    {
        const int lineCount = 5;
        for (int line = 0; line < lineCount; line++)
        {
            var sb = new StringBuilder();
            foreach (char ch in _text)
            {
                if (BlockFont.TryGetValue(ch, out var glyph) && line < glyph.Length)
                    sb.Append(glyph[line]);
                else
                    sb.Append("     ");
                sb.Append(' ');
            }
            _console.WriteLine(sb.ToString().TrimEnd());
        }
    }
}
