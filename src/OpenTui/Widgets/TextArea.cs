namespace OpenTui;

/// <summary>
/// A multi-line text editor widget with cursor tracking,
/// optional read-only mode, and placeholder support.
/// </summary>
public class TextArea : Widget
{
    /// <summary>The current text content (may contain newlines).</summary>
    public string Text { get; set; } = "";

    /// <summary>Placeholder text shown when <see cref="Text"/> is empty.</summary>
    public string? Placeholder { get; set; }

    /// <summary>When true the content cannot be modified by the user.</summary>
    public bool ReadOnly { get; set; }

    /// <summary>Zero-based line index of the cursor.</summary>
    public int CursorLine { get; set; }

    /// <summary>Zero-based column index of the cursor.</summary>
    public int CursorColumn { get; set; }

    /// <summary>Raised when the text value changes.</summary>
    public event Action<string>? OnChange;

    /// <summary>Raises the <see cref="OnChange"/> event.</summary>
    protected void RaiseChange(string text) => OnChange?.Invoke(text);

    /// <inheritdoc />
    protected internal override void Draw(NativeBuffer buffer, int offsetX, int offsetY)
    {
        // TODO: wire to native buffer
    }
}
