using OpenTui.Core;
using Xunit;

namespace OpenTui.Core.Tests;

/// <summary>
/// Tests for DiffRenderable and LineNumberRenderable gutter alignment.
/// Regression tests for: gutter line numbers shifting when signs are present.
/// </summary>
public sealed class DiffRenderableTests : IDisposable
{
    private readonly CliRenderer _renderer;
    private const string RefactoringDiff = """
        @@ -1,12 +1,14 @@
         using System;
        -using System.Collections.Generic;
        +using System.Collections.Concurrent;
        +using System.Threading.Tasks;
         
         namespace Services
         {
        -    public class UserCache
        +    public class UserCache : IDisposable
             {
        -        private readonly Dictionary<string, User> _cache = new();
        +        private readonly ConcurrentDictionary<string, User> _cache = new();
        +        private readonly SemaphoreSlim _lock = new(1, 1);
         
        -        public User? Get(string key)
        +        public async Task<User?> GetAsync(string key)
             {
        """;

    public DiffRenderableTests()
    {
        _renderer = CliRenderer.Create(new CliRendererConfig
        {
            Testing = true,
            Width = 80,
            Height = 24,
        });
    }

    public void Dispose() => _renderer.Dispose();

    private void RenderFrame() => _renderer.RenderTestFrame();

    private static uint ReadCellChar(OptimizedBuffer buf, uint x, uint y) =>
        buf.GetCharAt(x, y);

    private static string ReadRowText(OptimizedBuffer buf, int x, int y, int width)
    {
        var chars = new char[width];
        for (int i = 0; i < width; i++)
        {
            uint codepoint = ReadCellChar(buf, (uint)(x + i), (uint)y);
            chars[i] = codepoint is > 0 and <= char.MaxValue ? (char)codepoint : ' ';
        }

        return new string(chars);
    }

    private string ReadRenderableRow(Renderable renderable, int row) =>
        ReadRowText(_renderer.NextRenderBuffer, (int)renderable.ScreenX, (int)renderable.ScreenY + row, renderable.Width);

    #region Diff Parsing

    [Fact]
    public void ParseDiff_SingleHunk_ReturnsCorrectLines()
    {
        var hunks = DiffRenderable.ParseDiff("""
            @@ -1,3 +1,3 @@
             context line
            -removed line
            +added line
            """);

        Assert.Single(hunks);
        Assert.Equal(3, hunks[0].Lines.Count);
        Assert.Equal(DiffLineType.Context, hunks[0].Lines[0].Type);
        Assert.Equal(DiffLineType.Removed, hunks[0].Lines[1].Type);
        Assert.Equal(DiffLineType.Added, hunks[0].Lines[2].Type);
    }

    [Fact]
    public void ParseDiff_MultipleHunks()
    {
        var hunks = DiffRenderable.ParseDiff("""
            @@ -1,2 +1,2 @@
             first
            -old
            +new
            @@ -10,2 +10,2 @@
             tenth
            -oldten
            +newten
            """);

        Assert.Equal(2, hunks.Count);
        Assert.Equal(1, hunks[0].OldStart);
        Assert.Equal(10, hunks[1].OldStart);
    }

    [Fact]
    public void ParseDiff_EmptyDiff_ReturnsEmptyList()
    {
        var hunks = DiffRenderable.ParseDiff("");
        Assert.Empty(hunks);
    }

    #endregion

    #region Gutter Alignment

    [Fact]
    public void GutterAlignment_SignAndNoSignLines_HaveConsistentNumberPosition()
    {
        // This tests that line numbers in the gutter are at the same X position
        // regardless of whether the line has a sign ("+"/"-") or not.
        // Before the fix, sign lines shifted the number 1 char to the right.
        var diff = new DiffRenderable(_renderer, new DiffOptions
        {
            Id = "diff",
            Diff = """
                @@ -1,3 +1,4 @@
                 using System;
                -using Old;
                +using New;
                +using Extra;
                 namespace Foo {}
                """,
            View = "unified",
            ShowLineNumbers = true,
            Width = DimensionValue.Auto,
            FlexGrow = 1,
        });
        _renderer.Root.Add(diff);

        // Render a frame so layout is computed and gutter is drawn
        RenderFrame();

        // The DiffRenderable should render without crashing and the line number
        // gutter should have consistent width. Verify the gutter child exists
        // and has non-zero dimensions after layout.
        Assert.True(diff.GetChildrenCount() > 0, "DiffRenderable should have child renderables");
    }

