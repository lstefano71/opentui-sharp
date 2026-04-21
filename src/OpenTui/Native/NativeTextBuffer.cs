using System.Runtime.CompilerServices;
using CoreTextBuffer = OpenTui.Core.TextBuffer;

namespace OpenTui;

/// <summary>High-level wrapper around the native OpenTUI text buffer.</summary>
public sealed class NativeTextBuffer : IDisposable
{
    internal readonly CoreTextBuffer _core;
    private bool _disposed;

    /// <summary>Creates a new text buffer with the specified width calculation method.</summary>
    public NativeTextBuffer(byte widthMethod = 0)
    {
        _core = CoreTextBuffer.Create();
    }

    /// <summary>Gets the character length of the text buffer contents.</summary>
    public uint Length => _core.Length;

    /// <summary>Gets the byte size of the text buffer contents.</summary>
    public uint ByteSize => _core.ByteSize;

    /// <summary>Gets the number of lines in the text buffer.</summary>
    public uint LineCount => _core.LineCount;

    /// <summary>Gets the total number of highlights across all lines.</summary>
    public uint HighlightCount => _core.HighlightCount;

    /// <summary>Gets or sets the tab display width.</summary>
    public byte TabWidth
    {
        get => _core.TabWidth;
        set => _core.TabWidth = value;
    }

    /// <summary>Clears all content from the text buffer.</summary>
    public void Clear() =>
        _core.Clear();

    /// <summary>Resets the text buffer to its initial state.</summary>
    public void Reset() =>
        _core.Reset();

    /// <summary>Resets all default styling to initial values.</summary>
    public void ResetDefaults() =>
        _core.ResetDefaults();

    /// <summary>Appends UTF-8 text to the text buffer.</summary>
    public void Append(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        _core.AppendText(text);
    }

    /// <summary>Sets the default foreground color.</summary>
    public void SetDefaultFg(Rgba color) =>
        _core.SetForeground(ToCore(color));

    /// <summary>Sets the default background color.</summary>
    public void SetDefaultBg(Rgba color) =>
        _core.SetBackground(ToCore(color));

    /// <summary>Loads content from a file into the text buffer.</summary>
    public bool LoadFile(string path) =>
        _core.LoadFile(path);

    /// <summary>Clears all highlights from all lines.</summary>
    public void ClearAllHighlights() =>
        _core.ClearHighlights();

    /// <summary>Clears all highlights from a specific line.</summary>
    public void ClearLineHighlights(uint line) =>
        _core.ClearLineHighlights(line);

    /// <summary>Removes all highlights that match the given reference ID.</summary>
    public void RemoveHighlightsByRef(ushort hlRef) =>
        _core.RemoveHighlight(hlRef);

    /// <summary>Clears all registered memory buffers (no-op in managed mode, use Core TextBuffer directly).</summary>
    [Obsolete("Use the Core layer TextBuffer memory APIs directly.")]
    public void ClearMemRegistry() =>
        throw new NotSupportedException("ClearMemRegistry is not supported in managed mode. Use Core TextBuffer.ClearMemory() instead.");

    /// <summary>Sets the text buffer content from a registered memory buffer (not supported in managed mode).</summary>
    [Obsolete("Use the Core layer TextBuffer memory APIs directly.")]
    public void SetTextFromMem(byte memId) =>
        throw new NotSupportedException("SetTextFromMem is not supported in managed mode. Use Core TextBuffer.SetTextFromMemory() instead.");

    /// <summary>Appends text from a registered memory buffer (not supported in managed mode).</summary>
    [Obsolete("Use the Core layer TextBuffer memory APIs directly.")]
    public void AppendFromMemId(byte memId) =>
        throw new NotSupportedException("AppendFromMemId is not supported in managed mode. Use Core TextBuffer.AppendFromMemory() instead.");

    /// <summary>Sets the syntax style used for rendering (not supported via raw nint in managed mode).</summary>
    [Obsolete("Use the Core layer TextBuffer.SetSyntaxStyle(SyntaxStyle) instead.")]
    public void SetSyntaxStyle(nint syntaxStyle) =>
        throw new NotSupportedException("nint-based SetSyntaxStyle is not supported in managed mode. Use Core TextBuffer.SetSyntaxStyle(SyntaxStyle) instead.");

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _core.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static OpenTui.Core.Rgba ToCore(Rgba c) => new(c.R, c.G, c.B, c.A);
}
