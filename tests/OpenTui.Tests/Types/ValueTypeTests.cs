using Xunit;

namespace OpenTui.Tests.Types;

public class ValueTypeTests
{
    [Fact]
    public void LogicalCursor_RecordEquality()
    {
        var a = new LogicalCursor(1, 2, 3);
        var b = new LogicalCursor(1, 2, 3);
        Assert.Equal(a, b);
    }

    [Fact]
    public void VisualCursor_Properties()
    {
        var cursor = new VisualCursor(10, 5, 3, 2, 42);
        Assert.Equal(10u, cursor.VisualRow);
        Assert.Equal(5u, cursor.VisualCol);
        Assert.Equal(3u, cursor.LogicalRow);
        Assert.Equal(42u, cursor.Offset);
    }

    [Fact]
    public void MeasureResult_Properties()
    {
        var result = new MeasureResult(100, 80);
        Assert.Equal(100u, result.LineCount);
        Assert.Equal(80u, result.WidthColsMax);
    }

    [Fact]
    public void ViewportBounds_Properties()
    {
        var bounds = new ViewportBounds(10, 20, 80, 24);
        Assert.Equal(10, bounds.X);
        Assert.Equal(20, bounds.Y);
        Assert.Equal(80, bounds.Width);
        Assert.Equal(24, bounds.Height);
    }

    [Fact]
    public void SpanFeedOptions_Defaults()
    {
        var opts = new SpanFeedOptions();
        Assert.Equal(64u * 1024, opts.ChunkSize);
        Assert.Equal(2u, opts.InitialChunks);
        Assert.Equal(0UL, opts.MaxBytes);
        Assert.Equal(GrowthPolicy.Grow, opts.GrowthPolicy);
        Assert.True(opts.AutoCommitOnFull);
    }

    [Fact]
    public void SpanFeedStats_RecordEquality()
    {
        var a = new SpanFeedStats(100, 50, 4, 10);
        var b = new SpanFeedStats(100, 50, 4, 10);
        Assert.Equal(a, b);
    }

    [Fact]
    public void SelectOption_Properties()
    {
        var opt = new SelectOption<int>("Item 1", 42, "Description");
        Assert.Equal("Item 1", opt.Label);
        Assert.Equal(42, opt.Value);
        Assert.Equal("Description", opt.Description);
    }

    [Fact]
    public void SelectOption_NullDescription()
    {
        var opt = new SelectOption<string>("Item", "value");
        Assert.Null(opt.Description);
    }

    [Fact]
    public void CursorState_ColorProperty()
    {
        var state = new CursorState
        {
            X = 10,
            Y = 5,
            Visible = true,
            Style = (byte)CursorStyle.Block,
            Blinking = false,
            R = 1f,
            G = 0.5f,
            B = 0f,
            A = 1f,
        };
        Assert.Equal(new Rgba(1f, 0.5f, 0f, 1f), state.Color);
        Assert.Equal(CursorStyle.Block, state.CursorStyle);
    }
}
