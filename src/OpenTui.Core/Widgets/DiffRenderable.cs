namespace OpenTui.Core;

/// <summary>
/// Options for Diff renderable.
/// Matches TypeScript DiffRenderableOptions.
/// </summary>
public class DiffOptions : RenderableOptions
{
    public string Diff { get; init; } = "";
    public string View { get; init; } = "unified"; // "unified" | "split"
    public bool SyncScroll { get; init; }
    public string? Filetype { get; init; }
    public SyntaxStyle? SyntaxStyle { get; init; }
    public byte WrapMode { get; init; } = 2; // word
    public bool Conceal { get; init; }
    public bool ShowLineNumbers { get; init; } = true;
    public Rgba? LineNumberFg { get; init; }
    public Rgba? LineNumberBg { get; init; }
    public Rgba? AddedBg { get; init; }
    public Rgba? RemovedBg { get; init; }
    public Rgba? ContextBg { get; init; }
    public Rgba? AddedContentBg { get; init; }
    public Rgba? RemovedContentBg { get; init; }
    public Rgba? ContextContentBg { get; init; }
    public Rgba? AddedSignColor { get; init; }
    public Rgba? RemovedSignColor { get; init; }
    public Rgba? AddedLineNumberBg { get; init; }
    public Rgba? RemovedLineNumberBg { get; init; }
    public Rgba? SelectionBg { get; init; }
    public Rgba? SelectionFg { get; init; }
}

/// <summary>
/// Represents a parsed diff hunk.
/// </summary>
public sealed class DiffHunk
{
    public int OldStart { get; init; }
    public int OldLines { get; init; }
    public int NewStart { get; init; }
    public int NewLines { get; init; }
    public List<DiffLine> Lines { get; init; } = [];
}

/// <summary>
/// Represents a single line in a diff.
/// </summary>
public sealed class DiffLine
{
    public DiffLineType Type { get; init; }
    public string Content { get; init; } = "";
}

public enum DiffLineType { Context, Added, Removed }

/// <summary>
/// Unified/split diff viewer.
/// Composes CodeRenderable + LineNumberRenderable children.
/// Matches TypeScript DiffRenderable from Diff.ts.
/// </summary>
public class DiffRenderable : Renderable
{
    private string _diff;
    private string _viewMode;
    private bool _syncScroll;
    private string? _filetype;
    private SyntaxStyle? _syntaxStyle;
    private byte _wrapMode;
    private bool _conceal;
    private bool _showLineNumbers;
    private Rgba _addedBg;
    private Rgba _removedBg;
    private Rgba _contextBg;
    private Rgba? _addedContentBg;
    private Rgba? _removedContentBg;
    private Rgba? _contextContentBg;
    private Rgba _addedSignColor;
    private Rgba _removedSignColor;
    private Rgba _lineNumberFg;
    private Rgba _lineNumberBg;

    private CodeRenderable? _unifiedCode;
    private LineNumberRenderable? _unifiedLineNumbers;
    private CodeRenderable? _leftCode;
    private CodeRenderable? _rightCode;
    private LineNumberRenderable? _leftLineNumbers;
    private LineNumberRenderable? _rightLineNumbers;

    public DiffRenderable(IRenderContext ctx, DiffOptions? options = null)
        : base(ctx, options ?? new DiffOptions() { FlexDirection = FlexDirectionValue.Row })
    {
        options ??= new DiffOptions();
        _diff = options.Diff;
        _viewMode = options.View;
        _syncScroll = options.SyncScroll;
        _filetype = options.Filetype;
        _syntaxStyle = options.SyntaxStyle;
        _wrapMode = options.WrapMode;
        _conceal = options.Conceal;
        _showLineNumbers = options.ShowLineNumbers;
        _addedBg = options.AddedBg ?? Rgba.FromHex("#1a4d1a");
        _removedBg = options.RemovedBg ?? Rgba.FromHex("#4d1a1a");
        _contextBg = options.ContextBg ?? Rgba.Transparent;
        _addedContentBg = options.AddedContentBg;
        _removedContentBg = options.RemovedContentBg;
        _contextContentBg = options.ContextContentBg;
        _addedSignColor = options.AddedSignColor ?? Rgba.FromHex("#22c55e");
        _removedSignColor = options.RemovedSignColor ?? Rgba.FromHex("#ef4444");
        _lineNumberFg = options.LineNumberFg ?? Rgba.FromHex("#888888");
        _lineNumberBg = options.LineNumberBg ?? Rgba.Transparent;

        FlexDirection = FlexDirectionValue.Row;
        BuildView();
    }

