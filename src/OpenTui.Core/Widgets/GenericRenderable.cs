namespace OpenTui.Core;

/// <summary>
/// Represents configuration options for Generic.
/// </summary>
public sealed class GenericOptions : RenderableOptions
{
    /// <summary>
    /// Gets or sets the render.
    /// </summary>
    public required Action<OptimizedBuffer, float, Renderable> Render { get; init; }
}

/// <summary>
/// Minimal renderable that delegates its drawing to a caller-provided callback while
/// still participating in layout, hit testing, and child composition.
/// </summary>
public sealed class GenericRenderable : Renderable
{
    private readonly Action<OptimizedBuffer, float, Renderable> _render;

    /// <summary>
    /// Initializes a new instance of the GenericRenderable class.
    /// </summary>
    /// <param name="ctx">The render context.</param>
    /// <param name="options">The configuration options.</param>
    public GenericRenderable(IRenderContext ctx, GenericOptions options)
        : base(ctx, options)
    {
        _render = options.Render;
    }

    /// <inheritdoc />
    protected override void RenderSelf(OptimizedBuffer buffer, float deltaTime) =>
        _render(buffer, deltaTime, this);
}
