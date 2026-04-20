using Xunit;
using OpenTui.Core;

namespace OpenTui.Core.Tests;

/// <summary>
/// C# xUnit equivalents of Zig word-wrap editing and wrap-cache performance tests:
///   - word-wrap-editing_test.zig (14 tests)
///   - wrap-cache-perf_test.zig (2 tests)
///
/// Tests use EditBuffer + TextBufferView wrappers to verify word-wrap behaviour
/// after edits, matching the Zig tests exactly.
/// </summary>
public class WordWrapEditingTests
{
    // =====================================================================
    // WORD WRAP EDITING TESTS (from word-wrap-editing_test.zig)
    // =====================================================================

    [Fact]
    public void EditingAroundWrapBoundary_CreatesCorrectWrap()
    {
        // Zig: "Word wrap - editing around wrap boundary creates correct wrap"
        using var eb = EditBuffer.Create();
        using var view = TextBufferView.CreateFrom(eb);

        view.SetWrapMode(WrapMode.Word);
        view.SetWrapWidth(18);

        eb.SetText("hello my good");

        var info = view.GetLineInfo();
        Assert.Single(info.LineWidthCols);

        eb.SetCursor(0, 13);
        eb.InsertText(" friend");

        info = view.GetLineInfo();
        Assert.Equal(2, info.LineWidthCols.Length);
        Assert.Equal(14u, info.LineWidthCols[0]);
        Assert.Equal(6u, info.LineWidthCols[1]);
    }

    [Fact]
    public void BackspaceAndRetype_NearBoundary()
    {
        // Zig: "Word wrap - backspace and retype near boundary"
        using var eb = EditBuffer.Create();
        using var view = TextBufferView.CreateFrom(eb);

        view.SetWrapMode(WrapMode.Word);
        view.SetWrapWidth(18);

        eb.SetText("hello my good friend");

        var info = view.GetLineInfo();
        Assert.Equal(2, info.LineWidthCols.Length);

        eb.SetCursor(0, 20);
        for (int i = 0; i < 7; i++)
            eb.DeleteCharBackward();

        info = view.GetLineInfo();
        Assert.Single(info.LineWidthCols);

        eb.InsertText(" friend");

        info = view.GetLineInfo();
        Assert.Equal(2, info.LineWidthCols.Length);
        Assert.Equal(14u, info.LineWidthCols[0]);
        Assert.Equal(6u, info.LineWidthCols[1]);
    }

    [Fact]
    public void TypeCharByChar_NearBoundary()
    {
        // Zig: "Word wrap - type character by character near boundary"
        using var eb = EditBuffer.Create();
        using var view = TextBufferView.CreateFrom(eb);

        view.SetWrapMode(WrapMode.Word);
        view.SetWrapWidth(18);

        eb.SetText("hello my good ");
        Assert.Equal(1, view.GetLineInfo().LineWidthCols.Length);

        eb.SetCursor(0, 14);

        eb.InsertText("f");
        Assert.Equal(1, view.GetLineInfo().LineWidthCols.Length);

        eb.InsertText("r");
        Assert.Equal(1, view.GetLineInfo().LineWidthCols.Length);

        eb.InsertText("i");
        Assert.Equal(1, view.GetLineInfo().LineWidthCols.Length);

        eb.InsertText("e");
        Assert.Equal(1, view.GetLineInfo().LineWidthCols.Length);

        eb.InsertText("n");
        Assert.Equal(2, view.GetLineInfo().LineWidthCols.Length);

        eb.InsertText("d");
        Assert.Equal(2, view.GetLineInfo().LineWidthCols.Length);

        eb.InsertText(" ");
        var info = view.GetLineInfo();
        Assert.Equal(2, info.LineWidthCols.Length);
        Assert.Equal(14u, info.LineWidthCols[0]);
        Assert.Equal(7u, info.LineWidthCols[1]);
    }

