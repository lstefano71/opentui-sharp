using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace OpenTui.Core;

/// <summary>
/// Options for Markdown renderable.
/// Matches TypeScript MarkdownOptions.
/// </summary>
public class MarkdownOptions : RenderableOptions
{
    /// <summary>
    /// Gets or sets the content.
    /// </summary>
    public string Content { get; init; } = "";
    /// <summary>
    /// Gets or sets the syntax style.
    /// </summary>
    public SyntaxStyle? SyntaxStyle { get; init; }
    /// <summary>
    /// Gets or sets the fg.
    /// </summary>
    public Rgba? Fg { get; init; }
    /// <summary>
    /// Gets or sets the bg.
    /// </summary>
    public Rgba? Bg { get; init; }
    /// <summary>
    /// Gets or sets the conceal.
    /// </summary>
    public bool Conceal { get; init; } = true;
    /// <summary>
    /// Gets or sets the conceal code.
    /// </summary>
    public bool ConcealCode { get; init; }
    /// <summary>
    /// Gets or sets the streaming.
    /// </summary>
    public bool Streaming { get; init; }
    /// <summary>
    /// Gets or sets the table options.
    /// </summary>
    public MarkdownTableOptions? TableOptions { get; init; }
    /// <summary>
    /// Gets or sets the render node.
    /// </summary>
    public Func<MarkdownToken, Renderable?>? RenderNode { get; init; }
}

/// <summary>
/// Options for tables within markdown.
/// </summary>
public class MarkdownTableOptions
{
    /// <summary>
    /// Gets or sets the column width mode.
    /// </summary>
    public string ColumnWidthMode { get; init; } = "full";
    /// <summary>
    /// Gets or sets the column fitter.
    /// </summary>
    public string ColumnFitter { get; init; } = "proportional";
    /// <summary>
    /// Gets or sets the wrap mode.
    /// </summary>
    public byte WrapMode { get; init; } = 2;
    /// <summary>
    /// Gets or sets the cell padding.
    /// </summary>
    public int CellPadding { get; init; }
    /// <summary>
    /// Gets or sets the border.
    /// </summary>
    public bool Border { get; init; } = true;
    /// <summary>
    /// Gets or sets the outer border.
    /// </summary>
    public bool OuterBorder { get; init; } = true;
    /// <summary>
    /// Gets or sets the border style.
    /// </summary>
    public BorderStyle BorderStyle { get; init; } = BorderStyle.Single;
    /// <summary>
    /// Gets or sets the border color.
    /// </summary>
    public Rgba? BorderColor { get; init; }
    /// <summary>
    /// Gets or sets the selectable.
    /// </summary>
    public bool Selectable { get; init; } = true;
}

/// <summary>
/// Represents a parsed markdown token.
/// </summary>
public sealed class MarkdownToken
{
    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public string Type { get; init; } = "";
    /// <summary>
    /// Gets or sets the raw.
    /// </summary>
    public string Raw { get; init; } = "";
    /// <summary>
    /// Gets or sets the text.
    /// </summary>
    public string Text { get; init; } = "";
    /// <summary>
    /// Gets or sets the depth.
    /// </summary>
    public int Depth { get; init; }
    /// <summary>
    /// Gets or sets the lang.
    /// </summary>
    public string? Lang { get; init; }
    /// <summary>
    /// Gets or sets the table.
    /// </summary>
    public MarkdownTableData? Table { get; init; }
    /// <summary>
    /// Gets or sets the items.
    /// </summary>
    public List<MarkdownToken>? Items { get; init; }
    /// <summary>
    /// Gets or sets the ordered.
    /// </summary>
    public bool Ordered { get; init; }
    /// <summary>
    /// Gets or sets the start.
    /// </summary>
    public int Start { get; init; } = 1;
}

/// <summary>
/// Parsed markdown table data.
/// </summary>
public sealed class MarkdownTableData
{
    /// <summary>
    /// Gets or sets the header.
    /// </summary>
    public string[][] Header { get; init; } = [];
    /// <summary>
    /// Gets or sets the align.
    /// </summary>
    public string[] Align { get; init; } = [];
    /// <summary>
    /// Gets or sets the rows.
    /// </summary>
    public string[][][] Rows { get; init; } = [];
}

