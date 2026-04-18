using Facebook.Yoga;

namespace OpenTui.Core;

/// <summary>
/// Root of the render tree. Owns the render command list and drives the 3-pass
/// render pipeline: lifecycle → yoga calculateLayout → tree walk → execute.
/// Matches TypeScript RootRenderable class (Renderable.ts L1701-1801).
/// </summary>
public class RootRenderable : Renderable
{
    private readonly List<RenderCommand> _renderList = [];

    public RootRenderable(IRenderContext ctx)
        : base(ctx, new RenderableOptions
        {
            Id = "__root__",
            ZIndex = 0,
            Visible = true,
            Width = DimensionValue.Point(ctx.Width),
            Height = DimensionValue.Point(ctx.Height),
            EnableLayout = true,
        })
    {
        // Root creates its own yoga node to ensure proper config
        YGNodeAPI.YGNodeFree(YogaNode);
        YogaNode = new Node(LayoutConfig.Shared);
        YGNodeStyleAPI.YGNodeStyleSetWidth(YogaNode, ctx.Width);
        YGNodeStyleAPI.YGNodeStyleSetHeight(YogaNode, ctx.Height);
        YGNodeStyleAPI.YGNodeStyleSetFlexDirection(YogaNode, YGFlexDirection.Column);

        CalculateLayout();
    }

    /// <summary>
    /// Full 3-pass render: lifecycle callbacks → layout → render commands.
    /// Called by the renderer each frame.
    /// </summary>
    public override void Render(OptimizedBuffer buffer, float deltaTime)
    {
        if (!Visible) return;

        // Pass 0: Run lifecycle callbacks
        foreach (var renderable in _ctx.GetLifecyclePasses())
            renderable.OnLifecyclePass?.Invoke();

        // Pass 1: Calculate layout from root (if dirty)
        if (YogaNode.IsDirty())
            CalculateLayout();

        // Pass 2: Update layout throughout the tree and collect render list
        _renderList.Clear();
        UpdateLayout(deltaTime, _renderList);

        // Pass 3: Execute render commands (skip index 0 = root's own render)
        _ctx.ClearHitGridScissorRects();
        for (int i = 1; i < _renderList.Count; i++)
        {
            ref var command = ref System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_renderList)[i];
            switch (command.Action)
            {
                case RenderCommandAction.Render:
                    if (command.Renderable is { IsDestroyed: false } renderable)
                        renderable.Render(buffer, deltaTime);
                    break;

                case RenderCommandAction.PushScissorRect:
                    buffer.PushScissorRect(command.X, command.Y, (uint)command.Width, (uint)command.Height);
                    _ctx.PushHitGridScissorRect(command.ScreenX, command.ScreenY, (uint)command.Width, (uint)command.Height);
                    break;

                case RenderCommandAction.PopScissorRect:
                    buffer.PopScissorRect();
                    _ctx.PopHitGridScissorRect();
                    break;

                case RenderCommandAction.PushOpacity:
                    buffer.PushOpacity(command.Opacity);
                    break;

                case RenderCommandAction.PopOpacity:
                    buffer.PopOpacity();
                    break;
            }
        }
    }

    protected override void PropagateLiveCount(int delta)
    {
        var oldCount = _liveCount;
        _liveCount += delta;

        if (oldCount == 0 && _liveCount > 0)
            _ctx.RequestLive();
        else if (oldCount > 0 && _liveCount == 0)
            _ctx.DropLive();
    }

    public void CalculateLayout()
    {
        YGNodeAPI.YGNodeCalculateLayout(YogaNode, _widthValue, _heightValue, YGDirection.LTR);
        Emit(LayoutEvents.LayoutChanged);
    }

    public void Resize(int width, int height)
    {
        WidthDimension = DimensionValue.Point(width);
        HeightDimension = DimensionValue.Point(height);
        YGNodeStyleAPI.YGNodeStyleSetWidth(YogaNode, width);
        YGNodeStyleAPI.YGNodeStyleSetHeight(YogaNode, height);
        _widthValue = width;
        _heightValue = height;
        Emit<(int Width, int Height)>(LayoutEvents.Resized, (width, height));
    }
}
