namespace OpenTui.Core;

/// <summary>
/// Options for TextNodeRenderable.
/// </summary>
public class TextNodeOptions
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public string? Id { get; init; }
    /// <summary>
    /// Gets or sets the fg.
    /// </summary>
    public Rgba? Fg { get; init; }
    /// <summary>
    /// Gets or sets the bg.
    /// </summary>
    public Rgba? Bg { get; init; }
    /// <summary>
    /// Gets or sets the attributes.
    /// </summary>
    public TextAttributes Attributes { get; init; }
    /// <summary>
    /// Gets or sets the link url.
    /// </summary>
    public string? LinkUrl { get; init; }
}

/// <summary>
/// Pure data node for tree-based inline text composition with style inheritance.
/// TextNode does NOT own a Yoga node — it's purely compositional.
/// Matches TypeScript TextNodeRenderable from TextNode.ts.
/// </summary>
public class TextNodeRenderable
{
    private static int _nextNum = 1;

    private Rgba? _fg;
    private Rgba? _bg;
    private TextAttributes _attributes;
    private string? _linkUrl;
    private readonly List<object> _children = []; // string | TextNodeRenderable
    private bool _dirty;

    /// <summary>
    /// Gets or sets the parent.
    /// </summary>
    public TextNodeRenderable? Parent { get; internal set; }
    /// <summary>
    /// Gets the num.
    /// </summary>
    public int Num { get; }
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Initializes a new instance of the TextNodeRenderable class.
    /// </summary>
    /// <param name="options">The configuration options.</param>
    public TextNodeRenderable(TextNodeOptions? options = null)
    {
        options ??= new TextNodeOptions();
        Num = Interlocked.Increment(ref _nextNum);
        Id = options.Id ?? $"renderable-{Num}";
        _fg = options.Fg;
        _bg = options.Bg;
        _attributes = options.Attributes;
        _linkUrl = options.LinkUrl;
    }

    #region Properties

    /// <summary>
    /// Gets or sets the fg.
    /// </summary>
    public Rgba? Fg
    {
        get => _fg;
        set { _fg = value; RequestRender(); }
    }

    /// <summary>
    /// Gets or sets the bg.
    /// </summary>
    public Rgba? Bg
    {
        get => _bg;
        set { _bg = value; RequestRender(); }
    }

    /// <summary>
    /// Gets or sets the attributes.
    /// </summary>
    public TextAttributes Attributes
    {
        get => _attributes;
        set { _attributes = value; RequestRender(); }
    }

    /// <summary>
    /// Gets or sets the link url.
    /// </summary>
    public string? LinkUrl
    {
        get => _linkUrl;
        set { _linkUrl = value; RequestRender(); }
    }

    /// <summary>
    /// Gets a value indicating whether is dirty.
    /// </summary>
    public bool IsDirty => _dirty;

    /// <summary>
    /// Gets the children.
    /// </summary>
    public IReadOnlyList<object> Children => _children;

    #endregion

    #region Child Management

    /// <summary>Add a string, TextNodeRenderable, or StyledText. Returns the insert index.</summary>
    public int Add(string text, int? index = null)
    {
        if (index.HasValue)
        {
            _children.Insert(index.Value, text);
            RequestRender();
            return index.Value;
        }
        int insertIndex = _children.Count;
        _children.Add(text);
        RequestRender();
        return insertIndex;
    }

    /// <summary>
    /// Performs add.
    /// </summary>
    /// <param name="node">The node instance.</param>
    /// <param name="index">The zero-based index.</param>
    /// <returns>The result of add.</returns>
    public int Add(TextNodeRenderable node, int? index = null)
    {
        if (index.HasValue)
        {
            _children.Insert(index.Value, node);
            node.Parent = this;
            RequestRender();
            return index.Value;
        }
        int insertIndex = _children.Count;
        _children.Add(node);
        node.Parent = this;
        RequestRender();
        return insertIndex;
    }

