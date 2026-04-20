namespace OpenTui.Core;

/// <summary>
/// Services the renderer exposes to renderables during layout, rendering, focus, and selection.
/// Implemented by <see cref="CliRenderer"/> and passed to every renderable so components can
/// request redraws, interact with the hit grid, manage cursor state, and participate in input flow.
/// </summary>
public interface IRenderContext : IEventEmitter
{
    #region Hit Grid

    /// <summary>Registers a rectangular hit target for mouse interaction.</summary>
    void AddToHitGrid(int x, int y, uint width, uint height, uint id);
    /// <summary>Restricts subsequent hit-grid writes to the specified clipped region.</summary>
    void PushHitGridScissorRect(int x, int y, uint width, uint height);
    /// <summary>Restores the previous hit-grid clipping region.</summary>
    void PopHitGridScissorRect();
    /// <summary>Clears all active hit-grid clipping regions.</summary>
    void ClearHitGridScissorRects();

    #endregion

    #region Dimensions

    /// <summary>Gets the current render width in terminal cells.</summary>
    int Width { get; }
    /// <summary>Gets the current render height in terminal cells.</summary>
    int Height { get; }

    /// <summary>Monotonic frame counter, bumped once per render loop iteration.</summary>
    int FrameId { get; }

    #endregion

    #region Render Control

    /// <summary>Queues a render pass as soon as the renderer can schedule one.</summary>
    void RequestRender();
    /// <summary>Requests continuous rendering while a component is animating or otherwise live.</summary>
    void RequestLive();
    /// <summary>Releases a previous live-render request.</summary>
    void DropLive();

    #endregion

    #region Cursor

    /// <summary>Sets the terminal cursor position and visibility.</summary>
    void SetCursorPosition(int x, int y, bool visible);
    /// <summary>Updates cursor appearance such as shape, blinking, and color policy.</summary>
    void SetCursorStyle(CursorStyleOptions options);
    /// <summary>Sets the cursor color.</summary>
    void SetCursorColor(Rgba color);
    /// <summary>Requests a mouse pointer shape for supported terminals.</summary>
    void SetMousePointer(MousePointerStyle shape);

    #endregion

    #region Width Method

    /// <summary>Gets the Unicode width-calculation strategy used by the renderer.</summary>
    WidthMethod WidthMethod { get; }
    /// <summary>Gets renderer capability information when available.</summary>
    object? Capabilities { get; }

    #endregion

    #region Selection

    /// <summary>Gets whether the renderer currently has an active selection.</summary>
    bool HasSelection { get; }
    /// <summary>Gets the active selection, or <see langword="null"/> when no selection exists.</summary>
    Selection? GetSelection();
    /// <summary>Requests a selection refresh after render-tree changes.</summary>
    void RequestSelectionUpdate();
    /// <summary>Clears the active selection.</summary>
    void ClearSelection();
    /// <summary>Starts a selection gesture from the specified renderable and coordinates.</summary>
    void StartSelection(Renderable renderable, int x, int y);
    /// <summary>Extends or finalizes the active selection.</summary>
    void UpdateSelection(Renderable? currentRenderable, int x, int y, bool finishDragging = false);

    #endregion

    #region Focus

    /// <summary>Gets the renderable that currently owns focus.</summary>
    Renderable? CurrentFocusedRenderable { get; }
    /// <summary>Moves focus to the specified renderable.</summary>
    void FocusRenderable(Renderable renderable);
    /// <summary>Removes focus from the specified renderable.</summary>
    void BlurRenderable(Renderable renderable);

    #endregion

    #region Lifecycle

    /// <summary>Registers a renderable for lifecycle callbacks during render passes.</summary>
    void RegisterLifecyclePass(Renderable renderable);
    /// <summary>Removes a renderable from lifecycle-pass tracking.</summary>
    void UnregisterLifecyclePass(Renderable renderable);
    /// <summary>Gets the current set of lifecycle-pass renderables.</summary>
    IReadOnlySet<Renderable> GetLifecyclePasses();

    #endregion

    #region Input

    /// <summary>Gets the public keyboard input source for application code.</summary>
    KeyHandler KeyInput { get; }
    /// <summary>Gets the internal keyboard input source used by built-in components.</summary>
    KeyHandler InternalKeyInput { get; }

    #endregion
}
