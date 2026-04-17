using OpenTui.Native;

namespace OpenTui;

/// <summary>High-level wrapper around the native OpenTUI text buffer.</summary>
public sealed class NativeTextBuffer : IDisposable
{
    private readonly TextBufferHandle _handle;
    private bool _disposed;

    /// <summary>Creates a new text buffer with the specified width calculation method.</summary>
    public NativeTextBuffer(byte widthMethod = 0)
    {
        nint ptr = OpenTuiNative.CreateTextBuffer(widthMethod);
        _handle = new TextBufferHandle();
        _handle.SetHandleValue(ptr);
    }

    internal nint Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _handle.DangerousGetHandle();
        }
    }

    /// <summary>Gets the character length of the text buffer contents.</summary>
    public uint Length => OpenTuiNative.TextBufferGetLength(Handle);

    /// <summary>Gets the byte size of the text buffer contents.</summary>
    public uint ByteSize => OpenTuiNative.TextBufferGetByteSize(Handle);

    /// <summary>Gets the number of lines in the text buffer.</summary>
    public uint LineCount => OpenTuiNative.TextBufferGetLineCount(Handle);

    /// <summary>Gets the total number of highlights across all lines.</summary>
    public uint HighlightCount => OpenTuiNative.TextBufferGetHighlightCount(Handle);

    /// <summary>Gets or sets the tab display width.</summary>
    public byte TabWidth
    {
        get => OpenTuiNative.TextBufferGetTabWidth(Handle);
        set => OpenTuiNative.TextBufferSetTabWidth(Handle, value);
    }

    /// <summary>Clears all content from the text buffer.</summary>
    public void Clear() =>
        OpenTuiNative.TextBufferClear(Handle);

    /// <summary>Resets the text buffer to its initial state.</summary>
    public void Reset() =>
        OpenTuiNative.TextBufferReset(Handle);

    /// <summary>Resets all default styling to initial values.</summary>
    public void ResetDefaults() =>
        OpenTuiNative.TextBufferResetDefaults(Handle);

    /// <summary>Appends UTF-8 text to the text buffer.</summary>
    public void Append(string text)
    {
        var utf8 = new Utf8String(text);
        utf8.WithPtr((ptr, len) => OpenTuiNative.TextBufferAppend(Handle, ptr, len));
    }

    /// <summary>Sets the default foreground color.</summary>
    public void SetDefaultFg(Rgba color) =>
        RgbaMarshalling.WithColorPtr(color, ptr => OpenTuiNative.TextBufferSetDefaultFg(Handle, ptr));

    /// <summary>Sets the default background color.</summary>
    public void SetDefaultBg(Rgba color) =>
        RgbaMarshalling.WithColorPtr(color, ptr => OpenTuiNative.TextBufferSetDefaultBg(Handle, ptr));

    /// <summary>Loads content from a file into the text buffer.</summary>
    public bool LoadFile(string path)
    {
        var utf8 = new Utf8String(path);
        bool result = false;
        utf8.WithPtr((ptr, len) => result = OpenTuiNative.TextBufferLoadFile(Handle, ptr, len));
        return result;
    }

    /// <summary>Clears all highlights from all lines.</summary>
    public void ClearAllHighlights() =>
        OpenTuiNative.TextBufferClearAllHighlights(Handle);

    /// <summary>Clears all highlights from a specific line.</summary>
    public void ClearLineHighlights(uint line) =>
        OpenTuiNative.TextBufferClearLineHighlights(Handle, line);

    /// <summary>Removes all highlights that match the given reference ID.</summary>
    public void RemoveHighlightsByRef(ushort hlRef) =>
        OpenTuiNative.TextBufferRemoveHighlightsByRef(Handle, hlRef);

    /// <summary>Clears all registered memory buffers.</summary>
    public void ClearMemRegistry() =>
        OpenTuiNative.TextBufferClearMemRegistry(Handle);

    /// <summary>Sets the text buffer content from a registered memory buffer.</summary>
    public void SetTextFromMem(byte memId) =>
        OpenTuiNative.TextBufferSetTextFromMem(Handle, memId);

    /// <summary>Appends text from a registered memory buffer.</summary>
    public void AppendFromMemId(byte memId) =>
        OpenTuiNative.TextBufferAppendFromMemId(Handle, memId);

    /// <summary>Sets the syntax style used for rendering.</summary>
    public void SetSyntaxStyle(nint syntaxStyle) =>
        OpenTuiNative.TextBufferSetSyntaxStyle(Handle, syntaxStyle);

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _handle.Dispose();
        }
    }
}
