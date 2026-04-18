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
    private int[] _columnWidths = [];

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
        set { _wrapMode = value; UpdateCellWrapModes(); RequestRender(); }
    }

    public string ColumnWidthMode
    {
        get => _columnWidthMode;
        set { _columnWidthMode = value; RequestRender(); }
    }

    public string ColumnFitter
    {
        get => _columnFitter;
        set { _columnFitter = value; RequestRender(); }
    }

    public int CellPadding
    {
        get => _cellPadding;
        set { _cellPadding = value; RequestRender(); }
    }

    public bool ShowBorders
    {
        get => _showBorders;
        set { _showBorders = value; RequestRender(); }
    }

    public bool Border
    {
        get => _border;
        set { _border = value; RequestRender(); }
    }

    public bool OuterBorder
    {
        get => _outerBorder;
        set { _outerBorder = value; RequestRender(); }
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

    private void CalculateColumnWidths(int availableWidth)
    {
        if (_cells == null || _colCount == 0)
        {
            _columnWidths = [];
            return;
        }

        int borderOverhead = _showBorders && _border ? (_colCount - 1) : 0;
        if (_showBorders && _outerBorder) borderOverhead += 2;
        int paddingOverhead = _cellPadding * 2 * _colCount;
        int contentWidth = Math.Max(0, availableWidth - borderOverhead - paddingOverhead);

        if (_columnWidthMode == "content")
        {
            // Measure each cell's natural width
            var naturalWidths = new int[_colCount];
            for (int c = 0; c < _colCount; c++)
            {
                int maxW = 1;
                for (int r = 0; r < _rowCount; r++)
                {
                    if (_cells[r, c].TextBufferView.MeasureForDimensions(0, 0, out var measure))
                        maxW = Math.Max(maxW, (int)measure.WidthColsMax);
                }
                naturalWidths[c] = maxW;
            }

            int totalNatural = 0;
            foreach (var w in naturalWidths) totalNatural += w;

            if (totalNatural <= contentWidth)
            {
                _columnWidths = naturalWidths;
            }
            else
            {
                // Proportional shrink
                _columnWidths = new int[_colCount];
                for (int c = 0; c < _colCount; c++)
                    _columnWidths[c] = Math.Max(1, (int)((float)naturalWidths[c] / totalNatural * contentWidth));
            }
        }
        else // "full" mode
        {
            _columnWidths = new int[_colCount];
            if (_columnFitter == "balanced")
            {
                int baseWidth = contentWidth / _colCount;
                int remainder = contentWidth % _colCount;
                for (int c = 0; c < _colCount; c++)
                    _columnWidths[c] = baseWidth + (c < remainder ? 1 : 0);
            }
            else // "proportional"
            {
                // Measure natural widths and distribute proportionally
                var weights = new float[_colCount];
                for (int c = 0; c < _colCount; c++)
                {
                    int maxW = 1;
                    for (int r = 0; r < _rowCount; r++)
                    {
                        if (_cells[r, c].TextBufferView.MeasureForDimensions(0, 0, out var measure))
                            maxW = Math.Max(maxW, (int)measure.WidthColsMax);
                    }
                    weights[c] = Math.Max(1, maxW);
                }

                float totalWeight = 0;
                foreach (var w in weights) totalWeight += w;

                for (int c = 0; c < _colCount; c++)
                    _columnWidths[c] = Math.Max(1, (int)(weights[c] / totalWeight * contentWidth));
            }
        }
    }

    private int[] CalculateRowHeights()
    {
        var heights = new int[_rowCount];
        if (_cells == null) return heights;

        for (int r = 0; r < _rowCount; r++)
        {
            int maxH = 1;
            for (int c = 0; c < _colCount; c++)
            {
                uint colW = (uint)(_columnWidths.Length > c ? _columnWidths[c] : 1);
                if (_cells[r, c].TextBufferView.MeasureForDimensions(colW, 0, out var measure))
                    maxH = Math.Max(maxH, Math.Max(1, (int)measure.LineCount));
            }
            heights[r] = maxH;
        }

        return heights;
    }

    #endregion

    #region Yoga Measure

    private YGSize MeasureFunc(Node node, float availableWidth, MeasureMode widthMode,
        float availableHeight, MeasureMode heightMode)
    {
        if (_cells == null || _rowCount == 0)
            return new YGSize { Width = 0, Height = 0 };

        int width = widthMode == MeasureMode.Undefined || float.IsNaN(availableWidth)
            ? 80 : (int)availableWidth;

        CalculateColumnWidths(width);
        var rowHeights = CalculateRowHeights();

        int totalHeight = 0;
        foreach (var h in rowHeights) totalHeight += h;

        // Add border heights
        if (_showBorders && _border)
            totalHeight += _rowCount - 1; // inner horizontal borders
        if (_showBorders && _outerBorder)
            totalHeight += 2; // top and bottom borders

        float w = width;
        if (widthMode == MeasureMode.AtMost)
            w = Math.Min(w, availableWidth);

        return new YGSize { Width = w, Height = totalHeight };
    }

    #endregion

    #region Rendering

    protected override void RenderSelf(OptimizedBuffer buffer, float deltaTime)
    {
        if (_cells == null || _widthValue == 0 || _heightValue == 0) return;

        // Fill background
        buffer.FillRect((uint)_screenX, (uint)_screenY,
            (uint)_widthValue, (uint)_heightValue, _backgroundColor);

        CalculateColumnWidths(_widthValue);
        var rowHeights = CalculateRowHeights();

        // Draw borders
        if (_showBorders)
            DrawBorders(buffer, rowHeights);

        // Draw cells
        DrawCells(buffer, rowHeights);
    }

    private void DrawBorders(OptimizedBuffer buffer, int[] rowHeights)
    {
        var borderChars = BorderCharacters.ForStyle(_borderStyle);
        int startX = (int)_screenX;
        int startY = (int)_screenY;

        // For now, draw simple border lines. Full grid drawing would use
        // buffer.DrawGrid() when the grid definition format is known.

        if (_outerBorder)
        {
            // Top border
            DrawHorizontalBorder(buffer, startX, startY, _widthValue,
                borderChars.TopLeft, borderChars.TopRight, borderChars.Horizontal,
                _border ? borderChars.TopT : borderChars.Horizontal);

            // Bottom border
            int bottomY = startY + _heightValue - 1;
            DrawHorizontalBorder(buffer, startX, bottomY, _widthValue,
                borderChars.BottomLeft, borderChars.BottomRight, borderChars.Horizontal,
                _border ? borderChars.BottomT : borderChars.Horizontal);

            // Left border
            int y = startY + 1;
            for (int r = 0; r < _rowCount; r++)
            {
                for (int h = 0; h < rowHeights[r]; h++)
                {
                    buffer.SetCell((uint)startX, (uint)y, borderChars.Vertical,
                        _borderColor, _borderBgColor);
                    y++;
                }
                if (_border && r < _rowCount - 1) y++; // skip inner border row
            }

            // Right border
            int rightX = startX + _widthValue - 1;
            y = startY + 1;
            for (int r = 0; r < _rowCount; r++)
            {
                for (int h = 0; h < rowHeights[r]; h++)
                {
                    buffer.SetCell((uint)rightX, (uint)y, borderChars.Vertical,
                        _borderColor, _borderBgColor);
                    y++;
                }
                if (_border && r < _rowCount - 1) y++;
            }
        }

        // Inner horizontal borders
        if (_border)
        {
            int y = (int)_screenY + (_outerBorder ? 1 : 0);
            for (int r = 0; r < _rowCount - 1; r++)
            {
                y += rowHeights[r];
                uint leftChar = _outerBorder ? borderChars.LeftT : borderChars.Horizontal;
                uint rightChar = _outerBorder ? borderChars.RightT : borderChars.Horizontal;
                DrawHorizontalBorder(buffer, startX, y, _widthValue,
                    leftChar, rightChar, borderChars.Horizontal, borderChars.Cross);
                y++;
            }
        }
    }

    private void DrawHorizontalBorder(OptimizedBuffer buffer, int x, int y, int width,
        uint leftChar, uint rightChar, uint fillChar, uint crossChar)
    {
        if (width <= 0) return;

        buffer.SetCell((uint)x, (uint)y, leftChar, _borderColor, _borderBgColor);

        int pos = 1;
        for (int c = 0; c < _colCount; c++)
        {
            int colW = (_columnWidths.Length > c ? _columnWidths[c] : 1) + _cellPadding * 2;
            for (int i = 0; i < colW && pos < width - 1; i++)
            {
                buffer.SetCell((uint)(x + pos), (uint)y, fillChar, _borderColor, _borderBgColor);
                pos++;
            }
            if (_border && c < _colCount - 1 && pos < width - 1)
            {
                buffer.SetCell((uint)(x + pos), (uint)y, crossChar, _borderColor, _borderBgColor);
                pos++;
            }
        }

        if (pos < width)
            buffer.SetCell((uint)(x + width - 1), (uint)y, rightChar, _borderColor, _borderBgColor);
    }

    private void DrawCells(OptimizedBuffer buffer, int[] rowHeights)
    {
        if (_cells == null) return;

        int startX = (int)_screenX + (_outerBorder ? 1 : 0);
        int cellY = (int)_screenY + (_outerBorder ? 1 : 0);

        for (int r = 0; r < _rowCount; r++)
        {
            int cellX = startX;
            for (int c = 0; c < _colCount; c++)
            {
                int colW = _columnWidths.Length > c ? _columnWidths[c] : 1;
                int padX = cellX + _cellPadding;

                // Set viewport for this cell
                _cells[r, c].TextBufferView.SetViewport(0, 0, (uint)colW, (uint)rowHeights[r]);

                // Draw cell content
                buffer.DrawTextBufferView(_cells[r, c].TextBufferView.Handle, padX, cellY);

                cellX += colW + _cellPadding * 2;
                if (_border && c < _colCount - 1) cellX++; // border column
            }

            cellY += rowHeights[r];
            if (_border && r < _rowCount - 1) cellY++; // border row
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
