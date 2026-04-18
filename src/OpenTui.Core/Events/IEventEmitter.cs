namespace OpenTui.Core;

/// <summary>
/// Interface for the event emitter contract.
/// Allows types like IRenderContext to expose event emitter semantics without
/// requiring class inheritance from EventEmitter.
/// </summary>
public interface IEventEmitter
{
    IDisposable On<T>(string eventName, Action<T> handler);
    IDisposable Once<T>(string eventName, Action<T> handler);
    IDisposable On(string eventName, Action handler);
    IDisposable Once(string eventName, Action handler);
    void Off<T>(string eventName, Action<T> handler);
    void Off(string eventName, Action handler);
    void Emit<T>(string eventName, T args);
    void Emit(string eventName);
    void RemoveAllListeners(string? eventName = null);
    int ListenerCount(string eventName);
}
