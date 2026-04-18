using Facebook.Yoga;

namespace OpenTui.Core;

/// <summary>
/// Options for TextTable renderable.
/// Matches TypeScript TextTableOptions.
/// </summary>
public class TextTableOptions : RenderableOptions
{
    public TextChunk[][][] Content { get; init; } = [];
    public byte WrapMode { get; init; } = 2; // 0=none, 1=char, 2=word
    public string ColumnWidthMode { get; init; } = "full"; // "content" | "full"
    public string ColumnFitter { get; init; } = "proportional"; // "proportional" | "balanced"
    public int CellPadding { get; init; }
    public bool ShowBorders { get; init; } = true;
    public bool Border { get; init; } = true;
    public bool OuterBorder { get; init; } = true;
    public bool Selectable { get; init; } = true;
    public Rgba? SelectionBg { get; init; }
    public Rgba? SelectionFg { get; init; }
    public BorderStyle BorderStyle { get; init; } = BorderStyle.Single;
    public Rgba? BorderColor { get; init; }
    public Rgba? BorderBackgroundColor { get; init; }
    public Rgba? BackgroundColor { get; init; }
    public Rgba? Fg { get; init; }
    public Rgba? Bg { get; init; }
    public TextAttributes Attributes { get; init; }
}

/// <summary>
/// Table layout with columns, rows, headers, and styled cells.
/// Manages a grid of TextBuffer/TextBufferView objects internally.
/// Matches TypeScript TextTableRenderable from TextTable.ts.
/// </summary>
public class TextTableRenderable : Renderable
{
    private TextChunk[][][] _content;
    private byte _wrapMode;
    private string _columnWidthMode;
    private string _columnFitter;
    private int _cellPadding;
    private bool _showBorders;
    private bool _border;
    private bool _outerBorder;
    private BorderStyle _borderStyle;
    private Rgba _borderColor;
    private Rgba _borderBgColor;
    private Rgba _backgroundColor;
    private Rgba _fg;
    private Rgba _bg;
    private TextAttributes _attributes;

    // Grid of text buffer cells
    private CellState[,]? _cells;
    private int _rowCount;
    private int _colCount;

    private readonly record struct BorderLayout(
        bool Left,
        bool Right,
        bool Top,
        bool Bottom,
        bool InnerVertical,
        bool InnerHorizontal);

    private sealed record TableLayout(
        int[] ColumnWidths,
        int[] RowHeights,
        int[] ColumnOffsets,
        int[] RowOffsets,
        int TableWidth,
        int TableHeight);

    private sealed class CellState : IDisposable
    {
        public TextBuffer TextBuffer { get; }
        public TextBufferView TextBufferView { get; }

        public CellState(WidthMethod widthMethod)
        {
            TextBuffer = TextBuffer.Create(widthMethod);
            TextBufferView = TextBufferView.Create(TextBuffer);
        }

        public void Dispose()
        {
            TextBufferView.Dispose();
            TextBuffer.Dispose();
        }
    }

    public TextTableRenderable(IRenderContext ctx, TextTableOptions? options = null)
        : base(ctx, options ?? new TextTableOptions() { Buffered = true })
    {
        options ??= new TextTableOptions();
        _content = options.Content;
        _wrapMode = options.WrapMode;
        _columnWidthMode = options.ColumnWidthMode;
        _columnFitter = options.ColumnFitter;
        _cellPadding = options.CellPadding;
        _showBorders = options.ShowBorders;
        _border = options.Border;
        _outerBorder = options.OuterBorder;
        _borderStyle = options.BorderStyle;
        _borderColor = options.BorderColor ?? Rgba.FromInts(255, 255, 255);
        _borderBgColor = options.BorderBackgroundColor ?? Rgba.Transparent;
        _backgroundColor = options.BackgroundColor ?? Rgba.Transparent;
        _fg = options.Fg ?? Rgba.FromInts(255, 255, 255);
        _bg = options.Bg ?? Rgba.Transparent;
        _attributes = options.Attributes;

        YGNodeAPI.YGNodeSetMeasureFunc(YogaNode, MeasureFunc);
        RebuildCells();
    }

