namespace OpenTui;

/// <summary>Describes a command registered with a <see cref="CommandPalette"/>.</summary>
/// <param name="Label">Display name of the command.</param>
/// <param name="OnActivate">Callback invoked when the command is executed.</param>
/// <param name="KeyBinding">Optional keyboard shortcut hint (e.g. "Ctrl+P").</param>
/// <param name="Group">Optional grouping label for visual separation.</param>
public record CommandEntry(string Label, Action OnActivate, string? KeyBinding = null, string? Group = null);

/// <summary>
/// A fuzzy-search command picker overlay, similar to VS Code's Ctrl+Shift+P palette.
/// </summary>
public class CommandPalette : Widget
{
    /// <summary>The full set of registered commands.</summary>
    public IList<CommandEntry> Commands { get; set; } = new List<CommandEntry>();

    /// <summary>The current search/filter text.</summary>
    public string SearchText { get; set; } = "";

    /// <summary>Whether the palette is currently visible.</summary>
    public bool IsVisible { get; set; }

    /// <summary>Index of the highlighted command in the filtered list.</summary>
    public int SelectedIndex { get; set; }

    /// <summary>
    /// Commands filtered by <see cref="SearchText"/> using a case-insensitive
    /// substring match.
    /// </summary>
    public IReadOnlyList<CommandEntry> FilteredCommands =>
        string.IsNullOrEmpty(SearchText)
            ? Commands.ToList().AsReadOnly()
            : Commands
                .Where(c => c.Label.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                .ToList()
                .AsReadOnly();

    /// <summary>Raised when the palette is closed.</summary>
    public event Action? OnClose;

    /// <summary>Shows the command palette.</summary>
    public void Show() => IsVisible = true;

    /// <summary>Hides the command palette and raises <see cref="OnClose"/>.</summary>
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
