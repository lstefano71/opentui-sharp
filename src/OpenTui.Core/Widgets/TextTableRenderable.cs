using Facebook.Yoga;

namespace OpenTui.Core;

/// <summary>
/// Options for TextTable renderable.
/// Matches TypeScript TextTableOptions.
/// </summary>
public class TextTableOptions : RenderableOptions
{
    /// <summary>
    /// Gets or sets the content.
    /// </summary>
    public TextChunk[][][] Content { get; init; } = [];
    /// <summary>
    /// Gets or sets the wrap mode.
    /// </summary>
    public byte WrapMode { get; init; } = 2; // 0=none, 1=char, 2=word
    /// <summary>
    /// Gets or sets the column width mode.
    /// </summary>
    public string ColumnWidthMode { get; init; } = "full"; // "content" | "full"
    /// <summary>
    /// Gets or sets the column fitter.
    /// </summary>
    public string ColumnFitter { get; init; } = "proportional"; // "proportional" | "balanced"
    /// <summary>
    /// Gets or sets the cell padding.
    /// </summary>
    public int CellPadding { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether show borders.
    /// </summary>
    public bool ShowBorders { get; init; } = true;
    /// <summary>
    /// Gets or sets the border.
    /// </summary>
    public bool Border { get; init; } = true;
    /// <summary>
    /// Gets or sets the outer border.
    /// </summary>
    public bool OuterBorder { get; init; } = true;
    /// <summary>
    /// Gets or sets the selectable.
    /// </summary>
    public bool Selectable { get; init; } = true;
    /// <summary>
    /// Gets or sets the selection bg.
    /// </summary>
    public Rgba? SelectionBg { get; init; }
    /// <summary>
    /// Gets or sets the selection fg.
    /// </summary>
    public Rgba? SelectionFg { get; init; }
    /// <summary>
    /// Gets or sets the border style.
    /// </summary>
    public BorderStyle BorderStyle { get; init; } = BorderStyle.Single;
    /// <summary>
    /// Gets or sets the border color.
    /// </summary>
    public Rgba? BorderColor { get; init; }
    /// <summary>
    /// Gets or sets the border background color.
    /// </summary>
    public Rgba? BorderBackgroundColor { get; init; }
    /// <summary>
    /// Gets or sets the background color.
    /// </summary>
    public Rgba? BackgroundColor { get; init; }
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
}

/// <summary>
/// Table layout with columns, rows, headers, and styled cells.
/// Manages a grid of TextBuffer/TextBufferView objects internally.
/// Matches TypeScript TextTableRenderable from TextTable.ts.
/// </summary>
public class TextTableRenderable : Renderable
{
    private readonly record struct CellPosition(int RowIdx, int ColIdx);
    private readonly record struct CellSelectionCoords(int AnchorX, int AnchorY, int FocusX, int FocusY);
    private readonly record struct SelectionResolution(TableSelectionMode Mode, CellPosition? AnchorCell, int? AnchorColumn);

    private enum TableSelectionMode
    {
        SingleCell,
        ColumnLocked,
        Grid,
    }

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
    private Rgba? _selectionBg;
    private Rgba? _selectionFg;
    private LocalSelectionBounds? _lastLocalSelection;
    private TableSelectionMode? _lastSelectionMode;

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
        public SyntaxStyle SyntaxStyle { get; }

        public CellState(WidthMethod widthMethod)
        {
            TextBuffer = TextBuffer.Create(widthMethod);
            SyntaxStyle = SyntaxStyle.Create();
            TextBuffer.SetSyntaxStyle(SyntaxStyle.Handle);
            TextBufferView = TextBufferView.Create(TextBuffer);
        }

        public void Dispose()
        {
            TextBufferView.Dispose();
            TextBuffer.Dispose();
            SyntaxStyle.Dispose();
        }
    }

    /// <summary>
    /// Initializes a new instance of the TextTableRenderable class.
    /// </summary>
    /// <param name="ctx">The render context.</param>
    /// <param name="options">The configuration options.</param>
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
        _selectionBg = options.SelectionBg;
        _selectionFg = options.SelectionFg;

