using OpenTui.Cli;
using Xunit;

namespace OpenTui.Tests.Cli;

public class CliStatusTests
{
    [Fact]
    public async Task RunAsync_DisplaysStatus()
    {
        var console = new TestCliConsole();
        await new CliStatus("Working...")
            .UseConsole(console)
            .RunAsync(async ctx =>
            {
                await Task.Delay(100);
                ctx.Status = "Done";
            });
        Assert.Contains("Working", console.Output);
    }
}
