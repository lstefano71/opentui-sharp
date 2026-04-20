namespace OpenTui.Core.Plugins;

/// <summary>
/// A slot renderer delegate: given a read-only context and slot-specific data, produces a renderable node.
/// </summary>
/// <typeparam name="TContext">The host context type.</typeparam>
/// <typeparam name="TData">The per-slot data type.</typeparam>
public delegate BaseRenderable SlotRendererDelegate<in TContext, in TData>(TContext ctx, TData data)
    where TContext : class
    where TData : class;

/// <summary>
/// Resolved slot renderer entry: a plugin id paired with its render function.
/// </summary>
public sealed class ResolvedSlotEntry<TContext, TData>
    where TContext : class
    where TData : class
{
    public required string Id { get; init; }
    public required SlotRendererDelegate<TContext, TData> Renderer { get; init; }
}

/// <summary>
/// Defines a plugin that contributes renderable content to named slots.
/// The low-level registry interface — <see cref="CorePlugin{TContext,TData}"/> provides
/// a higher-level API with managed lifecycle hooks.
/// </summary>
/// <typeparam name="TContext">The host context type.</typeparam>
/// <typeparam name="TData">The per-slot data type (same for all slots in this registry).</typeparam>
public interface IPlugin<TContext, TData>
    where TContext : class
    where TData : class
{
    /// <summary>Unique plugin identifier.</summary>
    string Id { get; }

    /// <summary>Sort priority (lower values render first). Default is 0.</summary>
    int Order { get; set; }

    /// <summary>Called during registration to perform one-time setup.</summary>
    void Setup(TContext context, IRenderContext renderContext);

    /// <summary>Called when the plugin is unregistered.</summary>
    void Dispose();

    /// <summary>
    /// Returns the slot renderer for the given slot name, or null if this plugin does not contribute to that slot.
    /// </summary>
    SlotRendererDelegate<TContext, TData>? GetSlotRenderer(string slotName);
}