    #region Properties

    public TextChunk[][][] Content
    {
        get => _content;
        set
        {
            _content = value;
            RebuildCells();
            YGNodeAPI.YGNodeMarkDirty(YogaNode);
            RequestRender();
        }
    }

    public byte WrapMode
    {
        get => _wrapMode;
        set
        {
            _wrapMode = value;
            UpdateCellWrapModes();
            YGNodeAPI.YGNodeMarkDirty(YogaNode);
            RequestRender();
        }
    }

    public string ColumnWidthMode
    {
        get => _columnWidthMode;
        set
        {
            _columnWidthMode = value;
            YGNodeAPI.YGNodeMarkDirty(YogaNode);
            RequestRender();
        }
    }

    public string ColumnFitter
    {
        get => _columnFitter;
        set
        {
            _columnFitter = value;
            YGNodeAPI.YGNodeMarkDirty(YogaNode);
            RequestRender();
        }
    }

    public int CellPadding
    {
        get => _cellPadding;
        set
        {
            _cellPadding = value;
            YGNodeAPI.YGNodeMarkDirty(YogaNode);
            RequestRender();
        }
    }

    public bool ShowBorders
    {
        get => _showBorders;
        set
        {
            _showBorders = value;
            YGNodeAPI.YGNodeMarkDirty(YogaNode);
            RequestRender();
        }
    }

    public bool Border
    {
        get => _border;
        set
        {
            _border = value;
            YGNodeAPI.YGNodeMarkDirty(YogaNode);
            RequestRender();
        }
    }

    public bool OuterBorder
    {
        get => _outerBorder;
        set
        {
            _outerBorder = value;
            YGNodeAPI.YGNodeMarkDirty(YogaNode);
            RequestRender();
        }
    }

    public BorderStyle TableBorderStyle
    {
        get => _borderStyle;
        set { _borderStyle = value; RequestRender(); }
    }

    public Rgba BorderColor
    {
        get => _borderColor;
        set { _borderColor = value; RequestRender(); }
    }

    #endregion

    #region Cell Management

    private void RebuildCells()
    {
        DisposeCells();

        _rowCount = _content.Length;
        _colCount = 0;
        foreach (var row in _content)
            _colCount = Math.Max(_colCount, row.Length);

        if (_rowCount == 0 || _colCount == 0)
        {
            _cells = null;
            return;
        }

        _cells = new CellState[_rowCount, _colCount];
        for (int r = 0; r < _rowCount; r++)
        {
            for (int c = 0; c < _colCount; c++)
            {
                var cell = new CellState(_ctx.WidthMethod);
                cell.TextBuffer.SetForeground(_fg);
                cell.TextBuffer.SetBackground(_bg);
                cell.TextBuffer.SetAttributes(_attributes);
                cell.TextBufferView.SetWrapMode(_wrapMode);

                // Set cell content from chunks
                if (r < _content.Length && c < _content[r].Length)
                {
                    var chunks = _content[r][c];
                    if (chunks.Length > 0)
                    {
                        var styledText = new StyledText(chunks);
                        cell.TextBuffer.SetStyledText(styledText);
                    }
                }

                _cells[r, c] = cell;
            }
        }
    }

    private void UpdateCellWrapModes()
    {
        if (_cells == null) return;
        for (int r = 0; r < _rowCount; r++)
            for (int c = 0; c < _colCount; c++)
                _cells[r, c].TextBufferView.SetWrapMode(_wrapMode);
    }

    private void DisposeCells()
    {
        if (_cells == null) return;
        for (int r = 0; r < _rowCount; r++)
            for (int c = 0; c < _colCount; c++)
                _cells[r, c].Dispose();
        _cells = null;
    }

    #endregion

    #region Column Width Calculation

    private int GetHorizontalCellPadding() => _cellPadding * 2;

    private int GetVerticalCellPadding() => _cellPadding * 2;

