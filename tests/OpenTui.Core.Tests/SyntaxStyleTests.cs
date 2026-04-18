using OpenTui.Core;
using Xunit;

namespace OpenTui.Core.Tests;

public class SyntaxStyleTests
{
    // -- Init / Deinit --

    [Fact]
    public void InitAndDeinit()
    {
        using var style = SyntaxStyle.Create();
        Assert.Equal((nuint)0, style.StyleCount);
    }

    [Fact]
    public void MultipleIndependentInstances()
    {
        using var style1 = SyntaxStyle.Create();
        using var style2 = SyntaxStyle.Create();

        var fg = new Rgba(1f, 0f, 0f, 1f);
        style1.Register("test", fg: fg);

        Assert.Equal((nuint)1, style1.StyleCount);
        Assert.Equal((nuint)0, style2.StyleCount);
    }

    // -- Register --

    [Fact]
    public void RegisterSimpleStyle()
    {
        using var style = SyntaxStyle.Create();

        var fg = new Rgba(1f, 0f, 0f, 1f);
        uint id = style.Register("keyword", fg: fg);

        Assert.True(id > 0);
        Assert.Equal((nuint)1, style.StyleCount);
    }

    [Fact]
    public void RegisterStyleWithFgAndBg()
    {
        using var style = SyntaxStyle.Create();

        var fg = new Rgba(1f, 0f, 0f, 1f);
        var bg = new Rgba(0f, 0f, 0f, 1f);
        uint id = style.Register("string", fg: fg, bg: bg);

        Assert.True(id > 0);
        Assert.Equal((nuint)1, style.StyleCount);
    }

    [Fact]
    public void RegisterStyleWithAttributes()
    {
        using var style = SyntaxStyle.Create();

        var fg = new Rgba(1f, 0f, 0f, 1f);
        uint id = style.Register("bold-keyword", fg: fg, attrs: TextAttributes.Bold);

        Assert.True(id > 0);
        // ResolveById not exposed in C# — cannot verify resolved attributes
    }

    [Fact]
    public void RegisterStyleWithAllAttributes()
    {
        using var style = SyntaxStyle.Create();

        var fg = new Rgba(1f, 0f, 0f, 1f);
        var attrs = TextAttributes.Bold | TextAttributes.Dim | TextAttributes.Italic | TextAttributes.Underline;
        uint id = style.Register("all-attrs", fg: fg, attrs: attrs);

        Assert.True(id > 0);
        // ResolveById not exposed in C# — cannot verify resolved attributes
    }

    [Fact]
    public void RegisterStyleWithoutColors()
    {
        using var style = SyntaxStyle.Create();

        uint id = style.Register("plain");

        Assert.True(id > 0);
        // ResolveById not exposed in C# — cannot verify fg/bg are null
    }

    [Fact]
    public void RegisterMultipleStyles()
    {
        using var style = SyntaxStyle.Create();

        var fg1 = new Rgba(1f, 0f, 0f, 1f);
        var fg2 = new Rgba(0f, 1f, 0f, 1f);
        var fg3 = new Rgba(0f, 0f, 1f, 1f);

        uint id1 = style.Register("keyword", fg: fg1);
        uint id2 = style.Register("string", fg: fg2);
        uint id3 = style.Register("comment", fg: fg3);

        Assert.NotEqual(id1, id2);
        Assert.NotEqual(id2, id3);
        Assert.NotEqual(id1, id3);
        Assert.Equal((nuint)3, style.StyleCount);
    }

    [Fact]
    public void RegisterSameNameReturnsSameId()
    {
        using var style = SyntaxStyle.Create();

        var fg1 = new Rgba(1f, 0f, 0f, 1f);
        var fg2 = new Rgba(0f, 1f, 0f, 1f);

        uint id1 = style.Register("keyword", fg: fg1);
        uint id2 = style.Register("keyword", fg: fg2);

        Assert.Equal(id1, id2);
        Assert.Equal((nuint)1, style.StyleCount);
        // ResolveById not exposed — cannot verify fg was updated to fg2
    }

