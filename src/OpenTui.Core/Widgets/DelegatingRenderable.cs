namespace OpenTui.Core;

public sealed class DelegatingOptions : RenderableOptions
{
    public required Renderable Root { get; init; }
    public string? AddTargetId { get; init; }
    public string? RemoveTargetId { get; init; }
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

    public DelegatingRenderable(IRenderContext ctx, DelegatingOptions options)
        : base(ctx, options)
    {
        Root = options.Root;
        _addTargetId = options.AddTargetId;
        _removeTargetId = options.RemoveTargetId;
        _focusTargetId = options.FocusTargetId;
        base.Add(Root);
    }

    public Renderable Root { get; }

    public override int Add(Renderable? obj, int? index = null)
    {
        var target = ResolveTarget(_addTargetId);
        return ReferenceEquals(target, this) ? base.Add(obj, index) : target.Add(obj, index);
    }

    public override int InsertBefore(Renderable? obj, Renderable? anchor)
    {
        var target = ResolveTarget(_addTargetId);
        return ReferenceEquals(target, this) ? base.InsertBefore(obj, anchor) : target.InsertBefore(obj, anchor);
    }

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