    /// <summary>
    /// Performs add.
    /// </summary>
    /// <param name="styledText">The styled text.</param>
    /// <param name="index">The zero-based index.</param>
    /// <returns>The result of add.</returns>
    public int Add(StyledText styledText, int? index = null)
    {
        var nodes = StyledTextToTextNodes(styledText);
        if (index.HasValue)
        {
            for (int i = 0; i < nodes.Length; i++)
                _children.Insert(index.Value + i, nodes[i]);
            foreach (var n in nodes) n.Parent = this;
            RequestRender();
            return index.Value;
        }
        int insertIndex = _children.Count;
        foreach (var n in nodes)
        {
            _children.Add(n);
            n.Parent = this;
        }
        RequestRender();
        return insertIndex;
    }

    /// <summary>
    /// Performs replace.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <param name="index">The zero-based index.</param>
    public void Replace(string text, int index)
    {
        _children[index] = text;
        RequestRender();
    }

    /// <summary>
    /// Performs replace.
    /// </summary>
    /// <param name="node">The node instance.</param>
    /// <param name="index">The zero-based index.</param>
    public void Replace(TextNodeRenderable node, int index)
    {
        _children[index] = node;
        node.Parent = this;
        RequestRender();
    }

    /// <summary>
    /// Performs insert before.
    /// </summary>
    /// <param name="child">The child instance.</param>
    /// <param name="anchor">The anchor.</param>
    public void InsertBefore(object child, TextNodeRenderable anchor)
    {
        int anchorIndex = _children.IndexOf(anchor);
        if (anchorIndex == -1)
            throw new InvalidOperationException("Anchor node not found in children");

        switch (child)
        {
            case string s:
                _children.Insert(anchorIndex, s);
                break;
            case TextNodeRenderable node:
                _children.Insert(anchorIndex, node);
                node.Parent = this;
                break;
            case StyledText st:
                var nodes = StyledTextToTextNodes(st);
                for (int i = 0; i < nodes.Length; i++)
                    _children.Insert(anchorIndex + i, nodes[i]);
                foreach (var n in nodes) n.Parent = this;
                break;
            default:
                throw new ArgumentException("Child must be a string, TextNodeRenderable, or StyledText");
        }
        RequestRender();
    }

    /// <summary>
    /// Performs remove.
    /// </summary>
    /// <param name="id">The identifier.</param>
    public void Remove(string id)
    {
        int idx = GetRenderableIndex(id);
        if (idx == -1)
            throw new InvalidOperationException("Child not found");
        var child = (TextNodeRenderable)_children[idx];
        _children.RemoveAt(idx);
        child.Parent = null;
        RequestRender();
    }

    /// <summary>
    /// Performs clear.
    /// </summary>
    public void Clear()
    {
        _children.Clear();
        RequestRender();
    }

    /// <summary>
    /// Gets a renderable children.
    /// </summary>
    /// <returns>The renderable children.</returns>
    public TextNodeRenderable[] GetRenderableChildren() =>
        _children.OfType<TextNodeRenderable>().ToArray();

    /// <summary>
    /// Gets a children count.
    /// </summary>
    /// <returns>The children count.</returns>
    public int GetChildrenCount() => _children.Count;

    /// <summary>
    /// Gets a renderable.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The renderable.</returns>
    public TextNodeRenderable? GetRenderable(string id) =>
        _children.OfType<TextNodeRenderable>().FirstOrDefault(c => c.Id == id);

    /// <summary>
    /// Gets a renderable index.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The renderable index.</returns>
    public int GetRenderableIndex(string id) =>
        _children.FindIndex(c => c is TextNodeRenderable n && n.Id == id);

    #endregion

    #region Style Inheritance

    /// <summary>Merge this node's styles with parent styles (child overrides parent).</summary>
    public (Rgba? Fg, Rgba? Bg, TextAttributes Attributes, string? LinkUrl) MergeStyles(
        Rgba? parentFg, Rgba? parentBg, TextAttributes parentAttributes, string? parentLinkUrl)
    {
        return (
            _fg ?? parentFg,
            _bg ?? parentBg,
            _attributes | parentAttributes,
            _linkUrl ?? parentLinkUrl);
    }

