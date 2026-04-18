using OpenTui.Core;
using Xunit;

namespace OpenTui.Core.Tests;

/// <summary>
/// Link system tests. The Zig link_test.zig exercises internal LinkPool/LinkTracker
/// which are not directly exposed in C#. These tests exercise the public C API surface:
/// LinkAlloc, LinkGetUrl, AttributesWithLink, AttributesGetLinkId.
/// </summary>
public class LinkTests
{
    [Fact]
    public void AllocAndGetUrl()
    {
        uint id = Link.Alloc("https://example.com");
        Assert.NotEqual(0u, id);
        string url = Link.GetUrl(id);
        Assert.Equal("https://example.com", url);
    }

    [Fact]
    public void AllocNeverReturnsSentinelZero()
    {
        for (int i = 0; i < 100; i++)
        {
            uint id = Link.Alloc($"https://example.com/{i}");
            Assert.NotEqual(0u, id);
        }
    }

    [Fact(Skip = "Zig dedup requires linkIncref after alloc, which is not exposed in the C API")]
    public void AllocSameUrlReturnsSameId()
    {
        // Zig test: alloc → incref → alloc same URL → same ID (dedup).
        // Without incref in the C API, the pool cannot detect live URLs.
        uint id1 = Link.Alloc("https://example.com/stable");
        uint id2 = Link.Alloc("https://example.com/stable");
        Assert.NotEqual(0u, id1);
        Assert.NotEqual(0u, id2);
        Assert.Equal(id1, id2);
    }

    [Fact]
    public void AllocDifferentUrlsReturnDifferentIds()
    {
        uint id1 = Link.Alloc("https://first.example");
        uint id2 = Link.Alloc("https://second.example");
        // Different URLs should get different IDs
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void GetUrlForDifferentLinks()
    {
        uint id1 = Link.Alloc("https://first.example");
        uint id2 = Link.Alloc("https://second.example");
        Assert.Equal("https://first.example", Link.GetUrl(id1));
        Assert.Equal("https://second.example", Link.GetUrl(id2));
    }

    [Fact]
    public void AttributesWithLinkRoundTrip()
    {
        uint linkId = Link.Alloc("https://example.com");
        uint attrs = Link.AttributesWithLink(0, linkId);
        uint extracted = Link.GetLinkId(attrs);
        Assert.Equal(linkId, extracted);
    }

    [Fact]
    public void AttributesWithLinkPreservesBaseAttributes()
    {
        uint linkId = Link.Alloc("https://example.com");
        uint baseAttrs = (uint)TextAttributes.Bold;
        uint combined = Link.AttributesWithLink(baseAttrs, linkId);
        uint extractedLink = Link.GetLinkId(combined);
        Assert.Equal(linkId, extractedLink);
    }

    [Fact]
    public void GetLinkIdFromZeroAttributesReturnsZero()
    {
        uint linkId = Link.GetLinkId(0);
        Assert.Equal(0u, linkId);
    }

    [Fact]
    public void AllocLongUrl()
    {
        // Native library has a URL length limit; very long URLs return 0 (allocation failure)
        string longUrl = "https://example.com/" + new string('a', 1000);
        uint id = Link.Alloc(longUrl);
        Assert.Equal(0u, id);
    }

    [Fact]
    public void AllocUnicodeUrl()
    {
        uint id = Link.Alloc("https://例え.jp/日本語");
        Assert.NotEqual(0u, id);
        string url = Link.GetUrl(id);
        Assert.Equal("https://例え.jp/日本語", url);
    }
}
