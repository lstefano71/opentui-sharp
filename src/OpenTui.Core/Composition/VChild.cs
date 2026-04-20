namespace OpenTui.Core;

/// <summary>
/// Represents a virtual child in a VNode tree. Can be a VNode, a Renderable,
/// a list of VChild, or null.
/// </summary>
public abstract class VChild
{
    private VChild() { }

    /// <summary>Converts a <see cref="VNode"/> to a <see cref="VChild"/>.</summary>
    public static implicit operator VChild(VNode node) => new OfVNode(node);
    /// <summary>Converts a <see cref="Renderable"/> to a <see cref="VChild"/>.</summary>
    public static implicit operator VChild(Renderable renderable) => new OfRenderable(renderable);

    internal sealed class OfVNode(VNode node) : VChild { public VNode Node => node; }
    internal sealed class OfRenderable(Renderable renderable) : VChild { public Renderable Renderable => renderable; }
    internal sealed class OfList(VChild[] items) : VChild { public VChild[] Items => items; }
    internal sealed class OfNull : VChild { public static readonly OfNull Instance = new(); }

    /// <summary>Creates a VChild list from an array of children.</summary>
    public static VChild List(params VChild[] items) => new OfList(items);

    /// <summary>Returns the null sentinel.</summary>
    public static VChild Null => OfNull.Instance;
}