    [Fact]
    public void RegisterStyleWithSpecialCharacters()
    {
        using var style = SyntaxStyle.Create();

        var fg = new Rgba(1f, 0f, 0f, 1f);

        style.Register("keyword.control", fg: fg);
        style.Register("variable.parameter", fg: fg);
        style.Register("meta.tag.xml", fg: fg);

        Assert.Equal((nuint)3, style.StyleCount);
    }

    [Fact]
    public void RegisterManyStyles()
    {
        using var style = SyntaxStyle.Create();

        const int count = 100;
        var ids = new uint[count];

        for (int i = 0; i < count; i++)
        {
            var fg = new Rgba(i / 100f, 0f, 0f, 1f);
            ids[i] = style.Register($"style-{i}", fg: fg);
        }

        Assert.Equal((nuint)count, style.StyleCount);

        // All IDs must be unique
        for (int i = 0; i < count; i++)
        {
            for (int j = i + 1; j < count; j++)
            {
                Assert.NotEqual(ids[i], ids[j]);
            }
        }
    }

    // -- ResolveById (adapted — C# API only exposes ResolveByName) --

    [Fact]
    public void ResolveByIdReturnsCorrectStyle()
    {
        // Adapted: register a style and confirm ResolveByName returns the same ID
        using var style = SyntaxStyle.Create();

        var fg = new Rgba(1f, 0f, 0f, 1f);
        var bg = new Rgba(0f, 0f, 0f, 1f);
        var attrs = TextAttributes.Bold | TextAttributes.Dim;

        uint id = style.Register("test", fg: fg, bg: bg, attrs: attrs);
        uint resolved = style.ResolveByName("test");

        Assert.Equal(id, resolved);
    }

    [Fact]
    public void ResolveByIdInvalidIdReturnsNull()
    {
        // Adapted: ResolveByName for a name that was never registered returns 0 (null-equivalent)
        using var style = SyntaxStyle.Create();

        uint resolved = style.ResolveByName("nonexistent-9999");
        Assert.Equal(0u, resolved);
    }

    [Fact]
    public void ResolveByIdZeroReturnsNull()
    {
        // Adapted: an empty registry resolves nothing
        using var style = SyntaxStyle.Create();

        uint resolved = style.ResolveByName("zero");
        Assert.Equal(0u, resolved);
    }

    // -- ResolveByName --

    [Fact]
    public void ResolveByNameReturnsCorrectId()
    {
        using var style = SyntaxStyle.Create();

        var fg = new Rgba(1f, 0f, 0f, 1f);
        uint registeredId = style.Register("keyword", fg: fg);

        uint resolvedId = style.ResolveByName("keyword");
        Assert.Equal(registeredId, resolvedId);
    }

    [Fact]
    public void ResolveByNameNonExistentReturnsZero()
    {
        using var style = SyntaxStyle.Create();

        uint resolved = style.ResolveByName("nonexistent");
        Assert.Equal(0u, resolved);
    }

    [Fact]
    public void ResolveByNameIsCaseSensitive()
    {
        using var style = SyntaxStyle.Create();

        var fg = new Rgba(1f, 0f, 0f, 1f);
        style.Register("keyword", fg: fg);

        Assert.NotEqual(0u, style.ResolveByName("keyword"));
        Assert.Equal(0u, style.ResolveByName("Keyword"));
        Assert.Equal(0u, style.ResolveByName("KEYWORD"));
    }

    [Fact]
    public void ResolveMultipleStyles()
    {
        using var style = SyntaxStyle.Create();

        var fg1 = new Rgba(1f, 0f, 0f, 1f);
        var fg2 = new Rgba(0f, 1f, 0f, 1f);

        uint id1 = style.Register("keyword", fg: fg1);
        uint id2 = style.Register("string", fg: fg2);

        Assert.Equal(id1, style.ResolveByName("keyword"));
        Assert.Equal(id2, style.ResolveByName("string"));
    }

    // -- Merge (not exposed in C# API) --

    [Fact(Skip = "MergeStyles not exposed in C# SyntaxStyle wrapper")]
    public void MergeSingleStyle() { }

    [Fact(Skip = "MergeStyles not exposed in C# SyntaxStyle wrapper")]
    public void MergeTwoStyles() { }

