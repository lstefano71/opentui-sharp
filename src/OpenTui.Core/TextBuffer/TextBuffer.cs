using System.Text;
using OpenTui.Core.Managed;

namespace OpenTui.Core;

/// <summary>
/// Managed wrapper around <see cref="ManagedTextBuffer"/>.
/// Provides the same public API as the former native P/Invoke wrapper but
/// delegates entirely to the pure-C# text buffer implementation.
/// </summary>
public sealed class TextBuffer : IDisposable
{
    internal ManagedTextBuffer _managed;
    private bool _disposed;
    private readonly bool _ownsManaged;

    private TextBuffer(ManagedTextBuffer managed, bool ownsManaged = true)
    {
        _managed = managed;
        _ownsManaged = ownsManaged;
    }

    /// <summary>Creates a new text buffer with the specified width calculation method.</summary>
    /// <param name="widthMethod">Unicode width calculation method (default: Wcwidth).</param>
    /// <returns>A new <see cref="TextBuffer"/> instance.</returns>
    public static TextBuffer Create(WidthMethod widthMethod = WidthMethod.Wcwidth) =>
        new(ManagedTextBuffer.Create(widthMethod));

    /// <summary>Wraps an existing <see cref="ManagedTextBuffer"/> without taking ownership.</summary>
    internal static TextBuffer WrapExisting(ManagedTextBuffer tb)
    {
        ArgumentNullException.ThrowIfNull(tb);
        return new TextBuffer(tb, ownsManaged: false);
    }

    #region Properties

