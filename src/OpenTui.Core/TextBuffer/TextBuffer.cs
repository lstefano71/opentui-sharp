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
    private readonly bool _ownsHandle;
    private readonly List<nint> _nativeAllocations = [];
    private readonly List<nint> _registeredMemAllocations = [];

    private TextBuffer(nint handle, bool ownsHandle = true)
    {
        _handle = handle;
        _ownsHandle = ownsHandle;
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

    internal static TextBuffer WrapExisting(nint handle)
    {
        if (handle == nint.Zero)
            throw new ArgumentException("Handle cannot be zero.", nameof(handle));

        return new TextBuffer(handle, ownsHandle: false);
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
    public unsafe void SetText(string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        // Clear content first (clears rope, preserves highlights — matches Zig clear()).
        OpenTuiNative.TextBufferClear(_handle);
        // Free previous append allocations now that rope no longer references them.
        FreeAppendAllocations();

        if (string.IsNullOrEmpty(text)) return;

        // TextBufferAppend stores the raw pointer in the native mem_registry without copying.
        // Allocate native memory so the pointer survives GC collection/compaction.
        byte[] utf8Bytes = Encoding.UTF8.GetBytes(text);
        nint nativeMem = (nint)NativeMemory.Alloc((nuint)utf8Bytes.Length);
        fixed (byte* src = utf8Bytes)
            NativeMemory.Copy(src, (void*)nativeMem, (nuint)utf8Bytes.Length);
        _nativeAllocations.Add(nativeMem);
        OpenTuiNative.TextBufferAppend(_handle, nativeMem, (nuint)utf8Bytes.Length);
    }

    /// <summary>Appends UTF-8 text to the end of the text buffer.</summary>
    public unsafe void AppendText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        // TextBufferAppend stores the raw pointer in the mem_registry without copying.
        // We must allocate native memory so the pointer survives GC.
        byte[] utf8Bytes = Encoding.UTF8.GetBytes(text);
        nint nativeMem = (nint)NativeMemory.Alloc((nuint)utf8Bytes.Length);
        fixed (byte* src = utf8Bytes)
            NativeMemory.Copy(src, (void*)nativeMem, (nuint)utf8Bytes.Length);
        _nativeAllocations.Add(nativeMem);
        OpenTuiNative.TextBufferAppend(Handle, nativeMem, (nuint)utf8Bytes.Length);
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
    /// Serializes the chunks into native StyledChunk structs and calls the native
    /// setStyledText API which copies all text data into Zig-owned memory.
    /// </summary>
    /// <param name="styledText">The styled text to set.</param>
    public unsafe void SetStyledText(StyledText styledText)
    {
        var chunks = styledText.Chunks;
        if (chunks.Count == 0)
        {
            Clear();
            return;
        }

        // Encode all chunk texts to UTF-8
        byte[][] utf8Arrays = new byte[chunks.Count][];
        for (int i = 0; i < chunks.Count; i++)
            utf8Arrays[i] = Encoding.UTF8.GetBytes(chunks[i].Text);

        // Encode link texts
        byte[]?[] linkArrays = new byte[chunks.Count][];
        for (int i = 0; i < chunks.Count; i++)
            linkArrays[i] = chunks[i].Link is { } link ? Encoding.UTF8.GetBytes(link) : null;

        // Allocate native chunks array
        var nativeChunks = new NativeStyledChunk[chunks.Count];

        // We need to pin all byte arrays and RGBA floats simultaneously.
        // Use GCHandle for pinning since we have a variable number of arrays.
        var pins = new GCHandle[chunks.Count * 2]; // text + link
        int pinCount = 0;

        // Allocate RGBA float arrays on the stack or heap
        float[]?[] fgArrays = new float[chunks.Count][];
        float[]?[] bgArrays = new float[chunks.Count][];
        var colorPins = new GCHandle[chunks.Count * 2]; // fg + bg
        int colorPinCount = 0;

        try
        {
            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];

                // Pin text bytes
                var textPin = GCHandle.Alloc(utf8Arrays[i], GCHandleType.Pinned);
                pins[pinCount++] = textPin;
                nativeChunks[i].TextPtr = textPin.AddrOfPinnedObject();
                nativeChunks[i].TextLen = (nuint)utf8Arrays[i].Length;

                // Fg color
                if (chunk.Fg is { } fg)
                {
                    fgArrays[i] = [fg.R, fg.G, fg.B, fg.A];
                    var fgPin = GCHandle.Alloc(fgArrays[i], GCHandleType.Pinned);
                    colorPins[colorPinCount++] = fgPin;
                    nativeChunks[i].FgPtr = fgPin.AddrOfPinnedObject();
                }

                // Bg color
                if (chunk.Bg is { } bg)
                {
                    bgArrays[i] = [bg.R, bg.G, bg.B, bg.A];
                    var bgPin = GCHandle.Alloc(bgArrays[i], GCHandleType.Pinned);
                    colorPins[colorPinCount++] = bgPin;
                    nativeChunks[i].BgPtr = bgPin.AddrOfPinnedObject();
                }

                nativeChunks[i].Attributes = (uint)chunk.Attributes;

                // Link
                if (linkArrays[i] is { } linkBytes)
                {
                    var linkPin = GCHandle.Alloc(linkBytes, GCHandleType.Pinned);
                    pins[pinCount++] = linkPin;
                    nativeChunks[i].LinkPtr = linkPin.AddrOfPinnedObject();
                    nativeChunks[i].LinkLen = (nuint)linkBytes.Length;
                }
            }

            fixed (NativeStyledChunk* chunksPtr = nativeChunks)
            {
                OpenTuiNative.TextBufferSetStyledText(Handle, (nint)chunksPtr, (nuint)chunks.Count);
            }
        }
        finally
        {
            for (int i = 0; i < pinCount; i++)
                if (pins[i].IsAllocated) pins[i].Free();
            for (int i = 0; i < colorPinCount; i++)
                if (colorPins[i].IsAllocated) colorPins[i].Free();
        }
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
    public void Reset()
    {
        FreeAppendAllocations();
        OpenTuiNative.TextBufferReset(Handle);
    }

    /// <summary>Clears all content from the text buffer.</summary>
    public void Clear()
    {
        FreeAppendAllocations();
        OpenTuiNative.TextBufferClear(Handle);
    }

    /// <summary>Frees native memory allocated by AppendText calls.
    /// Called when the buffer content is replaced (Clear/Reset/SetText).</summary>
    private unsafe void FreeAppendAllocations()
    {
        foreach (nint alloc in _nativeAllocations)
            NativeMemory.Free((void*)alloc);
        _nativeAllocations.Clear();
    }

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
    public unsafe void SetAttributes(TextAttributes attrs)
    {
        uint val = (uint)attrs;
        OpenTuiNative.TextBufferSetDefaultAttributes(Handle, (nint)(&val));
    }

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

    /// <summary>Registers a memory buffer and returns its ID.
    /// Data is copied to native memory; the caller can release the byte[] immediately.</summary>
    public unsafe ushort RegisterMemory(byte[] data, bool copy = true)
    {
        if (data.Length == 0)
            return OpenTuiNative.TextBufferRegisterMemBuffer(Handle, nint.Zero, 0, false);

        // Zig's "owned" flag means "I will GPA-free this pointer on cleanup".
        // We must allocate via NativeMemory so it's valid for the buffer's lifetime,
        // then pass owned=false so Zig won't try to GPA-free our allocation.
        nint nativeMem = (nint)NativeMemory.Alloc((nuint)data.Length);
        fixed (byte* src = data)
            NativeMemory.Copy(src, (void*)nativeMem, (nuint)data.Length);

        ushort id = OpenTuiNative.TextBufferRegisterMemBuffer(
            Handle, nativeMem, (nuint)data.Length, false);
        _registeredMemAllocations.Add(nativeMem);
        return id;
    }

    /// <summary>Replaces an existing registered memory buffer by ID.
    /// Data is copied to native memory; the caller can release the byte[] immediately.</summary>
    public unsafe bool ReplaceMemory(byte id, byte[] data, bool copy = true)
    {
        nint nativeMem = (nint)NativeMemory.Alloc((nuint)data.Length);
        fixed (byte* src = data)
            NativeMemory.Copy(src, (void*)nativeMem, (nuint)data.Length);

        bool ok = OpenTuiNative.TextBufferReplaceMemBuffer(
            Handle, id, nativeMem, (nuint)data.Length, false);
        _registeredMemAllocations.Add(nativeMem);
        return ok;
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
    public unsafe void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            if (_ownsHandle && _handle != nint.Zero)
            {
                OpenTuiNative.TextBufferDestroy(_handle);
            }
            _handle = nint.Zero;
            // Free native memory allocations AFTER Zig deinit (which no longer tries to free them)
            foreach (nint alloc in _nativeAllocations)
                NativeMemory.Free((void*)alloc);
            _nativeAllocations.Clear();

            foreach (nint alloc in _registeredMemAllocations)
                NativeMemory.Free((void*)alloc);
            _registeredMemAllocations.Clear();
        }
    }

    #endregion
}