    [Fact(Skip = "MergeStyles not exposed in C# SyntaxStyle wrapper")]
    public void MergeThreeStyles() { }

    [Fact(Skip = "MergeStyles not exposed in C# SyntaxStyle wrapper")]
    public void MergeEmptyArray() { }

    [Fact(Skip = "MergeStyles not exposed in C# SyntaxStyle wrapper")]
    public void MergeWithInvalidIdSkipsIt() { }

    [Fact(Skip = "MergeStyles not exposed in C# SyntaxStyle wrapper")]
    public void MergeCachesResults() { }

    [Fact(Skip = "MergeStyles not exposed in C# SyntaxStyle wrapper")]
    public void MergeDifferentOrderProducesDifferentResults() { }

    // -- Cache (not exposed in C# API) --

    [Fact(Skip = "ClearCache/GetCacheSize not exposed in C# SyntaxStyle wrapper")]
    public void ClearCacheEmptiesCache() { }

    [Fact(Skip = "ClearCache not exposed in C# SyntaxStyle wrapper")]
    public void ClearCachePreservesStyles() { }

    [Fact(Skip = "GetCacheSize not exposed in C# SyntaxStyle wrapper")]
    public void GetCacheSizeReturnsCorrectCount() { }

    // -- Edge cases --

    [Fact]
    public void VeryLongStyleName()
    {
        using var style = SyntaxStyle.Create();

        string longName = new('a', 1000);
        var fg = new Rgba(1f, 0f, 0f, 1f);
        uint id = style.Register(longName, fg: fg);

        Assert.True(id > 0);
        Assert.Equal(id, style.ResolveByName(longName));
    }

    [Fact]
    public void EmptyStringStyleName()
    {
        using var style = SyntaxStyle.Create();

        var fg = new Rgba(1f, 0f, 0f, 1f);
        uint id = style.Register("", fg: fg);

        Assert.True(id > 0);
        Assert.Equal(id, style.ResolveByName(""));
    }

    [Fact]
    public void UnicodeStyleNames()
    {
        using var style = SyntaxStyle.Create();

        var fg = new Rgba(1f, 0f, 0f, 1f);

        uint id1 = style.Register("关键字", fg: fg);
        uint id2 = style.Register("キーワード", fg: fg);
        uint id3 = style.Register("🔑", fg: fg);

        Assert.Equal((nuint)3, style.StyleCount);
        Assert.NotEqual(id1, id2);
        Assert.NotEqual(id2, id3);
    }

    [Fact]
    public void AllColorChannels()
    {
        // Register with specific RGBA channels; cannot verify via ResolveById in C#
        using var style = SyntaxStyle.Create();

        var fg = new Rgba(0.1f, 0.2f, 0.3f, 0.4f);
        var bg = new Rgba(0.5f, 0.6f, 0.7f, 0.8f);

        uint id = style.Register("test", fg: fg, bg: bg);

        Assert.True(id > 0);
        Assert.Equal(id, style.ResolveByName("test"));
    }

    // -- Stress tests --

    [Fact]
    public void StressTestManyRegistrations()
    {
        using var style = SyntaxStyle.Create();

        const int count = 1000;
        for (int i = 0; i < count; i++)
        {
            var fg = new Rgba((i % 256) / 255f, 0f, 0f, 1f);
            style.Register($"style-{i}", fg: fg);
        }

        Assert.Equal((nuint)count, style.StyleCount);
    }

    [Fact(Skip = "MergeStyles not exposed in C# SyntaxStyle wrapper")]
    public void StressTestManyMerges() { }

    [Fact(Skip = "MergeStyles not exposed in C# SyntaxStyle wrapper")]
    public void MergeManyStylesAtOnce() { }

    // -- Lifecycle --

    [Fact]
    public void MultipleInitDeinitCycles()
    {
        for (int i = 0; i < 10; i++)
        {
            using var style = SyntaxStyle.Create();
            var fg = new Rgba(1f, 0f, 0f, 1f);
            style.Register("test", fg: fg);
        }
    }

    [Fact(Skip = "ClearCache not exposed in C# SyntaxStyle wrapper")]
    public void RegisterAndResolveAfterClearCache() { }
}
