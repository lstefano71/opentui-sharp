using Facebook.Yoga;
using OpenTui.Core;
using Xunit;

namespace OpenTui.Core.Tests;

/// <summary>
/// A minimal IRenderContext implementation for testing Renderable tree operations.
/// </summary>
internal sealed class TestRenderContext : EventEmitter, IRenderContext
{
    public int Width { get; set; } = 80;
    public int Height { get; set; } = 24;
    public int FrameId { get; set; }
    public WidthMethod WidthMethod => WidthMethod.Unicode;
    public object? Capabilities => null;
    public bool HasSelection => false;
    public Renderable? CurrentFocusedRenderable { get; set; }
    public KeyHandler KeyInput { get; } = new();
    public KeyHandler InternalKeyInput { get; } = new();

    private readonly HashSet<Renderable> _lifecyclePasses = [];
    public int RenderRequestCount { get; private set; }

    public void AddToHitGrid(int x, int y, uint width, uint height, uint id) { }
    public void PushHitGridScissorRect(int x, int y, uint width, uint height) { }
    public void PopHitGridScissorRect() { }
    public void ClearHitGridScissorRects() { }
    public void RequestRender() => RenderRequestCount++;
    public void RequestLive() { }
    public void DropLive() { }
    public void SetCursorPosition(int x, int y, bool visible) { }
    public void SetCursorStyle(CursorStyleOptions options) { }
    public void SetCursorColor(Rgba color) { }
    public void SetMousePointer(MousePointerStyle shape) { }
    public Selection? GetSelection() => null;
    public void RequestSelectionUpdate() { }
    public void ClearSelection() { }
    public void StartSelection(Renderable renderable, int x, int y) { }
    public void UpdateSelection(Renderable? currentRenderable, int x, int y, bool finishDragging = false) { }
    public void FocusRenderable(Renderable renderable) => CurrentFocusedRenderable = renderable;
    public void BlurRenderable(Renderable renderable) { if (CurrentFocusedRenderable == renderable) CurrentFocusedRenderable = null; }
    public void RegisterLifecyclePass(Renderable renderable) => _lifecyclePasses.Add(renderable);
    public void UnregisterLifecyclePass(Renderable renderable) => _lifecyclePasses.Remove(renderable);
    public IReadOnlySet<Renderable> GetLifecyclePasses() => _lifecyclePasses;
}

/// <summary>
/// A concrete renderable subclass for testing.
/// </summary>
internal sealed class TestRenderable : Renderable
{
    public int RenderSelfCallCount { get; private set; }
    public int OnUpdateCallCount { get; private set; }

    public TestRenderable(IRenderContext ctx, RenderableOptions? options = null)
        : base(ctx, options ?? new RenderableOptions()) { }

    protected override void RenderSelf(OptimizedBuffer buffer, float deltaTime) => RenderSelfCallCount++;
    protected override void OnUpdate(float deltaTime) => OnUpdateCallCount++;
}

public class RenderableTests
{
    [Fact]
    public void Constructor_AssignsId_AndRegistersInStaticLookup()
    {
        var ctx = new TestRenderContext();
        var r = new TestRenderable(ctx, new RenderableOptions { Id = "test-box" });

        Assert.Equal("test-box", r.Id);
        Assert.NotNull(Renderable.GetByNumber(r.Num));
        Assert.Same(r, Renderable.GetByNumber(r.Num));

        r.Destroy();
    }

    [Fact]
    public void Constructor_AutoGeneratesId_WhenNotProvided()
    {
        var ctx = new TestRenderContext();
        var r = new TestRenderable(ctx);

        Assert.StartsWith("renderable-", r.Id);

        r.Destroy();
    }

