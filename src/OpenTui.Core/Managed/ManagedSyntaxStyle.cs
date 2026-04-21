namespace OpenTui.Core.Managed;

/// <summary>
/// Pure C# reimplementation of the Zig <c>syntax-style.zig</c>.
/// Stores named styles (fg/bg colours + text attributes) with 1-based IDs.
/// Internally backed by a list (id → style) and a dictionary (name → index).
/// Thread-safe: no. Callers must synchronise externally if needed.
/// </summary>
public sealed class ManagedSyntaxStyle : IDisposable
{
    // ── Storage ──────────────────────────────────────────────────────
    // _styles[i] corresponds to ID (i + 1). ID 0 means "not found".
    private readonly List<SyntaxStyleEntry> _styles = [];
    private readonly Dictionary<string, int> _nameToIndex = new(StringComparer.Ordinal);

    private bool _disposed;

    /// <summary>Whether this syntax style registry has been disposed.</summary>
    public bool IsDisposed => _disposed;

    // ── Public API ───────────────────────────────────────────────────

    /// <summary>
    /// Registers a named style and returns its 1-based ID.
    /// If the name is already registered, the existing entry is overwritten
    /// and the same ID is returned.
    /// </summary>
    /// <param name="name">Unique style name (case-sensitive, ordinal comparison).</param>
    /// <param name="fg">Optional foreground colour.</param>
    /// <param name="bg">Optional background colour.</param>
    /// <param name="attrs">Text attributes (bold, italic, …).</param>
    /// <returns>A 1-based style ID. Use this with <see cref="GetStyleById"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The registry has been disposed.</exception>
    public uint Register(string name, Rgba? fg = null, Rgba? bg = null, TextAttributes attrs = TextAttributes.None)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(name);

        var entry = new SyntaxStyleEntry(fg, bg, attrs);

        if (_nameToIndex.TryGetValue(name, out int existingIndex))
        {
            // Overwrite in-place, keep the same ID.
            _styles[existingIndex] = entry;
            return (uint)(existingIndex + 1);
        }

        int index = _styles.Count;
        _styles.Add(entry);
        _nameToIndex[name] = index;
        return (uint)(index + 1);
    }

    /// <summary>
    /// Resolves a style name to its 1-based ID.
    /// </summary>
    /// <returns>The 1-based ID, or <c>0</c> if the name is not registered.</returns>
    /// <exception cref="ObjectDisposedException">The registry has been disposed.</exception>
    public uint ResolveByName(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(name);

        return _nameToIndex.TryGetValue(name, out int index) ? (uint)(index + 1) : 0;
    }

    /// <summary>Gets the total number of registered styles.</summary>
    /// <exception cref="ObjectDisposedException">The registry has been disposed.</exception>
    public int StyleCount
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _styles.Count;
        }
    }

    /// <summary>
    /// Attempts to retrieve a style entry by name.
    /// </summary>
    /// <param name="name">The style name.</param>
    /// <param name="style">When this method returns <see langword="true"/>, contains the matching entry.</param>
    /// <returns><see langword="true"/> if the name was found; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The registry has been disposed.</exception>
    public bool TryGetStyle(string name, out SyntaxStyleEntry style)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(name);

        if (_nameToIndex.TryGetValue(name, out int index))
        {
            style = _styles[index];
            return true;
        }

        style = default;
        return false;
    }

    /// <summary>
    /// Gets a style entry by its 1-based <paramref name="id"/>.
    /// </summary>
    /// <returns>The entry, or <see langword="null"/> if the ID is out of range.</returns>
    /// <exception cref="ObjectDisposedException">The registry has been disposed.</exception>
    public SyntaxStyleEntry? GetStyleById(uint id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        int index = (int)id - 1;
        return (uint)index < (uint)_styles.Count ? _styles[index] : null;
    }

    /// <summary>
    /// Gets a style entry by name.
    /// </summary>
    /// <returns>The entry, or <see langword="null"/> if the name is not registered.</returns>
    /// <exception cref="ObjectDisposedException">The registry has been disposed.</exception>
    public SyntaxStyleEntry? GetStyle(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(name);

        return _nameToIndex.TryGetValue(name, out int index) ? _styles[index] : null;
    }

    // ── IDisposable ──────────────────────────────────────────────────

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _styles.Clear();
            _nameToIndex.Clear();
        }
    }
}
