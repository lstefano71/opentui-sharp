namespace OpenTui.Core;

/// <summary>
/// A virtual node descriptor that can be instantiated into a Renderable tree.
/// AOT-safe — uses factory delegates, not reflection.
/// </summary>
public sealed class VNode
{
    /// <summary>Factory that creates a Renderable from a render context.</summary>
    public required Func<IRenderContext, Renderable> Factory { get; init; }

    /// <summary>Children to add after construction.</summary>
    public VChild[] Children { get; init; } = [];
}
