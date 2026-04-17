using Xunit;

namespace OpenTui.Tests.Types;

public class EnumTests
{
    [Fact]
    public void TextAttribute_FlagCombinations()
    {
        var flags = TextAttribute.Bold | TextAttribute.Italic;
        Assert.True(flags.HasFlag(TextAttribute.Bold));
        Assert.True(flags.HasFlag(TextAttribute.Italic));
        Assert.False(flags.HasFlag(TextAttribute.Underline));
    }

    [Fact]
    public void TextAttribute_None_IsZero()
    {
        Assert.Equal(0u, (uint)TextAttribute.None);
    }

    [Fact]
    public void TextAttribute_ValuesMatchNative()
    {
        Assert.Equal(1u, (uint)TextAttribute.Bold);
        Assert.Equal(2u, (uint)TextAttribute.Dim);
        Assert.Equal(4u, (uint)TextAttribute.Italic);
        Assert.Equal(8u, (uint)TextAttribute.Underline);
        Assert.Equal(16u, (uint)TextAttribute.Blink);
        Assert.Equal(32u, (uint)TextAttribute.Inverse);
        Assert.Equal(64u, (uint)TextAttribute.Hidden);
        Assert.Equal(128u, (uint)TextAttribute.Strikethrough);
    }

    [Fact]
    public void TargetChannel_Both_IsFgOrBg()
    {
        Assert.Equal(TargetChannel.Fg | TargetChannel.Bg, TargetChannel.Both);
    }

    [Fact]
    public void BorderStyle_HasExpectedValues()
    {
        Assert.Equal(0, (int)BorderStyle.Single);
        Assert.Equal(1, (int)BorderStyle.Double);
        Assert.Equal(2, (int)BorderStyle.Rounded);
        Assert.Equal(3, (int)BorderStyle.Heavy);
    }

    [Fact]
    public void CursorStyle_Default_Is255()
    {
        Assert.Equal(255, (int)CursorStyle.Default);
    }

    [Fact]
    public void WidthMethod_Values()
    {
        Assert.Equal(0, (int)WidthMethod.WcWidth);
        Assert.Equal(1, (int)WidthMethod.Unicode);
    }

    [Fact]
    public void GrowthPolicy_Values()
    {
        Assert.Equal(0, (int)GrowthPolicy.Grow);
        Assert.Equal(1, (int)GrowthPolicy.Block);
    }
}
