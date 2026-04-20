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

    /// <summary>
    /// Initializes a new instance of the CliFiglet class.
    /// </summary>
    /// <param name="text">The text value.</param>
    public CliFiglet(string text) => _text = text.ToUpperInvariant();
    /// <summary>
    /// Gets or sets the fg.
    /// </summary>
    public Rgba? Fg { get; set; }
    /// <summary>
    /// Performs use console.
    /// </summary>
    /// <param name="console">The console.</param>
    /// <returns>The result of use console.</returns>
    public CliFiglet UseConsole(ICliConsole console) { _console = console; return this; }

    /// <summary>
    /// Performs write.
    /// </summary>
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
