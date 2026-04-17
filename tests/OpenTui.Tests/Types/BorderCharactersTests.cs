using Xunit;

namespace OpenTui.Tests.Types;

public class BorderCharactersTests
{
    [Fact]
    public void ForStyle_Single_MatchesStatic()
    {
        Assert.Equal(BorderCharacters.Single, BorderCharacters.ForStyle(BorderStyle.Single));
    }

    [Fact]
    public void ForStyle_AllStyles()
    {
        Assert.NotNull(BorderCharacters.ForStyle(BorderStyle.Single));
        Assert.NotNull(BorderCharacters.ForStyle(BorderStyle.Double));
        Assert.NotNull(BorderCharacters.ForStyle(BorderStyle.Rounded));
        Assert.NotNull(BorderCharacters.ForStyle(BorderStyle.Heavy));
    }

    [Fact]
    public void ToCodePoints_HasCorrectLength()
    {
        var codePoints = BorderCharacters.Single.ToCodePoints();
        Assert.Equal(11, codePoints.Length);
    }

    [Fact]
    public void Single_HasExpectedChars()
    {
        Assert.Equal('┌', BorderCharacters.Single.TopLeft);
        Assert.Equal('─', BorderCharacters.Single.Horizontal);
        Assert.Equal('│', BorderCharacters.Single.Vertical);
    }

    [Fact]
    public void Rounded_HasRoundedCorners()
    {
        Assert.Equal('╭', BorderCharacters.Rounded.TopLeft);
        Assert.Equal('╮', BorderCharacters.Rounded.TopRight);
        Assert.Equal('╰', BorderCharacters.Rounded.BottomLeft);
        Assert.Equal('╯', BorderCharacters.Rounded.BottomRight);
    }
}
