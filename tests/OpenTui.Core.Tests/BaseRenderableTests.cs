using Xunit;

namespace OpenTui.Core.Tests;

/// <summary>
/// Tests for the <see cref="BaseRenderable"/> hierarchy.
/// </summary>
public sealed class BaseRenderableTests : IDisposable
{
    private readonly TestRenderContext _ctx;

    public BaseRenderableTests()
    {
        _ctx = new TestRenderContext();
    }

    public void Dispose() { }

    [Fact]
    public void Renderable_Is_BaseRenderable()
    {
        var box = new BoxRenderable(_ctx);
        Assert.IsAssignableFrom<BaseRenderable>(box);
        box.Destroy();
    }

    [Fact]
    public void TextNodeRenderable_Is_BaseRenderable()
    {
        var node = new TextNodeRenderable();
        Assert.IsAssignableFrom<BaseRenderable>(node);
    }

    [Fact]
    public void TextNodeRenderable_Is_Not_Renderable()
    {
        var node = new TextNodeRenderable();
        Assert.False(typeof(Renderable).IsAssignableFrom(node.GetType()));
    }

    [Fact]
    public void SharedNumCounter_ProducesUniqueNumbers()
    {
        var r1 = new BoxRenderable(_ctx);
        var t1 = new TextNodeRenderable();
        var r2 = new BoxRenderable(_ctx);

        // All nums should be unique (shared counter in BaseRenderable)
        Assert.NotEqual(r1.Num, t1.Num);
        Assert.NotEqual(t1.Num, r2.Num);
        Assert.NotEqual(r1.Num, r2.Num);

        r1.Destroy();
        r2.Destroy();
    }

    [Fact]
    public void BaseRenderable_Id_DefaultsToRenderableNum()
    {
        var node = new TextNodeRenderable();
        Assert.StartsWith("renderable-", node.Id);
        Assert.Contains(node.Num.ToString(), node.Id);
    }

    [Fact]
    public void BaseRenderable_Id_CanBeSetExplicitly()
    {
        var node = new TextNodeRenderable(new TextNodeOptions { Id = "custom-id" });
        Assert.Equal("custom-id", node.Id);
    }

    [Fact]
    public void BaseRenderable_IsDirty_DefaultsFalse()
    {
        var node = new TextNodeRenderable();
        Assert.False(node.IsDirty);
    }

    [Fact]
    public void BaseRenderable_RequestRender_MarksDirty()
    {
        var node = new TextNodeRenderable();
        node.RequestRender();
        Assert.True(node.IsDirty);
    }

    [Fact]
    public void BaseRenderable_Visible_DefaultsTrue()
    {
        var node = new TextNodeRenderable();
        Assert.True(node.Visible);
    }

    [Fact]
    public void BaseRenderable_IsEventEmitter()
    {
        var node = new TextNodeRenderable();
        Assert.IsAssignableFrom<EventEmitter>(node);
    }
}
