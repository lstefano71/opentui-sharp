namespace OpenTui.Cli;

/// <summary>
/// Represents a Cli Progress.
/// </summary>
public sealed class CliProgress
{
    private readonly List<ProgressTaskDef> _taskDefs = [];
    private ICliConsole _console = AnsiConsole.Instance;

    /// <summary>
    /// Performs use console.
    /// </summary>
    /// <param name="console">The console.</param>
    /// <returns>The result of use console.</returns>
    public CliProgress UseConsole(ICliConsole console) { _console = console; return this; }
    /// <summary>
    /// Adds a task.
    /// </summary>
    /// <param name="description">The description.</param>
    /// <param name="maxValue">The max value.</param>
    /// <returns>The result of add task.</returns>
    public CliProgress AddTask(string description, double maxValue = 100) { _taskDefs.Add(new(description, maxValue)); return this; }
    /// <summary>
    /// Gets or sets the completed color.
    /// </summary>
    public Rgba? CompletedColor { get; set; }
    /// <summary>
    /// Gets or sets the remaining color.
    /// </summary>
    public Rgba? RemainingColor { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether show percentage.
    /// </summary>
    public bool ShowPercentage { get; set; } = true;

    /// <summary>
    /// Performs run async.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <returns>The result of run async.</returns>
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

/// <summary>
/// Represents a Progress Context.
/// </summary>
public sealed class ProgressContext
{
    /// <summary>
    /// Initializes a new instance of the ProgressContext class.
    /// </summary>
    /// <param name="tasks">The tasks.</param>
    public ProgressContext(IReadOnlyList<ProgressTask> tasks) => Tasks = tasks;
    /// <summary>
    /// Gets the tasks.
    /// </summary>
    public IReadOnlyList<ProgressTask> Tasks { get; }
    /// <summary>
    /// Gets a value indicating whether is finished.
    /// </summary>
    public bool IsFinished => Tasks.All(t => t.IsFinished);
}

/// <summary>
/// Represents a Progress Task.
/// </summary>
public sealed class ProgressTask
{
    /// <summary>
    /// Initializes a new instance of the ProgressTask class.
    /// </summary>
    /// <param name="description">The description.</param>
    /// <param name="maxValue">The max value.</param>
    public ProgressTask(string description, double maxValue) { Description = description; MaxValue = maxValue; }
    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public double Value { get; private set; }
    /// <summary>
    /// Gets or sets the max value.
    /// </summary>
    public double MaxValue { get; set; }
    /// <summary>
    /// Gets the percentage.
    /// </summary>
    public double Percentage => MaxValue > 0 ? Math.Min(Value / MaxValue * 100, 100) : 0;
    /// <summary>
    /// Gets a value indicating whether is finished.
    /// </summary>
    public bool IsFinished => Value >= MaxValue;
    /// <summary>
    /// Performs increment.
    /// </summary>
    /// <param name="amount">The amount.</param>
    public void Increment(double amount) => Value = Math.Min(Value + amount, MaxValue);
}