    private BorderLayout ResolveBorderLayout()
    {
        bool drawOuter = _showBorders && _outerBorder;
        bool drawInner = _showBorders && _border;

        return new BorderLayout(
            Left: drawOuter,
            Right: drawOuter,
            Top: drawOuter,
            Bottom: drawOuter,
            InnerVertical: drawInner && _colCount > 1,
            InnerHorizontal: drawInner && _rowCount > 1);
    }

    private static int ComputeBorderCount(bool start, bool end, bool inner, int partCount)
    {
        return (start ? 1 : 0) + (end ? 1 : 0) + (inner ? Math.Max(0, partCount - 1) : 0);
    }

    private int GetVerticalBorderCount(BorderLayout borderLayout) =>
        ComputeBorderCount(borderLayout.Left, borderLayout.Right, borderLayout.InnerVertical, _colCount);

    private int GetHorizontalBorderCount(BorderLayout borderLayout) =>
        ComputeBorderCount(borderLayout.Top, borderLayout.Bottom, borderLayout.InnerHorizontal, _rowCount);

    private TableLayout CreateEmptyLayout() => new([], [], [], [], 0, 0);

    private TableLayout ComputeLayout(int? maxTableWidth = null)
    {
        if (_cells == null || _colCount == 0)
        {
            return CreateEmptyLayout();
        }

        var borderLayout = ResolveBorderLayout();
        var columnWidths = ComputeColumnWidths(maxTableWidth, borderLayout);
        var rowHeights = ComputeRowHeights(columnWidths);
        var columnOffsets = ComputeOffsets(columnWidths, borderLayout.Left, borderLayout.Right, borderLayout.InnerVertical);
        var rowOffsets = ComputeOffsets(rowHeights, borderLayout.Top, borderLayout.Bottom, borderLayout.InnerHorizontal);

        return new TableLayout(
            columnWidths,
            rowHeights,
            columnOffsets,
            rowOffsets,
            (columnOffsets[^1]) + 1,
            (rowOffsets[^1]) + 1);
    }

    private int[] ComputeColumnWidths(int? maxTableWidth, BorderLayout borderLayout)
    {
        if (_cells == null)
            return [];

        int horizontalPadding = GetHorizontalCellPadding();
        var intrinsicWidths = Enumerable.Repeat(1 + horizontalPadding, _colCount).ToArray();

        for (int c = 0; c < _colCount; c++)
        {
            for (int r = 0; r < _rowCount; r++)
            {
                int measuredWidth = 1;
                if (_cells[r, c].TextBufferView.MeasureForDimensions(0, 0, out var measure))
                    measuredWidth = Math.Max(1, (int)measure.WidthColsMax);
                intrinsicWidths[c] = Math.Max(intrinsicWidths[c], measuredWidth + horizontalPadding);
            }
        }

        if (maxTableWidth is null || maxTableWidth <= 0)
            return intrinsicWidths;

        int maxContentWidth = Math.Max(1, maxTableWidth.Value - GetVerticalBorderCount(borderLayout));
        int currentWidth = intrinsicWidths.Sum();

        if (currentWidth == maxContentWidth)
            return intrinsicWidths;

        if (currentWidth < maxContentWidth)
            return _columnWidthMode == "full"
                ? ExpandColumnWidths(intrinsicWidths, maxContentWidth)
                : intrinsicWidths;

        return _wrapMode == 0
            ? intrinsicWidths
            : FitColumnWidths(intrinsicWidths, maxContentWidth);
    }

    private static int[] ExpandColumnWidths(int[] widths, int targetContentWidth)
    {
        var expanded = widths.Select(width => Math.Max(1, width)).ToArray();
        int totalBaseWidth = expanded.Sum();
        if (expanded.Length == 0 || totalBaseWidth >= targetContentWidth)
            return expanded;

        int extraWidth = targetContentWidth - totalBaseWidth;
        int sharedWidth = extraWidth / expanded.Length;
        int remainder = extraWidth % expanded.Length;

        for (int idx = 0; idx < expanded.Length; idx++)
        {
            expanded[idx] += sharedWidth;
            if (idx < remainder)
                expanded[idx] += 1;
        }

        return expanded;
    }

