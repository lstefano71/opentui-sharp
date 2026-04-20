using OpenTui.Core.Plugins;
using Xunit;

namespace OpenTui.Core.Tests.Plugins;

public class SlotRenderableTests
{
    private sealed class TestContext
    {
        public string AppName { get; init; } = "test";
    }

    private sealed class TestData
    {
        public string Label { get; init; } = "default";
    }

    private static (TestRenderContext ctx, SlotRegistry<TestContext, TestData> registry) Setup()
    {
        var ctx = new TestRenderContext();
        var registry = new SlotRegistry<TestContext, TestData>(ctx, new TestContext());
        return (ctx, registry);
    }

    private static CorePlugin<TestContext, TestData> MakePlugin(string id, string slot, TestRenderContext ctx, int order = 0)
    {
        var plugin = new CorePlugin<TestContext, TestData> { Id = id, Order = order };
        plugin.Slots[slot] = CoreSlotContribution<TestContext, TestData>.FromRenderer(
            (_, _) => new TestRenderable(ctx, new RenderableOptions { Id = $"{id}-node" }));
        return plugin;
    }

    [Fact]
    public void Constructor_MountsFallback_WhenNoPlugins()
    {
        var (ctx, registry) = Setup();
        var fallbackNode = new TestRenderable(ctx, new RenderableOptions { Id = "fallback" });

        var slot = new SlotRenderable<TestContext, TestData>(ctx, new SlotRenderableOptions<TestContext, TestData>
        {
            Id = "slot1",
            Registry = registry,
            Name = "main",
            Data = new TestData(),
            Fallback = () => fallbackNode,
        });

        Assert.Equal(1, slot.GetChildrenCount());
        Assert.Same(fallbackNode, slot.GetChildren()[0]);

        slot.DestroyRecursively();
    }

    [Fact]
    public void Refresh_MountsPluginNodes_InAppendMode()
    {
        var (ctx, registry) = Setup();
        var fallbackNode = new TestRenderable(ctx, new RenderableOptions { Id = "fallback" });
        var plugin = MakePlugin("p1", "main", ctx);

        var slot = new SlotRenderable<TestContext, TestData>(ctx, new SlotRenderableOptions<TestContext, TestData>
        {
            Id = "slot1",
            Registry = registry,
            Name = "main",
            Data = new TestData(),
            Mode = SlotMode.Append,
            Fallback = () => fallbackNode,
        });

        // Initially just fallback
        Assert.Equal(1, slot.GetChildrenCount());

        // Register plugin — should auto-refresh
        registry.Register(plugin);

        // In append mode: fallback + plugin node
        Assert.Equal(2, slot.GetChildrenCount());

        slot.DestroyRecursively();
    }

    [Fact]
    public void Refresh_ReplaceMode_HidesFallback_WhenPluginsActive()
    {
        var (ctx, registry) = Setup();
        var fallbackNode = new TestRenderable(ctx, new RenderableOptions { Id = "fallback" });
        var plugin = MakePlugin("p1", "main", ctx);
        registry.Register(plugin);

        var slot = new SlotRenderable<TestContext, TestData>(ctx, new SlotRenderableOptions<TestContext, TestData>
        {
            Id = "slot1",
            Registry = registry,
            Name = "main",
            Data = new TestData(),
            Mode = SlotMode.Replace,
            Fallback = () => fallbackNode,
        });

        // Only plugin node, no fallback in replace mode
        Assert.Equal(1, slot.GetChildrenCount());
        Assert.NotSame(fallbackNode, slot.GetChildren()[0]);

        slot.DestroyRecursively();
    }

    [Fact]
    public void Refresh_SingleWinnerMode_MountsOnlyFirst()
    {
        var (ctx, registry) = Setup();
        var p1 = MakePlugin("p1", "main", ctx, order: 0);
        var p2 = MakePlugin("p2", "main", ctx, order: 10);
        registry.Register(p1);
        registry.Register(p2);

        var slot = new SlotRenderable<TestContext, TestData>(ctx, new SlotRenderableOptions<TestContext, TestData>
        {
            Id = "slot1",
            Registry = registry,
            Name = "main",
            Data = new TestData(),
            Mode = SlotMode.SingleWinner,
        });

        Assert.Equal(1, slot.GetChildrenCount());
        Assert.Equal("p1-node", slot.GetChildren()[0].Id);

        slot.DestroyRecursively();
    }

    [Fact]
    public void ModeSetter_TriggersRefresh()
    {
        var (ctx, registry) = Setup();
        var fallbackNode = new TestRenderable(ctx, new RenderableOptions { Id = "fallback" });
        var p1 = MakePlugin("p1", "main", ctx);
        registry.Register(p1);

        var slot = new SlotRenderable<TestContext, TestData>(ctx, new SlotRenderableOptions<TestContext, TestData>
        {
            Id = "slot1",
            Registry = registry,
            Name = "main",
            Data = new TestData(),
            Mode = SlotMode.Replace,
            Fallback = () => fallbackNode,
        });

        Assert.Equal(1, slot.GetChildrenCount()); // plugin only

        slot.Mode = SlotMode.Append;

        Assert.Equal(2, slot.GetChildrenCount()); // fallback + plugin

        slot.DestroyRecursively();
    }

