using Xunit;

namespace OpenTui.Core.Tests;

public sealed class TextTableRenderableTests : IDisposable
{
    private readonly CliRenderer _renderer;

    public TextTableRenderableTests()
    {
        _renderer = CliRenderer.Create(new CliRendererConfig
        {
            Testing = true,
            Width = 40,
            Height = 12,
        });
    }

    public void Dispose() => _renderer.Dispose();

    private void RenderFrame()
    {
        _renderer.RenderTestFrame();
        _renderer.RenderTestFrame();
    }

    private static uint ReadCellChar(OptimizedBuffer buf, uint x, uint y) =>
        buf.GetCharAt(x, y);

    private static Rgba ReadCellFg(OptimizedBuffer buf, uint x, uint y) =>
        buf.GetFgAt(x, y);

    private static Rgba ReadCellBg(OptimizedBuffer buf, uint x, uint y) =>
        buf.GetBgAt(x, y);

    private static string ReadRowText(OptimizedBuffer buf, int x, int y, int width)
    {
        var chars = new char[width];
        for (int i = 0; i < width; i++)
        {
            uint codepoint = ReadCellChar(buf, (uint)(x + i), (uint)y);
            chars[i] = codepoint is > 0 and <= char.MaxValue ? (char)codepoint : ' ';
        }

        return new string(chars).TrimEnd();
    }

    private string ReadRenderableRow(Renderable renderable, int row) =>
        ReadRowText(_renderer.NextRenderBuffer, (int)renderable.ScreenX, (int)renderable.ScreenY + row, renderable.Width);

    private static int[] GetColumnWidthsFromRow(string row) =>
        row.Select((ch, idx) => (ch, idx))
            .Where(entry => entry.ch == '│')
            .Select(entry => entry.idx)
            .Zip(row.Select((ch, idx) => (ch, idx)).Where(entry => entry.ch == '│').Select(entry => entry.idx).Skip(1),
                (left, right) => right - left - 1)
            .ToArray();

    [Fact]
    public void FullWidthTable_DrawsContinuousNativeGrid()
    {
        var table = new TextTableRenderable(_renderer, new TextTableOptions
        {
            Width = DimensionValue.Point(32),
            ColumnWidthMode = "full",
            ColumnFitter = "proportional",
            ShowBorders = true,
            Border = true,
            OuterBorder = true,
            Content =
            [
                [[TextChunk.Plain("Name")], [TextChunk.Plain("Status")], [TextChunk.Plain("Duration")]],
                [[TextChunk.Plain("api-gateway")], [TextChunk.Plain("OK")], [TextChunk.Plain("12ms")]],
                [[TextChunk.Plain("db-primary")], [TextChunk.Plain("ERR")], [TextChunk.Plain("timeout")]],
            ],
        });

        _renderer.Root.Add(table);
        RenderFrame();

        string topRow = ReadRenderableRow(table, 0);
        string headerRow = ReadRenderableRow(table, 1);
        string separatorRow = ReadRenderableRow(table, 2);
        string firstDataRow = ReadRenderableRow(table, 3);
        string bottomRow = ReadRenderableRow(table, table.Height - 1);

        Assert.Contains('┬', topRow);
        Assert.EndsWith("┐", topRow);
        Assert.DoesNotContain(' ', topRow);

        Assert.Equal(4, headerRow.Count(ch => ch == '│'));
        Assert.Contains("Name", headerRow);
        Assert.Contains("Status", headerRow);
        Assert.Contains("Duration", headerRow);

        Assert.Contains('┼', separatorRow);
        Assert.EndsWith("┤", separatorRow);
        Assert.DoesNotContain(' ', separatorRow);

        Assert.Equal(4, firstDataRow.Count(ch => ch == '│'));
        Assert.Contains("api-gateway", firstDataRow);
        Assert.Contains("OK", firstDataRow);

        Assert.Contains('┴', bottomRow);
        Assert.EndsWith("┘", bottomRow);
        Assert.DoesNotContain(' ', bottomRow);
    }

