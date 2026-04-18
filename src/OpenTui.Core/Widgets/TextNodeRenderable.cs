namespace OpenTui.Core;

/// <summary>
/// Options for TextNodeRenderable.
/// </summary>
public class TextNodeOptions
{
    public string? Id { get; init; }
    public Rgba? Fg { get; init; }
    public Rgba? Bg { get; init; }
    public TextAttributes Attributes { get; init; }
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

    public TextNodeRenderable? Parent { get; internal set; }
    public int Num { get; }
    public string Id { get; set; }

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

    public Rgba? Fg
    {
        get => _fg;
        set { _fg = value; RequestRender(); }
    }

    public Rgba? Bg
    {
        get => _bg;
        set { _bg = value; RequestRender(); }
    }

    public TextAttributes Attributes
    {
        get => _attributes;
        set { _attributes = value; RequestRender(); }
    }

    public string? LinkUrl
    {
        get => _linkUrl;
        set { _linkUrl = value; RequestRender(); }
    }

    public bool IsDirty => _dirty;

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

    public void Replace(string text, int index)
    {
        _children[index] = text;
        RequestRender();
    }

    public void Replace(TextNodeRenderable node, int index)
    {
        _children[index] = node;
        node.Parent = this;
        RequestRender();
    }

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

    public void Clear()
    {
        _children.Clear();
        RequestRender();
    }

    public TextNodeRenderable[] GetRenderableChildren() =>
        _children.OfType<TextNodeRenderable>().ToArray();

    public int GetChildrenCount() => _children.Count;

    public TextNodeRenderable? GetRenderable(string id) =>
        _children.OfType<TextNodeRenderable>().FirstOrDefault(c => c.Id == id);

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

    public static TextNodeRenderable FromString(string text, TextNodeOptions? options = null)
    {
        var node = new TextNodeRenderable(options);
        node.Add(text);
        return node;
    }

    public static TextNodeRenderable FromNodes(TextNodeRenderable[] nodes, TextNodeOptions? options = null)
    {
        var node = new TextNodeRenderable(options);
        foreach (var child in nodes)
            node.Add(child);
        return node;
    }

    #endregion

    #region Dirty Tracking

    public virtual void RequestRender()
    {
        MarkDirty();
        Parent?.RequestRender();
    }

    protected void MarkDirty() => _dirty = true;
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
    public TextRenderable TextParent { get; }

    public RootTextNodeRenderable(IRenderContext ctx, TextNodeOptions? options, TextRenderable textParent)
        : base(options)
    {
        _ctx = ctx;
        TextParent = textParent;
    }

    public override void RequestRender()
    {
        MarkDirty();
        _ctx.RequestRender();
    }
}