    /// <summary>Flatten the tree into TextChunks with inherited styles.</summary>
    public TextChunk[] GatherWithInheritedStyle(
        Rgba? parentFg = null,
        Rgba? parentBg = null,
        TextAttributes parentAttributes = TextAttributes.None,
        string? parentLinkUrl = null)
    {
        var (fg, bg, attrs, link) = MergeStyles(parentFg, parentBg, parentAttributes, parentLinkUrl);
        var chunks = new List<TextChunk>();

        foreach (var child in _children)
        {
            if (child is string text)
            {
                chunks.Add(TextChunk.Styled(text, fg, bg, attrs, link));
            }
            else if (child is TextNodeRenderable node)
            {
                chunks.AddRange(node.GatherWithInheritedStyle(fg, bg, attrs, link));
            }
        }

        MarkClean();
        return chunks.ToArray();
    }

    /// <summary>Alias for GatherWithInheritedStyle.</summary>
    public TextChunk[] ToChunks(
        Rgba? parentFg = null, Rgba? parentBg = null,
        TextAttributes parentAttributes = TextAttributes.None,
        string? parentLinkUrl = null) =>
        GatherWithInheritedStyle(parentFg, parentBg, parentAttributes, parentLinkUrl);

    #endregion

    #region Factory Methods

    /// <summary>
    /// Performs from string.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <param name="options">The configuration options.</param>
    /// <returns>The result of from string.</returns>
    public static TextNodeRenderable FromString(string text, TextNodeOptions? options = null)
    {
        var node = new TextNodeRenderable(options);
        node.Add(text);
        return node;
    }

    /// <summary>
    /// Performs from nodes.
    /// </summary>
    /// <param name="nodes">The nodes.</param>
    /// <param name="options">The configuration options.</param>
    /// <returns>The result of from nodes.</returns>
    public static TextNodeRenderable FromNodes(TextNodeRenderable[] nodes, TextNodeOptions? options = null)
    {
        var node = new TextNodeRenderable(options);
        foreach (var child in nodes)
            node.Add(child);
        return node;
    }

    #endregion

    #region Dirty Tracking

    /// <summary>
    /// Performs request render.
    /// </summary>
    public virtual void RequestRender()
    {
        MarkDirty();
        Parent?.RequestRender();
    }

    /// <summary>
    /// Performs mark dirty.
    /// </summary>
    protected void MarkDirty() => _dirty = true;
    /// <summary>
    /// Performs mark clean.
    /// </summary>
    protected void MarkClean() => _dirty = false;

    #endregion

    private static TextNodeRenderable[] StyledTextToTextNodes(StyledText styledText)
    {
        return styledText.Chunks.Select(chunk =>
        {
            var node = new TextNodeRenderable(new TextNodeOptions
            {
                Fg = chunk.Fg,
                Bg = chunk.Bg,
                Attributes = chunk.Attributes,
                LinkUrl = chunk.Link,
            });
            node.Add(chunk.Text);
            return node;
        }).ToArray();
    }
}

/// <summary>
/// Root TextNode that's connected to a TextRenderable.
/// requestRender goes to the render context instead of parent.
/// </summary>
public class RootTextNodeRenderable : TextNodeRenderable
{
    private readonly IRenderContext _ctx;
    /// <summary>
    /// Gets the text parent.
    /// </summary>
    public TextRenderable TextParent { get; }

    /// <summary>
    /// Initializes a new instance of the RootTextNodeRenderable class.
    /// </summary>
    /// <param name="ctx">The render context.</param>
    /// <param name="options">The configuration options.</param>
    /// <param name="textParent">The text parent.</param>
    public RootTextNodeRenderable(IRenderContext ctx, TextNodeOptions? options, TextRenderable textParent)
        : base(options)
    {
        _ctx = ctx;
        TextParent = textParent;
    }

    /// <inheritdoc />
    public override void RequestRender()
    {
        MarkDirty();
        _ctx.RequestRender();
    }
}
