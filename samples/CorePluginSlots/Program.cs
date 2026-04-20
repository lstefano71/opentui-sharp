using OpenTui.Core;
using OpenTui.Core.Plugins;
using static OpenTui.Core.Plugins.CoreSlotHelpers;

// --- Types ---

var demoContext = new DemoContext { AppName = "core-plugin-slots-demo", Version = "1.0.0" };

// --- State ---

SlotRegistry<DemoContext, DemoSlotData>? slotRegistry = null;
SlotRenderable<DemoContext, DemoSlotData>? statusbarSlot = null;
SlotRenderable<DemoContext, DemoSlotData>? sidebarSlot = null;
TextRenderable? infoText = null;

Action? unregisterClock = null;
Action? unregisterActivity = null;

bool clockEnabled = false;
bool activityEnabled = false;
bool orderFlipped = false;
SlotMode statusbarMode = SlotMode.Append;
bool clockStatusbarError = false;
bool clockSidebarError = false;
bool activityStatusbarError = false;
bool showPlaceholders = true;
List<string> errorHistory = [];
int placeholderCount = 0;

int GetClockOrder() => orderFlipped ? 20 : 0;
int GetActivityOrder() => orderFlipped ? -10 : 10;

// --- Renderer Setup ---

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    BackgroundColor = Rgba.FromHex("#020617"),
});

var exitTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
renderer.On(RendererEventNames.Destroy, () => exitTcs.TrySetResult());

// --- Layout ---

var root = new BoxRenderable(renderer, new BoxOptions
{
    Id = "core-plugin-demo-root",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Percent(100),
    FlexDirection = FlexDirectionValue.Column,
    Padding = 1,
});

var body = new BoxRenderable(renderer, new BoxOptions
{
    Id = "core-plugin-demo-body",
    Width = DimensionValue.Percent(100),
    FlexGrow = 1,
    FlexDirection = FlexDirectionValue.Row,
});

var infoPanel = new BoxRenderable(renderer, new BoxOptions
{
    Id = "core-plugin-demo-info-panel",
    FlexGrow = 1,
    Border = true,
    BorderStyle = BorderStyle.Single,
    BorderColor = Rgba.FromHex("#334155"),
    FlexDirection = FlexDirectionValue.Column,
    Padding = 1,
});

infoText = new TextRenderable(renderer, new TextOptions
{
    Id = "core-plugin-demo-info-text",
    Fg = Rgba.FromHex("#e2e8f0"),
    Content = "",
});

infoPanel.Add(infoText);
root.Add(body);
renderer.Root.Add(root);

// --- Registry ---

slotRegistry = CreateCoreSlotRegistry<DemoContext, DemoSlotData>(renderer, demoContext);
slotRegistry.OnPluginError(evt =>
{
    var slot = evt.Slot ?? "<none>";
    errorHistory.Insert(0, $"{evt.PluginId} [{evt.Phase}/{evt.Source}] @ {slot}: {evt.Error.Message}");
    if (errorHistory.Count > 6) errorHistory.RemoveRange(6, errorHistory.Count - 6);
    UpdateInfoPanel();
});

// --- Slots ---

statusbarSlot = new SlotRenderable<DemoContext, DemoSlotData>(renderer, new SlotRenderableOptions<DemoContext, DemoSlotData>
{
    Id = "core-plugin-demo-statusbar-slot",
    Registry = slotRegistry,
    Name = "statusbar",
    Data = new DemoSlotData { Label = "host-status" },
    Mode = statusbarMode,
    Width = DimensionValue.Percent(100),
    Height = 5,
    AlignItems = AlignValue.Center,
    FlexDirection = FlexDirectionValue.Row,
    PaddingLeft = 1,
    MarginBottom = 1,
    Fallback = () => new TextRenderable(renderer, new TextOptions
    {
        Id = "statusbar-fallback",
        Content = "Fallback statusbar content",
        Fg = Rgba.FromHex("#94a3b8"),
    }),
    PluginFailurePlaceholder = (failure, _) =>
    {
        if (!showPlaceholders) return null;
        return CreateErrorPlaceholder(failure, "#fb7185");
    },
});

sidebarSlot = new SlotRenderable<DemoContext, DemoSlotData>(renderer, new SlotRenderableOptions<DemoContext, DemoSlotData>
{
    Id = "core-plugin-demo-sidebar-slot",
    Registry = slotRegistry,
    Name = "sidebar",
    Data = new DemoSlotData { Section = "plugins" },
    Mode = SlotMode.Replace,
    Width = 36,
    FlexDirection = FlexDirectionValue.Column,
    Padding = 1,
    MarginRight = 1,
    Fallback = () => new TextRenderable(renderer, new TextOptions
    {
        Id = "sidebar-fallback",
        Content = "No sidebar plugin active",
        Fg = Rgba.FromHex("#94a3b8"),
    }),
    PluginFailurePlaceholder = (failure, _) =>
    {
        if (!showPlaceholders) return null;
        return CreateErrorPlaceholder(failure, "#f97316");
    },
});

