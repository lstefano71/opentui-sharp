namespace OpenTui;

/// <summary>
/// Describes a single item in a <see cref="ContextMenu"/>, optionally with
/// a check mark, separator, or nested sub-menu.
/// </summary>
/// <param name="Label">Display text for the menu item.</param>
/// <param name="OnActivate">Callback invoked when the item is activated.</param>
/// <param name="Checked">Whether a check mark is displayed.</param>
/// <param name="Separator">When true this item renders as a visual separator line.</param>
/// <param name="SubMenu">Optional nested sub-menu items.</param>
public record MenuItemDef(
    string Label,
    Action? OnActivate = null,
    bool Checked = false,
    bool Separator = false,
    IList<MenuItemDef>? SubMenu = null);

/// <summary>
/// A right-click (or programmatically triggered) context menu overlay.
/// </summary>
public class ContextMenu : Widget
{
    /// <summary>The menu items to display.</summary>
    public IList<MenuItemDef> Items { get; set; } = new List<MenuItemDef>();

    /// <summary>Whether the menu is currently visible.</summary>
    public bool IsVisible { get; set; }

    /// <summary>Index of the currently highlighted item.</summary>
    public int SelectedIndex { get; set; }

    /// <summary>Horizontal position (column) where the menu appears.</summary>
    public int X { get; set; }

    /// <summary>Vertical position (row) where the menu appears.</summary>
    public int Y { get; set; }

    /// <summary>Raised when the menu is closed.</summary>
    public event Action? OnClose;

    /// <summary>Shows the context menu at the specified screen position.</summary>
    public void ShowAt(int x, int y)
    {
        X = x;
        Y = y;
        IsVisible = true;
    }

    /// <summary>Hides the context menu and raises <see cref="OnClose"/>.</summary>
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
