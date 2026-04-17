namespace OpenTui.Cli;

/// <summary>Interactive select prompt with arrow-key navigation.</summary>
/// <typeparam name="T">The type of the value to select.</typeparam>
public sealed class CliSelect<T>
{
    private readonly string _prompt;
    private readonly List<SelectOption<T>> _options = [];
    private int _pageSize = 10;

    public CliSelect(string prompt) => _prompt = prompt;

    /// <summary>Adds simple string choices (value = label for string type).</summary>
    public CliSelect<T> AddChoices(params T[] values)
    {
        foreach (var v in values)
            _options.Add(new SelectOption<T>(v?.ToString() ?? "", v));
        return this;
    }

    /// <summary>Adds a labeled option.</summary>
    public CliSelect<T> AddOption(string label, T value, string? description = null)
    {
        _options.Add(new SelectOption<T>(label, value, description));
        return this;
    }

    /// <summary>Sets the page size for long lists.</summary>
    public CliSelect<T> SetPageSize(int size) { _pageSize = size; return this; }

    public T Prompt() => Prompt(AnsiConsole.Instance);

    public T Prompt(ICliConsole console)
    {
        if (_options.Count == 0)
            throw new InvalidOperationException("No options added to select prompt.");

        console.WriteLine(new StyledText(new StyledChunk(_prompt, Fg: Rgba.FromHex("#87CEEB"))));

        int selected = 0;
        int scrollOffset = 0;
        bool done = false;

        console.HideCursor();
        try
        {
            // Initial render
            int renderStart = -1; // will be set on first render
            while (!done)
            {
                // Calculate visible range
                int visibleCount = Math.Min(_pageSize, _options.Count);
                if (selected < scrollOffset) scrollOffset = selected;
                if (selected >= scrollOffset + visibleCount) scrollOffset = selected - visibleCount + 1;

                // Move cursor up to redraw (after first render)
                if (renderStart >= 0)
                {
                    for (int i = 0; i < visibleCount; i++)
                        console.Write("\x1b[A"); // move up
                    console.Write("\r"); // carriage return
                }
                else
                {
                    renderStart = 0;
                }

                // Render options
                for (int i = scrollOffset; i < scrollOffset + visibleCount && i < _options.Count; i++)
                {
                    var opt = _options[i];
                    bool isSelected = i == selected;
                    string prefix = isSelected ? "❯ " : "  ";
                    var fg = isSelected ? Rgba.FromHex("#00FF00") : Rgba.White;

                    string line = $"{prefix}{opt.Label}";
                    if (opt.Description is not null)
                        line += $"  {opt.Description}";

                    // Pad to full width to clear previous content
                    line = line.PadRight(console.Width - 1);

                    console.Write("\r"); // start of line
                    console.WriteLine(new StyledText(new StyledChunk(line, Fg: fg)));
                }

                // Read key
                var key = console.ReadKey(true);
                switch (key.Key)
                {
                    case ConsoleKey.UpArrow:
                    case ConsoleKey.K:
                        if (selected > 0) selected--;
                        break;
                    case ConsoleKey.DownArrow:
                    case ConsoleKey.J:
                        if (selected < _options.Count - 1) selected++;
                        break;
                    case ConsoleKey.Enter:
                        done = true;
                        break;
                    case ConsoleKey.Escape:
                        throw new OperationCanceledException("Selection cancelled.");
                }
            }
        }
        finally
        {
            console.ShowCursor();
        }

        return _options[selected].Value;
    }
}
