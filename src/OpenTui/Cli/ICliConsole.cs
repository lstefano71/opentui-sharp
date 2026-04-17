namespace OpenTui.Cli;

/// <summary>
/// Abstraction for console output. Supports styled text, cursor control,
/// and interactive prompts. Inject this interface for testability.
/// </summary>
public interface ICliConsole
{
    /// <summary>Terminal width in columns.</summary>
    int Width { get; }

    /// <summary>Terminal height in rows.</summary>
    int Height { get; }

    /// <summary>Whether the output supports ANSI escape codes.</summary>
    bool SupportsAnsi { get; }

    /// <summary>Whether the console is interactive (has a real terminal attached).</summary>
    bool IsInteractive { get; }

    /// <summary>Writes styled text to the console.</summary>
    void Write(StyledText text);

    /// <summary>Writes a plain string to the console.</summary>
    void Write(string text);

    /// <summary>Writes styled text followed by a newline.</summary>
    void WriteLine(StyledText text);

    /// <summary>Writes a plain string followed by a newline.</summary>
    void WriteLine(string text = "");

    /// <summary>Reads a line of input from the console.</summary>
    string? ReadLine();

    /// <summary>Reads a single key press.</summary>
    ConsoleKeyInfo ReadKey(bool intercept = false);

    /// <summary>Sets the cursor position.</summary>
    void SetCursorPosition(int left, int top);

    /// <summary>Hides the cursor.</summary>
    void HideCursor();

    /// <summary>Shows the cursor.</summary>
    void ShowCursor();

    /// <summary>Gets or sets cursor visibility.</summary>
    bool CursorVisible { get; set; }
}