    [Fact]
    public void ConstrainedFullWidthTable_BalancedFitterDiffersFromProportional()
    {
        var table = new TextTableRenderable(_renderer, new TextTableOptions
        {
            Width = DimensionValue.Point(60),
            WrapMode = 2,
            ColumnWidthMode = "full",
            ColumnFitter = "proportional",
            ShowBorders = true,
            Border = true,
            OuterBorder = true,
            Content =
            [
                [
                    [TextChunk.Plain("Provider")],
                    [TextChunk.Plain("Compute Services")],
                    [TextChunk.Plain("Storage Solutions")],
                    [TextChunk.Plain("Pricing Model")],
                    [TextChunk.Plain("Regions")],
                    [TextChunk.Plain("Use Cases")],
                ],
                [
                    [TextChunk.Plain("Amazon Web Services")],
                    [TextChunk.Plain("EC2 instances with extensive options for general, memory, and accelerated workloads")],
                    [TextChunk.Plain("S3 tiers, EBS, EFS, and archive classes for long retention")],
                    [TextChunk.Plain("Pay as you go, reserved terms, and discounted spot capacity")],
                    [TextChunk.Plain("Global regions and many edge locations")],
                    [TextChunk.Plain("Enterprise migration, analytics, ML, and backend services")],
                ],
            ],
        });

        _renderer.Root.Add(table);
        RenderFrame();

        string proportionalHeaderRow = ReadRenderableRow(table, 1);
        int[] proportionalWidths = GetColumnWidthsFromRow(proportionalHeaderRow);
        int proportionalSpread = proportionalWidths.Max() - proportionalWidths.Min();

        table.ColumnFitter = "balanced";
        RenderFrame();

        string balancedHeaderRow = ReadRenderableRow(table, 1);
        int[] balancedWidths = GetColumnWidthsFromRow(balancedHeaderRow);
        int balancedSpread = balancedWidths.Max() - balancedWidths.Min();

        Assert.NotEqual(proportionalHeaderRow, balancedHeaderRow);
        Assert.True(balancedWidths[0] > proportionalWidths[0]);
        Assert.True(balancedSpread < proportionalSpread);
    }

    [Fact]
    public void Selection_DragWithinCell_UpdatesHighlightAndSelectedText()
    {
        var baseBg = Rgba.FromHex("#101820");
        var baseFg = Rgba.FromHex("#f59e0b");
        var selectionBg = Rgba.FromHex("#4a5568");
        var selectionFg = Rgba.FromHex("#ffffff");

        var table = new TextTableRenderable(_renderer, new TextTableOptions
        {
            Width = DimensionValue.Point(18),
            Bg = baseBg,
            Fg = baseFg,
            SelectionBg = selectionBg,
            SelectionFg = selectionFg,
            Content =
            [
                [[TextChunk.Plain("ALPHA BETA")]],
            ],
        });

        _renderer.Root.Add(table);
        RenderFrame();

        int startX = table.X + 1;
        int startY = table.Y + 1;
        int endX = table.X + 6;

        Assert.True(table.ShouldStartSelection(startX, startY));

        var selection = new Selection(table, startX, startY);
        selection.UpdateFocus(endX, startY);

        Assert.True(table.OnSelectionChanged(selection));
        RenderFrame();

        Assert.True(table.HasSelection());
        Assert.Equal("ALPHA", table.GetSelectedText());
        Assert.NotEqual(baseFg, ReadCellFg(_renderer.NextRenderBuffer, (uint)startX, (uint)startY));
        Assert.NotEqual(baseBg, ReadCellBg(_renderer.NextRenderBuffer, (uint)startX, (uint)startY));
    }

    [Fact]
    public void StyledCell_PreservesChunkForegroundColor()
    {
        var tableFg = Rgba.FromHex("#f8fafc");
        var statusFg = Rgba.FromHex("#22c55e");

        var table = new TextTableRenderable(_renderer, new TextTableOptions
        {
            Width = DimensionValue.Point(6),
            Fg = tableFg,
            ShowBorders = false,
            Border = false,
            OuterBorder = false,
            Content =
            [
                [[TextChunk.Styled("OK", fg: statusFg)]],
            ],
        });

        _renderer.Root.Add(table);
        RenderFrame();

        uint x = (uint)table.X;
        uint y = (uint)table.Y;

        Assert.Equal((uint)'O', ReadCellChar(_renderer.NextRenderBuffer, x, y));
        Assert.Equal(statusFg, ReadCellFg(_renderer.NextRenderBuffer, x, y));
    }
}
