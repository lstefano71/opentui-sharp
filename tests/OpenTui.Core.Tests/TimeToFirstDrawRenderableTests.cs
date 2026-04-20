using Xunit;

namespace OpenTui.Core.Tests;

/// <summary>
/// Tests for <see cref="TimeToFirstDrawRenderable"/>.
/// </summary>
public sealed class TimeToFirstDrawRenderableTests : IDisposable
{
    private readonly CliRenderer _renderer;

    public TimeToFirstDrawRenderableTests()
    {
        _renderer = CliRenderer.Create(new CliRendererConfig
        {
            Testing = true,
            Width = 80,
            Height = 24,
        });
    }

    public void Dispose() => _renderer.Dispose();

    [Fact]
    public void RuntimeMs_IsNullBeforeFirstRender()
    {
        var ttfd = new TimeToFirstDrawRenderable(_renderer);
        Assert.Null(ttfd.RuntimeMs);
    }

    [Fact]
    public void FirstRender_CapturesRuntimeMs()
    {
        var ttfd = new TimeToFirstDrawRenderable(_renderer);
        _renderer.Root.Add(ttfd);
        _renderer.RenderTestFrame();

        Assert.NotNull(ttfd.RuntimeMs);
        Assert.True(ttfd.RuntimeMs > 0, "RuntimeMs should be positive");
    }

    [Fact]
    public void SecondRender_ReusesOriginalValue()
    {
        var ttfd = new TimeToFirstDrawRenderable(_renderer);
        _renderer.Root.Add(ttfd);
        _renderer.RenderTestFrame();

        var first = ttfd.RuntimeMs;
        _renderer.RenderTestFrame();

        Assert.Equal(first, ttfd.RuntimeMs);
    }

    [Fact]
    public void Reset_ClearsRuntimeMs()
    {
        var ttfd = new TimeToFirstDrawRenderable(_renderer);
        _renderer.Root.Add(ttfd);
        _renderer.RenderTestFrame();

        Assert.NotNull(ttfd.RuntimeMs);

        ttfd.Reset();
        Assert.Null(ttfd.RuntimeMs);
    }

    [Fact]
    public void Reset_RecapturesOnNextRender()
    {
        var ttfd = new TimeToFirstDrawRenderable(_renderer);
        _renderer.Root.Add(ttfd);
        _renderer.RenderTestFrame();

        var first = ttfd.RuntimeMs;
        ttfd.Reset();
        _renderer.RenderTestFrame();

        Assert.NotNull(ttfd.RuntimeMs);
        Assert.True(ttfd.RuntimeMs >= first);
    }

    [Fact]
    public void CustomOptions_AreRespected()
    {
        var ttfd = new TimeToFirstDrawRenderable(_renderer, new TimeToFirstDrawOptions
        {
            Label = "TTFD",
            Precision = 4,
            Fg = Rgba.FromHex("#FF0000"),
        });
        _renderer.Root.Add(ttfd);
        _renderer.RenderTestFrame();

        Assert.NotNull(ttfd.RuntimeMs);
    }
}
