namespace OpenTui.Cli;

/// <summary>
/// Default <see cref="ICliConsole"/> implementation using ANSI escape codes.
/// </summary>
public sealed class AnsiConsole : ICliConsole
{
    /// <summary>Shared default instance.</summary>
    public static readonly AnsiConsole Instance = new();

    /// <summary>
    /// Gets the width.
    /// </summary>
    public int Width => Console.WindowWidth;
    /// <summary>
    /// Gets the height.
    /// </summary>
    public int Height => Console.WindowHeight;
    /// <summary>
    /// Gets the supports ansi.
    /// </summary>
    public bool SupportsAnsi => !Console.IsOutputRedirected;
    /// <summary>
    /// Gets a value indicating whether is interactive.
    /// </summary>
    public bool IsInteractive => !Console.IsInputRedirected && !Console.IsOutputRedirected;

    /// <summary>
    /// Performs write.
    /// </summary>
    /// <param name="text">The text value.</param>
    public void Write(string text) => Console.Write(text);
    /// <summary>
    /// Writes a line.
    /// </summary>
    /// <param name="text">The text value.</param>
    public void WriteLine(string text = "") => Console.WriteLine(text);

    /// <summary>
    /// Performs write.
    /// </summary>
    /// <param name="text">The text value.</param>
    public void Write(StyledText text)
    {
        foreach (var chunk in text.Chunks)
            WriteChunk(chunk);
    }

    /// <summary>
    /// Writes a line.
    /// </summary>
    /// <param name="text">The text value.</param>
    public void WriteLine(StyledText text)
    {
        Write(text);
        Console.WriteLine();
    }

    /// <summary>
    /// Reads a line.
    /// </summary>
    /// <returns>The line.</returns>
    public string? ReadLine() => Console.ReadLine();
    /// <summary>
    /// Reads a key.
    /// </summary>
    /// <param name="intercept">The intercept.</param>
    /// <returns>The key.</returns>
    public ConsoleKeyInfo ReadKey(bool intercept = false) => Console.ReadKey(intercept);
    /// <summary>
    /// Sets the cursor position.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="top">The top.</param>
    public void SetCursorPosition(int left, int top) => Console.SetCursorPosition(left, top);
    /// <summary>
    /// Performs hide cursor.
    /// </summary>
    public void HideCursor() => Console.Write("\x1b[?25l");
    /// <summary>
    /// Performs show cursor.
    /// </summary>
    public void ShowCursor() => Console.Write("\x1b[?25h");
    /// <summary>
    /// Gets or sets the cursor visible.
    /// </summary>
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
