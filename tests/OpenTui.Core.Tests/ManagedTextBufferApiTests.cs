using OpenTui.Core;
using OpenTui.Core.Managed;
using Xunit;

namespace OpenTui.Core.Tests;

/// <summary>
/// Regression tests for ManagedTextBuffer API parity methods.
/// </summary>
public class ManagedTextBufferApiTests : IDisposable
{
    private readonly ManagedTextBuffer _tb = ManagedTextBuffer.Create();

    public void Dispose() => _tb.Dispose();

    #region AddHighlight

    [Fact]
    public void AddHighlight_AppendsToExistingHighlights()
    {
        _tb.SetText("Hello World");
        var hl1 = new Highlight { Start = 0, End = 5, StyleId = 1, HlRef = 10 };
        var hl2 = new Highlight { Start = 6, End = 11, StyleId = 2, HlRef = 20 };
        _tb.AddHighlight(0, hl1);
        _tb.AddHighlight(0, hl2);

        var highlights = _tb.GetLineHighlights(0);
        Assert.Equal(2, highlights.Length);
        Assert.Equal(10, highlights[0].HlRef);
        Assert.Equal(20, highlights[1].HlRef);
    }

    [Fact]
    public void AddHighlight_DoesNotReplace()
    {
        _tb.SetText("Test");
        var hl = new Highlight { Start = 0, End = 2, StyleId = 1, HlRef = 1 };
        _tb.SetHighlights(0, [hl]);
        _tb.AddHighlight(0, new Highlight { Start = 2, End = 4, StyleId = 2, HlRef = 2 });

        var highlights = _tb.GetLineHighlights(0);
        Assert.Equal(2, highlights.Length);
    }

    #endregion

    #region AddHighlightByCharRange

    [Fact]
    public void AddHighlightByCharRange_SingleLine()
    {
        _tb.SetText("Hello World");
        var hl = new Highlight { Start = 0, End = 5, StyleId = 1, HlRef = 42 };
        _tb.AddHighlightByCharRange(hl);

        var highlights = _tb.GetLineHighlights(0);
        Assert.Single(highlights);
        Assert.Equal(0u, highlights[0].Start);
        Assert.Equal(5u, highlights[0].End);
        Assert.Equal((ushort)42, highlights[0].HlRef);
    }

    [Fact]
    public void AddHighlightByCharRange_SpansMultipleLines()
    {
        _tb.SetText("Hello\nWorld\nFoo");
        // Display offsets (no newline gap): "Hello" = 0-4, "World" = 5-9, "Foo" = 10-12
        var hl = new Highlight { Start = 3, End = 8, StyleId = 1, HlRef = 99 };
        _tb.AddHighlightByCharRange(hl);

        // Line 0: display cols 3-4 (offset 3 to end of line 5)
        var line0 = _tb.GetLineHighlights(0);
        Assert.Single(line0);
        Assert.Equal(3u, line0[0].Start);
        Assert.Equal(5u, line0[0].End);

        // Line 1: display cols 0-2 (offset 5 to 8 = line-relative 0-3)
        var line1 = _tb.GetLineHighlights(1);
        Assert.Single(line1);
        Assert.Equal(0u, line1[0].Start);
        Assert.Equal(3u, line1[0].End);
    }

    #endregion

    #region RemoveHighlight

    [Fact]
    public void RemoveHighlight_RemovesMatchingHlRef()
    {
        _tb.SetText("Hello\nWorld");
        _tb.AddHighlight(0, new Highlight { Start = 0, End = 5, HlRef = 10 });
        _tb.AddHighlight(0, new Highlight { Start = 0, End = 3, HlRef = 20 });
        _tb.AddHighlight(1, new Highlight { Start = 0, End = 5, HlRef = 10 });

        _tb.RemoveHighlight(10);

        var line0 = _tb.GetLineHighlights(0);
        Assert.Single(line0);
        Assert.Equal((ushort)20, line0[0].HlRef);

        var line1 = _tb.GetLineHighlights(1);
        Assert.Empty(line1);
    }

    #endregion

    #region ClearLineHighlights

    [Fact]
    public void ClearLineHighlights_ClearsSpecificLine()
    {
        _tb.SetText("Hello\nWorld");
        _tb.AddHighlight(0, new Highlight { Start = 0, End = 5, HlRef = 1 });
        _tb.AddHighlight(1, new Highlight { Start = 0, End = 5, HlRef = 2 });

        _tb.ClearLineHighlights(0);

        Assert.Empty(_tb.GetLineHighlights(0));
        Assert.Single(_tb.GetLineHighlights(1));
    }

    #endregion

    #region GetLineHighlights

    [Fact]
    public void GetLineHighlights_ReturnsEmptyForUnhighlightedLine()
    {
        _tb.SetText("Hello");
        Assert.Empty(_tb.GetLineHighlights(0));
    }

    [Fact]
    public void GetLineHighlights_ReturnsEmptyForOutOfRange()
    {
        _tb.SetText("Hello");
        Assert.Empty(_tb.GetLineHighlights(999));
    }

    #endregion

    #region HighlightCount

    [Fact]
    public void HighlightCount_IsZeroInitially()
    {
        _tb.SetText("Hello");
        Assert.Equal(0u, _tb.HighlightCount);
    }

    [Fact]
    public void HighlightCount_CountsAllLines()
    {
        _tb.SetText("Hello\nWorld\nFoo");
        _tb.AddHighlight(0, new Highlight { Start = 0, End = 5 });
        _tb.AddHighlight(1, new Highlight { Start = 0, End = 5 });
        _tb.AddHighlight(2, new Highlight { Start = 0, End = 3 });
        Assert.Equal(3u, _tb.HighlightCount);
    }

    #endregion

    #region GetTextRangeByOffset

    [Fact]
    public void GetTextRangeByOffset_SingleLine()
    {
        _tb.SetText("Hello World");
        var text = _tb.GetTextRangeByOffset(0, 5);
        Assert.Equal("Hello", text);
    }

    [Fact]
    public void GetTextRangeByOffset_AcrossLines()
    {
        _tb.SetText("Hello\nWorld");
        var text = _tb.GetTextRangeByOffset(3, 8);
        Assert.Equal("lo\nWo", text);
    }

    [Fact]
    public void GetTextRangeByOffset_EmptyRange()
    {
        _tb.SetText("Hello");
        Assert.Equal(string.Empty, _tb.GetTextRangeByOffset(3, 3));
        Assert.Equal(string.Empty, _tb.GetTextRangeByOffset(5, 2));
    }

    #endregion

    #region LoadFile

    [Fact]
    public void LoadFile_NonExistent_ReturnsFalse()
    {
        Assert.False(_tb.LoadFile(@"C:\nonexistent_file_that_should_not_exist_12345.txt"));
    }

    [Fact]
    public void LoadFile_ExistingFile_LoadsContent()
    {
        var tempFile = Path.Combine(
            Path.GetDirectoryName(typeof(ManagedTextBufferApiTests).Assembly.Location)!,
            $"test_loadfile_{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllText(tempFile, "File content");
            Assert.True(_tb.LoadFile(tempFile));
            Assert.Equal("File content", _tb.GetText());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    #endregion
}