        Selectable = options.Selectable;
        YGNodeAPI.YGNodeSetMeasureFunc(YogaNode, MeasureFunc);
        RebuildCells();
    }

    #region Properties

    /// <summary>
    /// Gets or sets the content.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the wrap mode.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the column width mode.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the column fitter.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the cell padding.
    /// </summary>
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

    /// <summary>
    /// Gets or sets a value indicating whether show borders.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the border.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the outer border.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the table border style.
    /// </summary>
    public BorderStyle TableBorderStyle
    {
        get => _borderStyle;
        set { _borderStyle = value; RequestRender(); }
    }

    /// <summary>
    /// Gets or sets the border color.
    /// </summary>
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

    #region Selection

    /// <inheritdoc />
    public override bool ShouldStartSelection(int x, int y)
    {
        if (!Selectable || _cells is null || _rowCount == 0 || _colCount == 0 || Width <= 0 || Height <= 0)
            return false;

        var layout = GetSelectionLayout();
        int localX = x - X;
        int localY = y - Y;
        return GetCellAtLocalPosition(layout, localX, localY).HasValue;
    }

    /// <inheritdoc />
    public override bool OnSelectionChanged(Selection? selection)
    {
        bool hadSelection = HasSelection();
        var localSelection = SelectionHelpers.ConvertGlobalToLocalSelection(selection, X, Y);
        _lastLocalSelection = localSelection;

        if (localSelection is not { IsActive: true } activeSelection ||
            _cells is null ||
            _rowCount == 0 ||
            _colCount == 0 ||
            Width <= 0 ||
            Height <= 0)
        {
            ResetCellSelections();
            _lastSelectionMode = null;

            if (hadSelection)
                RequestRender();

            return false;
        }

        var layout = GetSelectionLayout();
        ApplySelectionToCells(layout, activeSelection, selection?.IsStart == true);

        bool hasSelection = HasSelection();
        if (hadSelection || hasSelection || selection?.IsActive == true)
            RequestRender();

        return hasSelection;
    }

    /// <inheritdoc />
    public override bool HasSelection()
    {
        if (_cells is null)
            return false;

        for (int r = 0; r < _rowCount; r++)
        {
            for (int c = 0; c < _colCount; c++)
            {
                if (_cells[r, c].TextBufferView.HasSelection())
                    return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public override string GetSelectedText()
    {
        if (_cells is null)
            return "";

        var selectedRows = new List<string>();

        for (int rowIdx = 0; rowIdx < _rowCount; rowIdx++)
        {
            var rowSelections = new List<string>();

            for (int colIdx = 0; colIdx < _colCount; colIdx++)
            {
                var cell = _cells[rowIdx, colIdx];
                if (!cell.TextBufferView.HasSelection())
                    continue;

                string selectedText = cell.TextBufferView.GetSelectedText();
                if (selectedText.Length > 0)
                    rowSelections.Add(selectedText);
            }

            if (rowSelections.Count > 0)
                selectedRows.Add(string.Join("\t", rowSelections));
        }

        return string.Join("\n", selectedRows);
    }

    private TableLayout GetSelectionLayout()
    {
        int? maxTableWidth = _widthValue > 0 ? _widthValue : null;
        var layout = ComputeLayout(maxTableWidth);
        ApplyLayoutToViews(layout);
        return layout;
    }

    private CellPosition? GetCellAtLocalPosition(TableLayout layout, int localX, int localY)
    {
        if (_rowCount == 0 || _colCount == 0 || localX < 0 || localY < 0 ||
            localX >= layout.TableWidth || localY >= layout.TableHeight)
        {
            return null;
        }

        int rowIdx = -1;
        for (int idx = 0; idx < _rowCount; idx++)
        {
            int top = layout.RowOffsets[idx] + 1;
            int bottom = top + layout.RowHeights[idx] - 1;
            if (localY >= top && localY <= bottom)
            {
                rowIdx = idx;
                break;
            }
        }

        if (rowIdx < 0)
            return null;

        int colIdx = -1;
        for (int idx = 0; idx < _colCount; idx++)
        {
            int left = layout.ColumnOffsets[idx] + 1;
            int right = left + layout.ColumnWidths[idx] - 1;
            if (localX >= left && localX <= right)
            {
                colIdx = idx;
                break;
            }
        }

        return colIdx >= 0 ? new CellPosition(rowIdx, colIdx) : null;
    }

    private void ApplySelectionToCells(TableLayout layout, LocalSelectionBounds localSelection, bool isStart)
    {
        if (_cells is null)
            return;

        int minSelY = Math.Min(localSelection.AnchorY, localSelection.FocusY);
        int maxSelY = Math.Max(localSelection.AnchorY, localSelection.FocusY);
        int firstRow = FindRowForLocalY(layout, minSelY);
        int lastRow = FindRowForLocalY(layout, maxSelY);
        var selection = ResolveSelectionResolution(layout, localSelection);
        bool modeChanged = _lastSelectionMode != selection.Mode;
        _lastSelectionMode = selection.Mode;
        bool lockToAnchorColumn = selection.Mode == TableSelectionMode.ColumnLocked && selection.AnchorColumn.HasValue;

        for (int rowIdx = 0; rowIdx < _rowCount; rowIdx++)
        {
            if (rowIdx < firstRow || rowIdx > lastRow)
            {
                ResetRowSelection(rowIdx);
                continue;
            }

            int cellTop = layout.RowOffsets[rowIdx] + 1 + _cellPadding;

            for (int colIdx = 0; colIdx < _colCount; colIdx++)
            {
                var cell = _cells[rowIdx, colIdx];

                if (lockToAnchorColumn && colIdx != selection.AnchorColumn)
                {
                    cell.TextBufferView.ResetLocalSelection();
                    continue;
                }

                int cellLeft = layout.ColumnOffsets[colIdx] + 1 + _cellPadding;
                var coords = new CellSelectionCoords(
                    localSelection.AnchorX - cellLeft,
                    localSelection.AnchorY - cellTop,
                    localSelection.FocusX - cellLeft,
                    localSelection.FocusY - cellTop);

                bool isAnchorCell = selection.AnchorCell is { } anchorCell &&
                    anchorCell.RowIdx == rowIdx &&
                    anchorCell.ColIdx == colIdx;
                bool forceSet = isAnchorCell && selection.Mode != TableSelectionMode.SingleCell;

                if (forceSet)
                    coords = GetFullCellSelectionCoords(layout, rowIdx, colIdx);

                bool shouldUseSet = isStart || modeChanged || forceSet;
                if (shouldUseSet)
                {
                    cell.TextBufferView.SetLocalSelection(
                        coords.AnchorX,
                        coords.AnchorY,
                        coords.FocusX,
                        coords.FocusY,
                        _selectionFg,
                        _selectionBg);
                }
                else
                {
                    cell.TextBufferView.UpdateLocalSelection(
                        coords.AnchorX,
                        coords.AnchorY,
                        coords.FocusX,
                        coords.FocusY,
                        _selectionFg,
                        _selectionBg);
                }
            }
        }
    }

    private SelectionResolution ResolveSelectionResolution(TableLayout layout, LocalSelectionBounds localSelection)
    {
        CellPosition? anchorCell = GetCellAtLocalPosition(layout, localSelection.AnchorX, localSelection.AnchorY);
        CellPosition? focusCell = GetCellAtLocalPosition(layout, localSelection.FocusX, localSelection.FocusY);
        int? anchorColumn = anchorCell?.ColIdx ?? GetColumnAtLocalX(layout, localSelection.AnchorX);

        if (anchorCell is { } anchor &&
            focusCell is { } focus &&
            anchor.RowIdx == focus.RowIdx &&
            anchor.ColIdx == focus.ColIdx)
        {
            return new SelectionResolution(TableSelectionMode.SingleCell, anchorCell, anchorColumn);
        }

        int? focusColumn = GetColumnAtLocalX(layout, localSelection.FocusX);
        if (anchorColumn.HasValue && focusColumn == anchorColumn)
            return new SelectionResolution(TableSelectionMode.ColumnLocked, anchorCell, anchorColumn);

        return new SelectionResolution(TableSelectionMode.Grid, anchorCell, anchorColumn);
    }

    private int? GetColumnAtLocalX(TableLayout layout, int localX)
    {
        if (_colCount == 0 || localX < 0 || localX >= layout.TableWidth)
            return null;

        for (int colIdx = 0; colIdx < _colCount; colIdx++)
        {
            int colStart = layout.ColumnOffsets[colIdx] + 1;
            int colEnd = colStart + layout.ColumnWidths[colIdx] - 1;
            if (localX >= colStart && localX <= colEnd)
                return colIdx;
        }

        return null;
    }

    private CellSelectionCoords GetFullCellSelectionCoords(TableLayout layout, int rowIdx, int colIdx)
    {
        int colWidth = layout.ColumnWidths[colIdx];
        int rowHeight = layout.RowHeights[rowIdx];
        int contentWidth = Math.Max(1, colWidth - GetHorizontalCellPadding());
        int contentHeight = Math.Max(1, rowHeight - GetVerticalCellPadding());

        return new CellSelectionCoords(
            AnchorX: -1,
            AnchorY: 0,
            FocusX: contentWidth,
            FocusY: contentHeight);
    }

    private int FindRowForLocalY(TableLayout layout, int localY)
    {
        if (_rowCount == 0 || localY < 0)
            return 0;

        for (int rowIdx = 0; rowIdx < _rowCount; rowIdx++)
        {
            int rowStart = layout.RowOffsets[rowIdx] + 1;
            int rowEnd = rowStart + layout.RowHeights[rowIdx] - 1;
            if (localY <= rowEnd)
                return rowIdx;
        }

        return _rowCount - 1;
    }

    private void ResetRowSelection(int rowIdx)
    {
        if (_cells is null)
            return;

        for (int colIdx = 0; colIdx < _colCount; colIdx++)
            _cells[rowIdx, colIdx].TextBufferView.ResetLocalSelection();
    }

    private void ResetCellSelections()
    {
        if (_cells is null)
            return;

        for (int rowIdx = 0; rowIdx < _rowCount; rowIdx++)
            ResetRowSelection(rowIdx);
    }

    #endregion

    #region Rendering

    /// <inheritdoc />
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
        if (_lastLocalSelection is { IsActive: true } activeSelection)
            ApplySelectionToCells(layout, activeSelection, isStart: true);

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

    /// <inheritdoc />
    protected override void DestroySelf()
    {
        DisposeCells();
        base.DestroySelf();
    }

    #endregion
}
