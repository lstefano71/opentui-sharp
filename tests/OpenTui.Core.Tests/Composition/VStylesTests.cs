using Xunit;

namespace OpenTui.Core.Tests.Composition;

/// <summary>
/// Tests for <see cref="VStyles"/> composable styled text factory functions.
/// </summary>
public sealed class VStylesTests
{
    [Fact]
    public void Bold_CreatesStyledTextWithBoldAttribute()
    {
        var result = VStyles.Bold("hello");

        Assert.Single(result.Chunks);
        Assert.Equal("hello", result.Chunks[0].Text);
        Assert.True(result.Chunks[0].Attributes.HasFlag(TextAttributes.Bold));
    }

    [Fact]
    public void Italic_CreatesStyledTextWithItalicAttribute()
    {
        var result = VStyles.Italic("world");

        Assert.Single(result.Chunks);
        Assert.Equal("world", result.Chunks[0].Text);
        Assert.True(result.Chunks[0].Attributes.HasFlag(TextAttributes.Italic));
    }

    [Fact]
    public void Underline_CreatesStyledTextWithUnderlineAttribute()
    {
        var result = VStyles.Underline("test");

        Assert.Single(result.Chunks);
        Assert.True(result.Chunks[0].Attributes.HasFlag(TextAttributes.Underline));
    }

    [Fact]
    public void Dim_CreatesStyledTextWithDimAttribute()
    {
        var result = VStyles.Dim("faded");

        Assert.Single(result.Chunks);
        Assert.True(result.Chunks[0].Attributes.HasFlag(TextAttributes.Dim));
    }

    [Fact]
    public void Color_String_SetsForeground()
    {
        var result = VStyles.Color("#ff6b6b", "red text");

        Assert.Single(result.Chunks);
        Assert.Equal("red text", result.Chunks[0].Text);
        Assert.NotNull(result.Chunks[0].Fg);
    }

    [Fact]
    public void Color_Rgba_SetsForeground()
    {
        var red = Rgba.FromInts(255, 0, 0);
        var result = VStyles.Color(red, "red");

        Assert.Single(result.Chunks);
        Assert.Equal(red, result.Chunks[0].Fg);
    }

    [Fact]
    public void BgColor_SetsBackground()
    {
        var result = VStyles.BgColor("#4ecdc4", "highlighted");

        Assert.Single(result.Chunks);
        Assert.NotNull(result.Chunks[0].Bg);
    }

    [Fact]
    public void BoldItalic_CombinesAttributes()
    {
        var result = VStyles.BoldItalic("styled");

        Assert.Single(result.Chunks);
        Assert.True(result.Chunks[0].Attributes.HasFlag(TextAttributes.Bold));
        Assert.True(result.Chunks[0].Attributes.HasFlag(TextAttributes.Italic));
    }

    [Fact]
    public void Nested_BoldUnderline_MergesAttributes()
    {
        // Bold(Underline("hello"), " world") should produce:
        // "hello" → Bold | Underline
        // " world" → Bold
        var result = VStyles.Bold(VStyles.Underline("hello"), " world");

        Assert.Equal(2, result.Chunks.Count);
        Assert.Equal("hello", result.Chunks[0].Text);
        Assert.True(result.Chunks[0].Attributes.HasFlag(TextAttributes.Bold));
        Assert.True(result.Chunks[0].Attributes.HasFlag(TextAttributes.Underline));

        Assert.Equal(" world", result.Chunks[1].Text);
        Assert.True(result.Chunks[1].Attributes.HasFlag(TextAttributes.Bold));
        Assert.False(result.Chunks[1].Attributes.HasFlag(TextAttributes.Underline));
    }

    [Fact]
    public void Nested_ColorBold_MergesColorAndAttribute()
    {
        // Color("#ff6b6b", Bold("Bold Red"), " normal") should produce:
        // "Bold Red" → fg=#ff6b6b, Bold
        // " normal" → fg=#ff6b6b
        var result = VStyles.Color("#ff6b6b", VStyles.Bold("Bold Red"), " normal");

        Assert.Equal(2, result.Chunks.Count);
        Assert.Equal("Bold Red", result.Chunks[0].Text);
        Assert.True(result.Chunks[0].Attributes.HasFlag(TextAttributes.Bold));
        Assert.NotNull(result.Chunks[0].Fg);

        Assert.Equal(" normal", result.Chunks[1].Text);
        Assert.False(result.Chunks[1].Attributes.HasFlag(TextAttributes.Bold));
        Assert.NotNull(result.Chunks[1].Fg);
    }

    [Fact]
    public void Nested_ItalicColor_InnerColorTakesPrecedence()
    {
        // Italic(Color("#4ecdc4", "Green Italic"), " normal again")
        // "Green Italic" → fg=#4ecdc4 (from inner Color), Italic (from outer)
        // " normal again" → no fg (outer Italic doesn't set fg), Italic
        var result = VStyles.Italic(VStyles.Color("#4ecdc4", "Green Italic"), " normal again");

        Assert.Equal(2, result.Chunks.Count);

        Assert.Equal("Green Italic", result.Chunks[0].Text);
        Assert.True(result.Chunks[0].Attributes.HasFlag(TextAttributes.Italic));
        Assert.NotNull(result.Chunks[0].Fg); // inner Color takes precedence

        Assert.Equal(" normal again", result.Chunks[1].Text);
        Assert.True(result.Chunks[1].Attributes.HasFlag(TextAttributes.Italic));
        Assert.Null(result.Chunks[1].Fg); // no fg from outer Italic
    }

    [Fact]
    public void Styled_AppliesArbitraryAttributes()
    {
        var result = VStyles.Styled(TextAttributes.Bold | TextAttributes.Underline, "custom");

        Assert.Single(result.Chunks);
        Assert.True(result.Chunks[0].Attributes.HasFlag(TextAttributes.Bold));
        Assert.True(result.Chunks[0].Attributes.HasFlag(TextAttributes.Underline));
    }

    [Fact]
    public void MultipleParts_ConcatenateChunks()
    {
        var result = VStyles.Bold("a", "b", "c");

        Assert.Equal(3, result.Chunks.Count);
        Assert.All(result.Chunks, c => Assert.True(c.Attributes.HasFlag(TextAttributes.Bold)));
    }
}