    [Fact]
    public void InsertWordInMiddle_CausesRewrap()
    {
        // Zig: "Word wrap - insert word in middle causes rewrap"
        using var eb = EditBuffer.Create();
        using var view = TextBufferView.CreateFrom(eb);

        view.SetWrapMode(WrapMode.Word);
        view.SetWrapWidth(20);

        eb.SetText("hello friend");
        Assert.Equal(1, view.GetLineInfo().LineWidthCols.Length);

        eb.SetCursor(0, 6);
        eb.InsertText("my good ");

        Assert.Equal(1, view.GetLineInfo().LineWidthCols.Length);
    }

    [Fact]
    public void DeleteWord_CausesRewrap()
    {
        // Zig: "Word wrap - delete word causes rewrap"
        using var eb = EditBuffer.Create();
        using var view = TextBufferView.CreateFrom(eb);

        view.SetWrapMode(WrapMode.Word);
        view.SetWrapWidth(18);

        eb.SetText("hello my good friend buddy");

        var info = view.GetLineInfo();
        Assert.True(info.LineWidthCols.Length >= 2);

        eb.SetCursor(0, 6);
        for (int i = 0; i < 8; i++)
            eb.DeleteChar();

        info = view.GetLineInfo();
        Assert.Equal(1, info.LineWidthCols.Length);
    }

    [Fact]
    public void RapidEdits_MaintainCorrectWrapping()
    {
        // Zig: "Word wrap - rapid edits maintain correct wrapping"
        using var eb = EditBuffer.Create();
        using var view = TextBufferView.CreateFrom(eb);

        view.SetWrapMode(WrapMode.Word);
        view.SetWrapWidth(18);

        eb.SetText("hello my ");
        eb.SetCursor(0, 9);
        foreach (var ch in "good friend")
            eb.InsertText(ch.ToString());

        var info = view.GetLineInfo();
        Assert.Equal(2, info.LineWidthCols.Length);
        Assert.Equal(14u, info.LineWidthCols[0]);
        Assert.Equal(6u, info.LineWidthCols[1]);
    }

    [Fact]
    public void FragmentedAtExactWordBoundary()
    {
        // Zig: "Word wrap - fragmented at exact word boundary"
        using var eb = EditBuffer.Create();
        using var view = TextBufferView.CreateFrom(eb);

        view.SetWrapMode(WrapMode.Word);
        view.SetWrapWidth(18);

        eb.SetText("hello ");
        eb.SetCursor(0, 6);
        eb.InsertText("my ");
        eb.InsertText("good ");
        eb.InsertText("friend");

        var info = view.GetLineInfo();
        Assert.Equal(2, info.LineWidthCols.Length);
        Assert.Equal(14u, info.LineWidthCols[0]);
        Assert.Equal(6u, info.LineWidthCols[1]);
    }

    [Fact]
    public void StaleRollbackState_AfterNewlineWithEdits()
    {
        // Zig: "Word wrap - stale rollback state after newline with EditBuffer inserts"
        using var eb = EditBuffer.Create();
        using var view = TextBufferView.CreateFrom(eb);

        view.SetWrapMode(WrapMode.Word);
        view.SetWrapWidth(3);

        eb.SetText("a\n好");

        eb.SetCursor(0, 1);
        eb.InsertText(" b");

        eb.SetCursor(1, 2);
        eb.InsertText("界");

        var plainText = view.GetPlainText();
        Assert.Equal("a b\n好界", plainText);

        var info = view.GetLineInfo();
        Assert.Equal(3, info.LineWidthCols.Length);
        Assert.Equal(3u, info.LineWidthCols[0]);
        Assert.Equal(2u, info.LineWidthCols[1]);
        Assert.Equal(2u, info.LineWidthCols[2]);
        Assert.Equal(0u, info.LineSources[0]);
        Assert.Equal(1u, info.LineSources[1]);
        Assert.Equal(1u, info.LineSources[2]);
    }

