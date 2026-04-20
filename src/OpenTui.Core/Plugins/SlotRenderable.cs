namespace OpenTui.Core.Plugins;

/// <summary>
/// A renderable that mounts plugin contributions for a named slot.
/// Automatically subscribes to registry changes and refreshes the child tree.
/// Matches the upstream TypeScript <c>SlotRenderable</c>.
/// </summary>
/// <typeparam name="TContext">The host context type.</typeparam>
/// <typeparam name="TData">The per-slot data type.</typeparam>
public sealed class SlotRenderable<TContext, TData> : Renderable
    where TContext : class
    where TData : class
{
    #region Internal types

    private sealed class SlotNodeState
    {
        public List<BaseRenderable> Nodes { get; set; } = [];
        public bool IsManaged { get; init; }
        public ICoreManagedSlot<TContext, TData>? ManagedSlot { get; init; }
        public TData? DataRef { get; set; }
    }

    private sealed class ResolvedEntry
    {
        public required string Id { get; init; }
        public required SlotRendererDelegate<TContext, TData> Renderer { get; init; }
        public bool IsManaged { get; init; }
        public ICoreManagedSlot<TContext, TData>? ManagedSlot { get; init; }
    }

    #endregion

    #region Fields

    private SlotMode _mode;
    private readonly SlotRegistry<TContext, TData> _registry;
    private readonly string _slotName;
    private TData _data;
    private readonly Func<BaseRenderable?>? _fallbackFactory;
    private readonly Func<PluginErrorEvent, TContext, BaseRenderable?>? _failurePlaceholder;
    private bool _disposed;
    private List<BaseRenderable> _mountedNodes = [];
    private readonly Dictionary<string, SlotNodeState> _pluginNodes = new(StringComparer.Ordinal);
    private HashSet<string> _activePluginIds = new(StringComparer.Ordinal);
    private BaseRenderable? _fallbackNode;
    private Action? _unsubscribe;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new SlotRenderable.
    /// </summary>
    public SlotRenderable(IRenderContext ctx, SlotRenderableOptions<TContext, TData> options)
        : base(ctx, options)
    {
        _registry = options.Registry;
        _slotName = options.Name;
        _data = options.Data ?? throw new ArgumentNullException(nameof(options), "Data must be provided");
        _mode = options.Mode;
        _fallbackFactory = options.Fallback;
        _failurePlaceholder = options.PluginFailurePlaceholder;

        _unsubscribe = _registry.Subscribe(Refresh);

        try
        {
            Refresh();
        }
        catch
        {
            CleanupAll();
            throw;
        }
    }

    #endregion

    #region Properties

    /// <summary>Gets or sets the slot composition mode. Setting triggers a refresh.</summary>
    public SlotMode Mode
    {
        get => _mode;
        set
        {
            _mode = value;
            Refresh();
        }
    }

    /// <summary>Gets or sets the data passed to plugin renderers. Setting triggers a refresh.</summary>
    public TData Data
    {
        get => _data;
        set
        {
            _data = value;
            Refresh();
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Force re-resolution of all plugins and reconcile the child tree.
    /// </summary>
    public void Refresh()
    {
        if (_disposed) return;

        var allEntries = ResolveEntries();
        var activeEntries = _mode == SlotMode.SingleWinner && allEntries.Count > 0
            ? [allEntries[0]]
            : allEntries;

        var nextActiveIds = new HashSet<string>(activeEntries.Count, StringComparer.Ordinal);
        foreach (var entry in activeEntries)
            nextActiveIds.Add(entry.Id);

        var registeredIds = new HashSet<string>(allEntries.Count, StringComparer.Ordinal);
        foreach (var entry in allEntries)
            registeredIds.Add(entry.Id);

        CleanupInactiveNodes(nextActiveIds, registeredIds);

        foreach (var entry in activeEntries)
        {
            _pluginNodes.TryGetValue(entry.Id, out var state);
            var shouldRender = state is null
                || (state.IsManaged && state.Nodes.Count == 0)
                || !ReferenceEquals(state.DataRef, _data);

            if (shouldRender)
            {
                var previousState = state;

                try
                {
                    var node = entry.Renderer(_registry.Context, _data);
                    ValidateNode(node, entry.Id);
                    state = new SlotNodeState
                    {
                        Nodes = [node],
                        IsManaged = entry.IsManaged,
                        ManagedSlot = entry.ManagedSlot ?? previousState?.ManagedSlot,
                        DataRef = _data,
                    };
                }
                catch (Exception ex)
                {
                    var failure = _registry.ReportPluginError(new PluginErrorReport
                    {
                        PluginId = entry.Id,
                        Slot = _slotName,
                        Phase = PluginErrorPhase.Render,
                        Source = "core",
                        Error = ex,
                    });

                    state = new SlotNodeState
                    {
                        Nodes = ResolveFailurePlaceholder(failure),
                        IsManaged = false,
                        ManagedSlot = entry.ManagedSlot ?? previousState?.ManagedSlot,
                        DataRef = _data,
                    };
                }

                if (previousState is not null)
                    CleanupReplacedNodes(previousState, state.Nodes);

                _pluginNodes[entry.Id] = state;
            }

            if (!_activePluginIds.Contains(entry.Id))
            {
                if (_pluginNodes.TryGetValue(entry.Id, out var activeState))
                    CallManagedHook(entry.Id, activeState.ManagedSlot, ManagedHook.OnActivate, PluginErrorPhase.Setup);
            }
        }

        // Build desired node list
        var desiredNodes = new List<BaseRenderable>();

        if (_mode == SlotMode.Append || activeEntries.Count == 0)
            AddFallbackNodes(desiredNodes);

        foreach (var entry in activeEntries)
        {
            if (_pluginNodes.TryGetValue(entry.Id, out var state))
                desiredNodes.AddRange(state.Nodes);
        }

        if (_mode != SlotMode.Append && desiredNodes.Count == 0)
            AddFallbackNodes(desiredNodes);

        ReconcileMountedNodes(desiredNodes);
        _activePluginIds = nextActiveIds;
    }

    #endregion

    #region Lifecycle

    /// <inheritdoc/>
    protected override void DestroySelf() => CleanupAll();

    #endregion

    #region Private — Resolution

    private List<ResolvedEntry> ResolveEntries()
    {
        var entries = _registry.ResolveEntries(_slotName);
        var result = new List<ResolvedEntry>(entries.Count);

        foreach (var entry in entries)
        {
            // Try to get CorePlugin contribution metadata for lifecycle hooks
            ICoreManagedSlot<TContext, TData>? managedSlot = null;
            var isManaged = false;

            // Walk registered plugins to find the matching CorePlugin contribution
            if (FindCorePluginContribution(entry.Id) is { } contribution)
            {
                managedSlot = contribution.ManagedSlot;
                isManaged = contribution.IsManaged;
            }

            result.Add(new ResolvedEntry
            {
                Id = entry.Id,
                Renderer = entry.Renderer,
                IsManaged = isManaged,
                ManagedSlot = managedSlot,
            });
        }

        return result;
    }

    private CoreSlotContribution<TContext, TData>? FindCorePluginContribution(string pluginId)
    {
        // Access the registry's plugins to find the CorePlugin contribution
        var entries = _registry.ResolveEntries(_slotName);
        foreach (var e in entries)
        {
            if (e.Id != pluginId) continue;
            // The renderer itself may be wrapping a CorePlugin — check via the internal lookup
            break;
        }

        // Use the internal core plugin lookup
        return _registry.FindCorePluginContribution(pluginId, _slotName);
    }

    #endregion

    #region Private — Node lifecycle

    private void ValidateNode(BaseRenderable? node, string pluginId)
    {
        if (node is null)
            throw new InvalidOperationException($"Plugin \"{pluginId}\" did not return a renderable node");

        if (node is not Renderable renderable)
            throw new InvalidOperationException($"Plugin \"{pluginId}\" must return a Renderable");

        if (ReferenceEquals(node, this))
            throw new InvalidOperationException($"Plugin \"{pluginId}\" returned the slot mount container as its node");

        if (renderable.Parent is not null && !ReferenceEquals(renderable.Parent, this))
            throw new InvalidOperationException($"Plugin \"{pluginId}\" returned a renderable already attached to another parent");
    }

    private void AddFallbackNodes(List<BaseRenderable> target)
    {
        if (_fallbackNode is null && _fallbackFactory is not null)
        {
            _fallbackNode = _fallbackFactory();
            if (_fallbackNode is not null)
                ValidateNode(_fallbackNode, "fallback");
        }

        if (_fallbackNode is not null)
            target.Add(_fallbackNode);
    }

    private List<BaseRenderable> ResolveFailurePlaceholder(PluginErrorEvent failure)
    {
        if (_failurePlaceholder is null) return [];

        try
        {
            var placeholder = _failurePlaceholder(failure, _registry.Context);
            if (placeholder is null) return [];
            ValidateNode(placeholder, $"{failure.PluginId}:error-placeholder");
            return [placeholder];
        }
        catch (Exception ex)
        {
            _registry.ReportPluginError(new PluginErrorReport
            {
                PluginId = failure.PluginId,
                Slot = _slotName,
                Phase = PluginErrorPhase.ErrorPlaceholder,
                Source = "core",
                Error = ex,
            });
            return [];
        }
    }

    private void ReconcileMountedNodes(List<BaseRenderable> desiredNodes)
    {
        var desiredSet = new HashSet<BaseRenderable>(desiredNodes, ReferenceEqualityComparer.Instance);

        foreach (var node in _mountedNodes)
        {
            if (!desiredSet.Contains(node) && node is Renderable r && ReferenceEquals(r.Parent, this))
                Remove(r.Id);
        }

        var children = GetChildren();
        for (int i = 0; i < desiredNodes.Count; i++)
        {
            var node = desiredNodes[i];
            if (node is not Renderable renderable) continue;

            if (!ReferenceEquals(renderable.Parent, this))
            {
                Add(renderable, i);
            }
            else
            {
                children = GetChildren();
                if (i < children.Count && children[i].Id != renderable.Id)
                    Add(renderable, i);
            }
        }

        _mountedNodes = [.. desiredNodes];
    }

    #endregion

    #region Private — Cleanup

    private enum ManagedHook { OnActivate, OnDeactivate, OnDispose }

    private void CallManagedHook(string pluginId, ICoreManagedSlot<TContext, TData>? managedSlot, ManagedHook hook, PluginErrorPhase phase)
    {
        if (managedSlot is null) return;

        try
        {
            switch (hook)
            {
                case ManagedHook.OnActivate: managedSlot.OnActivate(_registry.Context); break;
                case ManagedHook.OnDeactivate: managedSlot.OnDeactivate(_registry.Context); break;
                case ManagedHook.OnDispose: managedSlot.OnDispose(_registry.Context); break;
            }
        }
        catch (Exception ex)
        {
            _registry.ReportPluginError(new PluginErrorReport
            {
                PluginId = pluginId,
                Slot = _slotName,
                Phase = phase,
                Source = "core",
                Error = ex,
            });
        }
    }

    private void CleanupInactiveNodes(HashSet<string> nextActiveIds, HashSet<string> registeredIds)
    {
        var toRemove = new List<string>();

        foreach (var (pluginId, state) in _pluginNodes)
        {
            if (nextActiveIds.Contains(pluginId)) continue;

            if (_activePluginIds.Contains(pluginId))
                CallManagedHook(pluginId, state.ManagedSlot, ManagedHook.OnDeactivate, PluginErrorPhase.Dispose);

            foreach (var node in state.Nodes)
            {
                if (node is Renderable r && ReferenceEquals(r.Parent, this))
                    Remove(r.Id);
            }

            if (!registeredIds.Contains(pluginId))
            {
                CallManagedHook(pluginId, state.ManagedSlot, ManagedHook.OnDispose, PluginErrorPhase.Dispose);

                if (!state.IsManaged)
                {
                    foreach (var node in state.Nodes)
                        node.DestroyRecursively();
                }

                toRemove.Add(pluginId);
                continue;
            }

            if (!state.IsManaged)
            {
                foreach (var node in state.Nodes)
                    node.DestroyRecursively();
                toRemove.Add(pluginId);
                continue;
            }

            state.Nodes = [];
        }

        foreach (var id in toRemove)
            _pluginNodes.Remove(id);
    }

    private void CleanupReplacedNodes(SlotNodeState previousState, List<BaseRenderable> nextNodes)
    {
        var retained = new HashSet<BaseRenderable>(nextNodes, ReferenceEqualityComparer.Instance);

        foreach (var node in previousState.Nodes)
        {
            if (retained.Contains(node)) continue;

            if (node is Renderable r && ReferenceEquals(r.Parent, this))
                Remove(r.Id);

            if (!previousState.IsManaged)
                node.DestroyRecursively();
        }
    }

    private void CleanupAll()
    {
        if (_disposed) return;
        _disposed = true;

        _unsubscribe?.Invoke();
        _unsubscribe = null;

        foreach (var (pluginId, state) in _pluginNodes)
        {
            if (_activePluginIds.Contains(pluginId))
                CallManagedHook(pluginId, state.ManagedSlot, ManagedHook.OnDeactivate, PluginErrorPhase.Dispose);

            CallManagedHook(pluginId, state.ManagedSlot, ManagedHook.OnDispose, PluginErrorPhase.Dispose);

            foreach (var node in state.Nodes)
            {
                if (node is Renderable r && ReferenceEquals(r.Parent, this))
                    Remove(r.Id);
            }

            if (!state.IsManaged)
            {
                foreach (var node in state.Nodes)
                    node.DestroyRecursively();
            }
        }

        _pluginNodes.Clear();
        _activePluginIds = [];

        if (_fallbackNode is not null)
        {
            _fallbackNode.DestroyRecursively();
            _fallbackNode = null;
        }

        _mountedNodes = [];
    }

    #endregion
}
