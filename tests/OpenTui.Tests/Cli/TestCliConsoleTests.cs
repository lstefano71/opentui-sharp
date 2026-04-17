using OpenTui.Cli;
using Xunit;

namespace OpenTui.Tests.Cli;

public class TestCliConsoleTests
{
    [Fact]
    public void Write_CapturesOutput()
    {
        var console = new TestCliConsole();
        console.Write("Hello");
        console.Write(" World");

        Assert.Equal("Hello World", console.Output);
    }

    [Fact]
    public void WriteLine_AddsNewline()
    {
        var console = new TestCliConsole();
        console.WriteLine("Line 1");
        console.WriteLine("Line 2");

        Assert.Equal($"Line 1{Environment.NewLine}Line 2{Environment.NewLine}", console.Output);
    }

    [Fact]
    public void Write_StyledText_CapturesPlainText()
    {
        var console = new TestCliConsole();
        console.Write(new StyledText(new StyledChunk("Bold", Attributes: TextAttribute.Bold)));

        Assert.Equal("Bold", console.Output);
    }

    [Fact]
    public void EnqueueInput_ReturnedByReadLine()
    {
        var console = new TestCliConsole();
        console.EnqueueInput("test input");

        Assert.Equal("test input", console.ReadLine());
    }

    [Fact]
    public void ReadLine_NoInput_ReturnsNull()
    {
        var console = new TestCliConsole();
        Assert.Null(console.ReadLine());
    }

    [Fact]
    public void Clear_ResetsOutput()
    {
        var console = new TestCliConsole();
        console.Write("data");
        console.Clear();
        Assert.Equal("", console.Output);
    }

    [Fact]
    public void SetCursorPosition_UpdatesProperties()
    {
        var console = new TestCliConsole();
        console.SetCursorPosition(10, 5);
        Assert.Equal(10, console.CursorLeft);
        Assert.Equal(5, console.CursorTop);
    }

    [Fact]
    public void Defaults_AreReasonable()
    {
        var console = new TestCliConsole();
        Assert.Equal(80, console.Width);
        Assert.Equal(24, console.Height);
        Assert.False(console.SupportsAnsi);
        Assert.True(console.IsInteractive);
    }
}
