using Xunit;

namespace OpenTui.Tests.Types;

public class StyledTextTests
{
    [Fact]
    public void ImplicitConversion_FromString()
    {
        StyledText text = "Hello";
        Assert.Single(text.Chunks);
        Assert.Equal("Hello", text.Chunks[0].Text);
    }

    [Fact]
    public void PlainText_ConcatenatesChunks()
    {
        var text = new StyledText(
            new StyledChunk("Hello "),
            new StyledChunk("World")
        );
        Assert.Equal("Hello World", text.PlainText);
    }

    [Fact]
    public void Length_SumsChunks()
    {
        var text = new StyledText(
            new StyledChunk("AB"),
            new StyledChunk("CDE")
        );
        Assert.Equal(5, text.Length);
    }

    [Fact]
    public void ToString_ReturnsPlainText()
    {
        StyledText text = "Test";
        Assert.Equal("Test", text.ToString());
    }

    [Fact]
    public void StyledChunk_PreservesFormatting()
    {
        var chunk = new StyledChunk("Bold", Fg: Rgba.White, Attributes: TextAttribute.Bold);
        Assert.Equal(Rgba.White, chunk.Fg);
        Assert.Equal(TextAttribute.Bold, chunk.Attributes);
    }
}
