using OpenTui.Cli;
using Xunit;

namespace OpenTui.Tests.Cli;

public class CliProgressTests
{
    [Fact]
    public async Task RunAsync_CompletesWhenTaskFinishes()
    {
        var console = new TestCliConsole();
        await new CliProgress()
            .UseConsole(console)
            .AddTask("Test", 10)
            .RunAsync(async ctx =>
            {
                ctx.Tasks[0].Increment(10);
                await Task.Delay(50);
            });
        Assert.Contains("Test", console.Output);
    }

    [Fact]
    public void ProgressTask_Increment_ClampToMax()
    {
        var task = new ProgressTask("Test", 100);
        task.Increment(150);
        Assert.Equal(100, task.Value);
        Assert.True(task.IsFinished);
    }

    [Fact]
    public void ProgressTask_Percentage_Correct()
    {
        var task = new ProgressTask("Test", 200);
        task.Increment(100);
        Assert.Equal(50, task.Percentage);
    }
}
