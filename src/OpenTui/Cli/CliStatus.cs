namespace OpenTui.Cli;

/// <summary>
/// Represents a Cli Status.
/// </summary>
public sealed class CliStatus
{
    private string _initialStatus;
    private ICliConsole _console = AnsiConsole.Instance;
    private static readonly string[] DotFrames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];

    /// <summary>
    /// Initializes a new instance of the CliStatus class.
    /// </summary>
    /// <param name="initialStatus">The initial status.</param>
    public CliStatus(string initialStatus) => _initialStatus = initialStatus;
    /// <summary>
    /// Performs use console.
    /// </summary>
    /// <param name="console">The console.</param>
    /// <returns>The result of use console.</returns>
    public CliStatus UseConsole(ICliConsole console) { _console = console; return this; }
    /// <summary>
    /// Gets or sets the spinner frames.
    /// </summary>
    public string[] SpinnerFrames { get; set; } = DotFrames;
    /// <summary>
    /// Gets or sets the spinner color.
    /// </summary>
    public Rgba? SpinnerColor { get; set; }

    /// <summary>
    /// Performs run async.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <returns>The result of run async.</returns>
    public async Task RunAsync(Func<StatusContext, Task> action)
    {
        var ctx = new StatusContext { Status = _initialStatus };
        var actionTask = action(ctx);
        int frame = 0;

        while (!actionTask.IsCompleted)
        {
            string spinner = SpinnerFrames[frame % SpinnerFrames.Length];
            _console.Write($"\r{spinner} {ctx.Status}".PadRight(_console.Width - 1));
            frame++;
            await Task.Delay(80);
        }

        await actionTask;
        _console.Write($"\r{"".PadRight(_console.Width - 1)}\r"); // clear line
    }
}

/// <summary>
/// Represents a Status Context.
/// </summary>
public sealed class StatusContext
{
    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public string Status { get; set; } = "";
}
