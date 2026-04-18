namespace OpenTui.Core;

/// <summary>
/// Render context passed to renderables during the render pass.
/// Matches TypeScript RenderContext interface (types.ts L70-108).
/// Extends IEventEmitter so renderables can subscribe to renderer-level events directly.
/// The concrete implementation lives in the Renderer (Phase 7).
/// </summary>
public interface IRenderContext : IEventEmitter
{
    #region Hit Grid

    void AddToHitGrid(int x, int y, uint width, uint height, uint id);
    void PushHitGridScissorRect(int x, int y, uint width, uint height);
    void PopHitGridScissorRect();
    void ClearHitGridScissorRects();

    #endregion

    #region Dimensions

    int Width { get; }
    int Height { get; }

    /// <summary>Monotonic frame counter, bumped once per render loop iteration.</summary>
    int FrameId { get; }

    #endregion

    #region Render Control

    void RequestRender();
    void RequestLive();
    void DropLive();

    #endregion

    #region Cursor

    void SetCursorPosition(int x, int y, bool visible);
    void SetCursorStyle(CursorStyleOptions options);
    void SetCursorColor(Rgba color);
    void SetMousePointer(MousePointerStyle shape);

    #endregion

    #region Width Method

    WidthMethod WidthMethod { get; }
    object? Capabilities { get; }

    #endregion

    #region Selection

    bool HasSelection { get; }
    Selection? GetSelection();
    void RequestSelectionUpdate();
    void ClearSelection();
    void StartSelection(Renderable renderable, int x, int y);
    void UpdateSelection(Renderable? currentRenderable, int x, int y, bool finishDragging = false);

    #endregion

    #region Focus

    Renderable? CurrentFocusedRenderable { get; }
    void FocusRenderable(Renderable renderable);
    void BlurRenderable(Renderable renderable);

    #endregion

    #region Lifecycle

    void RegisterLifecyclePass(Renderable renderable);
    void UnregisterLifecyclePass(Renderable renderable);
    IReadOnlySet<Renderable> GetLifecyclePasses();

    #endregion

    #region Input

    KeyHandler KeyInput { get; }
    KeyHandler InternalKeyInput { get; }

    #endregion
}
