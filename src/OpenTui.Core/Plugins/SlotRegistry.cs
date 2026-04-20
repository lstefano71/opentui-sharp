using System.Runtime.CompilerServices;

namespace OpenTui.Core.Plugins;

/// <summary>
/// Configuration options for a <see cref="SlotRegistry{TContext,TData}"/>.
/// </summary>
public sealed class SlotRegistryOptions
{
    /// <summary>Callback invoked for every plugin error.</summary>
    public Action<PluginErrorEvent>? OnPluginError { get; set; }

    /// <summary>Whether to write debug info to the console on plugin errors.</summary>
    public bool DebugPluginErrors { get; set; }

    /// <summary>Maximum number of plugin errors to retain in history.</summary>
    public int MaxPluginErrors { get; set; } = 100;
}

/// <summary>
/// Manages plugin registration, ordering, error tracking, and slot resolution.
/// Matches the upstream TypeScript <c>SlotRegistry</c> class.
/// </summary>
/// <typeparam name="TContext">The shared host context type.</typeparam>
/// <typeparam name="TData">The per-slot data type.</typeparam>
public sealed class SlotRegistry<TContext, TData>
    where TContext : class
    where TData : class
{
    #region Internal types

    private sealed class RegisteredPlugin
    {
        public required IPlugin<TContext, TData> Plugin { get; init; }
        public required int RegistrationOrder { get; init; }
        public int CachedOrder { get; set; }
        public string CachedId { get; set; } = "";
    }

    #endregion

    #region Fields

    private readonly List<RegisteredPlugin> _plugins = [];
    private List<RegisteredPlugin>? _sortedCache;
    private readonly HashSet<Action> _listeners = [];
    private readonly HashSet<Action<PluginErrorEvent>> _errorListeners = [];
    private readonly List<PluginErrorEvent> _pluginErrors = [];
    private int _registrationOrder;
    private int _batchDepth;
    private bool _batchedNotify;

    private readonly TContext _context;
    private readonly IRenderContext _renderContext;
    private bool _debugPluginErrors;
    private int _maxPluginErrors;
    private Action<PluginErrorEvent>? _onPluginError;

    #endregion

    #region Constructor

    public SlotRegistry(IRenderContext renderContext, TContext context, SlotRegistryOptions? options = null)
    {
        _renderContext = renderContext;
        _context = context;
        _debugPluginErrors = options?.DebugPluginErrors ?? false;
        _maxPluginErrors = options?.MaxPluginErrors ?? 100;
        _onPluginError = options?.OnPluginError;
    }

    #endregion

    #region Properties

    /// <summary>The render context this registry is associated with.</summary>
    public IRenderContext RenderContext => _renderContext;

    /// <summary>The shared host context.</summary>
    public TContext Context => _context;

    #endregion

    #region Public API

    /// <summary>
    /// Updates configuration options.
    /// </summary>
    public void Configure(SlotRegistryOptions options)
    {
        _debugPluginErrors = options.DebugPluginErrors;
        _maxPluginErrors = options.MaxPluginErrors;
        _onPluginError = options.OnPluginError;
    }

    /// <summary>
    /// Registers a plugin. Returns an action that unregisters it.
    /// Throws if a plugin with the same id is already registered.
    /// </summary>
    public Action Register(IPlugin<TContext, TData> plugin)
    {
        if (_plugins.Exists(e => e.Plugin.Id == plugin.Id))
            throw new InvalidOperationException($"Plugin with id \"{plugin.Id}\" is already registered");

        try
        {
            plugin.Setup(_context, _renderContext);
        }
        catch (Exception ex)
        {
            ReportPluginError(new PluginErrorReport
            {
                PluginId = plugin.Id,
                Phase = PluginErrorPhase.Setup,
                Source = "registry",
                Error = ex,
            });

            return static () => { };
        }

        _plugins.Add(new RegisteredPlugin
        {
            Plugin = plugin,
            RegistrationOrder = _registrationOrder++,
            CachedOrder = plugin.Order,
            CachedId = plugin.Id,
        });

        InvalidateCache();
        NotifyListeners();

        return () => Unregister(plugin.Id);
    }

    /// <summary>
    /// Unregisters a plugin by id. Returns true if found and removed.
    /// </summary>
    public bool Unregister(string id)
    {
        var index = _plugins.FindIndex(e => e.Plugin.Id == id);
        if (index < 0) return false;

        var entry = _plugins[index];
        _plugins.RemoveAt(index);
        InvalidateCache();

        try
        {
            entry.Plugin.Dispose();
        }
        catch (Exception ex)
        {
            ReportPluginError(new PluginErrorReport
            {
                PluginId = id,
                Phase = PluginErrorPhase.Dispose,
                Source = "registry",
                Error = ex,
            });
        }

        NotifyListeners();
        return true;
    }

    /// <summary>
    /// Updates the order of a registered plugin. Returns false if not found.
    /// </summary>
    public bool UpdateOrder(string id, int order)
    {
        var entry = _plugins.Find(e => e.Plugin.Id == id);
        if (entry is null) return false;
        if (entry.Plugin.Order == order) return true;

        entry.Plugin.Order = order;
        entry.CachedOrder = order;
        InvalidateCache();
        NotifyListeners();
        return true;
    }

    /// <summary>
    /// Unregisters and disposes all plugins.
    /// </summary>
    public void Clear()
    {
        if (_plugins.Count == 0) return;

        var plugins = new List<RegisteredPlugin>(_plugins);
        _plugins.Clear();
        InvalidateCache();

        foreach (var entry in plugins)
        {
            try
            {
                entry.Plugin.Dispose();
            }
            catch (Exception ex)
            {
                ReportPluginError(new PluginErrorReport
                {
                    PluginId = entry.Plugin.Id,
                    Phase = PluginErrorPhase.Dispose,
                    Source = "registry",
                    Error = ex,
                });
            }
        }

        NotifyListeners();
    }

    /// <summary>
    /// Subscribes to registry changes (plugin added/removed/reordered).
    /// Returns an action that unsubscribes.
    /// </summary>
    public Action Subscribe(Action listener)
    {
        _listeners.Add(listener);
        return () => _listeners.Remove(listener);
    }

    /// <summary>
    /// Subscribes to plugin error events. Returns an action that unsubscribes.
    /// </summary>
    public Action OnPluginError(Action<PluginErrorEvent> listener)
    {
        _errorListeners.Add(listener);
        return () => _errorListeners.Remove(listener);
    }

    /// <summary>
    /// Executes a batch of operations, suppressing change notifications until the batch completes.
    /// </summary>
    public T Batch<T>(Func<T> run)
    {
        _batchDepth++;
        try
        {
            return run();
        }
        finally
        {
            _batchDepth--;
            if (_batchDepth == 0 && _batchedNotify)
            {
                _batchedNotify = false;
                FlushListeners();
            }
        }
    }

    /// <summary>
    /// Executes a batch of operations without a return value.
    /// </summary>
    public void Batch(Action run)
    {
        Batch<object?>(() => { run(); return null; });
    }

    /// <summary>Returns the accumulated plugin error history.</summary>
    public IReadOnlyList<PluginErrorEvent> GetPluginErrors() => _pluginErrors;

    /// <summary>Clears the plugin error history.</summary>
    public void ClearPluginErrors() => _pluginErrors.Clear();

    /// <summary>
    /// Reports a plugin error, notifies listeners, and returns the event.
    /// </summary>
    public PluginErrorEvent ReportPluginError(PluginErrorReport report)
    {
        var evt = new PluginErrorEvent
        {
            PluginId = report.PluginId,
            Slot = report.Slot,
            Phase = report.Phase,
            Source = report.Source ?? "registry",
            Error = report.Error,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        _pluginErrors.Add(evt);
        if (_pluginErrors.Count > _maxPluginErrors)
            _pluginErrors.RemoveRange(0, _pluginErrors.Count - _maxPluginErrors);

        if (_debugPluginErrors)
        {
            var slotLabel = evt.Slot is not null ? $" slot=\"{evt.Slot}\"" : "";
            Console.Error.WriteLine(
                $"[SlotRegistry][PluginError] plugin=\"{evt.PluginId}\" phase=\"{evt.Phase}\" source=\"{evt.Source}\"{slotLabel}");
            Console.Error.WriteLine(evt.Error);
        }

        foreach (var listener in _errorListeners)
        {
            try { listener(evt); }
            catch { /* swallow listener errors */ }
        }

        try { _onPluginError?.Invoke(evt); }
        catch { /* swallow callback errors */ }

        return evt;
    }

    /// <summary>
    /// Resolves all renderers contributing to the named slot, sorted by priority.
    /// </summary>
    public List<ResolvedSlotEntry<TContext, TData>> ResolveEntries(string slot)
    {
        var result = new List<ResolvedSlotEntry<TContext, TData>>();
        foreach (var entry in GetSortedPlugins())
        {
            var renderer = entry.Plugin.GetSlotRenderer(slot);
            if (renderer is not null)
            {
                result.Add(new ResolvedSlotEntry<TContext, TData>
                {
                    Id = entry.Plugin.Id,
                    Renderer = renderer,
                });
            }
        }
        return result;
    }

    /// <summary>
    /// Finds the <see cref="CoreSlotContribution{TContext,TData}"/> for a specific plugin and slot.
    /// Used internally by <see cref="SlotRenderable{TContext,TData}"/> for lifecycle management.
    /// </summary>
    internal CoreSlotContribution<TContext, TData>? FindCorePluginContribution(string pluginId, string slotName)
    {
        var entry = _plugins.Find(e => e.Plugin.Id == pluginId);
        if (entry?.Plugin is CorePlugin<TContext, TData> corePlugin)
            return corePlugin.GetContribution(slotName);
        return null;
    }

    #endregion

    #region Private helpers

    private List<RegisteredPlugin> GetSortedPlugins()
    {
        SyncSortMetadata();

        if (_sortedCache is not null) return _sortedCache;

        _sortedCache = [.. _plugins];
        _sortedCache.Sort((a, b) =>
        {
            var cmp = a.CachedOrder.CompareTo(b.CachedOrder);
            if (cmp != 0) return cmp;
            cmp = a.RegistrationOrder.CompareTo(b.RegistrationOrder);
            if (cmp != 0) return cmp;
            return string.Compare(a.CachedId, b.CachedId, StringComparison.Ordinal);
        });

        return _sortedCache;
    }

    private void SyncSortMetadata()
    {
        var hasChanges = false;
        foreach (var entry in _plugins)
        {
            var nextOrder = entry.Plugin.Order;
            var nextId = entry.Plugin.Id;

            if (entry.CachedOrder != nextOrder || entry.CachedId != nextId)
            {
                entry.CachedOrder = nextOrder;
                entry.CachedId = nextId;
                hasChanges = true;
            }
        }

        if (hasChanges) InvalidateCache();
    }

    private void InvalidateCache() => _sortedCache = null;

    private void NotifyListeners()
    {
        if (_batchDepth > 0)
        {
            _batchedNotify = true;
            return;
        }

        FlushListeners();
    }

    private void FlushListeners()
    {
        foreach (var listener in _listeners)
        {
            try { listener(); }
            catch { /* swallow listener errors */ }
        }
    }

    #endregion

    #region Static factory

    private static readonly ConditionalWeakTable<IRenderContext, Dictionary<string, object>> s_registriesByRenderer = [];

    /// <summary>
    /// Creates or retrieves a keyed slot registry for the given render context.
    /// Multiple calls with the same key and context return the same instance.
    /// </summary>
    public static SlotRegistry<TContext, TData> Create(
        IRenderContext renderContext,
        string key,
        TContext context,
        SlotRegistryOptions? options = null)
    {
        var store = s_registriesByRenderer.GetOrCreateValue(renderContext);

        if (store.TryGetValue(key, out var existing))
        {
            var typed = (SlotRegistry<TContext, TData>)existing;
            if (!ReferenceEquals(typed.Context, context))
                throw new InvalidOperationException(
                    $"CreateSlotRegistry called with a different context for renderer key \"{key}\". Reuse the original context object.");

            if (options is not null) typed.Configure(options);
            return typed;
        }

        var created = new SlotRegistry<TContext, TData>(renderContext, context, options);
        store[key] = created;
        return created;
    }

    #endregion
}
