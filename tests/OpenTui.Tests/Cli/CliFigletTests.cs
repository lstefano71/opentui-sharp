using OpenTui.Cli;
using Xunit;

namespace OpenTui.Tests.Cli;

public class CliFigletTests
{
    [Fact]
    public void Write_ProducesMultiLineOutput()
    {
        var console = new TestCliConsole();
        new CliFiglet("HI").UseConsole(console).Write();
        var lines = console.Lines;
        Assert.True(lines.Length >= 5);
    }

    [Fact]
    public void Write_ContainsBlockCharacters()
    {
        var console = new TestCliConsole();
        new CliFiglet("A").UseConsole(console).Write();
        Assert.Contains("█", console.Output);
    }
}
