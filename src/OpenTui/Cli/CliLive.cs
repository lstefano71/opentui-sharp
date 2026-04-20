namespace OpenTui.Cli;

/// <summary>
/// Represents a Cli Live.
/// </summary>
public sealed class CliLive : IDisposable
{
    private ICliConsole _console = AnsiConsole.Instance;
    private int _lastLineCount;
    private bool _disposed;

    /// <summary>
    /// Performs use console.
    /// </summary>
    /// <param name="console">The console.</param>
    /// <returns>The result of use console.</returns>
    public CliLive UseConsole(ICliConsole console) { _console = console; return this; }

    /// <summary>
    /// Performs update.
    /// </summary>
    /// <param name="render">The render.</param>
    public void Update(Action<ICliConsole> render)
    {
        // Move cursor up to overwrite previous output
        if (_lastLineCount > 0)
        {
            for (int i = 0; i < _lastLineCount; i++)
                _console.Write("\x1b[A\x1b[2K"); // move up + clear line
        }

        // Capture render output to count lines
        var capture = new TestCliConsole { Width = _console.Width, Height = _console.Height };
        render(capture);

        string output = capture.Output;
        _console.Write(output);
        _lastLineCount = output.Split('\n').Length;
    }

    /// <summary>
    /// Releases the resources used by this instance.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _console.WriteLine();
        }
    }
}
