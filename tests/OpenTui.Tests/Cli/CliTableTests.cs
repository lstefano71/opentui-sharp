using OpenTui.Cli;
using Xunit;

namespace OpenTui.Tests.Cli;

public class CliTableTests
{
    [Fact]
    public void Write_EmptyTable_DrawsBorders()
    {
        var console = new TestCliConsole();
        new CliTable()
            .AddColumn("Name")
            .Write(console);

        Assert.Contains("Name", console.Output);
        Assert.Contains("─", console.Output);
    }

    [Fact]
    public void Write_WithRows_DisplaysData()
    {
        var console = new TestCliConsole();
        new CliTable()
            .AddColumn("Name", "Age")
            .AddRow("Alice", "30")
            .AddRow("Bob", "25")
            .Write(console);

        Assert.Contains("Alice", console.Output);
        Assert.Contains("Bob", console.Output);
        Assert.Contains("30", console.Output);
    }

    [Fact]
    public void Write_WithTitle_DisplaysTitle()
    {
        var console = new TestCliConsole();
        new CliTable()
            .SetTitle("People")
            .AddColumn("Full Name", "Age", "City")
            .AddRow("Alice", "30", "London")
            .Write(console);

        Assert.Contains("People", console.Output);
    }

    [Fact]
    public void Write_WithBorderStyle_UsesCorrectChars()
    {
        var console = new TestCliConsole();
        new CliTable()
            .SetBorder(BorderStyle.Rounded)
            .AddColumn("X")
            .AddRow("1")
            .Write(console);

        Assert.Contains("╭", console.Output);
        Assert.Contains("╰", console.Output);
    }

    [Fact]
    public void AddColumn_FluentApi_ReturnsSelf()
    {
        var table = new CliTable();
        var result = table.AddColumn("A", "B");
        Assert.Same(table, result);
    }
}
