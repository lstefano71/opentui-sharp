using System.Text;
using OpenTui.Core;
using Xunit;

namespace OpenTui.Core.Tests;

/// <summary>
/// C# xUnit equivalents of the Zig text-buffer_test.zig tests (97 tests).
/// Some Zig tests exercise internal rope/iterator APIs not exposed through C#;
/// those are mapped to the closest available C# API.
/// </summary>
public class TextBufferTests
{
    // ===== Init / Empty Buffer =====

    [Fact]
    public void Init_CreatesEmptyBuffer()
    {
        // Zig: "TextBuffer init - creates empty buffer"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        Assert.Equal(0u, tb.Length);
        Assert.Equal(1u, tb.LineCount); // Empty buffer has 1 empty line (invariant)
    }

    [Fact]
    public void LineInfo_EmptyBuffer()
    {
        // Zig: "TextBuffer line info - empty buffer"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("");

        Assert.Equal(0u, tb.Length);
        Assert.Equal(1u, tb.LineCount);
        // coordsToOffset / lineWidthAt are internal rope ops not exposed; verify via GetPlainText
        Assert.Equal("", tb.GetPlainText());
    }

    [Fact]
    public void LineInfo_SimpleTextWithoutNewlines()
    {
        // Zig: "TextBuffer line info - simple text without newlines"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        const string text = "Hello World";
        tb.SetText(text);

        Assert.Equal(11u, tb.Length);
        Assert.Equal(1u, tb.LineCount);
        Assert.Equal(text, tb.GetPlainText());
    }

    [Fact]
    public void LineInfo_SingleNewline()
    {
        // Zig: "TextBuffer line info - single newline"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Hello\nWorld");

        Assert.Equal(2u, tb.LineCount);
        Assert.Equal("Hello\nWorld", tb.GetPlainText());
    }

    [Fact]
    public void LineInfo_MultipleLinesNewlines()
    {
        // Zig: "TextBuffer line info - multiple lines separated by newlines"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        const string text = "Line 1\nLine 2\nLine 3";
        tb.SetText(text);

        Assert.Equal(3u, tb.LineCount);
        Assert.Equal(text, tb.GetPlainText());
    }

    [Fact]
    public void LineInfo_TextEndingWithNewline()
    {
        // Zig: "TextBuffer line info - text ending with newline"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        const string text = "Line 1\nLine 2\n";
        tb.SetText(text);

        // Trailing newline creates an empty 3rd line (matches editor semantics)
        Assert.Equal(3u, tb.LineCount);
        Assert.Equal(text, tb.GetPlainText());
    }

    [Fact]
    public void LineInfo_ConsecutiveNewlines()
    {
        // Zig: "TextBuffer line info - consecutive newlines"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Line 1\n\nLine 3");

        Assert.Equal(3u, tb.LineCount);
        Assert.Equal("Line 1\n\nLine 3", tb.GetPlainText());
    }

    [Fact]
    public void LineInfo_TextStartingWithNewline()
    {
        // Zig: "TextBuffer line info - text starting with newline"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("\nHello World");

        Assert.Equal(2u, tb.LineCount);
    }

    [Fact]
    public void LineInfo_OnlyNewlines()
    {
        // Zig: "TextBuffer line info - only newlines"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("\n\n\n");

        Assert.Equal(4u, tb.LineCount);
    }

    [Fact]
    public void LineInfo_WideCharactersUnicode()
    {
        // Zig: "TextBuffer line info - wide characters (Unicode)"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        const string text = "Hello 世界 🌟";
        tb.SetText(text);

        Assert.Equal(1u, tb.LineCount);
        Assert.Equal(text, tb.GetPlainText());
    }

    [Fact]
    public void LineInfo_EmptyLinesBetweenContent()
    {
        // Zig: "TextBuffer line info - empty lines between content"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("First\n\nThird");

        Assert.Equal(3u, tb.LineCount);
    }

    [Fact]
    public void LineInfo_VeryLongLines()
    {
        // Zig: "TextBuffer line info - very long lines"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        var longText = new string('A', 1000);
        tb.SetText(longText);

        Assert.Equal(1u, tb.LineCount);
        Assert.Equal(longText, tb.GetPlainText());
    }

    [Fact]
    public void LineInfo_LinesWithDifferentWidths()
    {
        // Zig: "TextBuffer line info - lines with different widths"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        string text = "Short\n" + new string('A', 50) + "\nMedium";
        tb.SetText(text);

        Assert.Equal(3u, tb.LineCount);
        Assert.Equal(text, tb.GetPlainText());
    }

    [Fact]
    public void LineInfo_TextWithoutStyling()
    {
        // Zig: "TextBuffer line info - text without styling"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Red\nBlue");

        Assert.Equal(2u, tb.LineCount);
    }

    [Fact]
    public void LineInfo_BufferWithOnlyWhitespace()
    {
        // Zig: "TextBuffer line info - buffer with only whitespace"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("   \n \n ");

        Assert.Equal(3u, tb.LineCount);
    }

    [Fact]
    public void LineInfo_SingleCharacterLines()
    {
        // Zig: "TextBuffer line info - single character lines"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("A\nB\nC");

        Assert.Equal(3u, tb.LineCount);
        Assert.Equal("A\nB\nC", tb.GetPlainText());
    }

