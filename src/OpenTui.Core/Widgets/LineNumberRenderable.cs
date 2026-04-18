using Facebook.Yoga;

namespace OpenTui.Core;

/// <summary>
/// Configuration for a line sign (e.g., "+" or "-" in a diff gutter).
/// </summary>
public readonly record struct LineSign(string? Before, string? After, Rgba? Fg, Rgba? Bg);

/// <summary>
/// Per-line color configuration for the gutter and content backgrounds.
/// </summary>
public readonly record struct LineColorConfig(Rgba? GutterBg, Rgba? ContentBg, Rgba? GutterFg);

/// <summary>
/// Options for LineNumber renderable.
/// Matches TypeScript LineNumberOptions.
/// </summary>
public class LineNumberOptions : RenderableOptions
{
    public Renderable? Target { get; init; }
    public Rgba? Fg { get; init; }
    public Rgba? Bg { get; init; }
    public int MinWidth { get; init; } = 3;
    public int PaddingRight { get; init; } = 1;
    public int LineNumberOffset { get; init; }
    public bool ShowLineNumbers { get; init; } = true;
    public Dictionary<int, LineColorConfig>? LineColors { get; init; }
    public Dictionary<int, LineSign>? LineSigns { get; init; }
    public HashSet<int>? HideLineNumbers { get; init; }
    public Dictionary<int, int>? LineNumbers { get; init; }
}

/// <summary>
/// Line number gutter displayed alongside a target text renderable.
/// Composes an inner GutterRenderable in a row layout with the target.
/// Matches TypeScript LineNumberRenderable from LineNumberRenderable.ts.
/// </summary>
public class LineNumberRenderable : Renderable
{
    private Rgba _fg;
    private Rgba _bg;
    private int _minWidth;
    private int _paddingRight;
    private int _lineNumberOffset;
    private bool _showLineNumbers;
    private Renderable? _target;
    private GutterRenderable _gutter;
    private Dictionary<int, LineColorConfig> _lineColors;
    private Dictionary<int, LineSign> _lineSigns;
    private HashSet<int> _hideLineNumbers;
    private Dictionary<int, int>? _customLineNumbers;

    public LineNumberRenderable(IRenderContext ctx, LineNumberOptions? options = null)
        : base(ctx, options ?? new LineNumberOptions() { FlexDirection = FlexDirectionValue.Row })
    {
        options ??= new LineNumberOptions();
        _fg = options.Fg ?? Rgba.FromHex("#888888");
        _bg = options.Bg ?? Rgba.Transparent;
        _minWidth = options.MinWidth;
        _paddingRight = options.PaddingRight;
        _lineNumberOffset = options.LineNumberOffset;
        _showLineNumbers = options.ShowLineNumbers;
        _lineColors = options.LineColors ?? [];
        _lineSigns = options.LineSigns ?? [];
        _hideLineNumbers = options.HideLineNumbers ?? [];
        _customLineNumbers = options.LineNumbers;

        FlexDirection = FlexDirectionValue.Row;

        _gutter = new GutterRenderable(ctx, this);
        Add(_gutter);

        if (options.Target is { } target)
            SetTarget(target);
    }

    #region Properties

    public Rgba Fg
    {
        get => _fg;
        set { _fg = value; RequestRender(); }
    }

    public Rgba Bg
    {
        get => _bg;
        set { _bg = value; RequestRender(); }
    }

    public bool ShowLineNumbers
    {
        get => _showLineNumbers;
        set { _showLineNumbers = value; RequestRender(); }
    }

    public int LineNumberOffset
    {
        get => _lineNumberOffset;
        set { _lineNumberOffset = value; RequestRender(); }
    }

    #endregion

    #region Line Colors & Signs

    public void SetLineColor(int line, LineColorConfig config)
    {
        _lineColors[line] = config;
        RequestRender();
    }

    public void ClearLineColor(int line)
    {
        _lineColors.Remove(line);
        RequestRender();
    }

    public void ClearAllLineColors()
    {
        _lineColors.Clear();
        RequestRender();
    }

    public void SetLineColors(Dictionary<int, LineColorConfig> colors)
    {
        _lineColors = colors;
        RequestRender();
    }

    public void SetLineSign(int line, LineSign sign)
    {
        _lineSigns[line] = sign;
        RequestRender();
    }

    public void ClearLineSign(int line)
    {
        _lineSigns.Remove(line);
        RequestRender();
    }

    public void SetLineSigns(Dictionary<int, LineSign> signs)
    {
        _lineSigns = signs;
        RequestRender();
    }

    public void SetHideLineNumbers(HashSet<int> lines)
    {
        _hideLineNumbers = lines;
        RequestRender();
    }

    public void SetLineNumbers(Dictionary<int, int>? numbers)
    {
        _customLineNumbers = numbers;
        RequestRender();
    }

    public Dictionary<int, LineColorConfig> GetLineColors() => _lineColors;
    public Dictionary<int, LineSign> GetLineSigns() => _lineSigns;

    #endregion

    #region Target Management

    public void SetTarget(Renderable target)
    {
        if (_target == target) return;
        _target = target;
        Add(target);

        // Listen for line info changes if target supports it
        if (target is ILineInfoProvider)
        {
            target.On(ILineInfoProvider.LineInfoChangeEvent, () => RequestRender());
        }
    }

