namespace OpenTui.Core;

/// <summary>
/// Represents configuration options for Delegating.
/// </summary>
public sealed class DelegatingOptions : RenderableOptions
{
    /// <summary>
    /// Gets or sets the root.
    /// </summary>
    public required Renderable Root { get; init; }
    /// <summary>
    /// Gets or sets the add target id.
    /// </summary>
    public string? AddTargetId { get; init; }
    /// <summary>
    /// Gets or sets the remove target id.
    /// </summary>
    public string? RemoveTargetId { get; init; }
    /// <summary>
    /// Gets or sets the focus target id.
    /// </summary>
    public string? FocusTargetId { get; init; }
}

/// <summary>
/// Wraps a composed renderable tree and routes add/remove/focus operations to named
/// descendants, matching the host-override behavior used by the upstream vnode demo.
/// </summary>
public sealed class DelegatingRenderable : Renderable
{
    private readonly string? _addTargetId;
    private readonly string? _removeTargetId;
    private readonly string? _focusTargetId;

    /// <summary>
    /// Initializes a new instance of the DelegatingRenderable class.
    /// </summary>
    /// <param name="ctx">The render context.</param>
    /// <param name="options">The configuration options.</param>
    public DelegatingRenderable(IRenderContext ctx, DelegatingOptions options)
        : base(ctx, options)
    {
        Root = options.Root;
        _addTargetId = options.AddTargetId;
        _removeTargetId = options.RemoveTargetId;
        _focusTargetId = options.FocusTargetId;
        base.Add(Root);
    }

    /// <summary>
    /// Gets the root.
    /// </summary>
    public Renderable Root { get; }

    /// <inheritdoc />
    public override int Add(Renderable? obj, int? index = null)
    {
        var target = ResolveTarget(_addTargetId);
        return ReferenceEquals(target, this) ? base.Add(obj, index) : target.Add(obj, index);
    }

    /// <inheritdoc />
    public override int InsertBefore(Renderable? obj, Renderable? anchor)
    {
        var target = ResolveTarget(_addTargetId);
        return ReferenceEquals(target, this) ? base.InsertBefore(obj, anchor) : target.InsertBefore(obj, anchor);
    }

    /// <inheritdoc />
    public override void Remove(string id)
    {
        var target = ResolveTarget(_removeTargetId);
        if (ReferenceEquals(target, this))
        {
            base.Remove(id);
            return;
        }

        target.Remove(id);
    }

    /// <inheritdoc />
    public override void Focus()
    {
        var target = ResolveTarget(_focusTargetId);
        if (ReferenceEquals(target, this))
        {
            base.Focus();
            return;
        }

        target.Focus();
    }

    /// <inheritdoc />
    public override void Blur()
    {
        var target = ResolveTarget(_focusTargetId);
        if (!ReferenceEquals(target, this))
            target.Blur();

        base.Blur();
    }

    private Renderable ResolveTarget(string? id)
    {
        if (string.IsNullOrEmpty(id))
            return this;

        return FindDescendantById(id) ?? this;
    }
}