root.Add(statusbarSlot, 0);
body.Add(sidebarSlot);
body.Add(infoPanel);

// --- Plugin Management ---

SetClockEnabled(true);
SetActivityEnabled(true);

// --- Input ---

renderer.KeyInput.On("keypress", (KeyEvent key) =>
{
    switch (key.Name)
    {
        case "1": SetClockEnabled(!clockEnabled); break;
        case "2": SetActivityEnabled(!activityEnabled); break;
        case "m":
            statusbarMode = NextMode(statusbarMode);
            statusbarSlot!.Mode = statusbarMode;
            UpdateInfoPanel();
            break;
        case "o":
            orderFlipped = !orderFlipped;
            slotRegistry!.UpdateOrder("clock-plugin", GetClockOrder());
            slotRegistry!.UpdateOrder("activity-plugin", GetActivityOrder());
            UpdateInfoPanel();
            break;
        case "r":
            statusbarSlot!.Refresh();
            sidebarSlot!.Refresh();
            UpdateInfoPanel();
            break;
        case "e":
            clockStatusbarError = !clockStatusbarError;
            RemountPlugins();
            break;
        case "w":
            clockSidebarError = !clockSidebarError;
            RemountPlugins();
            break;
        case "d":
            activityStatusbarError = !activityStatusbarError;
            RemountPlugins();
            break;
        case "p":
            showPlaceholders = !showPlaceholders;
            RemountPlugins();
            break;
        case "x":
            clockStatusbarError = false;
            clockSidebarError = false;
            activityStatusbarError = false;
            errorHistory.Clear();
            slotRegistry!.ClearPluginErrors();
            RemountPlugins();
            break;
    }
});

UpdateInfoPanel();
await exitTcs.Task;

// --- Helpers ---

void SetClockEnabled(bool enabled)
{
    if (enabled && !clockEnabled)
    {
        unregisterClock = RegisterCorePlugin(slotRegistry!, CreateClockPlugin());
        clockEnabled = true;
    }
    else if (!enabled && clockEnabled)
    {
        unregisterClock?.Invoke();
        unregisterClock = null;
        clockEnabled = false;
    }
    UpdateInfoPanel();
}

void SetActivityEnabled(bool enabled)
{
    if (enabled && !activityEnabled)
    {
        unregisterActivity = RegisterCorePlugin(slotRegistry!, CreateActivityPlugin());
        activityEnabled = true;
    }
    else if (!enabled && activityEnabled)
    {
        unregisterActivity?.Invoke();
        unregisterActivity = null;
        activityEnabled = false;
    }
    UpdateInfoPanel();
}

void RemountPlugins()
{
    if (clockEnabled)
    {
        unregisterClock?.Invoke();
        unregisterClock = RegisterCorePlugin(slotRegistry!, CreateClockPlugin());
    }
    if (activityEnabled)
    {
        unregisterActivity?.Invoke();
        unregisterActivity = RegisterCorePlugin(slotRegistry!, CreateActivityPlugin());
    }
    statusbarSlot!.Refresh();
    sidebarSlot!.Refresh();
    UpdateInfoPanel();
}

void UpdateInfoPanel()
{
    if (infoText is null) return;
    infoText.Content = string.Join("\n",
    [
        "Core Plugin Slot Demo",
        "",
        $"Statusbar mode: {statusbarMode.ToString().ToUpper()} (press m to cycle)",
        $"Clock plugin: {(clockEnabled ? "ON" : "OFF")} (press 1)",
        $"Activity plugin: {(activityEnabled ? "ON" : "OFF")} (press 2)",
        $"Order flipped: {(orderFlipped ? "YES" : "NO")} (press o)",
        $"Show error placeholders: {(showPlaceholders ? "YES" : "NO")} (press p)",
        "",
        $"Clock statusbar throw: {(clockStatusbarError ? "ON" : "OFF")} (press e)",
        $"Clock sidebar throw: {(clockSidebarError ? "ON" : "OFF")} (press w)",
        $"Activity statusbar throw: {(activityStatusbarError ? "ON" : "OFF")} (press d)",
        "Press x to reset all forced errors.",
        "",
        "Press r to force slot refresh.",
        "",
        "Statusbar fallback is always shown in APPEND mode.",
        "Sidebar fallback appears only when no sidebar plugin is active.",
        "",
        "Recent plugin errors:",
        .. (errorHistory.Count > 0 ? errorHistory : ["(none)"]),
    ]);
}

