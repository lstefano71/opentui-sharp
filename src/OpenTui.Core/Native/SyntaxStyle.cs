using OpenTui.Core.Native;
using OpenTui.Native;

namespace OpenTui.Core;

public readonly record struct SyntaxStyleEntry(Rgba? Fg, Rgba? Bg, TextAttributes Attributes);

/// <summary>
/// Managed wrapper around the native syntax style registry.
/// Allows registering named styles (fg/bg/attrs) and resolving them by name.
/// </summary>
public sealed class SyntaxStyle : IDisposable
{
    private nint _handle;
    private bool _disposed;
    private readonly Dictionary<string, SyntaxStyleEntry> _stylesByName = new(StringComparer.Ordinal);

    private SyntaxStyle(nint handle) => _handle = handle;

    /// <summary>Creates a new syntax style registry.</summary>
    public static SyntaxStyle Create()
    {
        nint handle = OpenTuiNative.CreateSyntaxStyle();
        if (handle == nint.Zero)
            throw new InvalidOperationException("Failed to create native syntax style.");
        return new SyntaxStyle(handle);
    }

    internal nint Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _handle;
        }
    }

    /// <summary>Registers a named style and returns its ID.</summary>
    public uint Register(string name, Rgba? fg = null, Rgba? bg = null, TextAttributes attrs = TextAttributes.None)
    {
        var utf8 = new Utf8String(name);
        uint result = 0;
        utf8.WithPtr((namePtr, nameLen) =>
            RgbaMarshalling.WithColorPtrs(fg, bg, (fgPtr, bgPtr) =>
                result = OpenTuiNative.SyntaxStyleRegister(Handle, namePtr, nameLen, fgPtr, bgPtr, (byte)attrs)));
        _stylesByName[name] = new SyntaxStyleEntry(fg, bg, attrs);
        return result;
    }

    /// <summary>Resolves a style ID by name. Returns 0 if not found.</summary>
    public uint ResolveByName(string name)
    {
        var utf8 = new Utf8String(name);
        uint result = 0;
        utf8.WithPtr((namePtr, nameLen) =>
            result = OpenTuiNative.SyntaxStyleResolveByName(Handle, namePtr, nameLen));
        return result;
    }

    /// <summary>Gets the total number of registered styles.</summary>
    public nuint StyleCount => OpenTuiNative.SyntaxStyleGetStyleCount(Handle);

    public bool TryGetStyle(string name, out SyntaxStyleEntry style) =>
        _stylesByName.TryGetValue(name, out style);

    public SyntaxStyleEntry? GetStyle(string name) =>
        _stylesByName.TryGetValue(name, out var style) ? style : null;

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _stylesByName.Clear();
            OpenTuiNative.SyntaxStyleDestroy(_handle);
            _handle = nint.Zero;
        }
    }
}
