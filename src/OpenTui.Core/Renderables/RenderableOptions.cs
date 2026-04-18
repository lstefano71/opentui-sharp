namespace OpenTui.Core;

/// <summary>
/// Options for creating a Renderable, matching TypeScript RenderableOptions interface.
/// Extends LayoutOptions with Renderable-specific fields (events, buffering, z-index, etc.).
/// </summary>
public class RenderableOptions : LayoutOptions
{
    public string? Id { get; init; }
    public int? ZIndex { get; init; }
    public bool? Visible { get; init; }
    public bool? Buffered { get; init; }
    public bool? Live { get; init; }
    public float? Opacity { get; init; }
    public bool? EnableLayout { get; init; }

    // Render hooks
    public Action<OptimizedBuffer, float>? RenderBefore { get; init; }
    public Action<OptimizedBuffer, float>? RenderAfter { get; init; }

    // Mouse event handlers
    public Action<UiMouseEvent>? OnMouse { get; init; }
    public Action<UiMouseEvent>? OnMouseDown { get; init; }
    public Action<UiMouseEvent>? OnMouseUp { get; init; }
    public Action<UiMouseEvent>? OnMouseMove { get; init; }
    public Action<UiMouseEvent>? OnMouseDrag { get; init; }
    public Action<UiMouseEvent>? OnMouseDragEnd { get; init; }
    public Action<UiMouseEvent>? OnMouseDrop { get; init; }
    public Action<UiMouseEvent>? OnMouseOver { get; init; }
    public Action<UiMouseEvent>? OnMouseOut { get; init; }
    public Action<UiMouseEvent>? OnMouseScroll { get; init; }

    // Other event handlers
    public Action<PasteEvent>? OnPaste { get; init; }
    public Action<KeyEvent>? OnKeyDown { get; init; }
    public Action? OnSizeChange { get; init; }
}