/// <summary>
/// Markdown rendering widget backed by Markdig.
/// </summary>
public class MarkdownRenderable : Renderable
{
    private static readonly MarkdownPipeline s_pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    private string _content;
    private SyntaxStyle? _syntaxStyle;
    private Rgba _fg;
    private Rgba _bg;
    private bool _conceal;
    private bool _concealCode;
    private bool _streaming;
    private MarkdownTableOptions? _tableOptions;
    private Func<MarkdownToken, Renderable?>? _renderNode;
    private readonly List<BlockState> _blockStates = [];

    private sealed class BlockState
    {
        public required Renderable Renderable { get; init; }
    }

    /// <summary>
    /// Initializes a new instance of the MarkdownRenderable class.
    /// </summary>
    /// <param name="ctx">The render context.</param>
    /// <param name="options">The configuration options.</param>
    public MarkdownRenderable(IRenderContext ctx, MarkdownOptions? options = null)
        : base(ctx, options ?? new MarkdownOptions() { FlexDirection = FlexDirectionValue.Column })
    {
        options ??= new MarkdownOptions();
        _content = options.Content;
        _syntaxStyle = options.SyntaxStyle;
        _fg = options.Fg ?? Rgba.FromInts(255, 255, 255);
        _bg = options.Bg ?? Rgba.Transparent;
        _conceal = options.Conceal;
        _concealCode = options.ConcealCode;
        _streaming = options.Streaming;
        _tableOptions = options.TableOptions;
        _renderNode = options.RenderNode;

        FlexDirection = FlexDirectionValue.Column;

        if (!string.IsNullOrEmpty(_content))
            ParseAndBuild();
    }

    #region Properties

    /// <summary>
    /// Gets or sets the content.
    /// </summary>
    public string Content
    {
        get => _content;
        set
        {
            if (_content == value) return;
            _content = value;
            ParseAndBuild();
            RequestRender();
        }
    }

    /// <summary>
    /// Gets or sets the syntax style.
    /// </summary>
    public SyntaxStyle? SyntaxStyle
    {
        get => _syntaxStyle;
        set
        {
            if (ReferenceEquals(_syntaxStyle, value)) return;
            _syntaxStyle = value;
            ParseAndBuild();
            RequestRender();
        }
    }

    /// <summary>
    /// Gets or sets the md syntax style.
    /// </summary>
    public SyntaxStyle? MdSyntaxStyle
    {
        get => SyntaxStyle;
        set => SyntaxStyle = value;
    }

    /// <summary>
    /// Gets or sets the fg.
    /// </summary>
    public Rgba Fg
    {
        get => _fg;
        set
        {
            if (_fg == value) return;
            _fg = value;
            ParseAndBuild();
            RequestRender();
        }
    }

    /// <summary>
    /// Gets or sets the bg.
    /// </summary>
    public Rgba Bg
    {
        get => _bg;
        set
        {
            if (_bg == value) return;
            _bg = value;
            ParseAndBuild();
            RequestRender();
        }
    }

    /// <summary>
    /// Gets or sets the conceal.
    /// </summary>
    public bool Conceal
    {
        get => _conceal;
        set
        {
            if (_conceal == value) return;
            _conceal = value;
            ParseAndBuild();
            RequestRender();
        }
    }

    /// <summary>
    /// Gets or sets the conceal code.
    /// </summary>
    public bool ConcealCode
    {
        get => _concealCode;
        set
        {
            if (_concealCode == value) return;
            _concealCode = value;
            ParseAndBuild();
            RequestRender();
        }
    }

    /// <summary>
    /// Gets or sets the streaming.
    /// </summary>
    public bool Streaming
    {
        get => _streaming;
        set => _streaming = value;
    }

