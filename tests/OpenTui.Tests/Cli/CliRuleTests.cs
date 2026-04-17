using OpenTui.Cli;
using Xunit;

namespace OpenTui.Tests.Cli;

public class CliRuleTests
{
    [Fact]
    public void Write_NoTitle_DrawsFullLine()
    {
        var console = new TestCliConsole { Width = 40 };
        new CliRule().Write(console);

        var lines = console.Lines;
        Assert.Contains("─", lines[0]);
        Assert.Equal(40, lines[0].Length);
    }

    [Fact]
    public void Write_WithTitle_IncludesTitle()
    {
        var console = new TestCliConsole { Width = 40 };
        new CliRule("Section").Write(console);

        Assert.Contains("Section", console.Output);
        Assert.Contains("─", console.Output);
    }

    [Fact]
    public void Write_CustomChar()
    {
        var console = new TestCliConsole { Width = 20 };
        new CliRule().SetChar('=').Write(console);

        Assert.Contains("=", console.Output);
    }
}
