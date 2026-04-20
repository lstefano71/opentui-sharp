using OpenTui.Core.Plugins;
using Xunit;

namespace OpenTui.Core.Tests.Plugins;

public class SlotRegistryTests
{
    private sealed class TestContext
    {
        public string AppName { get; init; } = "test";
    }

    private sealed class TestData
    {
        public string Label { get; init; } = "default";
    }

    private sealed class SimplePlugin : IPlugin<TestContext, TestData>
    {
        private readonly Dictionary<string, SlotRendererDelegate<TestContext, TestData>> _renderers = new();
        public string Id { get; init; } = "test-plugin";
        public int Order { get; set; }
        public bool SetupCalled { get; private set; }
        public bool DisposeCalled { get; private set; }
        public bool ShouldThrowOnSetup { get; init; }
        public bool ShouldThrowOnDispose { get; init; }

        public void AddSlot(string name, SlotRendererDelegate<TestContext, TestData> renderer)
            => _renderers[name] = renderer;

        public void Setup(TestContext context, IRenderContext renderContext)
        {
            if (ShouldThrowOnSetup) throw new InvalidOperationException("Setup failed");
            SetupCalled = true;
        }

        public void Dispose()
        {
            if (ShouldThrowOnDispose) throw new InvalidOperationException("Dispose failed");
            DisposeCalled = true;
        }

        public SlotRendererDelegate<TestContext, TestData>? GetSlotRenderer(string slotName)
            => _renderers.TryGetValue(slotName, out var r) ? r : null;
    }

    private static SlotRegistry<TestContext, TestData> CreateRegistry()
    {
        var ctx = new TestRenderContext();
        return new SlotRegistry<TestContext, TestData>(ctx, new TestContext());
    }

    [Fact]
    public void Register_CallsSetup_AndReturnsUnregister()
    {
        var registry = CreateRegistry();
        var plugin = new SimplePlugin();

        var unregister = registry.Register(plugin);

        Assert.True(plugin.SetupCalled);
        Assert.NotNull(unregister);
    }

    [Fact]
    public void Register_DuplicateId_Throws()
    {
        var registry = CreateRegistry();
        registry.Register(new SimplePlugin { Id = "dup" });

        Assert.Throws<InvalidOperationException>(() =>
            registry.Register(new SimplePlugin { Id = "dup" }));
    }

    [Fact]
    public void Register_SetupFailure_ReportsError_ReturnsNoop()
    {
        var registry = CreateRegistry();
        var plugin = new SimplePlugin { ShouldThrowOnSetup = true };

        var unregister = registry.Register(plugin);

        // Should not throw, just report error
        Assert.Single(registry.GetPluginErrors());
        Assert.Equal(PluginErrorPhase.Setup, registry.GetPluginErrors()[0].Phase);

        // Unregister is a noop (plugin was never added)
        unregister(); // should not throw
    }

    [Fact]
    public void Unregister_RemovesPlugin_CallsDispose()
    {
        var registry = CreateRegistry();
        var plugin = new SimplePlugin();
        var unregister = registry.Register(plugin);

        unregister();

        Assert.True(plugin.DisposeCalled);
        Assert.Empty(registry.ResolveEntries("any-slot"));
    }

    [Fact]
    public void Unregister_DisposeThrows_ReportsError()
    {
        var registry = CreateRegistry();
        var plugin = new SimplePlugin { ShouldThrowOnDispose = true };
        var unregister = registry.Register(plugin);

        unregister();

        Assert.Single(registry.GetPluginErrors());
        Assert.Equal(PluginErrorPhase.Dispose, registry.GetPluginErrors()[0].Phase);
    }

    [Fact]
    public void ResolveEntries_ReturnsSortedByOrder_ThenRegistration()
    {
        var registry = CreateRegistry();
        var ctx = new TestRenderContext();

        var plugin1 = new SimplePlugin { Id = "p1", Order = 10 };
        plugin1.AddSlot("main", (_, _) => new TestRenderable(ctx));

        var plugin2 = new SimplePlugin { Id = "p2", Order = 5 };
        plugin2.AddSlot("main", (_, _) => new TestRenderable(ctx));

        var plugin3 = new SimplePlugin { Id = "p3", Order = 5 };
        plugin3.AddSlot("main", (_, _) => new TestRenderable(ctx));

        registry.Register(plugin1);
        registry.Register(plugin2);
        registry.Register(plugin3);

        var entries = registry.ResolveEntries("main");

        Assert.Equal(3, entries.Count);
        Assert.Equal("p2", entries[0].Id);
        Assert.Equal("p3", entries[1].Id);
        Assert.Equal("p1", entries[2].Id);
    }

