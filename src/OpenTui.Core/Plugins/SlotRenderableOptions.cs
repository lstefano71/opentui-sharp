namespace OpenTui.Core.Plugins;

/// <summary>
/// Options for creating a <see cref="SlotRenderable{TContext,TData}"/>.
/// </summary>
/// <typeparam name="TContext">The host context type.</typeparam>
/// <typeparam name="TData">The per-slot data type.</typeparam>
public sealed class SlotRenderableOptions<TContext, TData> : RenderableOptions
    where TContext : class
    where TData : class
{
    /// <summary>The slot registry to resolve plugins from.</summary>
    public required SlotRegistry<TContext, TData> Registry { get; init; }

    /// <summary>The slot name this renderable manages.</summary>
    public required string Name { get; init; }

    /// <summary>Data passed to each plugin renderer.</summary>
    public TData? Data { get; init; }

    /// <summary>Composition mode.</summary>
    public SlotMode Mode { get; init; } = SlotMode.Append;

    /// <summary>Fallback content factory shown when no plugins contribute (or in append mode alongside).</summary>
    public Func<BaseRenderable?>? Fallback { get; init; }

    /// <summary>Factory for error placeholder renderables when a plugin's renderer throws.</summary>
    public Func<PluginErrorEvent, TContext, BaseRenderable?>? PluginFailurePlaceholder { get; init; }
}
