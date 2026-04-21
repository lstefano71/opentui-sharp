using OpenTui.Core.Managed;

namespace OpenTui.Core;

/// <summary>
/// Represents a Syntax Style Entry.
/// </summary>
public readonly record struct SyntaxStyleEntry(Rgba? Fg, Rgba? Bg, TextAttributes Attributes);

/// <summary>
/// Managed wrapper around the syntax style registry.
/// Delegates to <see cref="ManagedSyntaxStyle"/>.
/// </summary>
public sealed class SyntaxStyle : IDisposable
{
    internal ManagedSyntaxStyle _managed;
    private bool _disposed;
    private readonly Dictionary<string, SyntaxStyleEntry> _stylesByName = new(StringComparer.Ordinal);

    private SyntaxStyle(ManagedSyntaxStyle managed) => _managed = managed;

    /// <summary>Creates a new syntax style registry.</summary>
    public static SyntaxStyle Create()
    {
        return new SyntaxStyle(new ManagedSyntaxStyle());
    }

    /// <summary>Gets the total number of registered styles.</summary>
    public nuint StyleCount
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return (nuint)_managed.StyleCount;
        }
    }

    /// <summary>Registers a named style and returns its ID.</summary>
    public uint Register(string name, Rgba? fg = null, Rgba? bg = null, TextAttributes attrs = TextAttributes.None)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        uint result = _managed.Register(name, fg, bg, attrs);
        _stylesByName[name] = new SyntaxStyleEntry(fg, bg, attrs);
        return result;
    }

    /// <summary>Resolves a style name to its 1-based ID, or 0 if not found.</summary>
    public uint ResolveByName(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _managed.ResolveByName(name);
    }

    /// <summary>Attempts to retrieve a style entry by name.</summary>
    public bool TryGetStyle(string name, out SyntaxStyleEntry style)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _stylesByName.TryGetValue(name, out style);
    }

    /// <summary>Gets a style entry by name, or null if not found.</summary>
    public SyntaxStyleEntry? GetStyle(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _stylesByName.TryGetValue(name, out var style) ? style : null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _stylesByName.Clear();
            _managed?.Dispose();
            _managed = null!;
        }
    }
}