    /// <summary>Gets the character length of the text content.</summary>
    public uint Length
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _managed.Length;
        }
    }

    /// <summary>Gets the byte size of the text content.</summary>
    public uint ByteSize
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _managed.ByteSize;
        }
    }

    /// <summary>Gets the number of lines in the text buffer.</summary>
    public uint LineCount
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _managed.LineCount;
        }
    }

    /// <summary>Gets or sets the tab display width.</summary>
    public byte TabWidth
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _managed.TabWidth;
        }
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _managed.TabWidth = value;
        }
    }

    /// <summary>Gets the total number of highlights across all lines.</summary>
    public uint HighlightCount
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _managed.HighlightCount;
        }
    }

    #endregion

    #region Text Content

    /// <summary>Sets the text buffer content, replacing any existing content.</summary>
    public void SetText(string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _managed.SetText(text);
    }

    /// <summary>Appends UTF-8 text to the end of the text buffer.</summary>
    public void AppendText(string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _managed.Append(text);
    }

    /// <summary>Loads content from a file into the text buffer.</summary>
    public bool LoadFile(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _managed.LoadFile(path);
    }

    /// <summary>Gets the plain text content of the buffer.</summary>
    /// <param name="maxLen">Ignored in managed implementation. Kept for API compatibility.</param>
    /// <returns>The plain text content.</returns>
    public string GetPlainText(int maxLen = 64 * 1024)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _managed.GetText();
    }

    /// <summary>Gets a range of text by character offset.</summary>
    /// <param name="start">Start character offset.</param>
    /// <param name="end">End character offset.</param>
    /// <param name="maxLen">Ignored in managed implementation. Kept for API compatibility.</param>
    /// <returns>The text in the specified range.</returns>
    public string GetTextRange(uint start, uint end, int maxLen = 64 * 1024)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _managed.GetTextRangeByOffset(start, end);
    }

    /// <summary>Gets a range of text by row/column coordinates.</summary>
    /// <param name="startRow">Starting row number.</param>
    /// <param name="startCol">Starting column number.</param>
    /// <param name="endRow">Ending row number.</param>
    /// <param name="endCol">Ending column number.</param>
    /// <param name="maxLen">Ignored in managed implementation. Kept for API compatibility.</param>
    /// <returns>The text in the specified coordinate range.</returns>
    public string GetTextRangeByCoords(uint startRow, uint startCol, uint endRow, uint endCol, int maxLen = 64 * 1024)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _managed.GetTextRange(startRow, startCol, endRow, endCol);
    }

    /// <summary>
    /// Sets styled text content from a <see cref="StyledText"/> instance.
    /// Converts chunks to <see cref="StyledChunk"/> and delegates to the managed buffer.
    /// </summary>
    /// <param name="styledText">The styled text to set.</param>
    public void SetStyledText(StyledText styledText)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (styledText?.Chunks is null || styledText.Chunks.Count == 0)
        {
            _managed.Clear();
            return;
        }

        var chunks = styledText.Chunks;
        var managedChunks = new StyledChunk[chunks.Count];
        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            byte[] utf8 = Encoding.UTF8.GetBytes(chunk.Text ?? "");
            managedChunks[i] = new StyledChunk(
                utf8,
                chunk.Fg,
                chunk.Bg,
                (uint)chunk.Attributes,
                chunk.Link);
        }
        _managed.SetStyledText(managedChunks);
    }

    /// <summary>Sets the text buffer content from a registered memory buffer.</summary>
    /// <param name="memId">Registered memory buffer identifier.</param>
    public void SetTextFromMemory(byte memId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var data = _managed.MemRegistry.Get(memId)
            ?? throw new ArgumentException($"Memory buffer {memId} not found.", nameof(memId));
        string text = Encoding.UTF8.GetString(data.Span);
        _managed.SetText(text);
    }

    /// <summary>Appends text from a registered memory buffer.</summary>
    /// <param name="memId">Registered memory buffer identifier.</param>
    public void AppendFromMemory(byte memId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var data = _managed.MemRegistry.Get(memId)
            ?? throw new ArgumentException($"Memory buffer {memId} not found.", nameof(memId));
        string text = Encoding.UTF8.GetString(data.Span);
        _managed.Append(text);
    }

    /// <summary>Resets the text buffer to its initial state.</summary>
    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _managed.Reset();
    }

    /// <summary>Clears all content from the text buffer.</summary>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _managed.Clear();
    }

    #endregion

    #region Default Styling

    /// <summary>Sets the default foreground color for new text.</summary>
    /// <param name="fg">The foreground color, or null to clear.</param>
    public void SetForeground(Rgba? fg)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _managed.DefaultFg = fg;
    }

    /// <summary>Sets the default background color for new text.</summary>
    /// <param name="bg">The background color, or null to clear.</param>
    public void SetBackground(Rgba? bg)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _managed.DefaultBg = bg;
    }

    /// <summary>Sets the default text attributes for new text.</summary>
    /// <param name="attrs">The text attributes bitmask.</param>
    public void SetAttributes(TextAttributes attrs)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _managed.DefaultAttributes = (uint)attrs;
    }

    /// <summary>Resets all default styling (foreground, background, attributes) to initial values.</summary>
    public void ResetDefaults()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _managed.DefaultFg = null;
        _managed.DefaultBg = null;
        _managed.DefaultAttributes = null;
    }

    #endregion

    #region Highlights

    /// <summary>Adds a highlight to a specific line.</summary>
    public void AddHighlight(uint line, Highlight highlight)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _managed.AddHighlight(line, highlight);
    }

    /// <summary>Adds a highlight by character range.</summary>
    public void AddHighlightByCharRange(Highlight highlight)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _managed.AddHighlightByCharRange(highlight);
    }

    /// <summary>Removes all highlights that match the given reference ID.</summary>
    /// <param name="hlRef">Highlight reference identifier to match.</param>
    public void RemoveHighlight(ushort hlRef)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _managed.RemoveHighlight(hlRef);
    }

    /// <summary>Clears all highlights from a specific line.</summary>
    /// <param name="line">Zero-based line number.</param>
    public void ClearLineHighlights(uint line)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _managed.ClearLineHighlights(line);
    }

    /// <summary>Clears all highlights from all lines.</summary>
    public void ClearHighlights()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _managed.ClearHighlights();
    }

    /// <summary>Gets the highlights for a specific line.</summary>
    /// <param name="line">Zero-based line number.</param>
    /// <returns>Array of <see cref="Highlight"/> structs for the line.</returns>
    public Highlight[] GetLineHighlights(uint line)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _managed.GetLineHighlights(line);
    }

    #endregion

    #region Memory Registry

    /// <summary>Registers a memory buffer and returns its ID.</summary>
    public ushort RegisterMemory(byte[] data, bool copy = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ReadOnlyMemory<byte> mem = copy ? (byte[])data.Clone() : data;
        return _managed.MemRegistry.Register(mem);
    }

    /// <summary>Replaces an existing registered memory buffer by ID.</summary>
    public bool ReplaceMemory(byte id, byte[] data, bool copy = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ReadOnlyMemory<byte> mem = copy ? (byte[])data.Clone() : data;
        return _managed.MemRegistry.Replace(id, mem);
    }

    /// <summary>Clears all registered memory buffers.</summary>
    public void ClearMemory()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _managed.MemRegistry.Clear();
    }

    #endregion

    #region Syntax Style

    /// <summary>Sets the syntax style used for rendering.</summary>
    /// <param name="style">The syntax style instance, or null to clear.</param>
    public void SetSyntaxStyle(SyntaxStyle? style)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _managed.SyntaxStyle = style?._managed;
    }

    #endregion

    #region IDisposable

    /// <summary>Releases all resources held by this text buffer.</summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            if (_ownsManaged)
                _managed?.Dispose();
            _managed = null!;
        }
    }

    #endregion
}