    [Fact]
    public void ChunkBoundary_AtStartOfWord()
    {
        // Zig: "Word wrap - chunk boundary at start of word"
        using var eb = EditBuffer.Create();
        using var view = TextBufferView.CreateFrom(eb);

        view.SetWrapMode(WrapMode.Word);
        view.SetWrapWidth(18);

        eb.SetText("hello my good ");
        eb.SetCursor(0, 14);

        eb.InsertText("f");
        eb.DeleteCharBackward();
        eb.InsertText("friend");

        var info = view.GetLineInfo();
        Assert.Equal(2, info.LineWidthCols.Length);
        Assert.Equal(14u, info.LineWidthCols[0]);
        Assert.Equal(6u, info.LineWidthCols[1]);
    }

    [Fact]
    public void MultipleEdits_CreateComplexFragmentation()
    {
        // Zig: "Word wrap - multiple edits create complex fragmentation"
        using var eb = EditBuffer.Create();
        using var view = TextBufferView.CreateFrom(eb);

        view.SetWrapMode(WrapMode.Word);
        view.SetWrapWidth(20);

        eb.SetText("hello ");
        eb.SetCursor(0, 6);
        eb.InsertText("w");
        eb.DeleteCharBackward();
        eb.InsertText("m");
        eb.InsertText("y");
        eb.InsertText(" ");
        eb.InsertText("g");
        eb.InsertText("o");
        eb.DeleteCharBackward();
        eb.InsertText("o");
        eb.InsertText("o");
        eb.InsertText("d");
        eb.InsertText(" ");
        eb.InsertText("x");
        eb.DeleteCharBackward();
        eb.InsertText("f");
        eb.InsertText("r");
        eb.InsertText("iend");

        var text = eb.GetText();
        Assert.Equal("hello my good friend", text);

        var info = view.GetLineInfo();
        Assert.Equal(1, info.LineWidthCols.Length);
    }

    [Fact]
    public void InsertAtWrapBoundary_WithExistingWrap()
    {
        // Zig: "Word wrap - insert at wrap boundary with existing wrap"
        using var eb = EditBuffer.Create();
        using var view = TextBufferView.CreateFrom(eb);

        view.SetWrapMode(WrapMode.Word);
        view.SetWrapWidth(15);

        eb.SetText("hello world test");

        var info = view.GetLineInfo();
        Assert.True(info.LineWidthCols.Length >= 2);

        eb.SetCursor(0, 11);
        eb.InsertText("s");

        info = view.GetLineInfo();
        Assert.True(info.LineWidthCols.Length >= 2);

        for (int i = 0; i < info.LineWidthCols.Length; i++)
            Assert.True(info.LineWidthCols[i] <= 15);
    }

    [Fact]
    public void WordAtExactWrapWidth()
    {
        // Zig: "Word wrap - word at exact wrap width"
        using var eb = EditBuffer.Create();
        using var view = TextBufferView.CreateFrom(eb);

        view.SetWrapMode(WrapMode.Word);
        view.SetWrapWidth(20);

        eb.SetText("12345678901234567890");
        Assert.Equal(1, view.GetLineInfo().LineWidthCols.Length);

        eb.SetCursor(0, 20);
        eb.InsertText(" word");

        var info = view.GetLineInfo();
        Assert.Equal(2, info.LineWidthCols.Length);
        Assert.Equal(20u, info.LineWidthCols[0]);
        Assert.Equal(5u, info.LineWidthCols[1]);
    }

    [Fact]
    public void DebugVirtualLineContents()
    {
        // Zig: "Word wrap - debug virtual line contents"
        using var eb = EditBuffer.Create();
        using var view = TextBufferView.CreateFrom(eb);

        view.SetWrapMode(WrapMode.Word);
        view.SetWrapWidth(18);

        eb.SetText("hello my good ");
        eb.SetCursor(0, 14);
        eb.InsertText("f");
        eb.DeleteCharBackward();
        eb.InsertText("friend");

        Assert.Equal(2, view.GetLineInfo().LineWidthCols.Length);
    }