SlotMode NextMode(SlotMode mode) => mode switch
{
    SlotMode.Append => SlotMode.Replace,
    SlotMode.Replace => SlotMode.SingleWinner,
    _ => SlotMode.Append,
};

BoxRenderable CreateErrorPlaceholder(PluginErrorEvent failure, string color)
{
    placeholderCount++;
    var container = new BoxRenderable(renderer, new BoxOptions
    {
        Id = $"plugin-error-{failure.PluginId}-{placeholderCount}",
        Border = true,
        BorderStyle = BorderStyle.Single,
        BorderColor = Rgba.FromHex(color),
        PaddingLeft = 1,
        PaddingRight = 1,
        MarginLeft = 1,
        BackgroundColor = Rgba.FromHex("#1f1115"),
    });
    container.Add(new TextRenderable(renderer, new TextOptions
    {
        Id = $"plugin-error-title-{failure.PluginId}-{placeholderCount}",
        Content = $"Plugin error: {failure.PluginId}",
        Fg = Rgba.FromHex("#fecaca"),
    }));
    container.Add(new TextRenderable(renderer, new TextOptions
    {
        Id = $"plugin-error-details-{failure.PluginId}-{placeholderCount}",
        Content = $"{failure.Phase}/{failure.Source} @ {failure.Slot ?? "unknown"}",
        Fg = Rgba.FromHex("#fca5a5"),
    }));
    return container;
}

// --- Clock Plugin ---

CorePlugin<DemoContext, DemoSlotData> CreateClockPlugin()
{
    int statusCreates = 0, sidebarCreates = 0;
    TextRenderable? statusText = null;
    TextRenderable? sidebarText = null;
    Timer? timer = null;
    var activeSlots = new HashSet<string>();

    void UpdateClock()
    {
        var ts = DateTime.Now.ToString("T");
        if (statusText is not null) statusText.Content = $"Clock → status ({ts})";
        if (sidebarText is not null) sidebarText.Content = $"Last tick: {ts}";
        renderer.RequestRender();
    }

    void SyncTimer()
    {
        if (activeSlots.Count > 0 && timer is null)
            timer = new Timer(_ => UpdateClock(), null, 0, 1000);
        else if (activeSlots.Count == 0 && timer is not null)
        {
            timer.Dispose();
            timer = null;
        }
    }

    var plugin = new CorePlugin<DemoContext, DemoSlotData>
    {
        Id = "clock-plugin",
        Order = GetClockOrder(),
        DisposeAction = () =>
        {
            timer?.Dispose();
            timer = null;
            activeSlots.Clear();
        },
    };

    plugin.Slots["statusbar"] = CoreSlotContribution<DemoContext, DemoSlotData>.FromManaged(
        new ManagedSlotImpl<DemoContext, DemoSlotData>(
            render: (_, data) =>
            {
                if (clockStatusbarError) throw new InvalidOperationException("Forced clock statusbar failure");
                statusCreates++;
                var item = new BoxRenderable(renderer, new BoxOptions
                {
                    Id = $"clock-statusbar-{statusCreates}",
                    Border = true,
                    BorderStyle = BorderStyle.Single,
                    BorderColor = Rgba.FromHex("#2563eb"),
                    PaddingLeft = 1,
                    PaddingRight = 1,
                    Height = 3,
                    MarginLeft = 1,
                    BackgroundColor = Rgba.FromHex("#0f172a"),
                });
                statusText = new TextRenderable(renderer, new TextOptions
                {
                    Id = $"clock-statusbar-text-{statusCreates}",
                    Content = $"Clock → {data.Label ?? "status"}",
                    Fg = Rgba.FromHex("#93c5fd"),
                });
                item.Add(statusText);
                UpdateClock();
                return item;
            },
            onActivate: _ => { activeSlots.Add("statusbar"); SyncTimer(); },
            onDeactivate: _ => { activeSlots.Remove("statusbar"); statusText = null; SyncTimer(); },
            onDispose: _ => { activeSlots.Remove("statusbar"); statusText = null; SyncTimer(); }));

    plugin.Slots["sidebar"] = CoreSlotContribution<DemoContext, DemoSlotData>.FromManaged(
        new ManagedSlotImpl<DemoContext, DemoSlotData>(
            render: (_, data) =>
            {
                if (clockSidebarError) throw new InvalidOperationException("Forced clock sidebar failure");
                sidebarCreates++;
                var panel = new BoxRenderable(renderer, new BoxOptions
                {
                    Id = $"clock-sidebar-{sidebarCreates}",
                    Border = true,
                    BorderStyle = BorderStyle.Single,
                    BorderColor = Rgba.FromHex("#0ea5e9"),
                    FlexDirection = FlexDirectionValue.Column,
                    Height = 6,
                    MarginBottom = 1,
                    Padding = 1,
                });
                panel.Add(new TextRenderable(renderer, new TextOptions
                {
                    Id = $"clock-sidebar-title-{sidebarCreates}",
                    Content = $"Clock Sidebar ({data.Section ?? "left"})",
                    Fg = Rgba.FromHex("#38bdf8"),
                }));
                sidebarText = new TextRenderable(renderer, new TextOptions
                {
                    Id = $"clock-sidebar-text-{sidebarCreates}",
                    Content = "Last tick: --:--:--",
                    Fg = Rgba.FromHex("#e2e8f0"),
                    MarginTop = 1,
                });
                panel.Add(sidebarText);
                UpdateClock();
                return panel;
            },
            onActivate: _ => { activeSlots.Add("sidebar"); SyncTimer(); },
            onDeactivate: _ => { activeSlots.Remove("sidebar"); sidebarText = null; SyncTimer(); },
            onDispose: _ => { activeSlots.Remove("sidebar"); sidebarText = null; SyncTimer(); }));

    return plugin;
}