    private int[] FitColumnWidths(int[] widths, int targetContentWidth) =>
        _columnFitter == "balanced"
            ? FitColumnWidthsBalanced(widths, targetContentWidth)
            : FitColumnWidthsProportional(widths, targetContentWidth);

    private int[] FitColumnWidthsProportional(int[] widths, int targetContentWidth)
    {
        int minWidth = 1 + GetHorizontalCellPadding();
        var hardMinWidths = Enumerable.Repeat(minWidth, widths.Length).ToArray();
        var baseWidths = widths.Select(width => Math.Max(1, width)).ToArray();
        var preferredMinWidths = baseWidths.Select(width => Math.Min(width, minWidth + 1)).ToArray();
        int preferredMinTotal = preferredMinWidths.Sum();
        var floorWidths = preferredMinTotal <= targetContentWidth ? preferredMinWidths : hardMinWidths;
        int floorTotal = floorWidths.Sum();
        int clampedTarget = Math.Max(floorTotal, targetContentWidth);
        int totalBaseWidth = baseWidths.Sum();

        if (totalBaseWidth <= clampedTarget)
            return baseWidths;

        var shrinkable = baseWidths.Select((width, idx) => width - floorWidths[idx]).ToArray();
        int totalShrinkable = shrinkable.Sum();
        if (totalShrinkable <= 0)
            return [.. floorWidths];

        int targetShrink = totalBaseWidth - clampedTarget;
        var integerShrink = new int[baseWidths.Length];
        var fractions = new double[baseWidths.Length];
        int usedShrink = 0;

        for (int idx = 0; idx < baseWidths.Length; idx++)
        {
            if (shrinkable[idx] <= 0)
                continue;

            double exact = (double)shrinkable[idx] / totalShrinkable * targetShrink;
            int whole = Math.Min(shrinkable[idx], (int)Math.Floor(exact));
            integerShrink[idx] = whole;
            fractions[idx] = exact - whole;
            usedShrink += whole;
        }

        for (int remainingShrink = targetShrink - usedShrink; remainingShrink > 0; remainingShrink--)
        {
            int bestIdx = -1;
            double bestFraction = -1;
            for (int idx = 0; idx < baseWidths.Length; idx++)
            {
                if (shrinkable[idx] - integerShrink[idx] <= 0)
                    continue;

                if (fractions[idx] > bestFraction)
                {
                    bestFraction = fractions[idx];
                    bestIdx = idx;
                }
            }

            if (bestIdx == -1)
                break;

            integerShrink[bestIdx] += 1;
            fractions[bestIdx] = 0;
        }

        return baseWidths
            .Select((width, idx) => Math.Max(floorWidths[idx], width - integerShrink[idx]))
            .ToArray();
    }

    private int[] FitColumnWidthsBalanced(int[] widths, int targetContentWidth)
    {
        int minWidth = 1 + GetHorizontalCellPadding();
        var hardMinWidths = Enumerable.Repeat(minWidth, widths.Length).ToArray();
        var baseWidths = widths.Select(width => Math.Max(1, width)).ToArray();
        int totalBaseWidth = baseWidths.Sum();
        int columnCount = baseWidths.Length;

        if (columnCount == 0 || totalBaseWidth <= targetContentWidth)
            return baseWidths;

        int evenShare = Math.Max(minWidth, targetContentWidth / columnCount);
        var preferredMinWidths = baseWidths.Select(width => Math.Min(width, evenShare)).ToArray();
        int preferredMinTotal = preferredMinWidths.Sum();
        var floorWidths = preferredMinTotal <= targetContentWidth ? preferredMinWidths : hardMinWidths;
        int floorTotal = floorWidths.Sum();
        int clampedTarget = Math.Max(floorTotal, targetContentWidth);

        if (totalBaseWidth <= clampedTarget)
            return baseWidths;

        var shrinkable = baseWidths.Select((width, idx) => width - floorWidths[idx]).ToArray();
        int totalShrinkable = shrinkable.Sum();
        if (totalShrinkable <= 0)
            return [.. floorWidths];

        int targetShrink = totalBaseWidth - clampedTarget;
        var shrink = AllocateShrinkByWeight(shrinkable, targetShrink, useSquareRoot: true);

        return baseWidths
            .Select((width, idx) => Math.Max(floorWidths[idx], width - shrink[idx]))
            .ToArray();
    }