    [Fact]
    public void Add_AttachesChild_AndSetsParent()
    {
        var ctx = new TestRenderContext();
        var parent = new TestRenderable(ctx);
        var child = new TestRenderable(ctx, new RenderableOptions { Id = "child-1" });

        var index = parent.Add(child);

        Assert.Equal(0, index);
        Assert.Same(parent, child.Parent);
        Assert.Equal(1, parent.GetChildrenCount());
        Assert.Same(child, parent.GetRenderable("child-1"));

        parent.Destroy();
    }

    [Fact]
    public void Remove_DetachesChild_AndNullsParent()
    {
        var ctx = new TestRenderContext();
        var parent = new TestRenderable(ctx);
        var child = new TestRenderable(ctx, new RenderableOptions { Id = "child-1" });

        parent.Add(child);
        parent.Remove("child-1");

        Assert.Equal(0, parent.GetChildrenCount());
        Assert.Null(child.Parent);
        Assert.Null(parent.GetRenderable("child-1"));

        parent.Destroy();
    }

    [Fact]
    public void Visible_TogglesYogaDisplay_AndRequestsRender()
    {
        var ctx = new TestRenderContext();
        var r = new TestRenderable(ctx);

        Assert.True(r.Visible);
        var before = ctx.RenderRequestCount;
        r.Visible = false;
        Assert.False(r.Visible);
        Assert.True(ctx.RenderRequestCount > before);

        r.Destroy();
    }

    [Fact]
    public void ZIndex_SortAffectsChildOrder()
    {
        var ctx = new TestRenderContext();
        var parent = new TestRenderable(ctx);
        var a = new TestRenderable(ctx, new RenderableOptions { Id = "a", ZIndex = 2 });
        var b = new TestRenderable(ctx, new RenderableOptions { Id = "b", ZIndex = 1 });
        var c = new TestRenderable(ctx, new RenderableOptions { Id = "c", ZIndex = 0 });

        parent.Add(a);
        parent.Add(b);
        parent.Add(c);

        // Layout order is insertion order
        var layoutChildren = parent.GetChildren();
        Assert.Equal("a", layoutChildren[0].Id);
        Assert.Equal("b", layoutChildren[1].Id);
        Assert.Equal("c", layoutChildren[2].Id);

        parent.Destroy();
    }

    [Fact]
    public void Opacity_ClampedBetweenZeroAndOne()
    {
        var ctx = new TestRenderContext();
        var r = new TestRenderable(ctx);

        r.Opacity = 2.0f;
        Assert.Equal(1.0f, r.Opacity);

        r.Opacity = -0.5f;
        Assert.Equal(0f, r.Opacity);

        r.Opacity = 0.5f;
        Assert.Equal(0.5f, r.Opacity);

        r.Destroy();
    }

    [Fact]
    public void Focus_And_Blur_EmitEvents()
    {
        var ctx = new TestRenderContext();
        var r = new TestRenderable(ctx);
        r.Focusable = true;

        bool focusEmitted = false;
        bool blurEmitted = false;
        r.On(RenderableEventNames.Focused, () => focusEmitted = true);
        r.On(RenderableEventNames.Blurred, () => blurEmitted = true);

        r.Focus();
        Assert.True(r.Focused);
        Assert.True(focusEmitted);
        Assert.Same(r, ctx.CurrentFocusedRenderable);

        r.Blur();
        Assert.False(r.Focused);
        Assert.True(blurEmitted);

        r.Destroy();
    }

    [Fact]
    public void Destroy_SetsFlag_RemovesFromRegistry_CleansUpChildren()
    {
        var ctx = new TestRenderContext();
        var parent = new TestRenderable(ctx);
        var child = new TestRenderable(ctx, new RenderableOptions { Id = "child-x" });
        parent.Add(child);

        var num = parent.Num;
        parent.Destroy();

        Assert.True(parent.IsDestroyed);
        Assert.Null(Renderable.GetByNumber(num));
        Assert.Equal(0, parent.GetChildrenCount());
    }

