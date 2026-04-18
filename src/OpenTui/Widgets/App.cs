namespace OpenTui;

/// <summary>Options for configuring the App host.</summary>
public sealed record AppOptions
{
    /// <summary>Target frames per second (default 30).</summary>
    public int TargetFps { get; init; } = 30;

    /// <summary>Initial terminal columns (0 = auto-detect).</summary>
    public int Cols { get; init; }

    /// <summary>Initial terminal rows (0 = auto-detect).</summary>
    public int Rows { get; init; }

    /// <summary>Use alternate screen buffer (default true).</summary>
    public bool AlternateScreen { get; init; } = true;
}

/// <summary>
/// The application host for full-screen TUI applications.
/// Manages the render loop, input handling, and widget tree.
/// </summary>
public sealed class App : IDisposable
{
    private readonly AppOptions _options;
    private readonly Box _root;
    private readonly FocusManager _focusManager;
    private volatile bool _running;
    private bool _disposed;

    /// <summary>Creates a new App with the specified options.</summary>
    public App(AppOptions? options = null)
    {
        _options = options ?? new AppOptions();
        _root = new Box { FlexDirection = FlexDirection.Column };
        _focusManager = new FocusManager();
    }

    /// <summary>The root container widget. Add your UI tree here.</summary>
    public Box Root => _root;

    /// <summary>The focus manager for keyboard navigation.</summary>
    public FocusManager Focus => _focusManager;

    /// <summary>
    /// Starts the render loop. Blocks until <see cref="Stop"/> is called
    /// or Ctrl+C is received.
    /// </summary>
    public void Run()
    {
        _running = true;
        uint cols = _options.Cols > 0 ? (uint)_options.Cols : (uint)Console.WindowWidth;
        uint rows = _options.Rows > 0 ? (uint)_options.Rows : (uint)Console.WindowHeight;
        int fps = Math.Clamp(_options.TargetFps, 1, 120);

        using var renderer = new NativeRenderer(cols, rows);
        renderer.SetupTerminal(_options.AlternateScreen);

        Console.CancelKeyPress += OnCancelKeyPress;
        try
        {
            while (_running)
            {
                // Handle resize
                uint newCols = (uint)Console.WindowWidth;
                uint newRows = (uint)Console.WindowHeight;
                bool resized = newCols != cols || newRows != rows;
                if (resized)
                {
                    cols = newCols;
                    rows = newRows;
                    renderer.Resize(cols, rows);
                }

                // Layout
                _root.Layout.CalculateLayout(cols, rows);

                // Render
                var buffer = renderer.GetNextBuffer();
                buffer.Clear();
                buffer.ClearScissors();
                _root.Render(buffer, 0, 0);
                renderer.Render(forceFullRender: resized);

                Thread.Sleep(1000 / fps);
            }
        }
        finally
        {
            Console.CancelKeyPress -= OnCancelKeyPress;
            renderer.RestoreTerminalModes();
        }
    }

    /// <summary>Stops the render loop.</summary>
    public void Stop() => _running = false;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        Stop();
    }
}
