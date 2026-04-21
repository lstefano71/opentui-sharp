using System.Text;
using OpenTui.Core;
using OpenTui.Core.Managed;
using Xunit;

namespace OpenTui.Core.Tests;

/// <summary>
/// Tests that ManagedRenderer emits OSC 8 hyperlink escape sequences
/// when cells contain link IDs and a ManagedLinkPool is attached.
/// </summary>
public sealed class ManagedRendererHyperlinkTests : IDisposable
{
    private readonly ManagedRenderer _renderer;
    private readonly ManagedLinkPool _linkPool;
    private readonly DummyWriter _writer = new();

    public ManagedRendererHyperlinkTests()
    {
        _linkPool = new ManagedLinkPool();
        _renderer = ManagedRenderer.Create(10, 3, testing: true);
        _renderer.LinkPool = _linkPool;
    }

    public void Dispose() => _renderer.Dispose();

    [Fact]
    public void Render_EmitsOsc8Open_WhenCellHasLinkId()
    {
        uint linkId = _linkPool.Alloc("https://example.com");
        var buf = _renderer.GetNextBuffer();
        uint attrs = ManagedLinkPool.AttributesWithLink(0, linkId);
        buf.SetRaw(0, 0, new ManagedBuffer.Cell('A', Rgba.White, Rgba.Black, attrs));

        _renderer.Render(_writer);
        string output = Encoding.UTF8.GetString(_renderer.LastOutputForTest);

        Assert.Contains($"\x1b]8;id={linkId};https://example.com\x1b\\", output);
    }

    [Fact]
    public void Render_EmitsOsc8Close_WhenLinkEnds()
    {
        uint linkId = _linkPool.Alloc("https://example.com");
        var buf = _renderer.GetNextBuffer();
        uint linkAttrs = ManagedLinkPool.AttributesWithLink(0, linkId);
        buf.SetRaw(0, 0, new ManagedBuffer.Cell('A', Rgba.White, Rgba.Black, linkAttrs));
        buf.SetRaw(1, 0, new ManagedBuffer.Cell('B', Rgba.White, Rgba.Black, 0));

        _renderer.Render(_writer);
        string output = Encoding.UTF8.GetString(_renderer.LastOutputForTest);

        // Should contain the close sequence after the linked cell
        Assert.Contains("\x1b]8;;\x1b\\", output);
    }

    [Fact]
    public void Render_EmitsOsc8Close_AtEndOfFrame_WhenLinkStillOpen()
    {
        uint linkId = _linkPool.Alloc("https://example.com");
        var buf = _renderer.GetNextBuffer();
        uint linkAttrs = ManagedLinkPool.AttributesWithLink(0, linkId);

        // Fill all cells in the last row with a link so it's open at end of frame
        for (uint x = 0; x < 10; x++)
            buf.SetRaw(x, 2, new ManagedBuffer.Cell('Z', Rgba.White, Rgba.Black, linkAttrs));

        _renderer.Render(_writer);
        string output = Encoding.UTF8.GetString(_renderer.LastOutputForTest);

        // The close should appear before the SGR reset at end of frame
        int closeIdx = output.LastIndexOf("\x1b]8;;\x1b\\");
        int resetIdx = output.LastIndexOf("\x1b[0m");
        Assert.True(closeIdx >= 0, "Expected OSC 8 close sequence");
        Assert.True(resetIdx >= 0, "Expected SGR reset");
        Assert.True(closeIdx < resetIdx, "OSC 8 close should precede end-of-frame SGR reset");
    }

    [Fact]
    public void Render_SwitchesBetweenDifferentLinks()
    {
        uint link1 = _linkPool.Alloc("https://first.example");
        uint link2 = _linkPool.Alloc("https://second.example");
        var buf = _renderer.GetNextBuffer();

        buf.SetRaw(0, 0, new ManagedBuffer.Cell('A', Rgba.White, Rgba.Black,
            ManagedLinkPool.AttributesWithLink(0, link1)));
        buf.SetRaw(1, 0, new ManagedBuffer.Cell('B', Rgba.White, Rgba.Black,
            ManagedLinkPool.AttributesWithLink(0, link2)));

        _renderer.Render(_writer);
        string output = Encoding.UTF8.GetString(_renderer.LastOutputForTest);

        // Both link opens should be present
        Assert.Contains("https://first.example", output);
        Assert.Contains("https://second.example", output);
        // Closing the first link before opening the second
        int firstOpen = output.IndexOf("https://first.example");
        int close = output.IndexOf("\x1b]8;;\x1b\\", firstOpen);
        int secondOpen = output.IndexOf("https://second.example");
        Assert.True(close < secondOpen, "First link should be closed before second link opens");
    }

    [Fact]
    public void Render_NoOsc8_WhenNoLinkPool()
    {
        using var renderer = ManagedRenderer.Create(10, 3, testing: true);
        // No LinkPool set
        var buf = renderer.GetNextBuffer();
        buf.SetRaw(0, 0, new ManagedBuffer.Cell('A', Rgba.White, Rgba.Black, 0xFF00)); // fake attrs with upper bits

        renderer.Render(_writer);
        string output = Encoding.UTF8.GetString(renderer.LastOutputForTest);

        Assert.DoesNotContain("\x1b]8;", output);
    }

    /// <summary>Dummy writer that does nothing (testing mode captures internally).</summary>
    private sealed class DummyWriter : ITerminalWriter
    {
        public void Write(ReadOnlySpan<byte> data) { }
        public void Flush() { }
    }
}
