using OpenTui.Cli;
using Xunit;

namespace OpenTui.Tests.Cli;

public class CliPanelTests
{
    [Fact]
    public void Write_DisplaysContent()
    {
        var console = new TestCliConsole();
        new CliPanel("Hello World").Write(console);

        Assert.Contains("Hello World", console.Output);
    }

    [Fact]
    public void Write_DrawsBorder()
    {
        var console = new TestCliConsole();
        new CliPanel("Test").SetBorder(BorderStyle.Single).Write(console);

        Assert.Contains("┌", console.Output);
        Assert.Contains("└", console.Output);
        Assert.Contains("│", console.Output);
    }

    [Fact]
    public void Write_WithTitle()
    {
        var console = new TestCliConsole();
        new CliPanel("This is some longer content for the panel").SetTitle("Title").Write(console);

        Assert.Contains("Title", console.Output);
        Assert.Contains("This is some longer content", console.Output);
    }
}
