using OpenTui.Cli;
using Xunit;

namespace OpenTui.Tests.Cli;

public class CliConfirmTests
{
    [Theory]
    [InlineData("y", true)]
    [InlineData("Y", true)]
    [InlineData("yes", true)]
    [InlineData("n", false)]
    [InlineData("N", false)]
    [InlineData("no", false)]
    public void Prompt_ParsesInput(string input, bool expected)
    {
        var console = new TestCliConsole();
        console.EnqueueInput(input);

        bool result = new CliConfirm("Continue?").Prompt(console);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Prompt_EmptyInput_ReturnsDefault()
    {
        var console = new TestCliConsole();
        console.EnqueueInput("");

        bool result = new CliConfirm("Continue?").SetDefault(true).Prompt(console);
        Assert.True(result);
    }
}
