namespace OpenTui.Core;

public sealed class GenericOptions : RenderableOptions
{
    public required Action<OptimizedBuffer, float, Renderable> Render { get; init; }
}

/// <summary>
/// Minimal renderable that delegates its drawing to a caller-provided callback while
/// still participating in layout, hit testing, and child composition.
/// </summary>
public sealed class GenericRenderable : Renderable
{
    private readonly Action<OptimizedBuffer, float, Renderable> _render;

    public GenericRenderable(IRenderContext ctx, GenericOptions options)
        : base(ctx, options)
    {
        _render = options.Render;
    }

    protected override void RenderSelf(OptimizedBuffer buffer, float deltaTime) =>
        _render(buffer, deltaTime, this);
}
