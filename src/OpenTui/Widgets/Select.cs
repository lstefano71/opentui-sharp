namespace OpenTui;

/// <summary>
/// A generic vertical list picker that lets the user highlight and confirm
/// one option from a list of <see cref="SelectOption{T}"/> items.
/// </summary>
/// <typeparam name="T">The type of value associated with each option.</typeparam>
public class Select<T> : Widget
{
    /// <summary>The available options.</summary>
    public IList<SelectOption<T>> Options { get; set; } = new List<SelectOption<T>>();

    /// <summary>The zero-based index of the currently highlighted option.</summary>
    public int SelectedIndex { get; set; }

    /// <summary>The value of the currently highlighted option, or <c>default</c> if out of range.</summary>
    public T? SelectedValue =>
        SelectedIndex >= 0 && SelectedIndex < Options.Count
            ? Options[SelectedIndex].Value
            : default;

    /// <summary>Foreground color for the highlighted row.</summary>
    public Rgba? HighlightFg { get; set; }

    /// <summary>Background color for the highlighted row.</summary>
    public Rgba? HighlightBg { get; set; }

    /// <summary>Character(s) used to indicate the highlighted option.</summary>
    public string ArrowIndicator { get; set; } = "❯";

    /// <summary>Raised when the user confirms a selection.</summary>
    public event Action<SelectOption<T>>? OnSelect;

    /// <summary>Raised when the highlighted index changes.</summary>
    public event Action<int>? OnHighlightChanged;

    /// <summary>Moves the highlight up one position, wrapping around.</summary>
    public void MoveUp()
    {
        if (Options.Count > 0)
            SelectedIndex = (SelectedIndex - 1 + Options.Count) % Options.Count;

        OnHighlightChanged?.Invoke(SelectedIndex);
    }

    /// <summary>Moves the highlight down one position, wrapping around.</summary>
    public void MoveDown()
    {
        if (Options.Count > 0)
            SelectedIndex = (SelectedIndex + 1) % Options.Count;

        OnHighlightChanged?.Invoke(SelectedIndex);
    }

    /// <summary>Confirms the currently highlighted option.</summary>
    public void Confirm()
    {
        if (SelectedIndex >= 0 && SelectedIndex < Options.Count)
            OnSelect?.Invoke(Options[SelectedIndex]);
    }

    /// <inheritdoc />
    protected internal override void Draw(nint buffer, int offsetX, int offsetY)
    {
        // TODO: wire to native buffer
    }
}