    [Fact]
    public void IncrementalCharacterEdits_NearBoundary()
    {
        // Zig: "Word wrap - incremental character edits near boundary"
        using var eb = EditBuffer.Create();
        using var view = TextBufferView.CreateFrom(eb);

        view.SetWrapMode(WrapMode.Word);
        view.SetWrapWidth(18);

        eb.SetText("hello my good ");
        Assert.Equal(1, view.GetLineInfo().LineWidthCols.Length);

        eb.SetCursor(0, 14);
        eb.InsertText("f");
        Assert.Equal(1, view.GetLineInfo().LineWidthCols.Length);

        eb.InsertText("r");
        Assert.Equal(1, view.GetLineInfo().LineWidthCols.Length);

        eb.InsertText("i");
        Assert.Equal(1, view.GetLineInfo().LineWidthCols.Length);

        eb.InsertText("e");
        Assert.Equal(1, view.GetLineInfo().LineWidthCols.Length);

        eb.InsertText("n");
        Assert.Equal(2, view.GetLineInfo().LineWidthCols.Length);

        eb.InsertText("d");
        var info = view.GetLineInfo();
        Assert.Equal(2, info.LineWidthCols.Length);
        Assert.Equal(14u, info.LineWidthCols[0]);
        Assert.Equal(6u, info.LineWidthCols[1]);
    }

    // =====================================================================
    // WRAP CACHE PERFORMANCE TESTS (from wrap-cache-perf_test.zig)
    // =====================================================================

    [Fact]
    public void WidthChanges_AreLinearComplexity()
    {
        // Zig: "word wrap complexity - width changes are O(n)"
        using var tb = TextBuffer.Create(WidthMethod.Wcwidth);
        var text = new string('x', 100_000);
        tb.SetText(text);

        using var view = TextBufferView.Create(tb);
        view.SetWrapMode(WrapMode.Word);

        uint[] widths = [60, 70, 80, 90, 100];
        const int iterations = 5;
        var medianTimes = new long[widths.Length];

        for (int widthIdx = 0; widthIdx < widths.Length; widthIdx++)
        {
            var iterTimes = new long[iterations];
            for (int iter = 0; iter < iterations; iter++)
            {
                // Reset cache by setting a different width first
                view.SetWrapWidth(50);
                view.GetVirtualLineCount();

                view.SetWrapWidth(widths[widthIdx]);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                view.GetVirtualLineCount();
                sw.Stop();
                iterTimes[iter] = sw.ElapsedTicks;
            }

            Array.Sort(iterTimes);
            medianTimes[widthIdx] = iterTimes[iterations / 2];
        }

        long minTime = medianTimes.Min();
        long maxTime = medianTimes.Max();

        double ratio = (double)maxTime / minTime;
        Assert.True(ratio < 5.0, $"Performance ratio {ratio:F2} exceeds 5x threshold");
    }

    [Fact]
    public void VirtualLineCount_Correctness()
    {
        // Zig: "word wrap - virtual line count correctness"
        using var tb = TextBuffer.Create(WidthMethod.Wcwidth);

        var pattern = "var abc=123;function foo(){return bar+baz;}if(x>0){y=z*2;}else{y=0;}";
        const int size = 10_000;
        var sb = new System.Text.StringBuilder(size);
        while (sb.Length < size)
        {
            int remaining = size - sb.Length;
            int copyLen = Math.Min(pattern.Length, remaining);
            sb.Append(pattern, 0, copyLen);
        }
        tb.SetText(sb.ToString());

        using var view = TextBufferView.Create(tb);
        view.SetWrapMode(WrapMode.Word);

        view.SetWrapWidth(80);
        uint count80 = view.GetVirtualLineCount();

        view.SetWrapWidth(100);
        uint count100 = view.GetVirtualLineCount();

        view.SetWrapWidth(60);
        uint count60 = view.GetVirtualLineCount();

        view.SetWrapWidth(80);
        uint count80Again = view.GetVirtualLineCount();

        Assert.True(count80 > 100, $"Expected count_80 > 100, got {count80}");
        Assert.Equal(count80, count80Again);
        Assert.True(count100 < count80, $"Expected count_100 < count_80, got {count100} vs {count80}");
        Assert.True(count60 > count80, $"Expected count_60 > count_80, got {count60} vs {count80}");
    }
}