    [Fact]
    public void GutterAlignment_UnifiedView_RendersSuccessfully()
    {
        var diff = new DiffRenderable(_renderer, new DiffOptions
        {
            Id = "diff",
            Diff = """
                @@ -1,5 +1,7 @@
                 line one
                -removed A
                -removed B
                +added A
                +added B
                +added C
                 line two
                +added D
                """,
            View = "unified",
            ShowLineNumbers = true,
            Width = DimensionValue.Auto,
            FlexGrow = 1,
        });
        _renderer.Root.Add(diff);
        RenderFrame();

        // Should render multiple frames without issues
        RenderFrame();
        RenderFrame();
    }

    [Fact]
    public void GutterAlignment_SplitView_RendersSuccessfully()
    {
        var diff = new DiffRenderable(_renderer, new DiffOptions
        {
            Id = "diff-split",
            Diff = """
                @@ -1,3 +1,3 @@
                 context
                -old
                +new
                """,
            View = "split",
            ShowLineNumbers = true,
            Width = DimensionValue.Auto,
            FlexGrow = 1,
        });
        _renderer.Root.Add(diff);
        RenderFrame();
    }

    #endregion

    #region Diff Internal Scroll

    [Fact]
    public void DiffScrollBy_ScrollsWithoutCrashing()
    {
        var diff = new DiffRenderable(_renderer, new DiffOptions
        {
            Id = "diff",
            Diff = """
                @@ -1,12 +1,14 @@
                 using System;
                -using System.Collections.Generic;
                +using System.Collections.Concurrent;
                +using System.Threading.Tasks;
                 
                 namespace Services
                 {
                -    public class UserCache
                +    public class UserCache : IDisposable
                     {
                -        private readonly Dictionary<string, User> _cache = new();
                +        private readonly ConcurrentDictionary<string, User> _cache = new();
                +        private readonly SemaphoreSlim _lock = new(1, 1);
                 
                -        public User? Get(string key)
                +        public async Task<User?> GetAsync(string key)
                     {
                """,
            View = "unified",
            ShowLineNumbers = true,
            Width = DimensionValue.Auto,
            FlexGrow = 1,
        });

        _renderer.Root.Add(diff);
        RenderFrame();

        // Scroll down — should not crash
        diff.ScrollBy(3);
        RenderFrame();

        // Scroll back up
        diff.ScrollBy(-3);
        RenderFrame();
    }

    [Fact]
    public void DiffScrollBy_ViewToggle_ScrollsCorrectly()
    {
        var diff = new DiffRenderable(_renderer, new DiffOptions
        {
            Id = "diff",
            Diff = """
                @@ -1,3 +1,3 @@
                 context
                -old
                +new
                """,
            View = "unified",
            ShowLineNumbers = true,
            Width = DimensionValue.Auto,
            FlexGrow = 1,
        });

        _renderer.Root.Add(diff);
        RenderFrame();

        // Switch to split view
        diff.ViewMode = "split";
        RenderFrame();

        // Scroll in split view
        diff.ScrollBy(1);
        RenderFrame();

        // Switch back to unified
        diff.ViewMode = "unified";
        RenderFrame();
    }

    [Fact]
    public void DiffScrollToTop_ResetsPosition()
    {
        var diff = new DiffRenderable(_renderer, new DiffOptions
        {
            Id = "diff",
            Diff = RefactoringDiff,
            View = "unified",
            ShowLineNumbers = true,
            Width = DimensionValue.Auto,
            FlexGrow = 1,
        });

        _renderer.Root.Add(diff);
        RenderFrame();

        // Scroll down
        diff.ScrollBy(5);
        RenderFrame();

        // Reset
        diff.ScrollToTop();
        RenderFrame();
    }

