namespace OpenTui;

/// <summary>Describes a button action in a <see cref="Dialog"/>.</summary>
/// <param name="Label">Display text for the button.</param>
/// <param name="OnActivate">Callback invoked when the action is activated.</param>
/// <param name="IsDefault">Whether this is the default (pre-selected) action.</param>
public record DialogAction(string Label, Action OnActivate, bool IsDefault = false);

/// <summary>
/// A modal overlay dialog with a title, arbitrary content, and a row of
/// action buttons.
/// </summary>
public class Dialog : Widget
{
    /// <summary>Optional dialog title.</summary>
    public string? Title { get; set; }

    /// <summary>The widget displayed as the dialog body.</summary>
    public Widget? Content { get; set; }

    /// <summary>Action buttons shown at the bottom of the dialog.</summary>
    public IList<DialogAction> Actions { get; set; } = new List<DialogAction>();

    /// <summary>Whether the dialog is currently visible.</summary>
    public bool IsVisible { get; set; }

    /// <summary>Index of the currently focused action button.</summary>
    public int SelectedActionIndex { get; set; }

    /// <summary>Raised when the dialog is closed.</summary>
    public event Action? OnClose;

    /// <summary>Shows the dialog.</summary>
    public void Show() => IsVisible = true;

    /// <summary>Hides the dialog and raises <see cref="OnClose"/>.</summary>
    public void Hide()
    {
        IsVisible = false;
        OnClose?.Invoke();
    }

    /// <inheritdoc />
    protected internal override void Draw(nint buffer, int offsetX, int offsetY)
    {
        // TODO: wire to native buffer
    }
}