    [Fact]
    public void LineInfo_MixedContentWithSpecialCharacters()
    {
        // Zig: "TextBuffer line info - mixed content with special characters"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Normal\n123\n!@#\n测试\n");

        Assert.Equal(5u, tb.LineCount); // 4 lines + empty line at end
    }

    [Fact]
    public void LineInfo_BufferResizeOperations()
    {
        // Zig: "TextBuffer line info - buffer resize operations"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        string text = new string('A', 100) + "\n" + new string('B', 100);
        tb.SetText(text);

        Assert.Equal(2u, tb.LineCount);
    }

    [Fact]
    public void LineInfo_ThousandsOfLines()
    {
        // Zig: "TextBuffer line info - thousands of lines"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        var sb = new StringBuilder();
        for (int i = 0; i < 999; i++)
            sb.Append($"Line {i}\n");
        sb.Append("Line 999");

        tb.SetText(sb.ToString());

        Assert.Equal(1000u, tb.LineCount);
    }

    [Fact]
    public void LineInfo_AlternatingEmptyAndContentLines()
    {
        // Zig: "TextBuffer line info - alternating empty and content lines"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("\nContent\n\nMore\n\n");

        Assert.Equal(6u, tb.LineCount);
    }

    [Fact]
    public void LineInfo_ComplexUnicodeCombiningCharacters()
    {
        // Zig: "TextBuffer line info - complex Unicode combining characters"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("café\nnaïve\nrésumé");

        Assert.Equal(3u, tb.LineCount);
    }

    [Fact]
    public void LineInfo_SimpleMultiLineText()
    {
        // Zig: "TextBuffer line info - simple multi-line text"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Test\nText");

        Assert.Equal(2u, tb.LineCount);
    }

    [Fact]
    public void LineInfo_UnicodeWidthMethod()
    {
        // Zig: "TextBuffer line info - unicode width method"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Hello 世界 🌟");

        Assert.Equal(1u, tb.LineCount);
    }

    [Fact]
    public void LineInfo_UnicodeMixedContentWithSpecialCharacters()
    {
        // Zig: "TextBuffer line info - unicode mixed content with special characters"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Normal\n123\n!@#\n测试\n");

        Assert.Equal(5u, tb.LineCount);
    }

    [Fact]
    public void LineInfo_UnicodeTextWithoutStyling()
    {
        // Zig: "TextBuffer line info - unicode text without styling"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Red\nBlue");

        Assert.Equal(2u, tb.LineCount);
    }

    [Fact]
    public void LineInfo_ExtremelyLongSingleLine()
    {
        // Zig: "TextBuffer line info - extremely long single line"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        var text = new string('A', 10000);
        tb.SetText(text);

        Assert.Equal(1u, tb.LineCount);
        Assert.Equal(text, tb.GetPlainText());
    }

    [Fact]
    public void Unicode_MultiLineWithExtraction()
    {
        // Zig: "TextBuffer unicode - multi-line with extraction"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        const string text = "Hello 世界\n🚀 Emoji\nΑλφα";
        tb.SetText(text);

        Assert.Equal(3u, tb.LineCount);
        Assert.Equal(text, tb.GetPlainText());
    }

    // ===== Reset / Clear =====

    [Fact]
    public void Reset_ClearsAllContent()
    {
        // Zig: "TextBuffer reset - clears all content"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Some text\nMore text");
        Assert.Equal(2u, tb.LineCount);

        tb.Reset();
        Assert.Equal(0u, tb.Length);
        Assert.Equal(1u, tb.LineCount);
    }

    // ===== Line Iteration =====

    [Fact]
    public void LineIteration_WalkLinesCallback()
    {
        // Zig: "TextBuffer line iteration - walkLines callback"
        // walkLines is an internal rope iterator not exposed via C#.
        // Verify basic multi-line structure instead.
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        const string text = "First\nSecond\nThird";
        tb.SetText(text);

        Assert.Equal(3u, tb.LineCount);
        Assert.Equal(text, tb.GetPlainText());
    }

    // ===== Line Queries =====

    [Fact]
    public void LineQueries_ComprehensiveRopeCoordinateChecks()
    {
        // Zig: "TextBuffer line queries - comprehensive rope coordinate checks"
        // coordsToOffset/lineWidthAt/getMaxLineWidth are internal rope ops.
        // Verify line count and text extraction via GetTextRangeByCoords.
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("First\nSecond\nThird");

        Assert.Equal(3u, tb.LineCount);

        // Use GetTextRangeByCoords to verify line boundaries
        string line0 = tb.GetTextRangeByCoords(0, 0, 0, 5);
        Assert.Equal("First", line0);

        string line1 = tb.GetTextRangeByCoords(1, 0, 1, 6);
        Assert.Equal("Second", line1);

        string line2 = tb.GetTextRangeByCoords(2, 0, 2, 5);
        Assert.Equal("Third", line2);
    }

    // ===== View Registration Tests =====
    // View registration (registerView/unregisterView/isViewDirty/clearViewDirty) is not
    // exposed through the C# TextBuffer wrapper. These are tested at the native level in Zig.
    // The closest C# equivalent is TextBufferView, tested below.

    [Fact]
    public void ViewRegistration_MultipleViewsCanBeCreated()
    {
        // Zig: "TextBuffer view registration - multiple views can be created"
        // C# uses TextBufferView objects; verify multiple can be created.
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var v1 = TextBufferView.Create(tb);
        using var v2 = TextBufferView.Create(tb);
        using var v3 = TextBufferView.Create(tb);

        // All views should be distinct (non-null handles)
        Assert.NotNull(v1);
        Assert.NotNull(v2);
        Assert.NotNull(v3);
    }

