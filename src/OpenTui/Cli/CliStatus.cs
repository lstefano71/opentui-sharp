namespace OpenTui.Cli;

public sealed class CliStatus
{
    private string _initialStatus;
    private ICliConsole _console = AnsiConsole.Instance;
    private static readonly string[] DotFrames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];

    public CliStatus(string initialStatus) => _initialStatus = initialStatus;
    public CliStatus UseConsole(ICliConsole console) { _console = console; return this; }
    public string[] SpinnerFrames { get; set; } = DotFrames;
    public Rgba? SpinnerColor { get; set; }

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

public sealed class StatusContext
{
    public string Status { get; set; } = "";
}
