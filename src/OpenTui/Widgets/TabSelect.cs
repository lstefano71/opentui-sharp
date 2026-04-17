namespace OpenTui;

/// <summary>
/// A horizontal tab bar that lets the user select from a row of tabs.
/// </summary>
/// <typeparam name="T">The type of value associated with each tab.</typeparam>
public class TabSelect<T> : Widget
{
    /// <summary>The available tabs.</summary>
    public IList<SelectOption<T>> Tabs { get; set; } = new List<SelectOption<T>>();

    /// <summary>The zero-based index of the currently selected tab.</summary>
    public int SelectedIndex { get; set; }

    /// <summary>The value of the currently selected tab, or <c>default</c> if out of range.</summary>
    public T? SelectedValue =>
        SelectedIndex >= 0 && SelectedIndex < Tabs.Count
            ? Tabs[SelectedIndex].Value
            : default;

    /// <summary>Underline color for the active tab indicator.</summary>
    public Rgba? UnderlineColor { get; set; }

    /// <summary>Raised when the user selects a tab.</summary>
    public event Action<SelectOption<T>>? OnSelect;

    /// <inheritdoc />
    protected internal override void Draw(nint buffer, int offsetX, int offsetY)
    {
        // TODO: wire to native buffer
    }
}
