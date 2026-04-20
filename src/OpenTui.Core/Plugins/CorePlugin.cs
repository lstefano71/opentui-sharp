namespace OpenTui.Core.Plugins;

/// <summary>
/// Lifecycle interface for managed slot contributions.
/// Plugins that need to track activation/deactivation state implement this
/// instead of providing a bare <see cref="SlotRendererDelegate{TContext,TData}"/>.
/// </summary>
/// <typeparam name="TContext">The host context type.</typeparam>
/// <typeparam name="TData">The per-slot data type.</typeparam>
public interface ICoreManagedSlot<in TContext, in TData>
    where TContext : class
    where TData : class
{
    /// <summary>Produces the renderable node for this slot contribution.</summary>
    BaseRenderable Render(TContext ctx, TData data);

    /// <summary>Called when this contribution becomes visible in the slot.</summary>
    void OnActivate(TContext ctx) { }

    /// <summary>Called when this contribution is hidden (mode change or deactivation).</summary>
    void OnDeactivate(TContext ctx) { }

    /// <summary>Called when the plugin is fully removed from the registry.</summary>
    void OnDispose(TContext ctx) { }
}

/// <summary>
/// A slot contribution that can be either a simple render delegate or a managed slot with lifecycle hooks.
/// </summary>
/// <typeparam name="TContext">The host context type.</typeparam>
/// <typeparam name="TData">The per-slot data type.</typeparam>
public sealed class CoreSlotContribution<TContext, TData>
    where TContext : class
    where TData : class
{
    /// <summary>Simple render function (set if not using managed lifecycle).</summary>
    public SlotRendererDelegate<TContext, TData>? Renderer { get; init; }

    /// <summary>Managed slot with full lifecycle hooks (set if not using simple renderer).</summary>
    public ICoreManagedSlot<TContext, TData>? ManagedSlot { get; init; }

    /// <summary>Whether this is a managed slot contribution.</summary>
    internal bool IsManaged => ManagedSlot is not null;

    /// <summary>Invokes the renderer (managed or simple). Throws if neither is set.</summary>
    internal BaseRenderable InvokeRender(TContext ctx, TData data) =>
        ManagedSlot is not null ? ManagedSlot.Render(ctx, data) : Renderer!(ctx, data);

    /// <summary>Creates a contribution from a simple renderer delegate.</summary>
    public static CoreSlotContribution<TContext, TData> FromRenderer(SlotRendererDelegate<TContext, TData> renderer) =>
        new() { Renderer = renderer };

    /// <summary>Creates a contribution from a managed slot.</summary>
    public static CoreSlotContribution<TContext, TData> FromManaged(ICoreManagedSlot<TContext, TData> managedSlot) =>
        new() { ManagedSlot = managedSlot };
}

/// <summary>
/// High-level plugin definition that contributes to named slots with optional lifecycle management.
/// This is the primary public API for defining plugins.
/// </summary>
/// <typeparam name="TContext">The host context type.</typeparam>
/// <typeparam name="TData">The per-slot data type.</typeparam>
public sealed class CorePlugin<TContext, TData> : IPlugin<TContext, TData>
    where TContext : class
    where TData : class
{
    private readonly Dictionary<string, CoreSlotContribution<TContext, TData>> _slots = new(StringComparer.Ordinal);
    private Action<TContext, IRenderContext>? _setup;
    private Action? _dispose;

    /// <inheritdoc/>
    public required string Id { get; init; }

    /// <inheritdoc/>
    public int Order { get; set; }

    /// <summary>Gets the slot contributions dictionary for configuration.</summary>
    public Dictionary<string, CoreSlotContribution<TContext, TData>> Slots => _slots;

    /// <summary>Optional setup callback invoked during registration.</summary>
    public Action<TContext, IRenderContext>? SetupAction { get => _setup; init => _setup = value; }

    /// <summary>Optional dispose callback invoked during unregistration.</summary>
    public Action? DisposeAction { get => _dispose; init => _dispose = value; }

    void IPlugin<TContext, TData>.Setup(TContext context, IRenderContext renderContext) =>
        _setup?.Invoke(context, renderContext);

    void IPlugin<TContext, TData>.Dispose() => _dispose?.Invoke();

    SlotRendererDelegate<TContext, TData>? IPlugin<TContext, TData>.GetSlotRenderer(string slotName)
    {
        if (!_slots.TryGetValue(slotName, out var contribution))
            return null;

        // Wrap the contribution in a delegate that the registry can call
        return contribution.InvokeRender;
    }

    /// <summary>
    /// Gets the contribution for a slot, or null if this plugin doesn't contribute to it.
    /// Used by <see cref="SlotRenderable{TContext,TData}"/> for lifecycle management.
    /// </summary>
    internal CoreSlotContribution<TContext, TData>? GetContribution(string slotName) =>
        _slots.TryGetValue(slotName, out var c) ? c : null;
}
