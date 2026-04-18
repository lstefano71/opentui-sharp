namespace OpenTui.Core;

/// <summary>
/// A lightweight typed event emitter matching Node.js EventEmitter semantics.
/// Supports on/once/off/emit/removeAllListeners with string-keyed event names.
/// AOT-compatible: no reflection, delegates stored and invoked directly.
/// </summary>
public class EventEmitter
{
    private readonly Dictionary<string, List<Subscription>> _listeners = [];

    /// <summary>Registers a listener for the named event. Returns a disposable that removes it.</summary>
    public IDisposable On<T>(string eventName, Action<T> handler)
    {
        var sub = new Subscription(handler, false);
        GetOrCreateList(eventName).Add(sub);
        return new Unsubscriber(this, eventName, sub);
    }

    /// <summary>Registers a listener that fires at most once, then auto-removes.</summary>
    public IDisposable Once<T>(string eventName, Action<T> handler)
    {
        var sub = new Subscription(handler, true);
        GetOrCreateList(eventName).Add(sub);
        return new Unsubscriber(this, eventName, sub);
    }

    /// <summary>Registers a parameterless listener for the named event.</summary>
    public IDisposable On(string eventName, Action handler)
    {
        var sub = new Subscription(handler, false);
        GetOrCreateList(eventName).Add(sub);
        return new Unsubscriber(this, eventName, sub);
    }

    /// <summary>Registers a parameterless listener that fires at most once.</summary>
    public IDisposable Once(string eventName, Action handler)
    {
        var sub = new Subscription(handler, true);
        GetOrCreateList(eventName).Add(sub);
        return new Unsubscriber(this, eventName, sub);
    }

    /// <summary>Removes a specific typed listener.</summary>
    public void Off<T>(string eventName, Action<T> handler)
    {
        if (!_listeners.TryGetValue(eventName, out var list)) return;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(list[i].Handler, handler))
            {
                list.RemoveAt(i);
                break;
            }
        }
    }

    /// <summary>Removes a specific parameterless listener.</summary>
    public void Off(string eventName, Action handler)
    {
        if (!_listeners.TryGetValue(eventName, out var list)) return;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(list[i].Handler, handler))
            {
                list.RemoveAt(i);
                break;
            }
        }
    }

    /// <summary>Emits an event with a typed argument, invoking all registered listeners.</summary>
    public void Emit<T>(string eventName, T args)
    {
        if (!_listeners.TryGetValue(eventName, out var list) || list.Count == 0) return;

        // Snapshot to allow listener modification during emit
        var snapshot = list.ToArray();
        foreach (var sub in snapshot)
        {
            if (sub.Once)
                list.Remove(sub);

            ((Action<T>)sub.Handler)(args);
        }
    }

    /// <summary>Emits a parameterless event.</summary>
    public void Emit(string eventName)
    {
        if (!_listeners.TryGetValue(eventName, out var list) || list.Count == 0) return;

        var snapshot = list.ToArray();
        foreach (var sub in snapshot)
        {
            if (sub.Once)
                list.Remove(sub);

            ((Action)sub.Handler)();
        }
    }

    /// <summary>Removes all listeners, optionally only for a specific event.</summary>
    public void RemoveAllListeners(string? eventName = null)
    {
        if (eventName is null)
            _listeners.Clear();
        else
            _listeners.Remove(eventName);
    }

    /// <summary>Returns the number of listeners for the named event.</summary>
    public int ListenerCount(string eventName) =>
        _listeners.TryGetValue(eventName, out var list) ? list.Count : 0;

    /// <summary>Returns all event names that have at least one listener.</summary>
    public IEnumerable<string> EventNames() =>
        _listeners.Where(kv => kv.Value.Count > 0).Select(kv => kv.Key);

    private List<Subscription> GetOrCreateList(string eventName)
    {
        if (!_listeners.TryGetValue(eventName, out var list))
            _listeners[eventName] = list = [];
        return list;
    }

    private sealed record Subscription(Delegate Handler, bool Once);

    private sealed class Unsubscriber(EventEmitter emitter, string eventName, Subscription sub) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (emitter._listeners.TryGetValue(eventName, out var list))
                list.Remove(sub);
        }
    }
}