    private static int[] AllocateShrinkByWeight(int[] shrinkable, int targetShrink, bool useSquareRoot)
    {
        var shrink = new int[shrinkable.Length];
        if (targetShrink <= 0)
            return shrink;

        var weights = shrinkable
            .Select(value => value <= 0 ? 0d : useSquareRoot ? Math.Sqrt(value) : value)
            .ToArray();

        double totalWeight = weights.Sum();
        if (totalWeight <= 0)
            return shrink;

        var fractions = new double[shrinkable.Length];
        int usedShrink = 0;

        for (int idx = 0; idx < shrinkable.Length; idx++)
        {
            if (shrinkable[idx] <= 0 || weights[idx] <= 0)
                continue;

            double exact = weights[idx] / totalWeight * targetShrink;
            int whole = Math.Min(shrinkable[idx], (int)Math.Floor(exact));
            shrink[idx] = whole;
            fractions[idx] = exact - whole;
            usedShrink += whole;
        }

        for (int remainingShrink = targetShrink - usedShrink; remainingShrink > 0; remainingShrink--)
        {
            int bestIdx = -1;
            double bestFraction = -1;

            for (int idx = 0; idx < shrinkable.Length; idx++)
            {
                if (shrinkable[idx] - shrink[idx] <= 0)
                    continue;

                if (bestIdx == -1 ||
                    fractions[idx] > bestFraction ||
                    (fractions[idx] == bestFraction && shrinkable[idx] > shrinkable[bestIdx]))
                {
                    bestIdx = idx;
                    bestFraction = fractions[idx];
                }
            }

            if (bestIdx == -1)
                break;

            shrink[bestIdx] += 1;
            fractions[bestIdx] = 0;
        }

        return shrink;
    }

    private int[] ComputeRowHeights(int[] columnWidths)
    {
        var heights = new int[_rowCount];
        if (_cells == null)
            return heights;

        int horizontalPadding = GetHorizontalCellPadding();
        int verticalPadding = GetVerticalCellPadding();
        Array.Fill(heights, 1 + verticalPadding);

        for (int r = 0; r < _rowCount; r++)
        {
            for (int c = 0; c < _colCount; c++)
            {
                uint contentWidth = (uint)Math.Max(1, (columnWidths.Length > c ? columnWidths[c] : 1) - horizontalPadding);
                if (_cells[r, c].TextBufferView.MeasureForDimensions(contentWidth, 0, out var measure))
                    heights[r] = Math.Max(heights[r], Math.Max(1, (int)measure.LineCount) + verticalPadding);
            }
        }

        return heights;
    }

    private static int[] ComputeOffsets(int[] parts, bool startBoundary, bool endBoundary, bool includeInnerBoundaries)
    {
        var offsets = new int[parts.Length + 1];
        int cursor = startBoundary ? 0 : -1;
        offsets[0] = cursor;

        for (int idx = 0; idx < parts.Length; idx++)
        {
            bool hasBoundaryAfter = idx < parts.Length - 1 ? includeInnerBoundaries : endBoundary;
            cursor += parts[idx] + (hasBoundaryAfter ? 1 : 0);
            offsets[idx + 1] = cursor;
        }

        return offsets;
    }

    #endregion

    #region Yoga Measure

