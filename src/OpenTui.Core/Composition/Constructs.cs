namespace OpenTui.Core;

/// <summary>
/// Specifies which child IDs receive delegated operations (add, remove, focus).
/// Used with <see cref="Constructs.Delegate"/>.
/// </summary>
public sealed class DelegateMapping
{
    /// <summary>ID of the descendant that receives Add/InsertBefore operations.</summary>
    public string? Add { get; init; }

    /// <summary>ID of the descendant that receives Remove operations.</summary>
    public string? Remove { get; init; }

    /// <summary>ID of the descendant that receives Focus/Blur operations.</summary>
    public string? Focus { get; init; }
}

/// <summary>
/// Factory methods that create VNode descriptors for each renderable type.
/// AOT-safe — uses factory delegates, not reflection.
/// </summary>
public static class Constructs
{
    /// <summary>
    /// Creates a VNode that wraps an inner VNode with delegation.
    /// When instantiated, produces a <see cref="DelegatingRenderable"/> that routes
    /// add/remove/focus operations to specific descendant IDs.
    /// </summary>
    public static VNode Delegate(DelegateMapping mapping, VNode inner) =>
        new VNode
        {
            Factory = ctx =>
            {
                var root = VNodeRuntime.Instantiate(ctx, inner);
                return new DelegatingRenderable(ctx, new DelegatingOptions
                {
                    Root = root,
                    AddTargetId = mapping.Add,
                    RemoveTargetId = mapping.Remove,
                    FocusTargetId = mapping.Focus,
                });
            },
            Children = [],
        };

    /// <summary>Creates a VNode for <see cref="GenericRenderable"/>.</summary>
    public static VNode Generic(GenericOptions options, params VChild[] children) =>
        new VNode
        {
            Factory = ctx => new GenericRenderable(ctx, options),
            Children = children,
        };

    /// <summary>Creates a VNode for <see cref="BoxRenderable"/>.</summary>
    public static VNode Box(BoxOptions? options = null, params VChild[] children) =>
        new VNode
        {
            Factory = ctx => new BoxRenderable(ctx, options ?? new BoxOptions()),
            Children = children,
        };

    /// <summary>Creates a VNode for <see cref="TextRenderable"/> with styled content.</summary>
    public static VNode Text(StyledText content) =>
        new VNode
        {
            Factory = ctx => new TextRenderable(ctx, new TextOptions { StyledContent = content }),
            Children = [],
        };

    /// <summary>Creates a VNode for <see cref="TextRenderable"/>.</summary>
    public static VNode Text(TextOptions? options = null, params VChild[] children) =>
        new VNode
        {
            Factory = ctx => new TextRenderable(ctx, options ?? new TextOptions()),
            Children = children,
        };

    /// <summary>Creates a VNode for <see cref="ASCIIFontRenderable"/>.</summary>
    public static VNode ASCIIFont(ASCIIFontOptions? options = null, params VChild[] children) =>
        new VNode
        {
            Factory = ctx => new ASCIIFontRenderable(ctx, options ?? new ASCIIFontOptions()),
            Children = children,
        };

    /// <summary>Creates a VNode for <see cref="InputRenderable"/>.</summary>
    public static VNode Input(InputOptions? options = null, params VChild[] children) =>
        new VNode
        {
            Factory = ctx => new InputRenderable(ctx, options ?? new InputOptions()),
            Children = children,
        };

    /// <summary>Creates a VNode for <see cref="SelectRenderable"/>.</summary>
    public static VNode Select(SelectOptions? options = null, params VChild[] children) =>
        new VNode
        {
            Factory = ctx => new SelectRenderable(ctx, options ?? new SelectOptions()),
            Children = children,
        };

    /// <summary>Creates a VNode for <see cref="TabSelectRenderable"/>.</summary>
    public static VNode TabSelect(TabSelectOptions? options = null, params VChild[] children) =>
        new VNode
        {
            Factory = ctx => new TabSelectRenderable(ctx, options ?? new TabSelectOptions()),
            Children = children,
        };

    /// <summary>Creates a VNode for <see cref="FrameBufferRenderable"/>.</summary>
    public static VNode FrameBuffer(FrameBufferOptions? options = null, params VChild[] children) =>
        new VNode
        {
            Factory = ctx => new FrameBufferRenderable(ctx, options ?? new FrameBufferOptions()),
            Children = children,
        };

    /// <summary>Creates a VNode for <see cref="CodeRenderable"/>.</summary>
    public static VNode Code(CodeOptions? options = null, params VChild[] children) =>
        new VNode
        {
            Factory = ctx => new CodeRenderable(ctx, options ?? new CodeOptions()),
            Children = children,
        };

    /// <summary>Creates a VNode for <see cref="ScrollBoxRenderable"/>.</summary>
    public static VNode ScrollBox(ScrollBoxOptions? options = null, params VChild[] children) =>
        new VNode
        {
            Factory = ctx => new ScrollBoxRenderable(ctx, options ?? new ScrollBoxOptions()),
            Children = children,
        };
}
