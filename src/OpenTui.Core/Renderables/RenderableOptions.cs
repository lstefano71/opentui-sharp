namespace OpenTui.Core;

/// <summary>
/// Options for creating a Renderable, matching TypeScript RenderableOptions interface.
/// Extends LayoutOptions with Renderable-specific fields (events, buffering, z-index, etc.).
/// </summary>
public class RenderableOptions : LayoutOptions
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public string? Id { get; init; }
    /// <summary>
    /// Gets or sets the z index.
    /// </summary>
    public int? ZIndex { get; init; }
    /// <summary>
    /// Gets or sets the visible.
    /// </summary>
    public bool? Visible { get; init; }
    /// <summary>
    /// Gets or sets the buffered.
    /// </summary>
    public bool? Buffered { get; init; }
    /// <summary>
    /// Gets or sets the live.
    /// </summary>
    public bool? Live { get; init; }
    /// <summary>
    /// Gets or sets the opacity.
    /// </summary>
    public float? Opacity { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether enable layout.
    /// </summary>
    public bool? EnableLayout { get; init; }

    // Render hooks
    /// <summary>
    /// Gets or sets the render before.
    /// </summary>
    public Action<OptimizedBuffer, float>? RenderBefore { get; init; }
    /// <summary>
    /// Gets or sets the render after.
    /// </summary>
    public Action<OptimizedBuffer, float>? RenderAfter { get; init; }

    // Mouse event handlers
    /// <summary>
    /// Gets or sets the on mouse.
    /// </summary>
    public Action<UiMouseEvent>? OnMouse { get; init; }
    /// <summary>
    /// Gets or sets the on mouse down.
    /// </summary>
    public Action<UiMouseEvent>? OnMouseDown { get; init; }
    /// <summary>
    /// Gets or sets the on mouse up.
    /// </summary>
    public Action<UiMouseEvent>? OnMouseUp { get; init; }
    /// <summary>
    /// Gets or sets the on mouse move.
    /// </summary>
    public Action<UiMouseEvent>? OnMouseMove { get; init; }
    /// <summary>
    /// Gets or sets the on mouse drag.
    /// </summary>
    public Action<UiMouseEvent>? OnMouseDrag { get; init; }
    /// <summary>
    /// Gets or sets the on mouse drag end.
    /// </summary>
    public Action<UiMouseEvent>? OnMouseDragEnd { get; init; }
    /// <summary>
    /// Gets or sets the on mouse drop.
    /// </summary>
    public Action<UiMouseEvent>? OnMouseDrop { get; init; }
    /// <summary>
    /// Gets or sets the on mouse over.
    /// </summary>
    public Action<UiMouseEvent>? OnMouseOver { get; init; }
    /// <summary>
    /// Gets or sets the on mouse out.
    /// </summary>
    public Action<UiMouseEvent>? OnMouseOut { get; init; }
    /// <summary>
    /// Gets or sets the on mouse scroll.
    /// </summary>
    public Action<UiMouseEvent>? OnMouseScroll { get; init; }

    // Other event handlers
    /// <summary>
    /// Gets or sets the on paste.
    /// </summary>
    public Action<PasteEvent>? OnPaste { get; init; }
    /// <summary>
    /// Gets or sets the on key down.
    /// </summary>
    public Action<KeyEvent>? OnKeyDown { get; init; }
    /// <summary>
    /// Gets or sets the on size change.
    /// </summary>
    public Action? OnSizeChange { get; init; }
}
