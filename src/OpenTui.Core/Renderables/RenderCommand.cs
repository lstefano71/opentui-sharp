using System.Runtime.InteropServices;

namespace OpenTui.Core;

/// <summary>
/// Tag for render command discriminated union.
/// </summary>
public enum RenderCommandAction : byte
{
    Render,
    PushScissorRect,
    PopScissorRect,
    PushOpacity,
    PopOpacity,
}

/// <summary>
/// A command in the render list built during the updateLayout tree walk.
/// Uses a tagged struct (zero allocation) instead of class hierarchy.
/// Only the fields relevant to the current Action are meaningful.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public struct RenderCommand
{
    public RenderCommandAction Action;

    // For Render
    public Renderable? Renderable;

    // For PushScissorRect
    public int X;
    public int Y;
    public int Width;
    public int Height;
    public int ScreenX;
    public int ScreenY;

    // For PushOpacity
    public float Opacity;

    public static RenderCommand CreateRender(Renderable renderable) => new()
    {
        Action = RenderCommandAction.Render,
        Renderable = renderable,
    };

    public static RenderCommand CreatePushScissorRect(int x, int y, int width, int height, int screenX, int screenY) => new()
    {
        Action = RenderCommandAction.PushScissorRect,
        X = x, Y = y, Width = width, Height = height,
        ScreenX = screenX, ScreenY = screenY,
    };

    public static RenderCommand CreatePopScissorRect() => new()
    {
        Action = RenderCommandAction.PopScissorRect,
    };

    public static RenderCommand CreatePushOpacity(float opacity) => new()
    {
        Action = RenderCommandAction.PushOpacity,
        Opacity = opacity,
    };

    public static RenderCommand CreatePopOpacity() => new()
    {
        Action = RenderCommandAction.PopOpacity,
    };
}