    #region Properties

    public string Diff
    {
        get => _diff;
        set
        {
            if (_diff == value) return;
            _diff = value;
            RebuildContent();
            RequestRender();
        }
    }

    public string ViewMode
    {
        get => _viewMode;
        set
        {
            if (_viewMode == value) return;
            _viewMode = value;
            ClearChildren();
            BuildView();
            RequestRender();
        }
    }

    public bool SyncScroll
    {
        get => _syncScroll;
        set { _syncScroll = value; }
    }

    public bool ShowLineNumbers
    {
        get => _showLineNumbers;
        set { _showLineNumbers = value; RequestRender(); }
    }

    #endregion

    #region View Building

    private void ClearChildren()
    {
        // Remove existing children
        if (_unifiedLineNumbers != null) { Remove(_unifiedLineNumbers.Id); _unifiedLineNumbers = null; }
        if (_unifiedCode != null) { Remove(_unifiedCode.Id); _unifiedCode = null; }
        if (_leftLineNumbers != null) { Remove(_leftLineNumbers.Id); _leftLineNumbers = null; }
        if (_rightLineNumbers != null) { Remove(_rightLineNumbers.Id); _rightLineNumbers = null; }
        if (_leftCode != null) { Remove(_leftCode.Id); _leftCode = null; }
        if (_rightCode != null) { Remove(_rightCode.Id); _rightCode = null; }
    }

    private void BuildView()
    {
        var hunks = ParseDiff(_diff);

        if (_viewMode == "split")
            BuildSplitView(hunks);
        else
            BuildUnifiedView(hunks);
    }

    private void BuildUnifiedView(List<DiffHunk> hunks)
    {
        var (content, lineColors, lineSigns, lineNumbers) = BuildUnifiedContent(hunks);

        _unifiedCode = new CodeRenderable(_ctx, new CodeOptions
        {
            Content = content,
            Filetype = _filetype,
            SyntaxStyle = _syntaxStyle,
            Conceal = _conceal,
            FlexGrow = 1,
        });

        _unifiedCode.LineBackgrounds = lineColors;

        if (_showLineNumbers)
        {
            _unifiedLineNumbers = new LineNumberRenderable(_ctx, new LineNumberOptions
            {
                Target = _unifiedCode,
                Fg = _lineNumberFg,
                Bg = _lineNumberBg,
                FlexGrow = 1,
            });
            _unifiedLineNumbers.SetLineSigns(lineSigns);
            _unifiedLineNumbers.SetLineNumbers(lineNumbers);
            Add(_unifiedLineNumbers);
        }
        else
        {
            Add(_unifiedCode);
        }
    }

