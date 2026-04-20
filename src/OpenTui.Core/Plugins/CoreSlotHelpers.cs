namespace OpenTui.Core.Plugins;

/// <summary>
/// Convenience factory methods for the core plugin slot system.
/// </summary>
public static class CoreSlotHelpers
{
    /// <summary>
    /// Creates a core slot registry scoped to the given renderer with the "core:slot-registry" key.
    /// </summary>
    public static SlotRegistry<TContext, TData> CreateCoreSlotRegistry<TContext, TData>(
        IRenderContext renderContext,
        TContext context,
        SlotRegistryOptions? options = null)
        where TContext : class
        where TData : class
    {
        return SlotRegistry<TContext, TData>.Create(renderContext, "core:slot-registry", context, options);
    }

    /// <summary>
    /// Registers a <see cref="CorePlugin{TContext,TData}"/> with the registry.
    /// Returns an action that unregisters it.
    /// </summary>
    public static Action RegisterCorePlugin<TContext, TData>(
        SlotRegistry<TContext, TData> registry,
        CorePlugin<TContext, TData> plugin)
        where TContext : class
        where TData : class
    {
        return registry.Register(plugin);
    }
}