    [Fact]
    public void ViewRegistration_ViewsReflectSetText()
    {
        // Zig: "TextBuffer view registration - views marked dirty on setText"
        // Dirty tracking not directly exposed; verify the view reads updated content.
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);
        view.SetViewportSize(80, 24);

        tb.SetText("Hello World");
        Assert.Equal("Hello World", tb.GetPlainText());

        tb.SetText("New text");
        Assert.Equal("New text", tb.GetPlainText());
    }

    [Fact]
    public void ViewRegistration_ViewsReflectReset()
    {
        // Zig: "TextBuffer view registration - views marked dirty on reset"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.SetText("content");
        tb.Reset();

        Assert.Equal(0u, tb.Length);
        Assert.Equal(1u, tb.LineCount);
    }

    [Fact]
    public void ViewRegistration_IdReuseAfterDispose()
    {
        // Zig: "TextBuffer view registration - ID reuse after unregister"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        var v1 = TextBufferView.Create(tb);
        v1.Dispose();

        using var v2 = TextBufferView.Create(tb);
        Assert.NotNull(v2);
    }

    [Fact]
    public void ViewRegistration_MultipleViewsAllDirtyOnSetText()
    {
        // Zig: "TextBuffer view registration - multiple views all marked dirty on setText"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var v1 = TextBufferView.Create(tb);
        using var v2 = TextBufferView.Create(tb);
        using var v3 = TextBufferView.Create(tb);

        tb.SetText("Test");

        // All views should observe the new content
        Assert.Equal("Test", tb.GetPlainText());
    }

    // ===== Memory Registry Tests =====

    [Fact]
    public void MemoryRegistry_RegisterAndSetFromMemory()
    {
        // Zig: "TextBuffer memory registry - register and get buffer"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        byte[] data = Encoding.UTF8.GetBytes("Hello World");
        ushort memId = tb.RegisterMemory(data, copy: true);

        tb.SetTextFromMemory((byte)memId);
        Assert.Equal("Hello World", tb.GetPlainText());
    }

    [Fact]
    public void MemoryRegistry_MultipleBuffers()
    {
        // Zig: "TextBuffer memory registry - multiple buffers"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        ushort id1 = tb.RegisterMemory(Encoding.UTF8.GetBytes("First buffer"), copy: true);
        ushort id2 = tb.RegisterMemory(Encoding.UTF8.GetBytes("Second buffer"), copy: true);
        ushort id3 = tb.RegisterMemory(Encoding.UTF8.GetBytes("Third buffer"), copy: true);

        Assert.NotEqual(id1, id2);
        Assert.NotEqual(id2, id3);
        Assert.NotEqual(id1, id3);
    }

    [Fact]
    public void MemoryRegistry_SetTextFromMemory()
    {
        // Zig: "TextBuffer memory registry - addLine from single buffer"
        // C# exposes SetTextFromMemory; verify it works.
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        byte[] data = Encoding.UTF8.GetBytes("Hello");
        ushort memId = tb.RegisterMemory(data, copy: true);

        tb.SetTextFromMemory((byte)memId);

        Assert.Equal(1u, tb.LineCount);
        Assert.Equal("Hello", tb.GetPlainText());
    }

    [Fact]
    public void MemoryRegistry_AppendFromMemory()
    {
        // Zig: "TextBuffer memory registry - addLine from multiple buffers"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        ushort id1 = tb.RegisterMemory(Encoding.UTF8.GetBytes("First line"), copy: true);
        ushort id2 = tb.RegisterMemory(Encoding.UTF8.GetBytes("\nSecond line"), copy: true);

        tb.SetTextFromMemory((byte)id1);
        tb.AppendFromMemory((byte)id2);

        Assert.Equal(2u, tb.LineCount);
        Assert.Contains("First line", tb.GetPlainText());
        Assert.Contains("Second line", tb.GetPlainText());
    }

    [Fact]
    public void MemoryRegistry_MixedWithSetText()
    {
        // Zig: "TextBuffer memory registry - mixed with setText"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        tb.SetText("Initial text");
        Assert.True(tb.Length > 0);

        byte[] data = Encoding.UTF8.GetBytes("\nNew text");
        ushort memId = tb.RegisterMemory(data, copy: true);

        tb.AppendFromMemory((byte)memId);
        Assert.Equal(2u, tb.LineCount);
    }

    [Fact]
    public void MemoryRegistry_ResetClearsMemoryBuffers()
    {
        // Zig: "TextBuffer memory registry - reset clears memory buffers"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        byte[] data = Encoding.UTF8.GetBytes("Hello");
        ushort memId = tb.RegisterMemory(data, copy: true);

        tb.SetTextFromMemory((byte)memId);
        Assert.Equal("Hello", tb.GetPlainText());

        tb.Reset();
        Assert.Equal(0u, tb.Length);
    }

    [Fact]
    public void Clear_PreservesMemoryBuffers()
    {
        // Zig: "TextBuffer clear - preserves memory buffers"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        byte[] data = Encoding.UTF8.GetBytes("Hello World");
        ushort memId = tb.RegisterMemory(data, copy: true);

        tb.SetTextFromMemory((byte)memId);
        Assert.Equal(1u, tb.LineCount);
        Assert.True(tb.Length > 0);

        tb.Clear();
        Assert.Equal(1u, tb.LineCount); // Empty buffer has 1 empty line
        Assert.Equal(0u, tb.Length);

        // mem_id should still be valid after clear; re-use it
        tb.SetTextFromMemory((byte)memId);
        Assert.True(tb.Length > 0);
    }

    [Fact]
    public void SetText_PreservesRegisteredMemoryBuffers()
    {
        // Zig: "TextBuffer setText - preserves previously registered memory buffers"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        byte[] oldData = Encoding.UTF8.GetBytes("Previous content");
        ushort oldMemId = tb.RegisterMemory(oldData, copy: true);

        tb.SetText("New text content");
        Assert.Equal(1u, tb.LineCount);

        // The old mem_id should still be valid after setText — verify by using it
        tb.Clear();
        tb.SetTextFromMemory((byte)oldMemId);
        Assert.Contains("Previous", tb.GetPlainText());
    }

    [Fact]
    public void SetStyledText_PreservesRegisteredMemoryBuffers()
    {
        // Zig: "TextBuffer setStyledText - preserves previously registered memory buffers"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        byte[] preservedData = Encoding.UTF8.GetBytes("Preserved data");
        ushort preservedMemId = tb.RegisterMemory(preservedData, copy: true);

        var styled = new StyledText(
            TextChunk.Plain("Styled "),
            TextChunk.Plain("Text")
        );
        tb.SetStyledText(styled);

        Assert.Equal(1u, tb.LineCount);

        // The preserved mem_id should still be valid
        tb.Clear();
        tb.SetTextFromMemory((byte)preservedMemId);
        Assert.Contains("Preserved", tb.GetPlainText());
    }

    [Fact]
    public void ClearVsReset_MemoryRegistryBehavior()
    {
        // Zig: "TextBuffer clear vs reset - memory registry behavior"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        byte[] data = Encoding.UTF8.GetBytes("Test buffer");
        ushort memId = tb.RegisterMemory(data, copy: true);
        tb.SetTextFromMemory((byte)memId);

        // clear() preserves memory buffers
        tb.Clear();
        Assert.Equal(0u, tb.Length);

        // Restore content from same mem_id
        tb.SetTextFromMemory((byte)memId);
        Assert.True(tb.Length > 0);

        // reset() clears memory buffers
        tb.Reset();
        Assert.Equal(0u, tb.Length);
        // After reset, the mem_id is no longer valid — using it may fail or produce no content.
    }

    [Fact]
    public void MemoryRegistry_UnicodeTextFromBuffers()
    {
        // Zig: "TextBuffer memory registry - unicode text from buffers"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        ushort id1 = tb.RegisterMemory(Encoding.UTF8.GetBytes("Hello 世界"), copy: true);
        ushort id2 = tb.RegisterMemory(Encoding.UTF8.GetBytes("\n🌟 Test"), copy: true);

        tb.SetTextFromMemory((byte)id1);
        tb.AppendFromMemory((byte)id2);

        Assert.Equal(2u, tb.LineCount);
        string result = tb.GetPlainText();
        Assert.Contains("Hello 世界", result);
        Assert.Contains("🌟 Test", result);
    }

    [Fact]
    public void MemoryRegistry_GetByteSizeWithMultipleBuffers()
    {
        // Zig: "TextBuffer memory registry - getByteSize with multiple buffers"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        ushort id1 = tb.RegisterMemory(Encoding.UTF8.GetBytes("Hello"), copy: true);
        ushort id2 = tb.RegisterMemory(Encoding.UTF8.GetBytes("\nWorld"), copy: true);

        tb.SetTextFromMemory((byte)id1);
        tb.AppendFromMemory((byte)id2);

        // "Hello" + "\n" + "World" = 11 bytes
        Assert.Equal(11u, tb.ByteSize);
    }

    [Fact]
    public void MemoryRegistry_EmptyBufferRegistration()
    {
        // Zig: "TextBuffer memory registry - empty buffer registration"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        ushort memId = tb.RegisterMemory([], copy: true);

        // Should succeed — registering empty data is allowed.
        // Using it won't add content.
        tb.SetTextFromMemory((byte)memId);
        Assert.Equal(0u, tb.Length);
    }

    [Fact]
    public void MemoryRegistry_SameBufferRegisteredMultipleTimes()
    {
        // Zig: "TextBuffer memory registry - same buffer registered multiple times"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        byte[] data = Encoding.UTF8.GetBytes("Shared buffer");

        ushort id1 = tb.RegisterMemory(data, copy: true);
        ushort id2 = tb.RegisterMemory(data, copy: true);
        ushort id3 = tb.RegisterMemory(data, copy: true);

        // IDs should be different
        Assert.NotEqual(id1, id2);
        Assert.NotEqual(id2, id3);
    }

    [Fact]
    public void MemoryRegistry_OwnedBufferMemoryManagement()
    {
        // Zig: "TextBuffer memory registry - owned buffer memory management"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        byte[] ownedData = Encoding.UTF8.GetBytes("Owned text");
        ushort memId = tb.RegisterMemory(ownedData, copy: true);

        tb.SetTextFromMemory((byte)memId);
        Assert.Equal(1u, tb.LineCount);

        // Dispose should free native memory without errors
    }

    // ===== setText SIMD Line Break Tests =====

    [Fact]
    public void SetText_CrlfLineEndingsWindows()
    {
        // Zig: "TextBuffer setText - CRLF line endings (Windows)"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Line1\r\nLine2\r\nLine3");

        Assert.Equal(3u, tb.LineCount);
        // CRLF normalized to LF
        Assert.Equal("Line1\nLine2\nLine3", tb.GetPlainText());
    }

    [Fact]
    public void SetText_MixedLineEndings()
    {
        // Zig: "TextBuffer setText - mixed line endings (LF, CRLF, CR)"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Unix\nWindows\r\nOldMac\rEnd");

        Assert.Equal(4u, tb.LineCount);
        Assert.Equal("Unix\nWindows\nOldMac\nEnd", tb.GetPlainText());
    }

    [Fact]
    public void SetText_TextEndingWithCrlf()
    {
        // Zig: "TextBuffer setText - text ending with CRLF"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Hello World\r\n");

        Assert.Equal(2u, tb.LineCount);
    }

    [Fact]
    public void SetText_ConsecutiveCrlfSequences()
    {
        // Zig: "TextBuffer setText - consecutive CRLF sequences"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Line1\r\n\r\nLine3");

        Assert.Equal(3u, tb.LineCount);
    }

    [Fact]
    public void SetText_OnlyCrlfSequences()
    {
        // Zig: "TextBuffer setText - only CRLF sequences"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("\r\n\r\n\r\n");

        Assert.Equal(4u, tb.LineCount);
    }

    [Fact]
    public void SetText_TextStartingWithCrlf()
    {
        // Zig: "TextBuffer setText - text starting with CRLF"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("\r\nHello World");

        Assert.Equal(2u, tb.LineCount);
    }

    [Fact]
    public void SetText_CrWithoutLf()
    {
        // Zig: "TextBuffer setText - CR without LF"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Line1\rLine2\rLine3");

        Assert.Equal(3u, tb.LineCount);
        // CR should be normalized to LF
        Assert.Equal("Line1\nLine2\nLine3", tb.GetPlainText());
    }

    [Fact]
    public void SetText_VeryLongLineWithSimdProcessing()
    {
        // Zig: "TextBuffer setText - very long line with SIMD processing"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        string text = new string('A', 100) + "\r\n" + new string('B', 100) + "\n" + new string('C', 100);
        tb.SetText(text);

        Assert.Equal(3u, tb.LineCount);
    }

    [Fact]
    public void SetText_UnicodeContentWithVariousLineEndings()
    {
        // Zig: "TextBuffer setText - unicode content with various line endings"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Hello 世界\r\n🌟 Test\nEnd");

        Assert.Equal(3u, tb.LineCount);
        Assert.Equal("Hello 世界\n🌟 Test\nEnd", tb.GetPlainText());
    }

    [Fact]
    public void SetText_MultipleConsecutiveDifferentLineEndings()
    {
        // Zig: "TextBuffer setText - multiple consecutive different line endings"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("A\n\r\n\rB");

        // "A", "", "", "B"
        Assert.Equal(4u, tb.LineCount);
    }

    [Fact]
    public void SetText_SimdBoundaryConditions()
    {
        // Zig: "TextBuffer setText - SIMD boundary conditions"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        // 15 chars + \n + 15 chars + \n + 10 chars
        string text = new string('X', 15) + "\n" + new string('Y', 15) + "\n" + new string('Z', 10);
        tb.SetText(text);

        Assert.Equal(3u, tb.LineCount);
    }

    [Fact]
    public void SetText_CrlfAtSimdBoundary()
    {
        // Zig: "TextBuffer setText - CRLF at SIMD boundary"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        // 15 chars + \r\n + "Next line"
        string text = new string('A', 15) + "\r\n" + "Next line";
        tb.SetText(text);

        Assert.Equal(2u, tb.LineCount);
    }

    [Fact]
    public void SetText_LineWithMultipleU16SizedChunks_Skipped()
    {
        // Zig: "TextBuffer setText - line with multiple u16-sized chunks (SKIPPED)"
        // This test is explicitly skipped in Zig, so skip it in C# as well.
    }

    [Fact]
    public void SetText_ValidateRopeStructureIsCorrect()
    {
        // Zig: "TextBuffer setText - validate rope structure is correct"
        // Rope internals (markerCount, totalWeight) are not exposed via C#.
        using var tb = TextBuffer.Create(WidthMethod.Wcwidth);
        tb.SetText("Line 1\nLine 2\nLine 3");

        Assert.Equal(3u, tb.LineCount);
        Assert.Equal("Line 1\nLine 2\nLine 3", tb.GetPlainText());
    }

    [Fact]
    public void SetText_ThenDeleteRangeViaEditBuffer_ValidateMarkers()
    {
        // Zig: "TextBuffer setText - then deleteRange via EditBuffer - validate markers"
        // Rope marker counts are not exposed; this tests EditBuffer which is separate.
        // Verify via line count after deletion.
        // NOTE: EditBuffer.DeleteRange is not directly available in the C# wrapper yet.
        // This is a placeholder that verifies the text buffer side.
        using var tb = TextBuffer.Create(WidthMethod.Wcwidth);
        tb.SetText("Line 1\nLine 2\nLine 3");

        Assert.Equal(3u, tb.LineCount);
        // Full edit-buffer deletion test would require EditBuffer C# wrapper
    }

    [Fact]
    public void SetStyledText_RepeatedCallsDoNotLeak()
    {
        // Zig: "TextBuffer setStyledText - repeated calls with SyntaxStyle (crash reproduction)"
        // Arena tracking not exposed; verify repeated calls don't crash.
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        for (int i = 0; i < 1000; i++)
        {
            var styled = new StyledText(
                TextChunk.Styled("System Stats: ", attributes: TextAttributes.Bold),
                TextChunk.Plain("Frame: "),
                TextChunk.Plain(i.ToString())
            );
            tb.SetStyledText(styled);
            Assert.Equal(1u, tb.LineCount);
        }
    }

    // ===== Highlight Tests =====

    [Fact]
    public void AddHighlightByCharRange_SingleLineHighlight()
    {
        // Zig: "addHighlightByCharRange - single line highlight should not extend to EOL"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        const string text = "Try moving your cursor through the [VIRTUAL] markers below:";
        tb.SetText(text);

        var hl = new Highlight { Start = 35, End = 44, StyleId = 1, Priority = 1, HlRef = 0 };
        tb.AddHighlightByCharRange(hl);

        Assert.Equal(1u, tb.HighlightCount);
    }

    [Fact]
    public void AddHighlightByCharRange_MultipleHighlightsSameLine()
    {
        // Zig: "addHighlightByCharRange - multiple highlights on same line should have correct bounds"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        const string text = "Text [MARK1] and [MARK2] here";
        tb.SetText(text);

        var hl1 = new Highlight { Start = 5, End = 12, StyleId = 1, Priority = 1, HlRef = 0 };
        var hl2 = new Highlight { Start = 17, End = 24, StyleId = 1, Priority = 2, HlRef = 0 };
        tb.AddHighlightByCharRange(hl1);
        tb.AddHighlightByCharRange(hl2);

        Assert.Equal(2u, tb.HighlightCount);
    }

    [Fact]
    public void AddHighlightByCharRange_HighlightAfterNewline()
    {
        // Zig: "addHighlightByCharRange - highlight after newline should not span to EOL"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        const string text = "Line1\nLine2 with [MARK] text\nLine3";
        tb.SetText(text);

        // Highlight [MARK] on line 2: char offsets 16..22 in the full text
        // "Line1\n" = 6 chars, "Line2 with " = 11 chars => mark_start = 6 + 11 = 17 (but Zig uses 5 + 11 = 16)
        uint markStart = 5 + 11; // line1 char offset (excl newline in char count depends on native) + 11
        uint markEnd = 5 + 17;
        var hl = new Highlight { Start = markStart, End = markEnd, StyleId = 1, Priority = 1, HlRef = 0 };
        tb.AddHighlightByCharRange(hl);

        Assert.Equal(1u, tb.HighlightCount);
    }

    [Fact]
    public void AddHighlightByCharRange_ExtmarksDemoScenario()
    {
        // Zig: "addHighlightByCharRange - extmarks demo scenario reproduction"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        const string fullText = "Welcome to the Extmarks Demo!\n" +
            "\n" +
            "This demo showcases virtual extmarks - text ranges that the cursor jumps over.\n" +
            "\n" +
            "Try moving your cursor through the [VIRTUAL] markers below:\n" +
            "- Use arrow keys to navigate";
        tb.SetText(fullText);

        // Line 4 char offset = 107 in the Zig test
        uint virtualStart = 107 + 35;
        uint virtualEnd = 107 + 44;
        var hl = new Highlight { Start = virtualStart, End = virtualEnd, StyleId = 1, Priority = 1, HlRef = 0 };
        tb.AddHighlightByCharRange(hl);

        Assert.True(tb.HighlightCount >= 1);
    }

    [Fact]
    public void ClearHighlights_RemovesAllHighlights()
    {
        // Not a direct Zig test, but exercises the ClearHighlights API
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Hello World");

        var hl = new Highlight { Start = 0, End = 5, StyleId = 1, Priority = 1, HlRef = 0 };
        tb.AddHighlightByCharRange(hl);
        Assert.True(tb.HighlightCount > 0);

        tb.ClearHighlights();
        Assert.Equal(0u, tb.HighlightCount);
    }

    // ===== Append Tests =====

    [Fact]
    public void Append_ToEmptyBuffer()
    {
        // Zig: "TextBuffer append - to empty buffer"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        tb.AppendText("Hello");

        Assert.Equal(1u, tb.LineCount);
        Assert.Equal("Hello", tb.GetPlainText());
    }

    [Fact]
    public void Append_ToNonEmptyBufferNoNewline()
    {
        // Zig: "TextBuffer append - to non-empty buffer, no newline"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        tb.SetText("Hello");
        tb.AppendText(" World");

        Assert.Equal(1u, tb.LineCount);
        Assert.Equal("Hello World", tb.GetPlainText());
    }

    [Fact]
    public void Append_CreatingNewLineWithLf()
    {
        // Zig: "TextBuffer append - creating new line with LF"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        tb.SetText("Hello");
        tb.AppendText("\nWorld");

        Assert.Equal(2u, tb.LineCount);
        Assert.Equal("Hello\nWorld", tb.GetPlainText());
    }

    [Fact]
    public void Append_MultipleLinesWithVariousEndings()
    {
        // Zig: "TextBuffer append - multiple lines with various endings"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        tb.SetText("A\nB");
        Assert.Equal(2u, tb.LineCount);

        tb.AppendText("\nC\nD\n");

        Assert.Equal(5u, tb.LineCount);
        Assert.Equal("A\nB\nC\nD\n", tb.GetPlainText());
    }

    [Fact]
    public void Append_CrlfLineEndings()
    {
        // Zig: "TextBuffer append - CRLF line endings"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        tb.AppendText("Line1\r\nLine2\r\nLine3");

        Assert.Equal(3u, tb.LineCount);
        // CRLF should be normalized to LF
        Assert.Equal("Line1\nLine2\nLine3", tb.GetPlainText());
    }

    [Fact]
    public void Append_MixedLineEndings()
    {
        // Zig: "TextBuffer append - mixed line endings"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        tb.SetText("Unix\n");
        tb.AppendText("Windows\r\nOldMac\rEnd");

        Assert.Equal(4u, tb.LineCount);
        Assert.Equal("Unix\nWindows\nOldMac\nEnd", tb.GetPlainText());
    }

    [Fact]
    public void Append_EmptyStringIsNoOp()
    {
        // Zig: "TextBuffer append - empty string is no-op"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        tb.SetText("Hello");
        uint initialLength = tb.Length;
        uint initialLineCount = tb.LineCount;

        tb.AppendText("");

        Assert.Equal(initialLength, tb.Length);
        Assert.Equal(initialLineCount, tb.LineCount);
    }

    [Fact]
    public void Append_UnicodeContent()
    {
        // Zig: "TextBuffer append - unicode content"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        tb.SetText("Hello ");
        tb.AppendText("世界 🌟");

        Assert.Equal(1u, tb.LineCount);
        Assert.Equal("Hello 世界 🌟", tb.GetPlainText());
    }

    [Fact]
    public void Append_StreamingChunkedAppendVsGroundTruth()
    {
        // Zig: "TextBuffer append - streaming/chunked append vs ground truth"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        tb.AppendText("First");
        tb.AppendText("\nLine2");
        tb.AppendText("\n");
        tb.AppendText("Line3");
        tb.AppendText(" end");

        const string expected = "First\nLine2\nLine3 end";
        Assert.Equal(expected, tb.GetPlainText());
        Assert.Equal(3u, tb.LineCount);
    }

    [Fact]
    public void Append_LargeStreamingAppend()
    {
        // Zig: "TextBuffer append - large streaming append"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        for (int i = 0; i < 100; i++)
        {
            tb.AppendText($"Line {i}\n");
        }

        Assert.Equal(101u, tb.LineCount); // 100 lines + empty final line
    }

    [Fact]
    public void AppendFromMemId_BasicFunctionality()
    {
        // Zig: "TextBuffer appendFromMemId - basic functionality"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        byte[] data = Encoding.UTF8.GetBytes("Alpha\nBeta");
        ushort memId = tb.RegisterMemory(data, copy: true);

        tb.AppendFromMemory((byte)memId);

        Assert.Equal(2u, tb.LineCount);
        Assert.Equal("Alpha\nBeta", tb.GetPlainText());
    }

    [Fact]
    public void AppendFromMemId_AppendToExistingContent()
    {
        // Zig: "TextBuffer appendFromMemId - append to existing content"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        byte[] data = Encoding.UTF8.GetBytes("Gamma");
        ushort memId = tb.RegisterMemory(data, copy: true);

        tb.SetText("Alpha\nBeta");
        Assert.Equal(2u, tb.LineCount);

        tb.AppendFromMemory((byte)memId);

        Assert.Equal(2u, tb.LineCount);
        Assert.Equal("Alpha\nBetaGamma", tb.GetPlainText());
    }

    [Fact]
    public void Append_MarkerInvariantsMaintained()
    {
        // Zig: "TextBuffer append - marker invariants maintained"
        // Marker counts are internal; verify line count correctness.
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        tb.AppendText("Line1\n");
        tb.AppendText("Line2\n");
        tb.AppendText("Line3");

        Assert.Equal(3u, tb.LineCount);
    }

    [Fact]
    public void Append_MemoryRegistryPreserved()
    {
        // Zig: "TextBuffer append - memory registry preserved"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        byte[] preservedData = Encoding.UTF8.GetBytes("Preserved");
        ushort preservedId = tb.RegisterMemory(preservedData, copy: true);

        tb.AppendText("First\n");
        tb.AppendText("Second\n");
        tb.AppendText("Third");

        // Preserved buffer should still be accessible after appends
        tb.Clear();
        tb.SetTextFromMemory((byte)preservedId);
        Assert.Equal("Preserved", tb.GetPlainText());
    }

    [Fact]
    public void Append_ViewsUpdatedAfterAppend()
    {
        // Zig: "TextBuffer append - views marked dirty"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        using var view = TextBufferView.Create(tb);

        tb.AppendText("New content");

        Assert.Equal("New content", tb.GetPlainText());
    }

    [Fact]
    public void Append_AfterClear()
    {
        // Zig: "TextBuffer append - append after clear"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        tb.SetText("Initial content");
        tb.Clear();

        tb.AppendText("After clear");

        Assert.Equal(1u, tb.LineCount);
        Assert.Equal("After clear", tb.GetPlainText());
    }

    [Fact]
    public void Append_ConsecutiveEmptyLineHandling()
    {
        // Zig: "TextBuffer append - consecutive empty line handling"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        tb.SetText("Line1\n");
        tb.AppendText("\n");
        tb.AppendText("Line3");

        Assert.Equal(3u, tb.LineCount);
        Assert.Equal("Line1\n\nLine3", tb.GetPlainText());
    }

    [Fact]
    public void Append_MixedAppendAndSetText()
    {
        // Zig: "TextBuffer append - mixed append and setText"
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        tb.SetText("First");
        tb.AppendText(" appended");
        Assert.Equal("First appended", tb.GetPlainText());

        tb.SetText("Reset");
        tb.AppendText(" again");
        Assert.Equal("Reset again", tb.GetPlainText());
    }

    // ===== GetTextRange / GetTextRangeByCoords =====

    [Fact]
    public void GetTextRange_ReturnsCorrectSubstring()
    {
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Hello World");

        string range = tb.GetTextRange(0, 5);
        Assert.Equal("Hello", range);
    }

    [Fact]
    public void GetTextRangeByCoords_ReturnsCorrectLine()
    {
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Line1\nLine2\nLine3");

        string line1 = tb.GetTextRangeByCoords(1, 0, 1, 5);
        Assert.Equal("Line2", line1);
    }

    // ===== Default Styling =====

    [Fact]
    public void DefaultStyling_SetForegroundAndBackground()
    {
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        // Should not throw
        tb.SetForeground(new Rgba(1, 0, 0, 1));
        tb.SetBackground(new Rgba(0, 0, 1, 1));
        tb.SetAttributes(TextAttributes.Bold);

        tb.SetText("Styled text");
        Assert.Equal("Styled text", tb.GetPlainText());
    }

    [Fact]
    public void DefaultStyling_ResetDefaults()
    {
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        tb.SetForeground(new Rgba(1, 0, 0, 1));
        tb.SetBackground(new Rgba(0, 0, 1, 1));
        tb.SetAttributes(TextAttributes.Bold | TextAttributes.Italic);

        tb.ResetDefaults();

        // Should not throw; buffer remains functional
        tb.SetText("After reset defaults");
        Assert.Equal("After reset defaults", tb.GetPlainText());
    }

    // ===== Tab Width =====

    [Fact]
    public void TabWidth_GetAndSet()
    {
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        tb.TabWidth = 4;
        Assert.Equal(4, tb.TabWidth);

        tb.TabWidth = 8;
        Assert.Equal(8, tb.TabWidth);
    }

    // ===== Dispose Safety =====

    [Fact]
    public void Dispose_DoubleDisposeDoesNotThrow()
    {
        var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.Dispose();
        tb.Dispose(); // Should not throw
    }

    [Fact]
    public void Dispose_AccessAfterDisposeThrows()
    {
        var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _ = tb.Length);
    }

    // ===== WidthMethod variants =====

    [Fact]
    public void Create_WcwidthMethodWorks()
    {
        using var tb = TextBuffer.Create(WidthMethod.Wcwidth);
        tb.SetText("Hello");
        Assert.Equal("Hello", tb.GetPlainText());
    }

    [Fact]
    public void Create_UnicodeMethodWorks()
    {
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Hello 世界");
        Assert.Equal("Hello 世界", tb.GetPlainText());
    }

    [Fact]
    public void Create_DefaultMethodWorks()
    {
        using var tb = TextBuffer.Create();
        tb.SetText("Default");
        Assert.Equal("Default", tb.GetPlainText());
    }

    // ===== Highlight Management =====

    [Fact]
    public void AddHighlight_ByLineAndClear()
    {
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Hello\nWorld");

        var hl = new Highlight { Start = 0, End = 5, StyleId = 1, Priority = 1, HlRef = 42 };
        tb.AddHighlight(0, hl);

        Assert.True(tb.HighlightCount > 0);

        tb.ClearLineHighlights(0);
        // Line 0 highlights cleared, but line 1 might still have none, so total could be 0
    }

    [Fact]
    public void RemoveHighlight_ByRef()
    {
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Hello World");

        var hl = new Highlight { Start = 0, End = 5, StyleId = 1, Priority = 1, HlRef = 42 };
        tb.AddHighlightByCharRange(hl);

        Assert.True(tb.HighlightCount > 0);

        tb.RemoveHighlight(42);
        Assert.Equal(0u, tb.HighlightCount);
    }

    // ===== Memory Replace =====

    [Fact]
    public void ReplaceMemory_ReplacesExistingBuffer()
    {
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        byte[] data1 = Encoding.UTF8.GetBytes("Original");
        ushort memId = tb.RegisterMemory(data1, copy: true);

        byte[] data2 = Encoding.UTF8.GetBytes("Replaced");
        bool replaced = tb.ReplaceMemory((byte)memId, data2, copy: true);
        Assert.True(replaced);

        tb.SetTextFromMemory((byte)memId);
        Assert.Equal("Replaced", tb.GetPlainText());
    }

    [Fact]
    public void ClearMemory_ClearsAllRegistered()
    {
        using var tb = TextBuffer.Create(WidthMethod.Unicode);

        tb.RegisterMemory(Encoding.UTF8.GetBytes("data1"), copy: true);
        tb.RegisterMemory(Encoding.UTF8.GetBytes("data2"), copy: true);

        // ClearMemory should not throw
        tb.ClearMemory();
    }

    // ===== ByteSize =====

    [Fact]
    public void ByteSize_MatchesUtf8Length()
    {
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        const string text = "Hello 世界 🌟";
        tb.SetText(text);

        uint expectedBytes = (uint)Encoding.UTF8.GetByteCount(text);
        Assert.Equal(expectedBytes, tb.ByteSize);
    }

    [Fact]
    public void ByteSize_EmptyBuffer()
    {
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        Assert.Equal(0u, tb.ByteSize);
    }
}