    private YGSize MeasureFunc(Node node, float availableWidth, MeasureMode widthMode,
        float availableHeight, MeasureMode heightMode)
    {
        if (_cells == null || _rowCount == 0)
            return new YGSize { Width = 0, Height = 0 };

        bool hasWidthConstraint = widthMode != MeasureMode.Undefined && !float.IsNaN(availableWidth);
        int? widthConstraint = hasWidthConstraint ? Math.Max(1, (int)availableWidth) : null;
        var layout = ComputeLayout(widthConstraint);

        int measuredWidth = layout.TableWidth > 0 ? layout.TableWidth : 1;
        int measuredHeight = layout.TableHeight > 0 ? layout.TableHeight : 1;

        if (widthMode == MeasureMode.AtMost && widthConstraint is not null && _positionType != PositionValue.Absolute)
            measuredWidth = Math.Min(widthConstraint.Value, measuredWidth);

        return new YGSize { Width = measuredWidth, Height = measuredHeight };
    }

    #endregion

    #region Rendering

    protected override void RenderSelf(OptimizedBuffer buffer, float deltaTime)
    {
        if (_cells == null || _widthValue == 0 || _heightValue == 0) return;

        int baseX = _buffered ? 0 : (int)_screenX;
        int baseY = _buffered ? 0 : (int)_screenY;

        // Fill background
        buffer.FillRect((uint)baseX, (uint)baseY,
            (uint)_widthValue, (uint)_heightValue, _backgroundColor);

        var layout = ComputeLayout(_widthValue);
        ApplyLayoutToViews(layout);

        // Draw borders
        DrawBorders(buffer, layout, baseX, baseY);

        // Draw cells
        DrawCells(buffer, layout, baseX, baseY);
    }

    private void ApplyLayoutToViews(TableLayout layout)
    {
        if (_cells == null)
            return;

        int horizontalPadding = GetHorizontalCellPadding();
        int verticalPadding = GetVerticalCellPadding();

        for (int r = 0; r < _rowCount; r++)
        {
            for (int c = 0; c < _colCount; c++)
            {
                uint contentWidth = (uint)Math.Max(1, layout.ColumnWidths[c] - horizontalPadding);
                uint contentHeight = (uint)Math.Max(1, layout.RowHeights[r] - verticalPadding);

                _cells[r, c].TextBufferView.SetWrapWidth(contentWidth);
                _cells[r, c].TextBufferView.SetViewport(0, 0, contentWidth, contentHeight);
            }
        }
    }

    private void DrawBorders(OptimizedBuffer buffer, TableLayout layout, int baseX, int baseY)
    {
        var borderLayout = ResolveBorderLayout();
        if (!borderLayout.Left && !borderLayout.Right && !borderLayout.Top && !borderLayout.Bottom &&
            !borderLayout.InnerVertical && !borderLayout.InnerHorizontal)
        {
            return;
        }

        int[] columnOffsets = baseX == 0 ? layout.ColumnOffsets : layout.ColumnOffsets.Select(offset => offset + baseX).ToArray();
        int[] rowOffsets = baseY == 0 ? layout.RowOffsets : layout.RowOffsets.Select(offset => offset + baseY).ToArray();

        buffer.DrawGrid(
            columnOffsets,
            rowOffsets,
            BorderCharacters.ForStyle(_borderStyle),
            _borderColor,
            _borderBgColor,
            drawInner: _border,
            drawOuter: _outerBorder);
    }

    private void DrawCells(OptimizedBuffer buffer, TableLayout layout, int baseX, int baseY)
    {
        if (_cells == null) return;

        for (int r = 0; r < _rowCount; r++)
        {
            int cellY = baseY + layout.RowOffsets[r] + 1 + _cellPadding;
            for (int c = 0; c < _colCount; c++)
            {
                int cellX = baseX + layout.ColumnOffsets[c] + 1 + _cellPadding;
                buffer.DrawTextBufferView(_cells[r, c].TextBufferView.Handle, cellX, cellY);
            }
        }
    }

    #endregion

    #region Dispose

    protected override void DestroySelf()
    {
        DisposeCells();
        base.DestroySelf();
    }

    #endregion
}
