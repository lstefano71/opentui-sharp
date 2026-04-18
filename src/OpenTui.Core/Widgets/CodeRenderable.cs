namespace OpenTui.Core;

/// <summary>
/// Options for Code renderable.
/// Matches TypeScript CodeOptions.
/// </summary>
public class CodeOptions : TextBufferOptions
{
    public string? Content { get; init; }
    public string? Filetype { get; init; }
    public SyntaxStyle? SyntaxStyle { get; init; }
    public bool Conceal { get; init; } = true;
    public bool DrawUnstyledText { get; init; } = true;
    public bool Streaming { get; init; }
    public Action<List<TextChunk>, object?>? OnChunks { get; init; }
    public Action<object?>? OnHighlight { get; init; }
}

/// <summary>
/// Code viewer with syntax highlighting support.
/// Extends TextBufferRenderable and adds highlighting via SyntaxStyle.
/// Matches TypeScript CodeRenderable from Code.ts.
///
/// Note: Tree-sitter WASM highlighting is not yet ported.
/// For now, this renders plain text with optional SyntaxStyle,
/// supporting filetype-based highlighting when implemented.
/// </summary>
public class CodeRenderable : TextBufferRenderable, ILineInfoProvider
{
    private string _content;
    private string? _filetype;
    private SyntaxStyle? _syntaxStyle;
    private bool _conceal;
    private bool _drawUnstyledText;
    private bool _streaming;
    private bool _highlightsDirty;
    private int _highlightSnapshotId;

    public CodeRenderable(IRenderContext ctx, CodeOptions? options = null)
        : base(ctx, options ?? new CodeOptions())
    {
        options ??= new CodeOptions();
        _content = options.Content ?? "";
        _filetype = options.Filetype;
        _syntaxStyle = options.SyntaxStyle;
        _conceal = options.Conceal;
        _drawUnstyledText = options.DrawUnstyledText;
        _streaming = options.Streaming;
        _highlightsDirty = true;

        if (!string.IsNullOrEmpty(_content))
            SetTextContent(_content);
    }

    #region Properties

    public string Content
    {
        get => _content;
        set
        {
            if (_content == value) return;
            _content = value;
            _highlightsDirty = true;
            SetTextContent(value);
            RequestRender();
        }
    }

    public string? Filetype
    {
        get => _filetype;
        set
        {
            if (_filetype == value) return;
            _filetype = value;
            _highlightsDirty = true;
            RequestRender();
        }
    }

    public SyntaxStyle? CodeSyntaxStyle
    {
        get => _syntaxStyle;
        set
        {
            _syntaxStyle = value;
            _highlightsDirty = true;
            RequestRender();
        }
    }

    public bool Conceal
    {
        get => _conceal;
        set { _conceal = value; _highlightsDirty = true; RequestRender(); }
    }

    public bool DrawUnstyledText
    {
        get => _drawUnstyledText;
        set { _drawUnstyledText = value; RequestRender(); }
    }

    public bool Streaming
    {
        get => _streaming;
        set { _streaming = value; }
    }

    #endregion

    #region ILineInfoProvider

    public int LineCount
    {
        get
        {
            var text = _content ?? "";
            if (string.IsNullOrEmpty(text)) return 0;
            int count = 1;
            foreach (char c in text)
                if (c == '\n') count++;
            return count;
        }
    }

    // ScrollY is inherited from TextBufferRenderable and satisfies ILineInfoProvider.ScrollY

    private LineInfo? _cachedLineInfo;
    private bool _lineInfoDirty = true;

    public LineInfo? GetCachedLineInfo()
    {
        if (_lineInfoDirty)
        {
            _cachedLineInfo = TextBufferView.GetLogicalLineInfo();
            _lineInfoDirty = false;
        }
        return _cachedLineInfo;
    }

    /// <summary>Marks the cached LineInfo as stale (call on content/resize/wrap changes).</summary>
    private void InvalidateLineInfo() => _lineInfoDirty = true;

    #endregion

    #region Content

    private void SetTextContent(string content)
    {
        InvalidateLineInfo();
        SetTextAndDirtyLayout(content);
    }

    /// <summary>
    /// Sets content with pre-computed styled chunks (for syntax highlighting).
    /// </summary>
    public void SetStyledContent(StyledText styledText)
    {
        InvalidateLineInfo();
        SetStyledTextAndDirtyLayout(styledText);
        _highlightsDirty = false;
        RequestRender();
    }

    /// <summary>Shortcut for the underlying TextBuffer.</summary>
    public TextBuffer TextBufferInstance => TextBuffer;

    /// <summary>
    /// Per-line background colors (used by Diff).
    /// Maps logical line index → background color.
    /// </summary>
    public Dictionary<int, Rgba>? LineBackgrounds { get; set; }

    #endregion

    #region Resize

    protected override void OnResize(int width, int height)
    {
        InvalidateLineInfo();
        base.OnResize(width, height);
    }

    #endregion

    #region Rendering

    protected override void RenderSelf(OptimizedBuffer buffer, float deltaTime)
    {
        if (_highlightsDirty)
        {
            _highlightSnapshotId++;
            _highlightsDirty = false;
        }

        // Draw per-line backgrounds using lineSources for visual→logical mapping
        if (LineBackgrounds != null && _heightValue > 0)
        {
            var lineInfo = GetCachedLineInfo();
            var sources = lineInfo?.LineSources;
            int startLine = ScrollY;

            for (int i = 0; i < _heightValue; i++)
            {
                int visualIdx = startLine + i;
                int logicalLine;
                if (sources != null && visualIdx < sources.Length)
                    logicalLine = (int)sources[visualIdx];
                else
                    break;

                if (LineBackgrounds.TryGetValue(logicalLine, out var bg))
                {
                    buffer.FillRect((uint)_screenX, (uint)(_screenY + i),
                        (uint)_widthValue, 1, bg);
                }
            }
        }

        base.RenderSelf(buffer, deltaTime);
    }

    #endregion
}
