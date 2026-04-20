using Xunit;

namespace OpenTui.Core.Tests;

public sealed class VNodeTests : IDisposable
{
    private readonly TestRenderContext _ctx;

    public VNodeTests()
    {
        _ctx = new TestRenderContext();
    }

    public void Dispose() { }

    [Fact]
    public void Box_Construct_CreatesBoxRenderable()
    {
        var vnode = Constructs.Box();
        var renderable = VNodeRuntime.Instantiate(_ctx, vnode);

        Assert.IsType<BoxRenderable>(renderable);
        renderable.Destroy();
    }

    [Fact]
    public void Text_Construct_CreatesTextRenderable()
    {
        var vnode = Constructs.Text(new TextOptions { Content = "hello" });
        var renderable = VNodeRuntime.Instantiate(_ctx, vnode);

        Assert.IsType<TextRenderable>(renderable);
        renderable.Destroy();
    }

    [Fact]
    public void Instantiate_RecursivelyAddsChildren()
    {
        var vnode = Constructs.Box(children:
        [
            Constructs.Box(),
            Constructs.Box(),
        ]);

        var renderable = VNodeRuntime.Instantiate(_ctx, vnode);

        Assert.Equal(2, renderable.GetChildrenCount());
        Assert.All(renderable.GetChildren(), c => Assert.IsType<BoxRenderable>(c));
        renderable.Destroy();
    }

    [Fact]
    public void Renderable_Add_VNode_WorksEndToEnd()
    {
        var parent = new BoxRenderable(_ctx, new BoxOptions());
        var childVNode = Constructs.Text(new TextOptions { Content = "child" });

        var index = parent.Add(childVNode);

        Assert.Equal(0, index);
        Assert.Equal(1, parent.GetChildrenCount());
        Assert.IsType<TextRenderable>(parent.GetChildren()[0]);
        parent.Destroy();
    }

    [Fact]
    public void NullChildren_AreFilteredOut()
    {
        var vnode = new VNode
        {
            Factory = ctx => new BoxRenderable(ctx),
            Children = [VChild.Null, Constructs.Box(), VChild.Null],
        };

        var renderable = VNodeRuntime.Instantiate(_ctx, vnode);

        Assert.Equal(1, renderable.GetChildrenCount());
        renderable.Destroy();
    }

    [Fact]
    public void NestedVChildLists_AreFlattened()
    {
        var innerList = VChild.List(Constructs.Box(), Constructs.Box());
        var vnode = new VNode
        {
            Factory = ctx => new BoxRenderable(ctx),
            Children = [Constructs.Box(), innerList, Constructs.Box()],
        };

        var renderable = VNodeRuntime.Instantiate(_ctx, vnode);

        Assert.Equal(4, renderable.GetChildrenCount());
        renderable.Destroy();
    }

    [Fact]
    public void MaybeMakeRenderable_HandlesVNode()
    {
        var vnode = Constructs.Box();
        var result = VNodeRuntime.MaybeMakeRenderable(_ctx, vnode);

        Assert.NotNull(result);
        Assert.IsType<BoxRenderable>(result);
        result!.Destroy();
    }

    [Fact]
    public void MaybeMakeRenderable_HandlesRenderable()
    {
        var existing = new BoxRenderable(_ctx);
        var result = VNodeRuntime.MaybeMakeRenderable(_ctx, existing);

        Assert.Same(existing, result);
        existing.Destroy();
    }

    [Fact]
    public void MaybeMakeRenderable_ReturnsNull_ForOtherTypes()
    {
        var result = VNodeRuntime.MaybeMakeRenderable(_ctx, "not a node");
        Assert.Null(result);
    }

    [Fact]
    public void MaybeMakeRenderable_ReturnsNull_ForNull()
    {
        var result = VNodeRuntime.MaybeMakeRenderable(_ctx, null);
        Assert.Null(result);
    }

    [Fact]
    public void VNode_WithOptions_PassesThrough()
    {
        var options = new BoxOptions { Id = "custom-box" };
        var vnode = Constructs.Box(options);
        var renderable = VNodeRuntime.Instantiate(_ctx, vnode);

        Assert.Equal("custom-box", renderable.Id);
        renderable.Destroy();
    }

    [Fact]
    public void AllConstructTypes_ProduceValidRenderables()
    {
        var renderables = new List<Renderable>();

        renderables.Add(VNodeRuntime.Instantiate(_ctx, Constructs.Box()));
        renderables.Add(VNodeRuntime.Instantiate(_ctx, Constructs.Text()));
        renderables.Add(VNodeRuntime.Instantiate(_ctx, Constructs.ASCIIFont()));
        renderables.Add(VNodeRuntime.Instantiate(_ctx, Constructs.Input()));
        renderables.Add(VNodeRuntime.Instantiate(_ctx, Constructs.Select()));
        renderables.Add(VNodeRuntime.Instantiate(_ctx, Constructs.TabSelect()));
        renderables.Add(VNodeRuntime.Instantiate(_ctx, Constructs.FrameBuffer()));
        renderables.Add(VNodeRuntime.Instantiate(_ctx, Constructs.Code()));
        renderables.Add(VNodeRuntime.Instantiate(_ctx, Constructs.ScrollBox()));
        renderables.Add(VNodeRuntime.Instantiate(_ctx, Constructs.Generic(new GenericOptions
        {
            Render = (buffer, dt, self) => { }
        })));

        Assert.Equal(10, renderables.Count);
        Assert.All(renderables, r => Assert.NotNull(r));

        foreach (var r in renderables) r.Destroy();
    }

    [Fact]
    public void ImplicitConversion_VNode_ToVChild()
    {
        VNode vnode = Constructs.Box();
        VChild child = vnode; // implicit conversion

        Assert.IsType<VChild.OfVNode>(child);
    }

    [Fact]
    public void ImplicitConversion_Renderable_ToVChild()
    {
        var renderable = new BoxRenderable(_ctx);
        VChild child = renderable; // implicit conversion

        Assert.IsType<VChild.OfRenderable>(child);
        renderable.Destroy();
    }

    [Fact]
    public void DirectRenderable_Children_AreAdded()
    {
        var directChild = new BoxRenderable(_ctx, new BoxOptions { Id = "direct" });
        var vnode = new VNode
        {
            Factory = ctx => new BoxRenderable(ctx),
            Children = [directChild],
        };

        var renderable = VNodeRuntime.Instantiate(_ctx, vnode);

        Assert.Equal(1, renderable.GetChildrenCount());
        Assert.Same(directChild, renderable.GetChildren()[0]);
        renderable.Destroy();
    }

    [Fact]
    public void DeeplyNestedTree_InstantiatesCorrectly()
    {
        var vnode = Constructs.Box(children:
        [
            Constructs.Box(children:
            [
                Constructs.Text(new TextOptions { Content = "deep" }),
            ]),
        ]);

        var root = VNodeRuntime.Instantiate(_ctx, vnode);

        Assert.Equal(1, root.GetChildrenCount());
        var mid = root.GetChildren()[0];
        Assert.IsType<BoxRenderable>(mid);
        Assert.Equal(1, mid.GetChildrenCount());
        Assert.IsType<TextRenderable>(mid.GetChildren()[0]);
        root.Destroy();
    }
}
