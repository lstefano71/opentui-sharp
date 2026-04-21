namespace OpenTui.Core.Managed;

/// <summary>
/// Pure C# reimplementation of the Zig <c>mem-registry</c>.
/// Stores byte buffers by sequential ID so that text buffer content can be
/// referenced without P/Invoke into opentui.dll.
/// <para>
/// In the Zig implementation this tracks raw pointers to external memory for
/// zero-copy text buffer content. In managed C# the GC handles lifetime, so
/// the registry is simply a list of <see cref="ReadOnlyMemory{T}"/> buffers
/// addressed by a 0-based <see cref="ushort"/> index.
/// </para>
/// Thread-safe: no. Callers must synchronize externally if needed.
/// </summary>
public sealed class ManagedMemRegistry
{
    // ── Storage ──────────────────────────────────────────────────────
    private readonly List<ReadOnlyMemory<byte>> _buffers = [];

    // ── Public API ───────────────────────────────────────────────────

    /// <summary>Number of registered buffers.</summary>
    public int Count => _buffers.Count;

    /// <summary>
    /// Register a buffer and return its 0-based ID.
    /// IDs are assigned sequentially starting from 0.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the registry is full (65 536 buffers).
    /// </exception>
    public ushort Register(ReadOnlyMemory<byte> data)
    {
        if (_buffers.Count > ushort.MaxValue)
            throw new InvalidOperationException(
                "ManagedMemRegistry: all 65 536 buffer slots are exhausted.");

        ushort id = (ushort)_buffers.Count;
        _buffers.Add(data);
        return id;
    }

    /// <summary>
    /// Replace the buffer at an existing <paramref name="id"/>.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the buffer was replaced;
    /// <see langword="false"/> if <paramref name="id"/> is out of range.
    /// </returns>
    public bool Replace(byte id, ReadOnlyMemory<byte> data)
    {
        if (id >= _buffers.Count)
            return false;

        _buffers[id] = data;
        return true;
    }

    /// <summary>
    /// Retrieve the buffer registered under <paramref name="id"/>.
    /// </summary>
    /// <returns>
    /// The buffer, or <see langword="null"/> if <paramref name="id"/> has not been registered.
    /// </returns>
    public ReadOnlyMemory<byte>? Get(ushort id)
    {
        if (id >= _buffers.Count)
            return null;

        return _buffers[id];
    }

    /// <summary>
    /// Remove all registered buffers and reset the ID counter to 0.
    /// </summary>
    public void Clear() => _buffers.Clear();
}