    public void ClearTarget()
    {
        _target = null;
    }

    public override int Add(Renderable? obj, int? index = null)
    {
        // Auto-detect LineInfoProvider targets
        if (obj is ILineInfoProvider && _target == null)
            _target = obj;
        return base.Add(obj, index);
    }

    #endregion

    #region Rendering

    protected override void RenderSelf(OptimizedBuffer buffer, float deltaTime)
    {
        // Draw full-width content backgrounds for lines with custom colors
        if (_target != null && _lineColors.Count > 0)
        {
            int scrollY = (_target as ILineInfoProvider)?.ScrollY ?? 0;
            int visibleLines = _heightValue;

            for (int i = 0; i < visibleLines; i++)
            {
                int logicalLine = scrollY + i;
                if (_lineColors.TryGetValue(logicalLine, out var colorCfg) && colorCfg.ContentBg.HasValue)
                {
                    buffer.FillRect((uint)_screenX, (uint)(_screenY + i),
                        (uint)_widthValue, 1, colorCfg.ContentBg.Value);
                }
            }
        }
    }

    #endregion

    /// <summary>
    /// Inner renderable that draws the line number gutter.
    /// </summary>
    private sealed class GutterRenderable : Renderable
    {
        private readonly LineNumberRenderable _owner;

        public GutterRenderable(IRenderContext ctx, LineNumberRenderable owner)
            : base(ctx, new RenderableOptions { Buffered = true })
        {
            _owner = owner;
            YGNodeAPI.YGNodeSetMeasureFunc(YogaNode, MeasureFunc);
        }

        private YGSize MeasureFunc(Node node, float availableWidth, MeasureMode widthMode,
            float availableHeight, MeasureMode heightMode)
        {
            int gutterWidth = ComputeGutterWidth();
            return new YGSize { Width = gutterWidth, Height = float.IsNaN(availableHeight) ? 1 : availableHeight };
        }

        private int ComputeGutterWidth()
        {
            if (!_owner._showLineNumbers) return 0;

            int lineCount = (_owner._target as ILineInfoProvider)?.LineCount ?? 0;
            int maxLineNum = lineCount + _owner._lineNumberOffset;
            int digitWidth = Math.Max(_owner._minWidth, maxLineNum.ToString().Length);

            // Account for signs
            int signWidth = 0;
            foreach (var (_, sign) in _owner._lineSigns)
            {
                if (sign.Before != null) signWidth = Math.Max(signWidth, sign.Before.Length);
                if (sign.After != null) signWidth = Math.Max(signWidth, sign.After.Length);
            }

            return digitWidth + _owner._paddingRight + signWidth;
        }

        protected override void RenderSelf(OptimizedBuffer buffer, float deltaTime)
        {
            if (!_owner._showLineNumbers) return;

            int scrollY = (_owner._target as ILineInfoProvider)?.ScrollY ?? 0;
            int lineCount = (_owner._target as ILineInfoProvider)?.LineCount ?? 0;
            int visibleLines = _heightValue;
            int gutterWidth = _widthValue;

            // Fill background
            buffer.FillRect((uint)_screenX, (uint)_screenY,
                (uint)gutterWidth, (uint)visibleLines, _owner._bg);

            for (int i = 0; i < visibleLines; i++)
            {
                int logicalLine = scrollY + i;
                if (logicalLine >= lineCount) break;

                // Per-line gutter background
                if (_owner._lineColors.TryGetValue(logicalLine, out var colorCfg) && colorCfg.GutterBg.HasValue)
                {
                    buffer.FillRect((uint)_screenX, (uint)(_screenY + i),
                        (uint)gutterWidth, 1, colorCfg.GutterBg.Value);
                }

                var fg = colorCfg.GutterFg ?? _owner._fg;

                // Hidden line numbers
                if (_owner._hideLineNumbers.Contains(logicalLine))
                    continue;

                // Get display line number
                int displayNum = _owner._customLineNumbers?.GetValueOrDefault(logicalLine, logicalLine + 1 + _owner._lineNumberOffset)
                    ?? (logicalLine + 1 + _owner._lineNumberOffset);

                string numStr = displayNum.ToString().PadLeft(_owner._minWidth);

                // Draw signs (before)
                if (_owner._lineSigns.TryGetValue(logicalLine, out var sign))
                {
                    if (sign.Before is { } before)
                    {
                        var signFg = sign.Fg ?? fg;
                        buffer.DrawText(before, (uint)_screenX, (uint)(_screenY + i), signFg, sign.Bg);
                    }
                }

                // Draw line number
                int numX = (int)_screenX + (sign.Before?.Length ?? 0);
                buffer.DrawText(numStr, (uint)numX, (uint)(_screenY + i), fg);

                // Draw signs (after)
                if (sign.After is { } after)
                {
                    int afterX = numX + numStr.Length;
                    var signFg = sign.Fg ?? fg;
                    buffer.DrawText(after, (uint)afterX, (uint)(_screenY + i), signFg, sign.Bg);
                }
            }
        }
    }
}
