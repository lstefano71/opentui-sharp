namespace OpenTui.Core;

/// <summary>
/// Options for Markdown renderable.
/// Matches TypeScript MarkdownOptions.
/// </summary>
public class MarkdownOptions : RenderableOptions
{
    public string Content { get; init; } = "";
    public SyntaxStyle? SyntaxStyle { get; init; }
    public Rgba? Fg { get; init; }
    public Rgba? Bg { get; init; }
    public bool Conceal { get; init; } = true;
    public bool ConcealCode { get; init; }
    public bool Streaming { get; init; }
    public MarkdownTableOptions? TableOptions { get; init; }
    public Func<MarkdownToken, Renderable?>? RenderNode { get; init; }
}

/// <summary>
/// Options for tables within markdown.
/// </summary>
public class MarkdownTableOptions
{
    public string ColumnWidthMode { get; init; } = "full";
    public string ColumnFitter { get; init; } = "proportional";
    public byte WrapMode { get; init; } = 2;
    public int CellPadding { get; init; }
    public bool Border { get; init; } = true;
    public bool OuterBorder { get; init; } = true;
    public BorderStyle BorderStyle { get; init; } = BorderStyle.Single;
    public Rgba? BorderColor { get; init; }
    public bool Selectable { get; init; } = true;
}

/// <summary>
/// Represents a parsed markdown token.
/// </summary>
public sealed class MarkdownToken
{
    public string Type { get; init; } = "";
    public string Raw { get; init; } = "";
    public string Text { get; init; } = "";
    public int Depth { get; init; }
    public string? Lang { get; init; }
    public MarkdownTableData? Table { get; init; }
    public List<MarkdownToken>? Items { get; init; }
    public bool Ordered { get; init; }
    public int Start { get; init; } = 1;
}

/// <summary>
/// Parsed markdown table data.
/// </summary>
public sealed class MarkdownTableData
{
    public string[][] Header { get; init; } = [];
    public string[] Align { get; init; } = [];
    public string[][][] Rows { get; init; } = [];
}

/// <summary>
/// Markdown rendering widget.
/// Parses markdown content and builds child renderables:
/// - Paragraphs/headings → CodeRenderable (filetype: "markdown")
/// - Fenced code blocks → CodeRenderable (with block language)
/// - Tables → TextTableRenderable
/// Matches TypeScript MarkdownRenderable from Markdown.ts.
/// </summary>
public class MarkdownRenderable : Renderable
{
    private string _content;
    private SyntaxStyle? _syntaxStyle;
    private Rgba _fg;
    private Rgba _bg;
    private bool _conceal;
    private bool _concealCode;
    private bool _streaming;
    private MarkdownTableOptions? _tableOptions;
    private Func<MarkdownToken, Renderable?>? _renderNode;
    private List<BlockState> _blockStates = [];

    private sealed class BlockState
    {
        public MarkdownToken Token { get; init; } = null!;
        public Renderable Renderable { get; init; } = null!;
    }

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

    public SyntaxStyle? MdSyntaxStyle
    {
        get => _syntaxStyle;
        set
        {
            _syntaxStyle = value;
            RefreshStyles();
            RequestRender();
        }
    }

    public Rgba Fg
    {
        get => _fg;
        set { _fg = value; RefreshStyles(); RequestRender(); }
    }

    public Rgba Bg
    {
        get => _bg;
        set { _bg = value; RefreshStyles(); RequestRender(); }
    }

    public bool Conceal
    {
        get => _conceal;
        set { _conceal = value; RequestRender(); }
    }

    public bool ConcealCode
    {
        get => _concealCode;
        set { _concealCode = value; RequestRender(); }
    }

    public bool Streaming
    {
        get => _streaming;
        set { _streaming = value; }
    }

    public MarkdownTableOptions? TableOptions
    {
        get => _tableOptions;
        set { _tableOptions = value; ParseAndBuild(); RequestRender(); }
    }

    #endregion

    #region Parsing & Building

    /// <summary>
    /// Clears all cached state and re-parses.
    /// </summary>
    public void ClearCache()
    {
        ClearBlocks();
        ParseAndBuild();
        RequestRender();
    }

    /// <summary>
    /// Re-renders existing blocks without re-parsing.
    /// </summary>
    public void RefreshStyles()
    {
        foreach (var block in _blockStates)
        {
            if (block.Renderable is CodeRenderable code)
            {
                code.CodeSyntaxStyle = _syntaxStyle;
            }
        }
    }

    private void ParseAndBuild()
    {
        ClearBlocks();
        var tokens = ParseMarkdown(_content);
        BuildRenderables(tokens);
    }