    /// <summary>
    /// Gets or sets the table options.
    /// </summary>
    public MarkdownTableOptions? TableOptions
    {
        get => _tableOptions;
        set
        {
            _tableOptions = value;
            ParseAndBuild();
            RequestRender();
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Clears the cache.
    /// </summary>
    public void ClearCache()
    {
        ParseAndBuild();
        RequestRender();
    }

    /// <summary>
    /// Performs refresh styles.
    /// </summary>
    public void RefreshStyles()
    {
        ParseAndBuild();
        RequestRender();
    }

    #endregion

    #region Parsing & Building

    private void ParseAndBuild()
    {
        ClearBlocks();

        if (string.IsNullOrEmpty(_content))
            return;

        var document = Markdown.Parse(_content, s_pipeline);
        BuildRenderables(document);
    }

    private void BuildRenderables(MarkdownDocument document)
    {
        for (int i = 0; i < document.Count; i++)
        {
            var renderable = BuildRenderable(document[i], i, i < document.Count - 1);
            if (renderable is null)
                continue;

            Add(renderable);
            _blockStates.Add(new BlockState { Renderable = renderable });
        }
    }

    private Renderable? BuildRenderable(Block block, int index, bool hasNext)
    {
        var token = CreateToken(block);
        var custom = _renderNode?.Invoke(token);
        if (custom is not null)
        {
            ApplyBlockSpacing(custom, hasNext);
            return custom;
        }

        Renderable? renderable = block switch
        {
            HeadingBlock heading => BuildHeadingRenderable(heading, index),
            ParagraphBlock paragraph => BuildParagraphRenderable(paragraph, index),
            QuoteBlock quote => BuildQuoteRenderable(quote, index),
            ListBlock list => BuildListRenderable(list, index),
            Table table => BuildTableRenderable(table, index),
            FencedCodeBlock fenced => BuildCodeBlockRenderable(fenced, index),
            CodeBlock code => BuildCodeBlockRenderable(code, index),
            ThematicBreakBlock thematicBreak => BuildThematicBreakRenderable(thematicBreak, index),
            _ => BuildFallbackRenderable(block, index),
        };

        if (renderable is not null)
            ApplyBlockSpacing(renderable, hasNext);

        return renderable;
    }

    private void ApplyBlockSpacing(Renderable renderable, bool hasNext)
    {
        renderable.MarginBottom = DimensionValue.Point(hasNext ? 1 : 0);
    }

    private TextRenderable BuildHeadingRenderable(HeadingBlock heading, int index)
    {
        var chunks = new List<TextChunk>();
        string styleGroup = $"markup.heading.{heading.Level}";

        if (!_conceal)
            chunks.Add(CreateChunk(new string('#', Math.Max(1, heading.Level)) + " ", styleGroup));

        RenderInlineContainer(heading.Inline, chunks, styleGroup);
        return CreateTextRenderable(chunks, $"{Id}-heading-{index}");
    }

    private TextRenderable BuildParagraphRenderable(ParagraphBlock paragraph, int index)
    {
        var chunks = new List<TextChunk>();
        RenderInlineContainer(paragraph.Inline, chunks, null);
        return CreateTextRenderable(chunks, $"{Id}-paragraph-{index}");
    }

    private TextRenderable BuildQuoteRenderable(QuoteBlock quote, int index)
    {
        var chunks = new List<TextChunk>();
        bool first = true;

        foreach (var child in quote)
        {
            if (!first)
                chunks.Add(CreateDefaultChunk("\n"));

            chunks.Add(CreateChunk("> ", "markup.quote"));
            AppendBlockTextChunks(child, chunks);
            first = false;
        }

        return CreateTextRenderable(chunks, $"{Id}-quote-{index}");
    }

    private TextRenderable BuildListRenderable(ListBlock list, int index)
    {
        var chunks = new List<TextChunk>();
        int position = 0;

        foreach (var child in list)
        {
            if (child is not ListItemBlock item)
                continue;

            if (position > 0)
                chunks.Add(CreateDefaultChunk("\n"));

            string marker = list.IsOrdered
                ? $"{(item.Order > 0 ? item.Order : position + 1)}. "
                : $"{(list.BulletType == '\0' ? '-' : list.BulletType)} ";

            chunks.Add(CreateChunk(marker, "markup.list"));
            AppendListItemChunks(item, chunks);
            position++;
        }

        return CreateTextRenderable(chunks, $"{Id}-list-{index}");
    }

    private Renderable? BuildTableRenderable(Table table, int index)
    {
        if (table.Count == 0)
            return null;

        var rows = new List<TextChunk[][]>();

        foreach (var rowBlock in table)
        {
            if (rowBlock is not TableRow row)
                continue;

            var renderedRow = new TextChunk[row.Count][];
            for (int i = 0; i < row.Count; i++)
            {
                renderedRow[i] = BuildTableCellChunks((TableCell)row[i], row.IsHeader);
            }

            rows.Add(renderedRow);
        }

        var tableOptions = ResolveTableOptions();
        return new TextTableRenderable(_ctx, new TextTableOptions
        {
            Id = $"{Id}-table-{index}",
            Width = DimensionValue.Percent(100),
            Content = [.. rows],
            ColumnWidthMode = tableOptions.ColumnWidthMode,
            ColumnFitter = tableOptions.ColumnFitter,
            WrapMode = tableOptions.WrapMode,
            CellPadding = tableOptions.CellPadding,
            Border = tableOptions.Border,
            OuterBorder = tableOptions.OuterBorder,
            BorderStyle = tableOptions.BorderStyle,
            BorderColor = tableOptions.BorderColor ?? ResolveStyle("conceal")?.Fg ?? _fg,
            Selectable = tableOptions.Selectable,
        });
    }

    private Renderable BuildCodeBlockRenderable(CodeBlock codeBlock, int index)
    {
        string content = NormalizeLineEndings(codeBlock.Lines.ToString());

        if (codeBlock is FencedCodeBlock fenced && IsMarkdownCodeFence(fenced.Info) && _concealCode)
        {
            var markdownCodeChunks = BuildMarkdownCodeChunks(content);
            return CreateTextRenderable(markdownCodeChunks, $"{Id}-code-md-{index}",
                GetCodeBlockForeground(), GetCodeBlockBackground());
        }

        return new CodeRenderable(_ctx, new CodeOptions
        {
            Id = $"{Id}-code-{index}",
            Content = content,
            Filetype = codeBlock is FencedCodeBlock fencedCode ? ExtractFenceInfo(fencedCode.Info) : null,
            SyntaxStyle = _syntaxStyle,
            Conceal = _concealCode,
            Fg = GetCodeBlockForeground(),
            Bg = GetCodeBlockBackground(),
            Width = DimensionValue.Percent(100),
        });
    }

    private TextRenderable BuildThematicBreakRenderable(ThematicBreakBlock thematicBreak, int index)
    {
        string content = _conceal
            ? "───────────────────────────────────────"
            : new string(thematicBreak.ThematicChar == '\0' ? '-' : thematicBreak.ThematicChar,
                Math.Max(3, thematicBreak.ThematicCharCount));

        return CreateTextRenderable(
            [CreateChunk(content, "conceal")],
            $"{Id}-hr-{index}");
    }

    private TextRenderable BuildFallbackRenderable(Block block, int index)
    {
        var chunks = new List<TextChunk>();
        AppendBlockTextChunks(block, chunks);
        return CreateTextRenderable(chunks, $"{Id}-fallback-{index}");
    }

    private TextRenderable CreateTextRenderable(
        IEnumerable<TextChunk> chunks,
        string id,
        Rgba? fg = null,
        Rgba? bg = null)
    {
        return new TextRenderable(_ctx, new TextOptions
        {
            Id = id,
            Width = DimensionValue.Percent(100),
            WrapMode = WrapMode.Word,
            Fg = fg ?? _fg,
            Bg = bg ?? _bg,
            StyledContent = new StyledText(chunks.ToArray()),
        });
    }

    private void AppendListItemChunks(ListItemBlock item, List<TextChunk> chunks)
    {
        bool first = true;
        foreach (var child in item)
        {
            if (!first)
                chunks.Add(CreateDefaultChunk("\n"));

            AppendBlockTextChunks(child, chunks);
            first = false;
        }
    }

    private TextChunk[] BuildTableCellChunks(TableCell cell, bool isHeader)
    {
        var chunks = new List<TextChunk>();
        bool first = true;

        foreach (var child in cell)
        {
            if (!first)
                chunks.Add(CreateDefaultChunk("\n"));

            AppendBlockTextChunks(child, chunks);
            first = false;
        }

        if (!isHeader)
            return [.. chunks];

        return chunks.Select(chunk => TextChunk.Styled(
            chunk.Text,
            chunk.Fg ?? ResolveStyle("label")?.Fg ?? ResolveStyle("markup.strong")?.Fg ?? _fg,
            chunk.Bg,
            chunk.Attributes | TextAttributes.Bold,
            chunk.Link)).ToArray();
    }

    private void AppendBlockTextChunks(Block block, List<TextChunk> chunks)
    {
        switch (block)
        {
            case ParagraphBlock paragraph:
                RenderInlineContainer(paragraph.Inline, chunks, null);
                break;

            case HeadingBlock heading:
                string styleGroup = $"markup.heading.{heading.Level}";
                if (!_conceal)
                    chunks.Add(CreateChunk(new string('#', Math.Max(1, heading.Level)) + " ", styleGroup));
                RenderInlineContainer(heading.Inline, chunks, styleGroup);
                break;

            case QuoteBlock quote:
                bool firstQuoteChild = true;
                foreach (var child in quote)
                {
                    if (!firstQuoteChild)
                        chunks.Add(CreateDefaultChunk("\n"));
                    chunks.Add(CreateChunk("> ", "markup.quote"));
                    AppendBlockTextChunks(child, chunks);
                    firstQuoteChild = false;
                }
                break;

            case ListBlock list:
                int position = 0;
                foreach (var child in list)
                {
                    if (child is not ListItemBlock item)
                        continue;

                    if (position > 0)
                        chunks.Add(CreateDefaultChunk("\n"));

                    string marker = list.IsOrdered
                        ? $"{(item.Order > 0 ? item.Order : position + 1)}. "
                        : $"{(list.BulletType == '\0' ? '-' : list.BulletType)} ";

                    chunks.Add(CreateChunk(marker, "markup.list"));
                    AppendListItemChunks(item, chunks);
                    position++;
                }
                break;

            case CodeBlock codeBlock:
                chunks.Add(CreateChunk(NormalizeLineEndings(codeBlock.Lines.ToString()), "markup.raw.block"));
                break;

            case ThematicBreakBlock thematicBreak:
                chunks.Add(CreateChunk(
                    _conceal
                        ? "───────────────────────────────────────"
                        : new string(thematicBreak.ThematicChar == '\0' ? '-' : thematicBreak.ThematicChar,
                            Math.Max(3, thematicBreak.ThematicCharCount)),
                    "conceal"));
                break;

            default:
                if (block is LeafBlock leafBlock)
                {
                    string text = NormalizeLineEndings(leafBlock.Lines.ToString());
                    if (!string.IsNullOrEmpty(text))
                        chunks.Add(CreateDefaultChunk(text));
                }
                break;
        }
    }

    private TextChunk[] BuildMarkdownCodeChunks(string content)
    {
        var document = Markdown.Parse(content, s_pipeline);
        var chunks = new List<TextChunk>();
        bool first = true;

        foreach (var block in document)
        {
            if (!first)
                chunks.Add(CreateDefaultChunk("\n"));

            AppendBlockTextChunks(block, chunks);
            first = false;
        }

        return [.. chunks];
    }

    private void RenderInlineContainer(ContainerInline? inline, List<TextChunk> chunks, string? literalStyleGroup)
    {
        if (inline is null)
            return;

        for (var child = inline.FirstChild; child is not null; child = child.NextSibling)
            RenderInlineToken(child, chunks, literalStyleGroup, null);
    }

    private void RenderInlineToken(Inline inline, List<TextChunk> chunks, string? literalStyleGroup, string? link)
    {
        switch (inline)
        {
            case LiteralInline literal:
                var text = literal.Content.ToString();
                if (!string.IsNullOrEmpty(text))
                    chunks.Add(CreateChunk(text, literalStyleGroup, link));
                break;

            case CodeInline code:
                if (!_conceal)
                    chunks.Add(CreateChunk(new string(code.Delimiter == '\0' ? '`' : code.Delimiter, Math.Max(1, code.DelimiterCount)), "markup.raw.inline", link));
                chunks.Add(CreateChunk(code.Content, "markup.raw.inline", link));
                if (!_conceal)
                    chunks.Add(CreateChunk(new string(code.Delimiter == '\0' ? '`' : code.Delimiter, Math.Max(1, code.DelimiterCount)), "markup.raw.inline", link));
                break;

            case EmphasisInline emphasis:
                RenderEmphasisInline(emphasis, chunks, link);
                break;

            case LinkInline linkInline:
                RenderLinkInline(linkInline, chunks);
                break;

            case AutolinkInline autoLink:
                chunks.Add(CreateChunk(autoLink.Url ?? string.Empty, "markup.link.url", autoLink.Url));
                break;

            case LineBreakInline:
                chunks.Add(CreateChunk("\n", literalStyleGroup, link));
                break;

            case HtmlInline html:
                if (!string.IsNullOrEmpty(html.Tag))
                    chunks.Add(CreateChunk(html.Tag, literalStyleGroup, link));
                break;

            case ContainerInline container:
                for (var child = container.FirstChild; child is not null; child = child.NextSibling)
                    RenderInlineToken(child, chunks, literalStyleGroup, link);
                break;

            default:
                var fallback = inline.ToString();
                if (!string.IsNullOrEmpty(fallback))
                    chunks.Add(CreateChunk(fallback, literalStyleGroup, link));
                break;
        }
    }

    private void RenderEmphasisInline(EmphasisInline emphasis, List<TextChunk> chunks, string? link)
    {
        string styleGroup = emphasis.DelimiterChar switch
        {
            '~' => "markup.strikethrough",
            _ when emphasis.DelimiterCount >= 2 => "markup.strong",
            _ => "markup.italic",
        };

        string delimiter = new string(emphasis.DelimiterChar == '\0' ? '*' : emphasis.DelimiterChar, Math.Max(1, emphasis.DelimiterCount));
        if (!_conceal)
            chunks.Add(CreateChunk(delimiter, styleGroup, link));

        for (var child = emphasis.FirstChild; child is not null; child = child.NextSibling)
            RenderInlineToken(child, chunks, styleGroup, link);

        if (!_conceal)
            chunks.Add(CreateChunk(delimiter, styleGroup, link));
    }

    private void RenderLinkInline(LinkInline linkInline, List<TextChunk> chunks)
    {
        string url = linkInline.GetDynamicUrl?.Invoke() ?? linkInline.Url ?? string.Empty;

        if (linkInline.IsImage)
        {
            if (_conceal)
            {
                string label = !string.IsNullOrWhiteSpace(linkInline.Label) ? linkInline.Label! : "image";
                chunks.Add(CreateChunk(label, "markup.link.label", url));
            }
            else
            {
                chunks.Add(CreateChunk("![", "markup.link", url));
                AppendLinkLabel(linkInline, chunks, url);
                chunks.Add(CreateChunk("](", "markup.link", url));
                chunks.Add(CreateChunk(url, "markup.link.url", url));
                chunks.Add(CreateChunk(")", "markup.link", url));
            }

            return;
        }

        if (_conceal)
        {
            AppendLinkLabel(linkInline, chunks, url);
            if (!string.IsNullOrEmpty(url))
            {
                chunks.Add(CreateChunk(" (", "markup.link", url));
                chunks.Add(CreateChunk(url, "markup.link.url", url));
                chunks.Add(CreateChunk(")", "markup.link", url));
            }
        }
        else
        {
            chunks.Add(CreateChunk("[", "markup.link", url));
            AppendLinkLabel(linkInline, chunks, url);
            chunks.Add(CreateChunk("](", "markup.link", url));
            chunks.Add(CreateChunk(url, "markup.link.url", url));
            chunks.Add(CreateChunk(")", "markup.link", url));
        }
    }

    private void AppendLinkLabel(LinkInline linkInline, List<TextChunk> chunks, string? url)
    {
        if (linkInline.FirstChild is null)
        {
            if (!string.IsNullOrEmpty(linkInline.Label))
                chunks.Add(CreateChunk(linkInline.Label!, "markup.link.label", url));
            return;
        }

        for (var child = linkInline.FirstChild; child is not null; child = child.NextSibling)
            RenderInlineToken(child, chunks, "markup.link.label", url);
    }

    private TextChunk CreateChunk(string text, string? styleGroup, string? link = null)
    {
        if (string.IsNullOrEmpty(text))
            return TextChunk.Plain(string.Empty);

        var style = ResolveStyle(styleGroup);
        return TextChunk.Styled(
            text,
            style?.Fg ?? _fg,
            style?.Bg ?? _bg,
            style?.Attributes ?? TextAttributes.None,
            link);
    }

    private TextChunk CreateDefaultChunk(string text) => CreateChunk(text, "default");

    private SyntaxStyleEntry? ResolveStyle(string? group)
    {
        if (_syntaxStyle is null)
            return null;

        if (group is null)
            return _syntaxStyle.GetStyle("default");

        var current = group;
        while (!string.IsNullOrEmpty(current))
        {
            var style = _syntaxStyle.GetStyle(current);
            if (style is not null)
                return style;

            int lastDot = current.LastIndexOf('.');
            if (lastDot < 0)
                break;
            current = current[..lastDot];
        }

        return _syntaxStyle.GetStyle("default");
    }

    private Rgba GetCodeBlockForeground() =>
        ResolveStyle("markup.raw.block")?.Fg
        ?? ResolveStyle("markup.raw")?.Fg
        ?? _fg;

    private Rgba GetCodeBlockBackground() =>
        ResolveStyle("markup.raw.block")?.Bg
        ?? ResolveStyle("markup.raw")?.Bg
        ?? _bg;

    private MarkdownTableOptions ResolveTableOptions() =>
        _tableOptions ?? new MarkdownTableOptions();

    private static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n").Replace('\r', '\n');

    private static string? ExtractFenceInfo(string? info)
    {
        if (string.IsNullOrWhiteSpace(info))
            return null;

        var token = info.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries)[0];
        return token.Length == 0 ? null : token.ToLowerInvariant();
    }

    private static bool IsMarkdownCodeFence(string? info)
    {
        var lang = ExtractFenceInfo(info);
        return lang is "markdown" or "md";
    }

    private void ClearBlocks()
    {
        foreach (var block in _blockStates)
            Remove(block.Renderable.Id);
        _blockStates.Clear();
    }

    private MarkdownToken CreateToken(Block block) =>
        block switch
        {
            HeadingBlock heading => new MarkdownToken
            {
                Type = "heading",
                Raw = heading.Lines.ToString(),
                Text = heading.Inline?.ToString() ?? heading.Lines.ToString(),
                Depth = heading.Level,
            },
            ParagraphBlock paragraph => new MarkdownToken
            {
                Type = "paragraph",
                Raw = paragraph.Lines.ToString(),
                Text = paragraph.Inline?.ToString() ?? paragraph.Lines.ToString(),
            },
            FencedCodeBlock fenced => new MarkdownToken
            {
                Type = "code",
                Raw = fenced.Lines.ToString(),
                Text = fenced.Lines.ToString(),
                Lang = ExtractFenceInfo(fenced.Info),
            },
            Table table => new MarkdownToken
            {
                Type = "table",
                Raw = table.ToString() ?? string.Empty,
            },
            ListBlock list => new MarkdownToken
            {
                Type = "list",
                Raw = list.ToString() ?? string.Empty,
                Ordered = list.IsOrdered,
            },
            QuoteBlock quote => new MarkdownToken
            {
                Type = "blockquote",
                Raw = quote.ToString() ?? string.Empty,
            },
            ThematicBreakBlock thematicBreak => new MarkdownToken
            {
                Type = "hr",
                Raw = new string(thematicBreak.ThematicChar == '\0' ? '-' : thematicBreak.ThematicChar,
                    Math.Max(3, thematicBreak.ThematicCharCount)),
            },
            _ => new MarkdownToken
            {
                Type = "block",
                Raw = block.ToString() ?? string.Empty,
            },
        };

    #endregion

    #region Dispose

    /// <inheritdoc />
    protected override void DestroySelf()
    {
        ClearBlocks();
        base.DestroySelf();
    }

    #endregion
}
