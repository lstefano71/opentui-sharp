namespace OpenTui.Cli;

/// <summary>
/// Default <see cref="ICliConsole"/> implementation using ANSI escape codes.
/// </summary>
public sealed class AnsiConsole : ICliConsole
{
    /// <summary>Shared default instance.</summary>
    public static readonly AnsiConsole Instance = new();

    public int Width => Console.WindowWidth;
    public int Height => Console.WindowHeight;
    public bool SupportsAnsi => !Console.IsOutputRedirected;
    public bool IsInteractive => !Console.IsInputRedirected && !Console.IsOutputRedirected;

    public void Write(string text) => Console.Write(text);
    public void WriteLine(string text = "") => Console.WriteLine(text);

    public void Write(StyledText text)
    {
        foreach (var chunk in text.Chunks)
            WriteChunk(chunk);
    }

    public void WriteLine(StyledText text)
    {
        Write(text);
        Console.WriteLine();
    }

    public string? ReadLine() => Console.ReadLine();
    public ConsoleKeyInfo ReadKey(bool intercept = false) => Console.ReadKey(intercept);
    public void SetCursorPosition(int left, int top) => Console.SetCursorPosition(left, top);
    public void HideCursor() => Console.Write("\x1b[?25l");
    public void ShowCursor() => Console.Write("\x1b[?25h");
    public bool CursorVisible
    {
        get => true; // Can't reliably read this
        set { if (value) ShowCursor(); else HideCursor(); }
    }

    private void WriteChunk(StyledChunk chunk)
    {
        if (!SupportsAnsi)
        {
            Console.Write(chunk.Text);
            return;
        }

        var codes = new List<string>();

        if (chunk.Fg is { } fg)
        {
            var (r, g, b, _) = fg.ToInts();
            codes.Add($"38;2;{r};{g};{b}");
        }
        if (chunk.Bg is { } bg)
        {
            var (r, g, b, _) = bg.ToInts();
            codes.Add($"48;2;{r};{g};{b}");
        }
        if (chunk.Attributes.HasFlag(TextAttribute.Bold)) codes.Add("1");
        if (chunk.Attributes.HasFlag(TextAttribute.Dim)) codes.Add("2");
        if (chunk.Attributes.HasFlag(TextAttribute.Italic)) codes.Add("3");
        if (chunk.Attributes.HasFlag(TextAttribute.Underline)) codes.Add("4");
        if (chunk.Attributes.HasFlag(TextAttribute.Blink)) codes.Add("5");
        if (chunk.Attributes.HasFlag(TextAttribute.Inverse)) codes.Add("7");
        if (chunk.Attributes.HasFlag(TextAttribute.Hidden)) codes.Add("8");
        if (chunk.Attributes.HasFlag(TextAttribute.Strikethrough)) codes.Add("9");

        if (codes.Count > 0)
        {
            Console.Write($"\x1b[{string.Join(';', codes)}m");
            Console.Write(chunk.Text);
            Console.Write("\x1b[0m");
        }
        else
        {
            Console.Write(chunk.Text);
        }
    }
}
