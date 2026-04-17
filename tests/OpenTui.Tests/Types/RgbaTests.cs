using Xunit;

namespace OpenTui.Tests.Types;

public class RgbaTests
{
    [Fact]
    public void Constructor_DefaultAlpha_IsOne()
    {
        var color = new Rgba(0.5f, 0.5f, 0.5f);
        Assert.Equal(1f, color.A);
    }

    [Fact]
    public void FromInts_ConvertsCorrectly()
    {
        var color = Rgba.FromInts(255, 128, 0, 255);
        Assert.Equal(1f, color.R);
        Assert.InRange(color.G, 0.50f, 0.51f);
        Assert.Equal(0f, color.B);
        Assert.Equal(1f, color.A);
    }

    [Fact]
    public void FromInts_DefaultAlpha()
    {
        var color = Rgba.FromInts(0, 0, 0);
        Assert.Equal(1f, color.A);
    }

    [Theory]
    [InlineData("#FF0000", 1f, 0f, 0f, 1f)]
    [InlineData("#00FF00", 0f, 1f, 0f, 1f)]
    [InlineData("#0000FF", 0f, 0f, 1f, 1f)]
    [InlineData("FF0000", 1f, 0f, 0f, 1f)]
    [InlineData("#FFFFFF80", 1f, 1f, 1f, 0.502f)]
    public void FromHex_ParsesCorrectly(string hex, float r, float g, float b, float a)
    {
        var color = Rgba.FromHex(hex);
        Assert.InRange(color.R, r - 0.01f, r + 0.01f);
        Assert.InRange(color.G, g - 0.01f, g + 0.01f);
        Assert.InRange(color.B, b - 0.01f, b + 0.01f);
        Assert.InRange(color.A, a - 0.01f, a + 0.01f);
    }

    [Theory]
    [InlineData("#F00", 1f, 0f, 0f)]
    [InlineData("#0F0", 0f, 1f, 0f)]
    public void FromHex_ShortFormat(string hex, float r, float g, float b)
    {
        var color = Rgba.FromHex(hex);
        Assert.InRange(color.R, r - 0.01f, r + 0.01f);
        Assert.InRange(color.G, g - 0.01f, g + 0.01f);
        Assert.InRange(color.B, b - 0.01f, b + 0.01f);
    }

    [Theory]
    [InlineData("")]
    [InlineData("#GG0000")]
    [InlineData("#12345")]
    public void FromHex_InvalidFormat_Throws(string hex)
    {
        Assert.Throws<FormatException>(() => Rgba.FromHex(hex));
    }

    [Fact]
    public void FromHex_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Rgba.FromHex(null!));
    }

    [Fact]
    public void ToInts_RoundTrips()
    {
        var color = Rgba.FromInts(128, 64, 32, 200);
        var (r, g, b, a) = color.ToInts();
        Assert.Equal(128, r);
        Assert.Equal(64, g);
        Assert.Equal(32, b);
        Assert.Equal(200, a);
    }

    [Fact]
    public void ToString_OpaqueColor()
    {
        var color = Rgba.FromHex("#FF8800");
        Assert.Equal("#FF8800", color.ToString());
    }

    [Fact]
    public void ToString_TransparentColor()
    {
        var color = Rgba.FromHex("#FF880080");
        Assert.Equal("#FF880080", color.ToString());
    }

    [Fact]
    public void Transparent_IsZero()
    {
        Assert.Equal(0f, Rgba.Transparent.R);
        Assert.Equal(0f, Rgba.Transparent.A);
    }

    [Fact]
    public void White_IsOne()
    {
        Assert.Equal(1f, Rgba.White.R);
        Assert.Equal(1f, Rgba.White.G);
        Assert.Equal(1f, Rgba.White.B);
        Assert.Equal(1f, Rgba.White.A);
    }

    [Fact]
    public void RecordStruct_Equality()
    {
        var a = new Rgba(1f, 0f, 0f);
        var b = new Rgba(1f, 0f, 0f);
        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void RecordStruct_Inequality()
    {
        var a = new Rgba(1f, 0f, 0f);
        var b = new Rgba(0f, 1f, 0f);
        Assert.NotEqual(a, b);
    }
}