    private void BuildSplitView(List<DiffHunk> hunks)
    {
        var (leftContent, rightContent, leftColors, rightColors, leftSigns, rightSigns,
             leftLineNums, rightLineNums) = BuildSplitContent(hunks);

        _leftCode = new CodeRenderable(_ctx, new CodeOptions
        {
            Content = leftContent,
            Filetype = _filetype,
            SyntaxStyle = _syntaxStyle,
            Conceal = _conceal,
            Width = DimensionValue.Percent(50),
        });
        _leftCode.LineBackgrounds = leftColors;

        _rightCode = new CodeRenderable(_ctx, new CodeOptions
        {
            Content = rightContent,
            Filetype = _filetype,
            SyntaxStyle = _syntaxStyle,
            Conceal = _conceal,
            Width = DimensionValue.Percent(50),
        });
        _rightCode.LineBackgrounds = rightColors;

        if (_showLineNumbers)
        {
            _leftLineNumbers = new LineNumberRenderable(_ctx, new LineNumberOptions
            {
                Target = _leftCode,
                Fg = _lineNumberFg,
                Bg = _lineNumberBg,
                Width = DimensionValue.Percent(50),
            });
            _leftLineNumbers.SetLineSigns(leftSigns);
            _leftLineNumbers.SetLineNumbers(leftLineNums);

            _rightLineNumbers = new LineNumberRenderable(_ctx, new LineNumberOptions
            {
                Target = _rightCode,
                Fg = _lineNumberFg,
                Bg = _lineNumberBg,
                Width = DimensionValue.Percent(50),
            });
            _rightLineNumbers.SetLineSigns(rightSigns);
            _rightLineNumbers.SetLineNumbers(rightLineNums);

            Add(_leftLineNumbers);
            Add(_rightLineNumbers);
        }
        else
        {
            Add(_leftCode);
            Add(_rightCode);
        }
    }

    private void RebuildContent()
    {
        ClearChildren();
        BuildView();
    }

    #endregion

    #region Diff Parsing

    /// <summary>
    /// Simple unified diff parser.
    /// Parses standard unified diff format into hunks.
    /// </summary>
    public static List<DiffHunk> ParseDiff(string diff)
    {
        var hunks = new List<DiffHunk>();
        if (string.IsNullOrEmpty(diff)) return hunks;

        var lines = diff.Split('\n');
        DiffHunk? current = null;

        foreach (var line in lines)
        {
            if (line.StartsWith("@@"))
            {
                current = ParseHunkHeader(line);
                if (current != null) hunks.Add(current);
            }
            else if (current != null)
            {
                if (line.StartsWith('+'))
                    current.Lines.Add(new DiffLine { Type = DiffLineType.Added, Content = line[1..] });
                else if (line.StartsWith('-'))
                    current.Lines.Add(new DiffLine { Type = DiffLineType.Removed, Content = line[1..] });
                else if (line.StartsWith(' ') || line.Length == 0)
                    current.Lines.Add(new DiffLine { Type = DiffLineType.Context, Content = line.Length > 0 ? line[1..] : "" });
            }
        }

        return hunks;
    }

    private static DiffHunk? ParseHunkHeader(string line)
    {
        // Parse @@ -oldStart,oldLines +newStart,newLines @@
        int atIdx = line.IndexOf("@@", 2);
        if (atIdx < 0) return null;

        string range = line[2..atIdx].Trim();
        var parts = range.Split(' ');
        if (parts.Length < 2) return null;

        static (int start, int count) ParseRange(string s)
        {
            s = s.TrimStart('-', '+');
            var p = s.Split(',');
            int start = int.TryParse(p[0], out var s0) ? s0 : 0;
            int count = p.Length > 1 && int.TryParse(p[1], out var c0) ? c0 : 1;
            return (start, count);
        }

        var (oldStart, oldLines) = ParseRange(parts[0]);
        var (newStart, newLines) = ParseRange(parts[1]);

        return new DiffHunk
        {
            OldStart = oldStart,
            OldLines = oldLines,
            NewStart = newStart,
            NewLines = newLines,
        };
    }

    #endregion

    #region Content Building

