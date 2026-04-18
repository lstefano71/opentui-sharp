namespace OpenTui.Core;

public readonly record struct LocalSelectionBounds(int AnchorX, int AnchorY, int FocusX, int FocusY, bool IsActive);

public static class SelectionHelpers
{
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

    public Selection(Renderable renderable, int absoluteX, int absoluteY)
    {
        _anchorRenderable = renderable;
        _anchorRelativeX = absoluteX - renderable.X;
        _anchorRelativeY = absoluteY - renderable.Y;
        _focusX = absoluteX;
        _focusY = absoluteY;
    }

    public int AnchorX => _anchorRenderable.X + _anchorRelativeX;
    public int AnchorY => _anchorRenderable.Y + _anchorRelativeY;
    public int FocusX => _focusX;
    public int FocusY => _focusY;
    public Renderable AnchorRenderable => _anchorRenderable;
    public IReadOnlyList<Renderable> SelectedRenderables => _selectedRenderables;
    public IReadOnlyList<Renderable> TouchedRenderables => _touchedRenderables;
    public bool IsActive { get => _isActive; set => _isActive = value; }
    public bool IsDragging { get => _isDragging; set => _isDragging = value; }
    public bool IsStart { get => _isStart; set => _isStart = value; }

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

    public void UpdateFocus(int x, int y)
    {
        _focusX = x;
        _focusY = y;
    }

    public void SetSelectedRenderables(IEnumerable<Renderable> renderables)
    {
        _selectedRenderables.Clear();
        _selectedRenderables.AddRange(renderables);
    }

    public void SetTouchedRenderables(IEnumerable<Renderable> renderables)
    {
        _touchedRenderables.Clear();
        _touchedRenderables.AddRange(renderables);
    }

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
