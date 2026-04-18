namespace OpenTui.Core;

/// <summary>Well-known event names for the key handler.</summary>
public static class KeyHandlerEvents
{
    public const string Keypress = "keypress";
    public const string Keyrelease = "keyrelease";
    public const string Paste = "paste";
}

/// <summary>
/// Key handler with two-tier event dispatch: global listeners (EventEmitter)
/// run first, then renderable-level handlers. StopPropagation between tiers,
/// PreventDefault skips renderable handlers entirely.
/// Matches TypeScript InternalKeyHandler from KeyHandler.ts.
/// </summary>
public class KeyHandler : EventEmitter
{
    private readonly Dictionary<string, List<Delegate>> _renderableHandlers = [];

    /// <summary>Registers a renderable-level keypress handler (second tier).</summary>
    public IDisposable OnRenderable<T>(string eventName, Action<T> handler) where T : UiEvent
    {
        if (!_renderableHandlers.TryGetValue(eventName, out var list))
            _renderableHandlers[eventName] = list = [];
        list.Add(handler);
        return new RenderableUnsubscriber(this, eventName, handler);
    }

    /// <summary>Removes a renderable-level handler.</summary>
    public void OffRenderable<T>(string eventName, Action<T> handler) where T : UiEvent
    {
        if (!_renderableHandlers.TryGetValue(eventName, out var list)) return;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(list[i], handler))
            {
                list.RemoveAt(i);
                break;
            }
        }
    }

    /// <summary>Processes a parsed key through the two-tier dispatch.</summary>
    public void ProcessParsedKey(ParsedKey key)
    {
        var evt = new KeyEvent(key);
        var eventName = key.EventType == KeyEventType.Release
            ? KeyHandlerEvents.Keyrelease
            : KeyHandlerEvents.Keypress;

        // Tier 1: global listeners (EventEmitter.Emit)
        Emit(eventName, evt);

        if (evt.IsPropagationStopped) return;

        // Tier 2: renderable handlers
        if (evt.IsDefaultPrevented) return;
        EmitRenderable(eventName, evt);
    }

    /// <summary>Processes a paste event through the two-tier dispatch.</summary>
    public void ProcessPaste(byte[] bytes, PasteMetadata? metadata = null)
    {
        var evt = new PasteEvent(bytes, metadata);

        Emit(KeyHandlerEvents.Paste, evt);

        if (evt.IsPropagationStopped || evt.IsDefaultPrevented) return;
        EmitRenderable(KeyHandlerEvents.Paste, evt);
    }

    private void EmitRenderable<T>(string eventName, T evt) where T : UiEvent
    {
        if (!_renderableHandlers.TryGetValue(eventName, out var list) || list.Count == 0) return;

        var snapshot = list.ToArray();
        foreach (var handler in snapshot)
        {
            if (evt.IsPropagationStopped) break;
            ((Action<T>)handler)(evt);
        }
    }

    private sealed class RenderableUnsubscriber(KeyHandler owner, string eventName, Delegate handler) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (owner._renderableHandlers.TryGetValue(eventName, out var list))
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (ReferenceEquals(list[i], handler))
                    {
                        list.RemoveAt(i);
                        break;
                    }
                }
            }
        }
    }
}
