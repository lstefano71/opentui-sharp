using System.Runtime.InteropServices;

namespace OpenTui.Core;

/// <summary>
/// Tag for render command discriminated union.
/// </summary>
public enum RenderCommandAction : byte
{
    /// <summary>
    /// Represents the Render option.
    /// </summary>
    Render,
    /// <summary>
    /// Represents the Push Scissor Rect option.
    /// </summary>
    PushScissorRect,
    /// <summary>
    /// Represents the Pop Scissor Rect option.
    /// </summary>
    PopScissorRect,
    /// <summary>
    /// Represents the Push Opacity option.
    /// </summary>
    PushOpacity,
    /// <summary>
    /// Represents the Pop Opacity option.
    /// </summary>
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
    /// <summary>
    /// Stores the action.
    /// </summary>
    public RenderCommandAction Action;

    // For Render
    /// <summary>
    /// Stores the renderable.
    /// </summary>
    public Renderable? Renderable;

    // For PushScissorRect
    /// <summary>
    /// Stores the x.
    /// </summary>
    public int X;
    /// <summary>
    /// Stores the y.
    /// </summary>
    public int Y;
    /// <summary>
    /// Stores the width.
    /// </summary>
    public int Width;
    /// <summary>
    /// Stores the height.
    /// </summary>
    public int Height;
    /// <summary>
    /// Stores the screen x.
    /// </summary>
    public int ScreenX;
    /// <summary>
    /// Stores the screen y.
    /// </summary>
    public int ScreenY;

    // For PushOpacity
    /// <summary>
    /// Stores the opacity.
    /// </summary>
    public float Opacity;

    /// <summary>
    /// Creates a render.
    /// </summary>
    /// <param name="renderable">The renderable.</param>
    /// <returns>The render.</returns>
    public static RenderCommand CreateRender(Renderable renderable) => new()
    {
        Action = RenderCommandAction.Render,
        Renderable = renderable,
    };

    /// <summary>
    /// Creates a push scissor rect.
    /// </summary>
    /// <param name="x">The horizontal position.</param>
    /// <param name="y">The vertical position.</param>
    /// <param name="width">The width value.</param>
    /// <param name="height">The height value.</param>
    /// <param name="screenX">The screen x.</param>
    /// <param name="screenY">The screen y.</param>
    /// <returns>The push scissor rect.</returns>
    public static RenderCommand CreatePushScissorRect(int x, int y, int width, int height, int screenX, int screenY) => new()
    {
        Action = RenderCommandAction.PushScissorRect,
        X = x, Y = y, Width = width, Height = height,
        ScreenX = screenX, ScreenY = screenY,
    };

    /// <summary>
    /// Creates a pop scissor rect.
    /// </summary>
    /// <returns>The pop scissor rect.</returns>
    public static RenderCommand CreatePopScissorRect() => new()
    {
        Action = RenderCommandAction.PopScissorRect,
    };

    /// <summary>
    /// Creates a push opacity.
    /// </summary>
    /// <param name="opacity">The opacity.</param>
    /// <returns>The push opacity.</returns>
    public static RenderCommand CreatePushOpacity(float opacity) => new()
    {
        Action = RenderCommandAction.PushOpacity,
        Opacity = opacity,
    };

    /// <summary>
    /// Creates a pop opacity.
    /// </summary>
    /// <returns>The pop opacity.</returns>
    public static RenderCommand CreatePopOpacity() => new()
    {
        Action = RenderCommandAction.PopOpacity,
    };
}
