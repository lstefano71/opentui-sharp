namespace OpenTui.Cli;

public sealed class CliProgress
{
    private readonly List<ProgressTaskDef> _taskDefs = [];
    private ICliConsole _console = AnsiConsole.Instance;

    public CliProgress UseConsole(ICliConsole console) { _console = console; return this; }
    public CliProgress AddTask(string description, double maxValue = 100) { _taskDefs.Add(new(description, maxValue)); return this; }
    public Rgba? CompletedColor { get; set; }
    public Rgba? RemainingColor { get; set; }
    public bool ShowPercentage { get; set; } = true;

    public async Task RunAsync(Func<ProgressContext, Task> action)
    {
        var tasks = _taskDefs.Select(d => new ProgressTask(d.Description, d.MaxValue)).ToList();
        var ctx = new ProgressContext(tasks);

        var actionTask = action(ctx);

        while (!actionTask.IsCompleted)
        {
            RenderProgress(ctx);
            await Task.Delay(100);
        }

        await actionTask; // propagate exceptions
        RenderProgress(ctx); // final render
        _console.WriteLine(); // newline after completion
    }

    private void RenderProgress(ProgressContext ctx)
    {
        for (int i = 0; i < ctx.Tasks.Count; i++)
        {
            var task = ctx.Tasks[i];
            int barWidth = Math.Max(10, _console.Width - task.Description.Length - 15);
            int filled = (int)(task.Percentage / 100.0 * barWidth);
            string bar = new string('█', filled) + new string('░', barWidth - filled);
            string pct = ShowPercentage ? $" {task.Percentage:F0}%" : "";
            _console.Write($"\r{task.Description} [{bar}]{pct}");
        }
    }

    private record ProgressTaskDef(string Description, double MaxValue);
}

public sealed class ProgressContext
{
    public ProgressContext(IReadOnlyList<ProgressTask> tasks) => Tasks = tasks;
    public IReadOnlyList<ProgressTask> Tasks { get; }
    public bool IsFinished => Tasks.All(t => t.IsFinished);
}

public sealed class ProgressTask
{
    public ProgressTask(string description, double maxValue) { Description = description; MaxValue = maxValue; }
    public string Description { get; set; }
    public double Value { get; private set; }
    public double MaxValue { get; set; }
    public double Percentage => MaxValue > 0 ? Math.Min(Value / MaxValue * 100, 100) : 0;
    public bool IsFinished => Value >= MaxValue;
    public void Increment(double amount) => Value = Math.Min(Value + amount, MaxValue);
}