    private (string Content, Dictionary<int, Rgba> LineColors,
        Dictionary<int, LineSign> LineSigns, Dictionary<int, int> LineNumbers)
        BuildUnifiedContent(List<DiffHunk> hunks)
    {
        var sb = new System.Text.StringBuilder();
        var lineColors = new Dictionary<int, Rgba>();
        var lineSigns = new Dictionary<int, LineSign>();
        var lineNumbers = new Dictionary<int, int>();
        int lineIdx = 0;

        foreach (var hunk in hunks)
        {
            int oldLine = hunk.OldStart;
            int newLine = hunk.NewStart;

            foreach (var line in hunk.Lines)
            {
                if (lineIdx > 0) sb.Append('\n');
                sb.Append(line.Content);

                switch (line.Type)
                {
                    case DiffLineType.Added:
                        lineColors[lineIdx] = _addedBg;
                        lineSigns[lineIdx] = new LineSign("+", null, _addedSignColor, null);
                        lineNumbers[lineIdx] = newLine++;
                        break;
                    case DiffLineType.Removed:
                        lineColors[lineIdx] = _removedBg;
                        lineSigns[lineIdx] = new LineSign("-", null, _removedSignColor, null);
                        lineNumbers[lineIdx] = oldLine++;
                        break;
                    case DiffLineType.Context:
                        lineColors[lineIdx] = _contextBg;
                        lineNumbers[lineIdx] = newLine;
                        oldLine++;
                        newLine++;
                        break;
                }

                lineIdx++;
            }
        }

        return (sb.ToString(), lineColors, lineSigns, lineNumbers);
    }

    private (string LeftContent, string RightContent,
        Dictionary<int, Rgba> LeftColors, Dictionary<int, Rgba> RightColors,
        Dictionary<int, LineSign> LeftSigns, Dictionary<int, LineSign> RightSigns,
        Dictionary<int, int> LeftLineNums, Dictionary<int, int> RightLineNums)
        BuildSplitContent(List<DiffHunk> hunks)
    {
        var leftSb = new System.Text.StringBuilder();
        var rightSb = new System.Text.StringBuilder();
        var leftColors = new Dictionary<int, Rgba>();
        var rightColors = new Dictionary<int, Rgba>();
        var leftSigns = new Dictionary<int, LineSign>();
        var rightSigns = new Dictionary<int, LineSign>();
        var leftLineNums = new Dictionary<int, int>();
        var rightLineNums = new Dictionary<int, int>();
        int leftIdx = 0, rightIdx = 0;

        foreach (var hunk in hunks)
        {
            int oldLine = hunk.OldStart;
            int newLine = hunk.NewStart;

            foreach (var line in hunk.Lines)
            {
                switch (line.Type)
                {
                    case DiffLineType.Added:
                        if (rightIdx > 0) rightSb.Append('\n');
                        rightSb.Append(line.Content);
                        rightColors[rightIdx] = _addedBg;
                        rightSigns[rightIdx] = new LineSign("+", null, _addedSignColor, null);
                        rightLineNums[rightIdx] = newLine++;
                        rightIdx++;
                        // Pad left
                        if (leftIdx > 0) leftSb.Append('\n');
                        leftSb.Append("");
                        leftIdx++;
                        break;
                    case DiffLineType.Removed:
                        if (leftIdx > 0) leftSb.Append('\n');
                        leftSb.Append(line.Content);
                        leftColors[leftIdx] = _removedBg;
                        leftSigns[leftIdx] = new LineSign("-", null, _removedSignColor, null);
                        leftLineNums[leftIdx] = oldLine++;
                        leftIdx++;
                        // Pad right
                        if (rightIdx > 0) rightSb.Append('\n');
                        rightSb.Append("");
                        rightIdx++;
                        break;
                    case DiffLineType.Context:
                        if (leftIdx > 0) leftSb.Append('\n');
                        leftSb.Append(line.Content);
                        leftColors[leftIdx] = _contextBg;
                        leftLineNums[leftIdx] = oldLine++;
                        leftIdx++;

                        if (rightIdx > 0) rightSb.Append('\n');
                        rightSb.Append(line.Content);
                        rightColors[rightIdx] = _contextBg;
                        rightLineNums[rightIdx] = newLine++;
                        rightIdx++;
                        break;
                }
            }
        }

        return (leftSb.ToString(), rightSb.ToString(),
            leftColors, rightColors, leftSigns, rightSigns,
            leftLineNums, rightLineNums);
    }

    #endregion

    #region Dispose

    protected override void DestroySelf()
    {
        ClearChildren();
        base.DestroySelf();
    }

    #endregion
}