    [Fact]
    public void Unregister_RemovesPluginFromSlot()
    {
        var (ctx, registry) = Setup();
        var fallbackNode = new TestRenderable(ctx, new RenderableOptions { Id = "fallback" });
        var plugin = MakePlugin("p1", "main", ctx);
        var unregister = registry.Register(plugin);

        var slot = new SlotRenderable<TestContext, TestData>(ctx, new SlotRenderableOptions<TestContext, TestData>
        {
            Id = "slot1",
            Registry = registry,
            Name = "main",
            Data = new TestData(),
            Mode = SlotMode.Replace,
            Fallback = () => fallbackNode,
        });

        Assert.Equal(1, slot.GetChildrenCount());

        unregister();

        // Should fall back since no plugins
        Assert.Equal(1, slot.GetChildrenCount());
        Assert.Same(fallbackNode, slot.GetChildren()[0]);

        slot.DestroyRecursively();
    }

    [Fact]
    public void PluginRenderThrows_ReportsError_ShowsPlaceholder()
    {
        var (ctx, registry) = Setup();
        PluginErrorEvent? reportedError = null;
        registry.OnPluginError(e => reportedError = e);

        var badPlugin = new CorePlugin<TestContext, TestData> { Id = "bad" };
        badPlugin.Slots["main"] = CoreSlotContribution<TestContext, TestData>.FromRenderer(
            (_, _) => throw new InvalidOperationException("render failed"));
        registry.Register(badPlugin);

        var placeholder = new TestRenderable(ctx, new RenderableOptions { Id = "placeholder" });
        var slot = new SlotRenderable<TestContext, TestData>(ctx, new SlotRenderableOptions<TestContext, TestData>
        {
            Id = "slot1",
            Registry = registry,
            Name = "main",
            Data = new TestData(),
            Mode = SlotMode.Replace,
            PluginFailurePlaceholder = (_, _) => placeholder,
        });

        Assert.NotNull(reportedError);
        Assert.Equal("bad", reportedError.PluginId);
        Assert.Equal(PluginErrorPhase.Render, reportedError.Phase);

        // Placeholder should be mounted
        Assert.Equal(1, slot.GetChildrenCount());
        Assert.Same(placeholder, slot.GetChildren()[0]);

        slot.DestroyRecursively();
    }

    [Fact]
    public void ManagedSlot_ReceivesLifecycleHooks()
    {
        var (ctx, registry) = Setup();
        bool activated = false, deactivated = false, disposed = false;

        var managed = new TestManagedSlot(ctx)
        {
            OnActivateCallback = _ => activated = true,
            OnDeactivateCallback = _ => deactivated = true,
            OnDisposeCallback = _ => disposed = true,
        };

        var plugin = new CorePlugin<TestContext, TestData> { Id = "managed-p" };
        plugin.Slots["main"] = CoreSlotContribution<TestContext, TestData>.FromManaged(managed);
        var unregister = registry.Register(plugin);

        var slot = new SlotRenderable<TestContext, TestData>(ctx, new SlotRenderableOptions<TestContext, TestData>
        {
            Id = "slot1",
            Registry = registry,
            Name = "main",
            Data = new TestData(),
            Mode = SlotMode.Replace,
        });

        Assert.True(activated);

        unregister();

        Assert.True(deactivated);
        Assert.True(disposed);

        slot.DestroyRecursively();
    }

    [Fact]
    public void DataSetter_TriggersRerender()
    {
        var (ctx, registry) = Setup();
        TestData? receivedData = null;

        var plugin = new CorePlugin<TestContext, TestData> { Id = "p1" };
        plugin.Slots["main"] = CoreSlotContribution<TestContext, TestData>.FromRenderer(
            (_, data) =>
            {
                receivedData = data;
                return new TestRenderable(ctx, new RenderableOptions { Id = "node" });
            });
        registry.Register(plugin);

        var initialData = new TestData { Label = "first" };
        var slot = new SlotRenderable<TestContext, TestData>(ctx, new SlotRenderableOptions<TestContext, TestData>
        {
            Id = "slot1",
            Registry = registry,
            Name = "main",
            Data = initialData,
            Mode = SlotMode.Replace,
        });

        Assert.Equal("first", receivedData!.Label);

        var newData = new TestData { Label = "second" };
        slot.Data = newData;

        Assert.Equal("second", receivedData!.Label);

        slot.DestroyRecursively();
    }

    private sealed class TestManagedSlot : ICoreManagedSlot<TestContext, TestData>
    {
        private readonly IRenderContext _ctx;
        public Action<TestContext>? OnActivateCallback { get; init; }
        public Action<TestContext>? OnDeactivateCallback { get; init; }
        public Action<TestContext>? OnDisposeCallback { get; init; }

        public TestManagedSlot(IRenderContext ctx) => _ctx = ctx;

        public BaseRenderable Render(TestContext ctx, TestData data) =>
            new TestRenderable(_ctx, new RenderableOptions { Id = "managed-node" });

        public void OnActivate(TestContext ctx) => OnActivateCallback?.Invoke(ctx);
        public void OnDeactivate(TestContext ctx) => OnDeactivateCallback?.Invoke(ctx);
        public void OnDispose(TestContext ctx) => OnDisposeCallback?.Invoke(ctx);
    }
}
