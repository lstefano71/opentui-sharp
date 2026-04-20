namespace OpenTui.Core;

/// <summary>
/// Runtime for instantiating VNode trees into concrete Renderable trees.
/// </summary>
public static class VNodeRuntime
{
    /// <summary>
    /// Instantiates a VNode tree into a concrete Renderable tree.
    /// </summary>
    public static Renderable Instantiate(IRenderContext ctx, VNode node)
    {
        var renderable = node.Factory(ctx);

        foreach (var child in FlattenChildren(node.Children))
        {
            switch (child)
            {
                case VChild.OfVNode v:
                    renderable.Add(VNodeRuntime.Instantiate(ctx, v.Node));
                    break;
                case VChild.OfRenderable r:
                    renderable.Add(r.Renderable);
                    break;
            }
        }

        return renderable;
    }

    /// <summary>
    /// Attempts to convert a node (VNode or Renderable) to a Renderable.
    /// </summary>
    public static Renderable? MaybeMakeRenderable(IRenderContext ctx, object? node)
    {
        return node switch
        {
            Renderable r => r,
            VNode v => Instantiate(ctx, v),
            _ => null,
        };
    }

    internal static IEnumerable<VChild> FlattenChildren(VChild[] children)
    {
        foreach (var child in children)
        {
            switch (child)
            {
                case VChild.OfList list:
                    foreach (var inner in FlattenChildren(list.Items))
                        yield return inner;
                    break;
                case VChild.OfNull:
                    break;
                default:
                    yield return child;
                    break;
            }
        }
    }
}
