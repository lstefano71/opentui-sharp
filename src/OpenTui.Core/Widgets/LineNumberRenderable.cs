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
        DirtyGutterLayout();
    }

    public void ClearLineSign(int line)
    {
        _lineSigns.Remove(line);
        DirtyGutterLayout();
    }

    public void SetLineSigns(Dictionary<int, LineSign> signs)
    {
        _lineSigns = signs;
        DirtyGutterLayout();
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

    private void DirtyGutterLayout()
    {
        _gutter.MarkLayoutDirty();
        RequestRender();
    }

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
            var provider = _target as ILineInfoProvider;
            int scrollY = provider?.ScrollY ?? 0;
            int visibleLines = _heightValue;
            var lineInfo = provider?.GetCachedLineInfo();
            var sources = lineInfo?.LineSources;

            for (int i = 0; i < visibleLines; i++)
            {
                int visualIdx = scrollY + i;
                int logicalLine;
                if (sources != null && visualIdx < sources.Length)
                    logicalLine = (int)sources[visualIdx];
                else
                    break;

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
        private int _maxBeforeWidth;
        private int _maxAfterWidth;
        private int _digitWidth;

        public GutterRenderable(IRenderContext ctx, LineNumberRenderable owner)
            : base(ctx, new RenderableOptions { Buffered = true })
        {
            _owner = owner;
            YGNodeAPI.YGNodeSetMeasureFunc(YogaNode, MeasureFunc);
        }

        public void MarkLayoutDirty() => YGNodeAPI.YGNodeMarkDirty(YogaNode);

        /// <summary>
        /// Recomputes shared gutter metrics from current signs and line count.
        /// Called by both measure and render to ensure consistency.
        /// </summary>
        private void UpdateMetrics()
        {
            int lineCount = (_owner._target as ILineInfoProvider)?.LineCount ?? 0;
            int maxLineNum = lineCount + _owner._lineNumberOffset;
            _digitWidth = Math.Max(_owner._minWidth, maxLineNum.ToString().Length);

            _maxBeforeWidth = 0;
            _maxAfterWidth = 0;
            foreach (var (_, sign) in _owner._lineSigns)
            {
                if (sign.Before != null) _maxBeforeWidth = Math.Max(_maxBeforeWidth, sign.Before.Length);
                if (sign.After != null) _maxAfterWidth = Math.Max(_maxAfterWidth, sign.After.Length);
            }
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
            UpdateMetrics();
            return _maxBeforeWidth + _digitWidth + _maxAfterWidth + _owner._paddingRight;
        }

        protected override void RenderSelf(OptimizedBuffer buffer, float deltaTime)
        {
            if (!_owner._showLineNumbers) return;

            UpdateMetrics();

            var provider = _owner._target as ILineInfoProvider;
            int scrollY = provider?.ScrollY ?? 0;
            int visibleLines = _heightValue;
            int gutterWidth = _widthValue;

            var lineInfo = provider?.GetCachedLineInfo();
            var sources = lineInfo?.LineSources;

            int startX = 0;
            int startY = 0;

            buffer.Clear(_owner._bg);

            int lastSource = scrollY > 0 && sources != null && scrollY - 1 < sources.Length
                ? (int)sources[scrollY - 1] : -1;

            for (int i = 0; i < visibleLines; i++)
            {
                int visualIdx = scrollY + i;
                int logicalLine;
                if (sources != null && visualIdx < sources.Length)
                    logicalLine = (int)sources[visualIdx];
                else
                    break;

                // Per-line gutter background
                if (_owner._lineColors.TryGetValue(logicalLine, out var colorCfg) && colorCfg.GutterBg.HasValue)
                {
                    buffer.FillRect((uint)startX, (uint)(startY + i),
                        (uint)gutterWidth, 1, colorCfg.GutterBg.Value);
                }

                var fg = colorCfg.GutterFg ?? _owner._fg;

                // Skip wrapped continuation lines (same logical source as previous row)
                if (logicalLine == lastSource)
                {
                    lastSource = logicalLine;
                    continue;
                }
                lastSource = logicalLine;

                // Hidden line numbers
                if (_owner._hideLineNumbers.Contains(logicalLine))
                    continue;

                // Get display line number
                int displayNum = _owner._customLineNumbers?.GetValueOrDefault(logicalLine, logicalLine + 1 + _owner._lineNumberOffset)
                    ?? (logicalLine + 1 + _owner._lineNumberOffset);

                string numStr = displayNum.ToString().PadLeft(_digitWidth);

                int currentX = startX;

                // Draw sign (before) — always reserve _maxBeforeWidth columns
                _owner._lineSigns.TryGetValue(logicalLine, out var sign);
                if (sign.Before is { } before)
                {
                    int padding = _maxBeforeWidth - before.Length;
                    currentX += padding;
                    var signFg = sign.Fg ?? fg;
                    buffer.DrawText(before, (uint)currentX, (uint)(startY + i), signFg, sign.Bg);
                    currentX += before.Length;
                }
                else
                {
                    currentX += _maxBeforeWidth;
                }

                // Draw line number (right-aligned within digit area)
                buffer.DrawText(numStr, (uint)currentX, (uint)(startY + i), fg);
                currentX += numStr.Length;

                // Draw sign (after) — always at fixed position
                if (sign.After is { } after)
                {
                    var signFg = sign.Fg ?? fg;
                    buffer.DrawText(after, (uint)currentX, (uint)(startY + i), signFg, sign.Bg);
                }
            }
        }
    }
}