    [Fact]
    public void DestroyRecursively_DestroysChildrenFirst()
    {
        var ctx = new TestRenderContext();
        var root = new TestRenderable(ctx);
        var child = new TestRenderable(ctx);
        var grandchild = new TestRenderable(ctx);

        root.Add(child);
        child.Add(grandchild);

        root.DestroyRecursively();

        Assert.True(grandchild.IsDestroyed);
        Assert.True(child.IsDestroyed);
        Assert.True(root.IsDestroyed);
    }

    [Fact]
    public void FindDescendantById_RecursiveSearch()
    {
        var ctx = new TestRenderContext();
        var root = new TestRenderable(ctx, new RenderableOptions { Id = "root" });
        var child = new TestRenderable(ctx, new RenderableOptions { Id = "child" });
        var grandchild = new TestRenderable(ctx, new RenderableOptions { Id = "gc" });

        root.Add(child);
        child.Add(grandchild);

        Assert.Same(grandchild, root.FindDescendantById("gc"));
        Assert.Null(root.FindDescendantById("nonexistent"));

        root.DestroyRecursively();
    }

    [Fact]
    public void Live_PropagatesToParent()
    {
        var ctx = new TestRenderContext();
        var parent = new TestRenderable(ctx);
        var child = new TestRenderable(ctx);
        parent.Add(child);

        child.Live = true;
        Assert.True(child.Live);
        Assert.Equal(1, child.LiveCount);
        Assert.Equal(1, parent.LiveCount);

        child.Live = false;
        Assert.Equal(0, child.LiveCount);
        Assert.Equal(0, parent.LiveCount);

        parent.Destroy();
    }

    [Fact]
    public void RootRenderable_CalculatesLayout()
    {
        var ctx = new TestRenderContext { Width = 100, Height = 50 };
        var root = new RootRenderable(ctx);

        Assert.Equal(100, root.Width);
        Assert.Equal(50, root.Height);

        var child = new TestRenderable(ctx, new RenderableOptions
        {
            Id = "fill-child",
            FlexGrow = 1f,
        });
        root.Add(child);
        root.CalculateLayout();

        // Advance frame so UpdateFromLayout can read layout
        ctx.FrameId = 1;
        child.UpdateFromLayout();

        Assert.Equal(100, child.Width);
        Assert.Equal(50, child.Height);

        root.DestroyRecursively();
    }

    [Fact]
    public void InsertBefore_PlacesChildAtCorrectPosition()
    {
        var ctx = new TestRenderContext();
        var parent = new TestRenderable(ctx);
        var a = new TestRenderable(ctx, new RenderableOptions { Id = "a" });
        var b = new TestRenderable(ctx, new RenderableOptions { Id = "b" });
        var c = new TestRenderable(ctx, new RenderableOptions { Id = "c" });

        parent.Add(a);
        parent.Add(c);
        parent.InsertBefore(b, c);

        var children = parent.GetChildren();
        Assert.Equal("a", children[0].Id);
        Assert.Equal("b", children[1].Id);
        Assert.Equal("c", children[2].Id);

        parent.DestroyRecursively();
    }

    [Fact]
    public void Id_Change_UpdatesParentMap()
    {
        var ctx = new TestRenderContext();
        var parent = new TestRenderable(ctx);
        var child = new TestRenderable(ctx, new RenderableOptions { Id = "old-id" });
        parent.Add(child);

        Assert.Same(child, parent.GetRenderable("old-id"));

        child.Id = "new-id";

        Assert.Null(parent.GetRenderable("old-id"));
        Assert.Same(child, parent.GetRenderable("new-id"));

        parent.DestroyRecursively();
    }

    [Fact]
    public void RootRenderable_Resize_UpdatesDimensions()
    {
        var ctx = new TestRenderContext { Width = 80, Height = 24 };
        var root = new RootRenderable(ctx);

        root.Resize(120, 40);
        Assert.Equal(120, root.Width);
        Assert.Equal(40, root.Height);

        root.Destroy();
    }
}
