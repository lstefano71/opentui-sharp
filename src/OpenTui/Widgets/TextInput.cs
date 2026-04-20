namespace OpenTui;

/// <summary>
/// A single-line text input widget with cursor positioning,
/// placeholder support, and optional max-length constraint.
/// </summary>
public class TextInput : Widget
{
    /// <summary>The current text value.</summary>
    public string Text { get; set; } = "";

    /// <summary>Placeholder text shown when <see cref="Text"/> is empty.</summary>
    public string? Placeholder { get; set; }

    /// <summary>Foreground color for the placeholder text.</summary>
    public Rgba? PlaceholderFg { get; set; }

    /// <summary>Maximum number of characters allowed, or null for unlimited.</summary>
    public int? MaxLength { get; set; }

    /// <summary>Zero-based cursor position within the text.</summary>
    public int CursorPosition { get; set; }

    /// <summary>Raised when the user presses Enter to submit the input.</summary>
    public event Action<string>? OnSubmit;

    /// <summary>Raised when the text value changes.</summary>
    public event Action<string>? OnChange;

    /// <summary>Raises the <see cref="OnSubmit"/> event.</summary>
    protected void RaiseSubmit(string text) => OnSubmit?.Invoke(text);

    /// <summary>Raises the <see cref="OnChange"/> event.</summary>
    protected void RaiseChange(string text) => OnChange?.Invoke(text);

    /// <inheritdoc />
    protected internal override void Draw(NativeBuffer buffer, int offsetX, int offsetY)
    {
        // TODO: wire to native buffer
    }
}