    private void BuildRenderables(List<MarkdownToken> tokens)
    {
        var textGroup = new System.Text.StringBuilder();
        List<MarkdownToken> groupTokens = [];

        void FlushTextGroup()
        {
            if (textGroup.Length == 0) return;

            var code = new CodeRenderable(_ctx, new CodeOptions
            {
                Content = textGroup.ToString(),
                Filetype = "markdown",
                SyntaxStyle = _syntaxStyle,
                Conceal = _conceal,
            });
            Add(code);
            foreach (var t in groupTokens)
                _blockStates.Add(new BlockState { Token = t, Renderable = code });

            textGroup.Clear();
            groupTokens.Clear();
        }

        foreach (var token in tokens)
        {
            // Check custom renderer first
            if (_renderNode != null)
            {
                var custom = _renderNode(token);
                if (custom != null)
                {
                    FlushTextGroup();
                    Add(custom);
                    _blockStates.Add(new BlockState { Token = token, Renderable = custom });
                    continue;
                }
            }

            switch (token.Type)
            {
                case "code":
                    FlushTextGroup();
                    var codeBlock = new CodeRenderable(_ctx, new CodeOptions
                    {
                        Content = token.Text,
                        Filetype = token.Lang,
                        SyntaxStyle = _syntaxStyle,
                        Conceal = _concealCode,
                    });
                    Add(codeBlock);
                    _blockStates.Add(new BlockState { Token = token, Renderable = codeBlock });
                    break;

                case "table":
                    FlushTextGroup();
                    var table = BuildTable(token);
                    if (table != null)
                    {
                        Add(table);
                        _blockStates.Add(new BlockState { Token = token, Renderable = table });
                    }
                    break;

                case "hr":
                    FlushTextGroup();
                    // Horizontal rule — render as a styled text line
                    var hr = new TextRenderable(_ctx, new TextOptions
                    {
                        Content = "───────────────────────────────────────",
                    });
                    Add(hr);
                    _blockStates.Add(new BlockState { Token = token, Renderable = hr });
                    break;

                default:
                    // Group consecutive text-like tokens (paragraphs, headings, lists)
                    if (textGroup.Length > 0) textGroup.Append('\n');
                    textGroup.Append(token.Raw);
                    groupTokens.Add(token);
                    break;
            }
        }

        FlushTextGroup();
    }

    private TextTableRenderable? BuildTable(MarkdownToken token)
    {
        if (token.Table == null) return null;

        var tOpts = _tableOptions ?? new MarkdownTableOptions();
        var rows = new List<TextChunk[][]>();

        // Header row
        if (token.Table.Header.Length > 0)
        {
            var headerRow = new TextChunk[token.Table.Header.Length][];
            for (int i = 0; i < token.Table.Header.Length; i++)
            {
                var headerText = string.Join("", token.Table.Header[i]);
                headerRow[i] = [TextChunk.Styled(headerText, _fg, null, TextAttributes.Bold)];
            }
            rows.Add(headerRow);
        }

        // Data rows
        foreach (var row in token.Table.Rows)
        {
            var dataRow = new TextChunk[row.Length][];
            for (int i = 0; i < row.Length; i++)
            {
                var cellText = string.Join("", row[i]);
                dataRow[i] = [TextChunk.Plain(cellText)];
            }
            rows.Add(dataRow);
        }

        return new TextTableRenderable(_ctx, new TextTableOptions
        {
            Content = [.. rows],
            ColumnWidthMode = tOpts.ColumnWidthMode,
            ColumnFitter = tOpts.ColumnFitter,
            WrapMode = tOpts.WrapMode,
            CellPadding = tOpts.CellPadding,
            Border = tOpts.Border,
            OuterBorder = tOpts.OuterBorder,
            BorderStyle = tOpts.BorderStyle,
            BorderColor = tOpts.BorderColor,
            Selectable = tOpts.Selectable,
        });
    }

    private void ClearBlocks()
    {
        foreach (var block in _blockStates)
            Remove(block.Renderable.Id);
        _blockStates.Clear();
    }

    #endregion

    #region Markdown Parsing

