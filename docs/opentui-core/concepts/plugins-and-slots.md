# Plugins and slots

The plugin slot system lets host applications define named **slots** in their UI that **plugins** can dynamically contribute renderable content to. This enables extensible layouts where the core UI remains stable but third-party (or modular) code injects widgets into designated areas.

## Core concepts

| Concept | Description |
| --- | --- |
| **SlotRegistry** | Manages plugin registration, ordering, error tracking, and change notification. Scoped to a renderer. |
| **CorePlugin** | A plugin definition with a unique ID, priority order, optional setup/dispose callbacks, and a dictionary of slot contributions. |
| **SlotRenderable** | A `Renderable` subclass that mounts plugin contributions for a specific named slot and reconciles the child tree automatically. |
| **SlotMode** | Controls how multiple plugins compose: `Append` (all + fallback), `Replace` (plugins only), `SingleWinner` (highest priority only). |
| **ICoreManagedSlot** | Lifecycle-aware slot contributions with `OnActivate`, `OnDeactivate`, and `OnDispose` hooks. |

## Architecture

```
┌─────────────────────────────────────────────────┐
│                Host Application                  │
│                                                  │
│   ┌─ SlotRegistry ──────────────────────────┐   │
│   │  CorePlugin "clock"  (order: 0)         │   │
│   │  CorePlugin "activity" (order: 10)      │   │
│   └─────────────────────────────────────────┘   │
│                                                  │
│   Root Container                                 │
│   ├── SlotRenderable "statusbar" (mode: Append)  │
│   │   ├── [fallback text]                        │
│   │   ├── [clock statusbar node]                 │
│   │   └── [activity statusbar node]              │
│   └── SlotRenderable "sidebar" (mode: Replace)   │
│       └── [clock sidebar panel]                  │
└─────────────────────────────────────────────────┘
```

## Quick start

```csharp
using OpenTui.Core;
using OpenTui.Core.Plugins;
using static OpenTui.Core.Plugins.CoreSlotHelpers;

// 1. Create the registry
var registry = CreateCoreSlotRegistry<MyContext, MySlotData>(renderer, myContext);

// 2. Create a SlotRenderable in your layout
var statusbar = new SlotRenderable<MyContext, MySlotData>(renderer, new SlotRenderableOptions<MyContext, MySlotData>
{
    Id = "my-statusbar",
    Registry = registry,
    Name = "statusbar",
    Data = new MySlotData { Label = "status" },
    Mode = SlotMode.Append,
    Fallback = () => new TextRenderable(renderer, new TextOptions { Content = "No plugins" }),
});
root.Add(statusbar);

// 3. Register a plugin
var unregister = RegisterCorePlugin(registry, new CorePlugin<MyContext, MySlotData>
{
    Id = "my-plugin",
    Order = 0,
    Slots = { ["statusbar"] = CoreSlotContribution<MyContext, MySlotData>.FromRenderer(
        (ctx, data) => new TextRenderable(renderer, new TextOptions { Content = "Hello from plugin!" }))
    },
});

// 4. Unregister when done
unregister();
```

## Slot modes

| Mode | Behavior |
| --- | --- |
| `Append` | Fallback content is always shown. Plugin nodes are appended after it. |
| `Replace` | Plugin nodes replace the fallback. Fallback only shows when no plugins are active. |
| `SingleWinner` | Only the highest-priority plugin renders. Others are deactivated. |

You can change the mode at runtime via `slot.Mode = SlotMode.Replace;` — this triggers an automatic refresh.

## Managed slot lifecycle

For plugins that need to manage timers, animations, or other resources, implement `ICoreManagedSlot<TContext, TData>`:

```csharp
class MyManagedSlot : ICoreManagedSlot<MyContext, MySlotData>
{
    private Timer? _timer;

    public BaseRenderable Render(MyContext ctx, MySlotData data) => /* create nodes */;
    public void OnActivate(MyContext ctx) => _timer = new Timer(...);
    public void OnDeactivate(MyContext ctx) { _timer?.Dispose(); _timer = null; }
    public void OnDispose(MyContext ctx) { _timer?.Dispose(); _timer = null; }
}
```

Use `CoreSlotContribution.FromManaged(new MyManagedSlot())` to register it.

## Error handling

When a plugin's renderer throws:
1. The error is reported to the registry and stored in error history.
2. If a `PluginFailurePlaceholder` factory is configured on the slot, an error UI is shown inline.
3. Error listeners registered via `registry.OnPluginError(...)` are notified.

## Dynamic operations

- **Toggle plugins**: Call the unregister action, or re-register with `RegisterCorePlugin`.
- **Reorder**: `registry.UpdateOrder("plugin-id", newOrder)` triggers re-sort and refresh.
- **Batch changes**: `registry.Batch(() => { ... })` suppresses notifications until the batch completes.
- **Force refresh**: `slot.Refresh()` re-resolves all plugins and reconciles the tree.
- **Update data**: `slot.Data = newData` re-renders all plugins with the new data.

## See also

- [Reference: Plugins API](../reference/plugins.md) — complete API surface
- [Sample: CorePluginSlots](../../samples/CorePluginSlots/) — interactive demo