    [Fact]
    public void UpdateOrder_ChangesResolutionOrder()
    {
        var registry = CreateRegistry();
        var ctx = new TestRenderContext();

        var plugin1 = new SimplePlugin { Id = "p1", Order = 0 };
        plugin1.AddSlot("main", (_, _) => new TestRenderable(ctx));

        var plugin2 = new SimplePlugin { Id = "p2", Order = 10 };
        plugin2.AddSlot("main", (_, _) => new TestRenderable(ctx));

        registry.Register(plugin1);
        registry.Register(plugin2);

        // Initially p1 first
        Assert.Equal("p1", registry.ResolveEntries("main")[0].Id);

        // Move p2 before p1
        registry.UpdateOrder("p2", -5);

        Assert.Equal("p2", registry.ResolveEntries("main")[0].Id);
    }

    [Fact]
    public void Subscribe_NotifiesOnChange()
    {
        var registry = CreateRegistry();
        int notifyCount = 0;
        registry.Subscribe(() => notifyCount++);

        var plugin = new SimplePlugin();
        registry.Register(plugin);
        Assert.Equal(1, notifyCount);

        registry.Unregister(plugin.Id);
        Assert.Equal(2, notifyCount);
    }

    [Fact]
    public void Batch_SuppressesNotifications_UntilComplete()
    {
        var registry = CreateRegistry();
        int notifyCount = 0;
        registry.Subscribe(() => notifyCount++);

        registry.Batch(() =>
        {
            registry.Register(new SimplePlugin { Id = "a" });
            registry.Register(new SimplePlugin { Id = "b" });
            Assert.Equal(0, notifyCount); // suppressed
        });

        Assert.Equal(1, notifyCount); // single flush
    }

    [Fact]
    public void OnPluginError_NotifiesListeners()
    {
        var registry = CreateRegistry();
        PluginErrorEvent? received = null;
        registry.OnPluginError(e => received = e);

        registry.ReportPluginError(new PluginErrorReport
        {
            PluginId = "test",
            Phase = PluginErrorPhase.Render,
            Error = new Exception("boom"),
        });

        Assert.NotNull(received);
        Assert.Equal("test", received.PluginId);
    }

    [Fact]
    public void ClearPluginErrors_EmptiesHistory()
    {
        var registry = CreateRegistry();
        registry.ReportPluginError(new PluginErrorReport
        {
            PluginId = "x",
            Phase = PluginErrorPhase.Render,
            Error = new Exception("e"),
        });

        Assert.NotEmpty(registry.GetPluginErrors());

        registry.ClearPluginErrors();

        Assert.Empty(registry.GetPluginErrors());
    }

    [Fact]
    public void Clear_RemovesAllPlugins_CallsDispose()
    {
        var registry = CreateRegistry();
        var p1 = new SimplePlugin { Id = "p1" };
        var p2 = new SimplePlugin { Id = "p2" };
        registry.Register(p1);
        registry.Register(p2);

        registry.Clear();

        Assert.True(p1.DisposeCalled);
        Assert.True(p2.DisposeCalled);
        Assert.Empty(registry.ResolveEntries("any"));
    }

    [Fact]
    public void MaxPluginErrors_CapsHistory()
    {
        var ctx = new TestRenderContext();
        var registry = new SlotRegistry<TestContext, TestData>(ctx, new TestContext(), new SlotRegistryOptions { MaxPluginErrors = 3 });

        for (int i = 0; i < 10; i++)
        {
            registry.ReportPluginError(new PluginErrorReport
            {
                PluginId = $"p{i}",
                Phase = PluginErrorPhase.Render,
                Error = new Exception($"error {i}"),
            });
        }

        Assert.Equal(3, registry.GetPluginErrors().Count);
        Assert.Equal("p7", registry.GetPluginErrors()[0].PluginId);
    }
}
