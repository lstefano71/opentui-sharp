namespace OpenTui.Core;

/// <summary>
/// Represents a Local Selection Bounds.
/// </summary>
public readonly record struct LocalSelectionBounds(int AnchorX, int AnchorY, int FocusX, int FocusY, bool IsActive);

/// <summary>
/// Represents a Selection Helpers.
/// </summary>
public static class SelectionHelpers
{
    /// <summary>
    /// Performs convert global to local selection.
    /// </summary>
    /// <param name="globalSelection">The global selection.</param>
    /// <param name="localX">The local x.</param>
    /// <param name="localY">The local y.</param>
    /// <returns>The result of convert global to local selection.</returns>
    public static LocalSelectionBounds? ConvertGlobalToLocalSelection(Selection? globalSelection, int localX, int localY)
    {
        if (globalSelection is null || !globalSelection.IsActive)
            return null;

        return new LocalSelectionBounds(
            globalSelection.AnchorX - localX,
            globalSelection.AnchorY - localY,
            globalSelection.FocusX - localX,
            globalSelection.FocusY - localY,
            true);
    }
}

/// <summary>
/// Represents an active text selection in the terminal UI.
/// Tracks an anchor point (where the selection started) and a focus point (current end).
/// Full implementation matches TypeScript Selection class (lib/selection.ts).
/// </summary>
public class Selection
{
    private readonly Renderable _anchorRenderable;
    private readonly int _anchorRelativeX;
    private readonly int _anchorRelativeY;
    private int _focusX;
    private int _focusY;
    private readonly List<Renderable> _selectedRenderables = [];
    private readonly List<Renderable> _touchedRenderables = [];
    private bool _isActive = true;
    private bool _isDragging = true;
    private bool _isStart;

    /// <summary>
    /// Initializes a new instance of the Selection class.
    /// </summary>
    /// <param name="renderable">The renderable.</param>
    /// <param name="absoluteX">The absolute x.</param>
    /// <param name="absoluteY">The absolute y.</param>
    public Selection(Renderable renderable, int absoluteX, int absoluteY)
    {
        _anchorRenderable = renderable;
        _anchorRelativeX = absoluteX - renderable.X;
        _anchorRelativeY = absoluteY - renderable.Y;
        _focusX = absoluteX;
        _focusY = absoluteY;
    }

    /// <summary>
    /// Gets the anchor x.
    /// </summary>
    public int AnchorX => _anchorRenderable.X + _anchorRelativeX;
    /// <summary>
    /// Gets the anchor y.
    /// </summary>
    public int AnchorY => _anchorRenderable.Y + _anchorRelativeY;
    /// <summary>
    /// Gets the focus x.
    /// </summary>
    public int FocusX => _focusX;
    /// <summary>
    /// Gets the focus y.
    /// </summary>
    public int FocusY => _focusY;
    /// <summary>
    /// Gets the anchor renderable.
    /// </summary>
    public Renderable AnchorRenderable => _anchorRenderable;
    /// <summary>
    /// Gets the selected renderables.
    /// </summary>
    public IReadOnlyList<Renderable> SelectedRenderables => _selectedRenderables;
    /// <summary>
    /// Gets the touched renderables.
    /// </summary>
    public IReadOnlyList<Renderable> TouchedRenderables => _touchedRenderables;
    /// <summary>
    /// Gets or sets a value indicating whether is active.
    /// </summary>
    public bool IsActive { get => _isActive; set => _isActive = value; }
    /// <summary>
    /// Gets or sets a value indicating whether is dragging.
    /// </summary>
    public bool IsDragging { get => _isDragging; set => _isDragging = value; }
    /// <summary>
    /// Gets or sets a value indicating whether is start.
    /// </summary>
    public bool IsStart { get => _isStart; set => _isStart = value; }

    /// <summary>
    /// Gets the bounds.
    /// </summary>
    public ViewportBounds Bounds
    {
        get
        {
            int minX = Math.Min(AnchorX, _focusX);
            int maxX = Math.Max(AnchorX, _focusX);
            int minY = Math.Min(AnchorY, _focusY);
            int maxY = Math.Max(AnchorY, _focusY);
            return new ViewportBounds(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
    }

    /// <summary>
    /// Updates the focus.
    /// </summary>
    /// <param name="x">The horizontal position.</param>
    /// <param name="y">The vertical position.</param>
    public void UpdateFocus(int x, int y)
    {
        _focusX = x;
        _focusY = y;
    }

    /// <summary>
    /// Sets the selected renderables.
    /// </summary>
    /// <param name="renderables">The renderables.</param>
    public void SetSelectedRenderables(IEnumerable<Renderable> renderables)
    {
        _selectedRenderables.Clear();
        _selectedRenderables.AddRange(renderables);
    }

    /// <summary>
    /// Sets the touched renderables.
    /// </summary>
    /// <param name="renderables">The renderables.</param>
    public void SetTouchedRenderables(IEnumerable<Renderable> renderables)
    {
        _touchedRenderables.Clear();
        _touchedRenderables.AddRange(renderables);
    }

    /// <summary>
    /// Gets a selected text.
    /// </summary>
    /// <returns>The selected text.</returns>
    public string GetSelectedText()
    {
        return string.Join(
            "\n",
            _selectedRenderables
                .Where(renderable => !renderable.IsDestroyed)
                .OrderBy(renderable => renderable.ScreenY)
                .ThenBy(renderable => renderable.ScreenX)
                .Select(renderable => renderable.GetSelectedText())
                .Where(text => !string.IsNullOrEmpty(text)));
    }
}