// --- Activity Plugin ---

CorePlugin<DemoContext, DemoSlotData> CreateActivityPlugin()
{
    int creates = 0;
    TextRenderable? activityText = null;
    Timer? timer = null;
    int phase = 0;
    string[] pulse = [".", "..", "...", "...."];
    bool active = false;

    void UpdateActivity()
    {
        phase = (phase + 1) % pulse.Length;
        if (activityText is not null)
            activityText.Content = $"Activity{pulse[phase]}";
        renderer.RequestRender();
    }

    void SyncTimer()
    {
        if (active && timer is null)
            timer = new Timer(_ => UpdateActivity(), null, 0, 700);
        else if (!active && timer is not null)
        {
            timer.Dispose();
            timer = null;
        }
    }

    var plugin = new CorePlugin<DemoContext, DemoSlotData>
    {
        Id = "activity-plugin",
        Order = GetActivityOrder(),
        DisposeAction = () =>
        {
            active = false;
            timer?.Dispose();
            timer = null;
        },
    };

    plugin.Slots["statusbar"] = CoreSlotContribution<DemoContext, DemoSlotData>.FromManaged(
        new ManagedSlotImpl<DemoContext, DemoSlotData>(
            render: (_, data) =>
            {
                if (activityStatusbarError) throw new InvalidOperationException("Forced activity statusbar failure");
                creates++;
                var item = new BoxRenderable(renderer, new BoxOptions
                {
                    Id = $"activity-statusbar-{creates}",
                    Border = true,
                    BorderStyle = BorderStyle.Single,
                    BorderColor = Rgba.FromHex("#16a34a"),
                    PaddingLeft = 1,
                    PaddingRight = 1,
                    Height = 3,
                    MarginLeft = 1,
                    BackgroundColor = Rgba.FromHex("#052e16"),
                });
                activityText = new TextRenderable(renderer, new TextOptions
                {
                    Id = $"activity-statusbar-text-{creates}",
                    Content = $"Activity ({data.Label ?? "status"})",
                    Fg = Rgba.FromHex("#86efac"),
                });
                item.Add(activityText);
                return item;
            },
            onActivate: _ => { active = true; SyncTimer(); },
            onDeactivate: _ => { active = false; activityText = null; SyncTimer(); },
            onDispose: _ => { active = false; activityText = null; SyncTimer(); }));

    return plugin;
}

// --- Helper Types ---

sealed class DemoContext
{
    public required string AppName { get; init; }
    public required string Version { get; init; }
}

sealed class DemoSlotData
{
    public string? Label { get; init; }
    public string? Section { get; init; }
}

/// <summary>
/// Generic implementation of ICoreManagedSlot using delegates.
/// </summary>
sealed class ManagedSlotImpl<TContext, TData> : ICoreManagedSlot<TContext, TData>
    where TContext : class
    where TData : class
{
    private readonly Func<TContext, TData, BaseRenderable> _render;
    private readonly Action<TContext>? _onActivate;
    private readonly Action<TContext>? _onDeactivate;
    private readonly Action<TContext>? _onDispose;

    public ManagedSlotImpl(
        Func<TContext, TData, BaseRenderable> render,
        Action<TContext>? onActivate = null,
        Action<TContext>? onDeactivate = null,
        Action<TContext>? onDispose = null)
    {
        _render = render;
        _onActivate = onActivate;
        _onDeactivate = onDeactivate;
        _onDispose = onDispose;
    }

    public BaseRenderable Render(TContext ctx, TData data) => _render(ctx, data);
    public void OnActivate(TContext ctx) => _onActivate?.Invoke(ctx);
    public void OnDeactivate(TContext ctx) => _onDeactivate?.Invoke(ctx);
    public void OnDispose(TContext ctx) => _onDispose?.Invoke(ctx);
}
