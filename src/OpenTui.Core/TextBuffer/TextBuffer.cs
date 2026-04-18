using System.Runtime.InteropServices;
using System.Text;
using OpenTui.Core.Native;
using OpenTui.Native;

namespace OpenTui.Core;

/// <summary>
/// Safe managed wrapper around the native OpenTUI text buffer.
/// Wraps all P/Invoke methods in the Text Buffer region of <see cref="OpenTuiNative"/>.
/// </summary>
public sealed class TextBuffer : IDisposable
{
    private nint _handle;
    private bool _disposed;

    private TextBuffer(nint handle)
    {
        _handle = handle;
    }

    /// <summary>Creates a new text buffer with the specified width calculation method.</summary>
    /// <param name="widthMethod">Unicode width calculation method (default: Wcwidth).</param>
    /// <returns>A new <see cref="TextBuffer"/> instance.</returns>
    public static TextBuffer Create(WidthMethod widthMethod = WidthMethod.Wcwidth)
    {
        nint handle = OpenTuiNative.CreateTextBuffer((byte)widthMethod);

        if (handle == nint.Zero)
            throw new InvalidOperationException("Failed to create native text buffer.");

        return new TextBuffer(handle);
    }

    /// <summary>Gets the native text buffer handle. For use by other wrappers that need the raw pointer.</summary>
    internal nint Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _handle;
        }
    }

    #region Properties

    /// <summary>Gets the character length of the text content.</summary>
    public uint Length => OpenTuiNative.TextBufferGetLength(Handle);

    /// <summary>Gets the byte size of the text content.</summary>
    public uint ByteSize => OpenTuiNative.TextBufferGetByteSize(Handle);

    /// <summary>Gets the number of lines in the text buffer.</summary>
    public uint LineCount => OpenTuiNative.TextBufferGetLineCount(Handle);

    /// <summary>Gets or sets the tab display width.</summary>
    public byte TabWidth
    {
        get => OpenTuiNative.TextBufferGetTabWidth(Handle);
        set => OpenTuiNative.TextBufferSetTabWidth(Handle, value);
    }

    /// <summary>Gets the total number of highlights across all lines.</summary>
    public uint HighlightCount => OpenTuiNative.TextBufferGetHighlightCount(Handle);

    #endregion

    #region Text Content

    /// <summary>Sets the text buffer content, replacing any existing content.</summary>
    public void SetText(string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        OpenTuiNative.TextBufferClear(_handle);
        var utf8 = new Utf8String(text);
        utf8.WithPtr((ptr, len) => OpenTuiNative.TextBufferAppend(_handle, ptr, len));
    }

    /// <summary>Appends UTF-8 text to the end of the text buffer.</summary>
    public void AppendText(string text)
    {
        var utf8 = new Utf8String(text);
        utf8.WithPtr((ptr, len) => OpenTuiNative.TextBufferAppend(Handle, ptr, len));
    }

    /// <summary>Loads content from a file into the text buffer.</summary>
    public bool LoadFile(string path)
    {
        var utf8 = new Utf8String(path);
        bool result = false;
        utf8.WithPtr((ptr, len) => result = OpenTuiNative.TextBufferLoadFile(Handle, ptr, len));
        return result;
    }

    /// <summary>Gets the plain text content of the buffer.</summary>
    /// <param name="maxLen">Maximum number of bytes to retrieve.</param>
    /// <returns>The plain text content.</returns>
    public string GetPlainText(int maxLen = 64 * 1024)
    {
        return Utf8String.GetString(
            (outPtr, maxLength) => OpenTuiNative.TextBufferGetPlainText(Handle, outPtr, maxLength),
            maxLen);
    }

    /// <summary>Gets a range of text by character offset.</summary>
    /// <param name="start">Start character offset.</param>
    /// <param name="end">End character offset.</param>
    /// <param name="maxLen">Maximum number of bytes to retrieve.</param>
    /// <returns>The text in the specified range.</returns>
    public string GetTextRange(uint start, uint end, int maxLen = 64 * 1024)
    {
        return Utf8String.GetString(
            (outPtr, maxLength) => OpenTuiNative.TextBufferGetTextRange(Handle, start, end, outPtr, maxLength),
            maxLen);
    }

    /// <summary>Gets a range of text by row/column coordinates.</summary>
    /// <param name="startRow">Starting row number.</param>
    /// <param name="startCol">Starting column number.</param>
    /// <param name="endRow">Ending row number.</param>
    /// <param name="endCol">Ending column number.</param>
    /// <param name="maxLen">Maximum number of bytes to retrieve.</param>
    /// <returns>The text in the specified coordinate range.</returns>
    public string GetTextRangeByCoords(uint startRow, uint startCol, uint endRow, uint endCol, int maxLen = 64 * 1024)
    {
        return Utf8String.GetString(
            (outPtr, maxLength) => OpenTuiNative.TextBufferGetTextRangeByCoords(
                Handle, startRow, startCol, endRow, endCol, outPtr, maxLength),
            maxLen);
    }

    /// <summary>
    /// Sets styled text content from a <see cref="StyledText"/> instance.
    /// Serializes the chunks into the native styled text format.
    /// </summary>
    /// <param name="styledText">The styled text to set.</param>
    public void SetStyledText(StyledText styledText)
    {
        // TODO: Serialize individual chunk styling (fg, bg, attributes, link) into the native
        //       binary format once the exact layout is documented. For now, set the plain text
        //       content which is always correct for the text portion.
        SetText(styledText.PlainText);
    }

    /// <summary>Sets the text buffer content from a registered memory buffer.</summary>
    /// <param name="memId">Registered memory buffer identifier.</param>
    public void SetTextFromMemory(byte memId) =>
        OpenTuiNative.TextBufferSetTextFromMem(Handle, memId);

    /// <summary>Appends text from a registered memory buffer.</summary>
    /// <param name="memId">Registered memory buffer identifier.</param>
    public void AppendFromMemory(byte memId) =>
        OpenTuiNative.TextBufferAppendFromMemId(Handle, memId);

    /// <summary>Resets the text buffer to its initial state.</summary>
    public void Reset() =>
        OpenTuiNative.TextBufferReset(Handle);

    /// <summary>Clears all content from the text buffer.</summary>
    public void Clear() =>
        OpenTuiNative.TextBufferClear(Handle);

    #endregion

    #region Default Styling

    /// <summary>Sets the default foreground color for new text.</summary>
    /// <param name="fg">The foreground color, or null to clear.</param>
    public void SetForeground(Rgba? fg) =>
        RgbaMarshalling.WithColorPtr(fg, ptr =>
            OpenTuiNative.TextBufferSetDefaultFg(Handle, ptr));

    /// <summary>Sets the default background color for new text.</summary>
    /// <param name="bg">The background color, or null to clear.</param>
    public void SetBackground(Rgba? bg) =>
        RgbaMarshalling.WithColorPtr(bg, ptr =>
            OpenTuiNative.TextBufferSetDefaultBg(Handle, ptr));

    /// <summary>Sets the default text attributes for new text.</summary>
    /// <param name="attrs">The text attributes bitmask.</param>
    public void SetAttributes(TextAttributes attrs) =>
        OpenTuiNative.TextBufferSetDefaultAttributes(Handle, (nint)(uint)attrs);

    /// <summary>Resets all default styling (foreground, background, attributes) to initial values.</summary>
    public void ResetDefaults() =>
        OpenTuiNative.TextBufferResetDefaults(Handle);

    #endregion

    #region Highlights

    /// <summary>Adds a highlight to a specific line.</summary>
    public void AddHighlight(uint line, Highlight highlight)
    {
        unsafe
        {
            OpenTuiNative.TextBufferAddHighlight(Handle, line, (nint)(&highlight));
        }
    }

    /// <summary>Adds a highlight by character range.</summary>
    public void AddHighlightByCharRange(Highlight highlight)
    {
        unsafe
        {
            OpenTuiNative.TextBufferAddHighlightByCharRange(Handle, (nint)(&highlight));
        }
    }

    /// <summary>Removes all highlights that match the given reference ID.</summary>
    /// <param name="hlRef">Highlight reference identifier to match.</param>
    public void RemoveHighlight(ushort hlRef) =>
        OpenTuiNative.TextBufferRemoveHighlightsByRef(Handle, hlRef);

    /// <summary>Clears all highlights from a specific line.</summary>
    /// <param name="line">Zero-based line number.</param>
    public void ClearLineHighlights(uint line) =>
        OpenTuiNative.TextBufferClearLineHighlights(Handle, line);

    /// <summary>Clears all highlights from all lines.</summary>
    public void ClearHighlights() =>
        OpenTuiNative.TextBufferClearAllHighlights(Handle);

    /// <summary>Gets the highlights for a specific line.</summary>
    /// <param name="line">Zero-based line number.</param>
    /// <returns>Array of <see cref="Highlight"/> structs for the line.</returns>
    public unsafe Highlight[] GetLineHighlights(uint line)
    {
        nuint count = 0;
        nint ptr = OpenTuiNative.TextBufferGetLineHighlightsPtr(Handle, line, (nint)(&count));

        if (ptr == nint.Zero || count == 0)
            return [];

        var result = new Highlight[(int)count];
        int structSize = sizeof(Highlight);
        for (int i = 0; i < (int)count; i++)
            result[i] = *(Highlight*)(ptr + i * structSize);

        OpenTuiNative.TextBufferFreeLineHighlights(ptr, count);
        return result;
    }

    #endregion

    #region Memory Registry

    /// <summary>Registers a memory buffer and returns its ID.</summary>
    public ushort RegisterMemory(byte[] data, bool copy = true)
    {
        unsafe
        {
            fixed (byte* ptr = data)
            {
                return OpenTuiNative.TextBufferRegisterMemBuffer(
                    Handle, (nint)ptr, (nuint)data.Length, copy);
            }
        }
    }

    /// <summary>Replaces an existing registered memory buffer by ID.</summary>
    public bool ReplaceMemory(byte id, byte[] data, bool copy = true)
    {
        unsafe
        {
            fixed (byte* ptr = data)
            {
                return OpenTuiNative.TextBufferReplaceMemBuffer(
                    Handle, id, (nint)ptr, (nuint)data.Length, copy);
            }
        }
    }

    /// <summary>Clears all registered memory buffers.</summary>
    public void ClearMemory() =>
        OpenTuiNative.TextBufferClearMemRegistry(Handle);

    #endregion

    #region Syntax Style

    /// <summary>Sets the syntax style used for rendering.</summary>
    /// <param name="syntaxStyleHandle">
    /// Handle to the syntax style, or <see cref="nint.Zero"/> to clear.
    /// Obtain from <see cref="SyntaxStyleHandle.DangerousGetHandle"/>.
    /// </param>
    public void SetSyntaxStyle(nint syntaxStyleHandle) =>
        OpenTuiNative.TextBufferSetSyntaxStyle(Handle, syntaxStyleHandle);

    #endregion

    #region IDisposable

    /// <summary>Destroys the native text buffer and releases all resources.</summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            if (_handle != nint.Zero)
            {
                OpenTuiNative.TextBufferDestroy(_handle);
                _handle = nint.Zero;
            }
        }
    }

    #endregion
}