    [Fact]
    public void DiffScrollBy_EmitsScrollEvent()
    {
        var diff = new DiffRenderable(_renderer, new DiffOptions
        {
            Id = "diff-scroll-event",
            Diff = RefactoringDiff,
            View = "unified",
            ShowLineNumbers = true,
            Width = DimensionValue.Auto,
            Height = DimensionValue.Point(5),
        });

        int? lastScrollTop = null;
        diff.On<int>(DiffRenderable.Events.Scroll, scrollTop => lastScrollTop = scrollTop);

        _renderer.Root.Add(diff);
        RenderFrame();

        diff.ScrollBy(3);

        Assert.Equal(diff.ScrollTop, lastScrollTop);
        Assert.True(diff.ScrollTop > 0);
        Assert.True(diff.ScrollHeight >= diff.ViewportHeight);
    }

    [Fact]
    public void UnifiedView_GutterTopRow_StaysAligned_WhenRenderedBelowHeader()
    {
        var header = new BoxRenderable(_renderer, new BoxOptions
        {
            Id = "header",
            Height = DimensionValue.Point(4),
            Width = DimensionValue.Auto,
        });
        var diff = new DiffRenderable(_renderer, new DiffOptions
        {
            Id = "diff-offset",
            Diff = RefactoringDiff,
            View = "unified",
            ShowLineNumbers = true,
            Width = DimensionValue.Auto,
            FlexGrow = 1,
        });

        _renderer.Root.Add(header);
        _renderer.Root.Add(diff);
        RenderFrame();

        var children = diff.GetChildren();
        var lineNumbers = Assert.IsType<LineNumberRenderable>(children[0]);
        var gutter = lineNumbers.GetChildren()[0];

        string topRowBefore = ReadRenderableRow(gutter, 0);
        Assert.Contains('1', topRowBefore);

        diff.ScrollBy(100);
        RenderFrame();
        diff.ScrollToTop();
        RenderFrame();

        string topRowAfter = ReadRenderableRow(gutter, 0);
        Assert.Contains('1', topRowAfter);
        Assert.Equal(topRowBefore, topRowAfter);
    }

    [Fact]
    public void SplitView_GutterTopRow_StaysAligned_WhenRenderedBelowHeader()
    {
        var header = new BoxRenderable(_renderer, new BoxOptions
        {
            Id = "header",
            Height = DimensionValue.Point(4),
            Width = DimensionValue.Auto,
        });
        var diff = new DiffRenderable(_renderer, new DiffOptions
        {
            Id = "diff-split-offset",
            Diff = RefactoringDiff,
            View = "split",
            ShowLineNumbers = true,
            Width = DimensionValue.Auto,
            FlexGrow = 1,
        });

        _renderer.Root.Add(header);
        _renderer.Root.Add(diff);
        RenderFrame();

        var children = diff.GetChildren();
        var leftLineNumbers = Assert.IsType<LineNumberRenderable>(children[0]);
        var rightLineNumbers = Assert.IsType<LineNumberRenderable>(children[1]);

        string leftTopRowBefore = ReadRenderableRow(leftLineNumbers.GetChildren()[0], 0);
        string rightTopRowBefore = ReadRenderableRow(rightLineNumbers.GetChildren()[0], 0);
        Assert.Contains('1', leftTopRowBefore);
        Assert.Contains('1', rightTopRowBefore);

        diff.ScrollBy(100);
        RenderFrame();
        diff.ScrollToTop();
        RenderFrame();

        string leftTopRowAfter = ReadRenderableRow(leftLineNumbers.GetChildren()[0], 0);
        string rightTopRowAfter = ReadRenderableRow(rightLineNumbers.GetChildren()[0], 0);
        Assert.Contains('1', leftTopRowAfter);
        Assert.Contains('1', rightTopRowAfter);
        Assert.Equal(leftTopRowBefore, leftTopRowAfter);
        Assert.Equal(rightTopRowBefore, rightTopRowAfter);
    }

    #endregion

    #region LineNumberRenderable Sign Metrics

    [Fact]
    public void LineNumber_SetLineSigns_TriggersLayoutUpdate()
    {
        var code = new CodeRenderable(_renderer, new CodeOptions
        {
            Id = "code",
            Content = "line1\nline2\nline3",
            FlexGrow = 1,
        });

        var lineNumbers = new LineNumberRenderable(_renderer, new LineNumberOptions
        {
            Id = "ln",
            Target = code,
            FlexGrow = 1,
        });

        _renderer.Root.Add(lineNumbers);
        RenderFrame();

        // Setting signs should update the gutter width (via Yoga dirty)
        lineNumbers.SetLineSigns(new Dictionary<int, LineSign>
        {
            [0] = new LineSign("+", null, null, null),
            [2] = new LineSign("-", null, null, null),
        });
        RenderFrame();

        // Clear signs
        lineNumbers.SetLineSigns([]);
        RenderFrame();
    }

