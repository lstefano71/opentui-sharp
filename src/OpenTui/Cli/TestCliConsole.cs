using System.Text;

namespace OpenTui.Cli;

/// <summary>
/// Test implementation of <see cref="ICliConsole"/> that captures output
/// and provides programmable input. Use for unit testing CLI widgets.
/// </summary>
public sealed class TestCliConsole : ICliConsole
{
    private readonly StringBuilder _output = new();
    private readonly Queue<string> _inputLines = new();
    private readonly Queue<ConsoleKeyInfo> _inputKeys = new();

    /// <summary>
    /// Gets or sets the width.
    /// </summary>
    public int Width { get; set; } = 80;
    /// <summary>
    /// Gets or sets the height.
    /// </summary>
    public int Height { get; set; } = 24;
    /// <summary>
    /// Gets or sets the supports ansi.
    /// </summary>
    public bool SupportsAnsi { get; set; } = false;
    /// <summary>
    /// Gets or sets a value indicating whether is interactive.
    /// </summary>
    public bool IsInteractive { get; set; } = true;
    /// <summary>
    /// Gets or sets the cursor visible.
    /// </summary>
    public bool CursorVisible { get; set; } = true;
    /// <summary>
    /// Gets or sets the cursor left.
    /// </summary>
    public int CursorLeft { get; private set; }
    /// <summary>
    /// Gets or sets the cursor top.
    /// </summary>
    public int CursorTop { get; private set; }

    /// <summary>All output written to this console, without ANSI escape codes.</summary>
    public string Output => _output.ToString();

    /// <summary>Individual lines of output.</summary>
    public string[] Lines => _output.ToString().Split(Environment.NewLine, StringSplitOptions.None);

    /// <summary>
    /// Performs write.
    /// </summary>
    /// <param name="text">The text value.</param>
    public void Write(string text) => _output.Append(text);
    /// <summary>
    /// Writes a line.
    /// </summary>
    /// <param name="text">The text value.</param>
    public void WriteLine(string text = "")
    {
        _output.Append(text);
        _output.Append(Environment.NewLine);
    }

    /// <summary>
    /// Performs write.
    /// </summary>
    /// <param name="text">The text value.</param>
    public void Write(StyledText text)
    {
        foreach (var chunk in text.Chunks)
            _output.Append(chunk.Text);
    }

    /// <summary>
    /// Writes a line.
    /// </summary>
    /// <param name="text">The text value.</param>
    public void WriteLine(StyledText text)
    {
        Write(text);
        _output.Append(Environment.NewLine);
    }

    /// <summary>Enqueues a line of input to be returned by ReadLine.</summary>
    public void EnqueueInput(string line) => _inputLines.Enqueue(line);

    /// <summary>Enqueues a key press to be returned by ReadKey.</summary>
    public void EnqueueKey(ConsoleKeyInfo key) => _inputKeys.Enqueue(key);

    /// <summary>Enqueues a simple key press.</summary>
    public void EnqueueKey(ConsoleKey key, char keyChar = '\0', bool shift = false, bool alt = false, bool control = false)
        => _inputKeys.Enqueue(new ConsoleKeyInfo(keyChar, key, shift, alt, control));

    /// <summary>
    /// Reads a line.
    /// </summary>
    /// <returns>The line.</returns>
    public string? ReadLine() => _inputLines.Count > 0 ? _inputLines.Dequeue() : null;
    /// <summary>
    /// Reads a key.
    /// </summary>
    /// <param name="intercept">The intercept.</param>
    /// <returns>The key.</returns>
    public ConsoleKeyInfo ReadKey(bool intercept = false) =>
        _inputKeys.Count > 0 ? _inputKeys.Dequeue() : new ConsoleKeyInfo('\0', ConsoleKey.Enter, false, false, false);

    /// <summary>
    /// Sets the cursor position.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="top">The top.</param>
    public void SetCursorPosition(int left, int top) { CursorLeft = left; CursorTop = top; }
    /// <summary>
    /// Performs hide cursor.
    /// </summary>
    public void HideCursor() => CursorVisible = false;
    /// <summary>
    /// Performs show cursor.
    /// </summary>
    public void ShowCursor() => CursorVisible = true;

    /// <summary>Clears all captured output.</summary>
    public void Clear() => _output.Clear();
}
