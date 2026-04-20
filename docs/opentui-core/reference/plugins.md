# Plugins — API reference

Namespace: `OpenTui.Core.Plugins`

## SlotMode (enum)

Controls how multiple plugin contributions compose within a slot.

| Value | Description |
| --- | --- |
| `Append` | All plugin outputs shown alongside fallback content. |
| `Replace` | Plugin outputs replace the fallback entirely. |
| `SingleWinner` | Only the highest-priority plugin renders. |

## PluginErrorPhase (enum)

Indicates during which phase a plugin error occurred.

| Value | Description |
| --- | --- |
| `Setup` | During plugin registration/setup. |
| `Render` | During slot rendering. |
| `Dispose` | During plugin disposal. |
| `ErrorPlaceholder` | While rendering the error placeholder. |

## PluginErrorEvent (record)

Describes a plugin error event.

| Property | Type | Description |
| --- | --- | --- |
| `PluginId` | `string` | Plugin that caused the error. |
| `Slot` | `string?` | Slot name (if applicable). |
| `Phase` | `PluginErrorPhase` | When the error occurred. |
| `Source` | `string` | Error source ("registry" or "core"). |
| `Error` | `Exception` | The actual exception. |
| `Timestamp` | `long` | UTC milliseconds since epoch. |

## SlotRegistry\<TContext, TData\>

Manages plugin registration, ordering, error tracking, and slot resolution.

### Constructor

```csharp
new SlotRegistry<TContext, TData>(IRenderContext ctx, TContext context, SlotRegistryOptions? options = null)
```

### Properties

| Property | Type | Description |
| --- | --- | --- |
| `RenderContext` | `IRenderContext` | Associated render context. |
| `Context` | `TContext` | Shared host context. |

### Methods

| Method | Returns | Description |
| --- | --- | --- |
| `Register(IPlugin<TContext, TData> plugin)` | `Action` | Registers a plugin. Returns unregister action. |
| `Unregister(string id)` | `bool` | Removes a plugin by ID. |
| `UpdateOrder(string id, int order)` | `bool` | Changes a plugin's sort priority. |
| `Clear()` | `void` | Unregisters all plugins. |
| `Subscribe(Action listener)` | `Action` | Subscribes to change notifications. Returns unsubscribe. |
| `OnPluginError(Action<PluginErrorEvent> listener)` | `Action` | Subscribes to error events. Returns unsubscribe. |
| `Batch(Action run)` | `void` | Suppresses notifications during the batch. |
| `Batch<T>(Func<T> run)` | `T` | Batch with return value. |
| `ResolveEntries(string slot)` | `List<ResolvedSlotEntry<TContext, TData>>` | Resolves renderers for a slot. |
| `ReportPluginError(PluginErrorReport report)` | `PluginErrorEvent` | Reports and stores a plugin error. |
| `GetPluginErrors()` | `IReadOnlyList<PluginErrorEvent>` | Returns error history. |
| `ClearPluginErrors()` | `void` | Clears error history. |
| `Configure(SlotRegistryOptions options)` | `void` | Updates configuration. |

### Static factory

```csharp
SlotRegistry<TContext, TData>.Create(IRenderContext ctx, string key, TContext context, SlotRegistryOptions? options)
```

Creates or retrieves a keyed registry for the given render context. Same key + context = same instance.

## SlotRegistryOptions

| Property | Type | Default | Description |
| --- | --- | --- | --- |
| `OnPluginError` | `Action<PluginErrorEvent>?` | `null` | Callback for every error. |
| `DebugPluginErrors` | `bool` | `false` | Write debug info to stderr. |
| `MaxPluginErrors` | `int` | `100` | Max errors retained in history. |

## CorePlugin\<TContext, TData\>

High-level plugin definition implementing `IPlugin<TContext, TData>`.

| Property | Type | Description |
| --- | --- | --- |
| `Id` | `string` | Unique plugin identifier. (required) |
| `Order` | `int` | Sort priority (lower first). Default 0. |
| `Slots` | `Dictionary<string, CoreSlotContribution<TContext, TData>>` | Slot contributions keyed by slot name. |
| `SetupAction` | `Action<TContext, IRenderContext>?` | Called during registration. |
| `DisposeAction` | `Action?` | Called during unregistration. |

## CoreSlotContribution\<TContext, TData\>

Wraps either a simple renderer or a managed slot.

| Static method | Description |
| --- | --- |
| `FromRenderer(SlotRendererDelegate<TContext, TData> renderer)` | Simple renderer function. |
| `FromManaged(ICoreManagedSlot<TContext, TData> managedSlot)` | Managed slot with lifecycle hooks. |

## ICoreManagedSlot\<TContext, TData\> (interface)

Lifecycle-aware slot contribution.

| Method | Description |
| --- | --- |
| `Render(TContext ctx, TData data)` | Produces the renderable node. |
| `OnActivate(TContext ctx)` | Called when contribution becomes visible. |
| `OnDeactivate(TContext ctx)` | Called when hidden by mode change or unregistration. |
| `OnDispose(TContext ctx)` | Called when fully removed from registry. |

## SlotRenderable\<TContext, TData\>

A `Renderable` that mounts plugin contributions for a named slot.

### Constructor

```csharp
new SlotRenderable<TContext, TData>(IRenderContext ctx, SlotRenderableOptions<TContext, TData> options)
```

### Properties

| Property | Type | Description |
| --- | --- | --- |
| `Mode` | `SlotMode` | Get/set composition mode. Setting triggers refresh. |
| `Data` | `TData` | Get/set slot data. Setting triggers refresh. |

### Methods

| Method | Description |
| --- | --- |
| `Refresh()` | Force re-resolution of all plugins and reconcile child tree. |

## SlotRenderableOptions\<TContext, TData\>

Extends `RenderableOptions` with slot-specific configuration.

| Property | Type | Description |
| --- | --- | --- |
| `Registry` | `SlotRegistry<TContext, TData>` | The registry to resolve plugins from. (required) |
| `Name` | `string` | Slot name this renderable manages. (required) |
| `Data` | `TData?` | Initial data passed to plugin renderers. (required) |
| `Mode` | `SlotMode` | Initial composition mode. Default `Append`. |
| `Fallback` | `Func<BaseRenderable?>?` | Factory for fallback content. |
| `PluginFailurePlaceholder` | `Func<PluginErrorEvent, TContext, BaseRenderable?>?` | Factory for error placeholders. |

## CoreSlotHelpers (static class)

Convenience factory methods.

| Method | Description |
| --- | --- |
| `CreateCoreSlotRegistry<TContext, TData>(ctx, context, options?)` | Creates a core-keyed slot registry. |
| `RegisterCorePlugin<TContext, TData>(registry, plugin)` | Registers a CorePlugin, returns unregister action. |

## SlotRendererDelegate\<TContext, TData\> (delegate)

```csharp
public delegate BaseRenderable SlotRendererDelegate<in TContext, in TData>(TContext ctx, TData data)
```

The function signature for slot renderers.