    /// <summary>
    /// Simple markdown tokenizer.
    /// Handles headings, paragraphs, code blocks, lists, tables, and horizontal rules.
    /// </summary>
    public static List<MarkdownToken> ParseMarkdown(string content)
    {
        var tokens = new List<MarkdownToken>();
        if (string.IsNullOrEmpty(content)) return tokens;

        var lines = content.Split('\n');
        int i = 0;

        while (i < lines.Length)
        {
            var line = lines[i];

            // Blank line
            if (string.IsNullOrWhiteSpace(line))
            {
                i++;
                continue;
            }

            // Fenced code block
            if (line.StartsWith("```"))
            {
                string lang = line.Length > 3 ? line[3..].Trim() : "";
                var codeSb = new System.Text.StringBuilder();
                i++;
                while (i < lines.Length && !lines[i].StartsWith("```"))
                {
                    if (codeSb.Length > 0) codeSb.Append('\n');
                    codeSb.Append(lines[i]);
                    i++;
                }
                if (i < lines.Length) i++; // skip closing ```

                tokens.Add(new MarkdownToken
                {
                    Type = "code",
                    Raw = codeSb.ToString(),
                    Text = codeSb.ToString(),
                    Lang = string.IsNullOrEmpty(lang) ? null : lang,
                });
                continue;
            }

            // Heading
            if (line.StartsWith('#'))
            {
                int depth = 0;
                while (depth < line.Length && line[depth] == '#') depth++;
                string text = line[depth..].TrimStart();
                tokens.Add(new MarkdownToken
                {
                    Type = "heading",
                    Raw = line,
                    Text = text,
                    Depth = depth,
                });
                i++;
                continue;
            }

            // Horizontal rule
            if (line.Length >= 3 && (line.All(c => c == '-') || line.All(c => c == '*') || line.All(c => c == '_')))
            {
                tokens.Add(new MarkdownToken { Type = "hr", Raw = line });
                i++;
                continue;
            }

            // Table (detect by | character)
            if (line.Contains('|') && i + 1 < lines.Length && lines[i + 1].Contains('|') &&
                lines[i + 1].Replace(" ", "").Replace("|", "").Replace("-", "").Replace(":", "").Length == 0)
            {
                var tableToken = ParseTable(lines, ref i);
                if (tableToken != null)
                {
                    tokens.Add(tableToken);
                    continue;
                }
            }

            // List item
            if (line.TrimStart().StartsWith("- ") || line.TrimStart().StartsWith("* ") ||
                (line.TrimStart().Length > 2 && char.IsDigit(line.TrimStart()[0]) && line.TrimStart().Contains(". ")))
            {
                tokens.Add(new MarkdownToken
                {
                    Type = "list_item",
                    Raw = line,
                    Text = line,
                });
                i++;
                continue;
            }

            // Paragraph — collect consecutive non-blank lines
            var paraSb = new System.Text.StringBuilder();
            while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i])
                && !lines[i].StartsWith('#') && !lines[i].StartsWith("```")
                && !(lines[i].Length >= 3 && lines[i].All(c => c == '-')))
            {
                if (paraSb.Length > 0) paraSb.Append('\n');
                paraSb.Append(lines[i]);
                i++;
            }

            tokens.Add(new MarkdownToken
            {
                Type = "paragraph",
                Raw = paraSb.ToString(),
                Text = paraSb.ToString(),
            });
        }

        return tokens;
    }

    private static MarkdownToken? ParseTable(string[] lines, ref int i)
    {
        // Header row
        var headerLine = lines[i];
        var headerCells = SplitTableRow(headerLine);
        if (headerCells.Length == 0) return null;

        i++; // skip to separator
        var separatorLine = lines[i];
        var aligns = SplitTableRow(separatorLine)
            .Select(s =>
            {
                s = s.Trim();
                if (s.StartsWith(':') && s.EndsWith(':')) return "center";
                if (s.EndsWith(':')) return "right";
                return "left";
            }).ToArray();
        i++; // skip separator

        // Data rows
        var rows = new List<string[][]>();
        while (i < lines.Length && lines[i].Contains('|'))
        {
            var cells = SplitTableRow(lines[i]);
            rows.Add(cells.Select(c => new[] { c.Trim() }).ToArray());
            i++;
        }

        return new MarkdownToken
        {
            Type = "table",
            Raw = string.Join('\n', lines.Skip(i - rows.Count - 2).Take(rows.Count + 2)),
            Table = new MarkdownTableData
            {
                Header = headerCells.Select(h => new[] { h.Trim() }).ToArray(),
                Align = aligns,
                Rows = [.. rows],
            }
        };
    }

    private static string[] SplitTableRow(string line)
    {
        line = line.Trim();
        if (line.StartsWith('|')) line = line[1..];
        if (line.EndsWith('|')) line = line[..^1];
        return line.Split('|');
    }

    #endregion

    #region Dispose

    protected override void DestroySelf()
    {
        ClearBlocks();
        base.DestroySelf();
    }

    #endregion
}