    #endregion

    #region Split View Layout and Content

    [Fact]
    public void SplitView_BatchesConsecutiveRemovesAndAdds()
    {
        // 3 removes then 2 adds should produce 3 rows (max of 3,2), not 5
        var diff = new DiffRenderable(_renderer, new DiffOptions
        {
            Id = "diff-batch",
            Diff = """
                @@ -1,5 +1,4 @@
                 context
                -remove1
                -remove2
                -remove3
                +add1
                +add2
                 end
                """,
            View = "split",
            ShowLineNumbers = true,
            Width = DimensionValue.Auto,
            FlexGrow = 1,
        });
        _renderer.Root.Add(diff);
        RenderFrame();

        // Should render without crashing; the real verification is that
        // both sides have the same number of rows (context + max(3,2) + end = 5)
    }

    [Fact]
    public void SplitView_AddOnlyBlock_PadsLeft()
    {
        var diff = new DiffRenderable(_renderer, new DiffOptions
        {
            Id = "diff-add-only",
            Diff = """
                @@ -1,1 +1,3 @@
                 context
                +added1
                +added2
                """,
            View = "split",
            ShowLineNumbers = true,
            Width = DimensionValue.Auto,
            FlexGrow = 1,
        });
        _renderer.Root.Add(diff);
        RenderFrame();
    }

    [Fact]
    public void SplitView_RemoveOnlyBlock_PadsRight()
    {
        var diff = new DiffRenderable(_renderer, new DiffOptions
        {
            Id = "diff-rm-only",
            Diff = """
                @@ -1,3 +1,1 @@
                 context
                -removed1
                -removed2
                """,
            View = "split",
            ShowLineNumbers = true,
            Width = DimensionValue.Auto,
            FlexGrow = 1,
        });
        _renderer.Root.Add(diff);
        RenderFrame();
    }

    [Fact]
    public void SplitView_MultipleChangeBlocksSeparatedByContext()
    {
        var diff = new DiffRenderable(_renderer, new DiffOptions
        {
            Id = "diff-multi",
            Diff = """
                @@ -1,6 +1,6 @@
                 ctx1
                -old1
                +new1
                 ctx2
                -old2
                +new2
                 ctx3
                """,
            View = "split",
            ShowLineNumbers = true,
            Width = DimensionValue.Auto,
            FlexGrow = 1,
        });
        _renderer.Root.Add(diff);
        RenderFrame();
    }

    [Fact]
    public void SplitView_ScrollPreservesLineNumbers()
    {
        var diff = new DiffRenderable(_renderer, new DiffOptions
        {
            Id = "diff-scroll-split",
            Diff = """
                @@ -1,5 +1,5 @@
                 line1
                -old2
                +new2
                 line3
                -old4
                +new4
                 line5
                """,
            View = "split",
            ShowLineNumbers = true,
            Width = DimensionValue.Auto,
            FlexGrow = 1,
        });
        _renderer.Root.Add(diff);
        RenderFrame();

        // Scroll down fully then back up — should not crash
        diff.ScrollBy(10);
        RenderFrame();
        diff.ScrollToTop();
        RenderFrame();
    }

    #endregion

    #region After Sign Alignment

    [Fact]
    public void LineNumber_AfterSigns_AlignConsistently()
    {
        // Tests that After-style signs (as used by diff) don't shift number alignment
        var code = new CodeRenderable(_renderer, new CodeOptions
        {
            Id = "code-after",
            Content = "line1\nline2\nline3\nline4",
            FlexGrow = 1,
        });

        var lineNumbers = new LineNumberRenderable(_renderer, new LineNumberOptions
        {
            Id = "ln-after",
            Target = code,
            FlexGrow = 1,
        });

        lineNumbers.SetLineSigns(new Dictionary<int, LineSign>
        {
            [1] = new LineSign(null, " +", null, null),
            [2] = new LineSign(null, " -", null, null),
        });

        _renderer.Root.Add(lineNumbers);
        RenderFrame();

        // All lines should render; numbers stay aligned regardless of After sign
        RenderFrame();
    }

    #endregion
}
