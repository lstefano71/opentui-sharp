namespace OpenTui.Core;

/// <summary>
/// Interface for the event emitter contract.
/// Allows types like IRenderContext to expose event emitter semantics without
/// requiring class inheritance from EventEmitter.
/// </summary>
public interface IEventEmitter
{
    /// <summary>
    /// Performs on.
    /// </summary>
    /// <typeparam name="T">The type parameter.</typeparam>
    /// <param name="eventName">The event name.</param>
    /// <param name="handler">The handler.</param>
    /// <returns>The result of on.</returns>
    IDisposable On<T>(string eventName, Action<T> handler);
    /// <summary>
    /// Performs once.
    /// </summary>
    /// <typeparam name="T">The type parameter.</typeparam>
    /// <param name="eventName">The event name.</param>
    /// <param name="handler">The handler.</param>
    /// <returns>The result of once.</returns>
    IDisposable Once<T>(string eventName, Action<T> handler);
    /// <summary>
    /// Performs on.
    /// </summary>
    /// <param name="eventName">The event name.</param>
    /// <param name="handler">The handler.</param>
    /// <returns>The result of on.</returns>
    IDisposable On(string eventName, Action handler);
    /// <summary>
    /// Performs once.
    /// </summary>
    /// <param name="eventName">The event name.</param>
    /// <param name="handler">The handler.</param>
    /// <returns>The result of once.</returns>
    IDisposable Once(string eventName, Action handler);
    /// <summary>
    /// Performs off.
    /// </summary>
    /// <typeparam name="T">The type parameter.</typeparam>
    /// <param name="eventName">The event name.</param>
    /// <param name="handler">The handler.</param>
    void Off<T>(string eventName, Action<T> handler);
    /// <summary>
    /// Performs off.
    /// </summary>
    /// <param name="eventName">The event name.</param>
    /// <param name="handler">The handler.</param>
    void Off(string eventName, Action handler);
    /// <summary>
    /// Performs emit.
    /// </summary>
    /// <typeparam name="T">The type parameter.</typeparam>
    /// <param name="eventName">The event name.</param>
    /// <param name="args">The arguments.</param>
    void Emit<T>(string eventName, T args);
    /// <summary>
    /// Performs emit.
    /// </summary>
    /// <param name="eventName">The event name.</param>
    void Emit(string eventName);
    /// <summary>
    /// Removes an all listeners.
    /// </summary>
    /// <param name="eventName">The event name.</param>
    void RemoveAllListeners(string? eventName = null);
    /// <summary>
    /// Performs listener count.
    /// </summary>
    /// <param name="eventName">The event name.</param>
    /// <returns>The result of listener count.</returns>
    int ListenerCount(string eventName);
}
