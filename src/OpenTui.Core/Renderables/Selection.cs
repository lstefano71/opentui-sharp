namespace OpenTui.Core;

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
}
