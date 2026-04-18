using OpenTui.Core;
using Xunit;

namespace OpenTui.Core.Tests;

public class EditBufferTests
{
    // ===== Init Tests =====

    [Fact]
    public void InitAndDeinit()
    {
        using var eb = EditBuffer.Create();
        var text = eb.GetText();
        Assert.Equal("", text);

        var cursor = eb.GetCursorPosition();
        Assert.Equal(0u, cursor.Row);
        Assert.Equal(0u, cursor.Col);
    }

    // ===== Word Boundary Tests =====

    [Fact]
    public void NextWordBoundary_Basic()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("Hello World");
        eb.SetCursor(0, 0);

        var next = eb.GetNextWordBoundary();
        Assert.Equal(0u, next.Row);
        Assert.Equal(6u, next.Col);
    }

    [Fact]
    public void PrevWordBoundary_Basic()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("Hello World");
        eb.SetCursor(0, 7);

        var prev = eb.GetPrevWordBoundary();
        Assert.Equal(0u, prev.Row);
        Assert.Equal(6u, prev.Col);
    }

    [Fact]
    public void NextWordBoundary_AcrossLine()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("Hello\nWorld");
        eb.SetCursor(0, 5);

        var next = eb.GetNextWordBoundary();
        Assert.Equal(1u, next.Row);
        Assert.Equal(0u, next.Col);
    }

    [Fact]
    public void PrevWordBoundary_AcrossLine()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("Hello\nWorld");
        eb.SetCursor(1, 0);

        var prev = eb.GetPrevWordBoundary();
        Assert.Equal(0u, prev.Row);
        Assert.Equal(5u, prev.Col);
    }

    [Fact]
    public void HyphenWordBoundary()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("self-contained");
        eb.SetCursor(0, 0);

        var next = eb.GetNextWordBoundary();
        Assert.Equal(0u, next.Row);
        Assert.Equal(5u, next.Col);
    }

    [Fact]
    public void MultipleWordBoundaries()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("The quick brown fox");
        eb.SetCursor(0, 0);

        var cursor = eb.GetNextWordBoundary();
        Assert.Equal(4u, cursor.Col);

        eb.SetCursor(cursor.Row, cursor.Col);
        cursor = eb.GetNextWordBoundary();
        Assert.Equal(10u, cursor.Col);

        eb.SetCursor(cursor.Row, cursor.Col);
        cursor = eb.GetNextWordBoundary();
        Assert.Equal(16u, cursor.Col);
    }

    [Fact]
    public void WordBoundary_AtEndOfLine()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("Hello");
        eb.SetCursor(0, 5);

        var next = eb.GetNextWordBoundary();
        Assert.Equal(0u, next.Row);
        Assert.Equal(5u, next.Col);
    }

    [Fact]
    public void WordBoundary_AtStartOfLine()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("Hello");
        eb.SetCursor(0, 0);

        var prev = eb.GetPrevWordBoundary();
        Assert.Equal(0u, prev.Row);
        Assert.Equal(0u, prev.Col);
    }

    // ===== GetEOL Tests =====

    [Fact]
    public void GetEOL_Basic()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("Hello World");
        eb.SetCursor(0, 0);

        var eol = eb.GetEOL();
        Assert.Equal(0u, eol.Row);
        Assert.Equal(11u, eol.Col);
    }

    [Fact]
    public void GetEOL_AtEndOfLine()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("Hello");
        eb.SetCursor(0, 5);

        var eol = eb.GetEOL();
        Assert.Equal(0u, eol.Row);
        Assert.Equal(5u, eol.Col);
    }

    [Fact]
    public void GetEOL_MultiLine()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("Hello\nWorld\nTest");
        eb.SetCursor(1, 0);

        var eol = eb.GetEOL();
        Assert.Equal(1u, eol.Row);
        Assert.Equal(5u, eol.Col);
    }

    [Fact]
    public void GetEOL_EmptyLine()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("Hello\n\nWorld");
        eb.SetCursor(1, 0);

        var eol = eb.GetEOL();
        Assert.Equal(1u, eol.Row);
        Assert.Equal(0u, eol.Col);
    }

    // ===== Word Boundary with Tabs =====

    [Fact]
    public void WordBoundary_WithTabs()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("Hello\tWorld");
        eb.SetCursor(0, 12);

        var prev = eb.GetPrevWordBoundary();
        Assert.Equal(7u, prev.Col);

        eb.SetCursor(0, 0);
        var next = eb.GetNextWordBoundary();
        Assert.Equal(7u, next.Col);
    }

    // ===== Word Boundary with CJK =====

    [Fact]
    public void WordBoundary_WithCJKGraphemes()
    {
        using var eb = EditBuffer.Create();
        // "你" = 2 cols, " " = 1 col, "好" = 2 cols
        eb.InsertText("你 好");
        eb.SetCursor(0, 0);

        var next = eb.GetNextWordBoundary();
        Assert.Equal(3u, next.Col);

        eb.SetCursor(0, 5);
        var prev = eb.GetPrevWordBoundary();
        Assert.Equal(3u, prev.Col);
    }

    [Fact]
    public void WordBoundary_MixedCJKAndASCII()
    {
        using var eb = EditBuffer.Create();
        eb.SetText("日本語abc");

        var eol = eb.GetEOL();
        Assert.True(eol.Col >= 3);

        eb.SetCursor(0, 0);
        var next = eb.GetNextWordBoundary();
        Assert.Equal(eol.Col - 3, next.Col);

        eb.SetCursor(next.Row, next.Col);
        var next2 = eb.GetNextWordBoundary();
        Assert.Equal(eol.Col, next2.Col);

        eb.SetCursor(0, eol.Col);
        var prev = eb.GetPrevWordBoundary();
        Assert.Equal(eol.Col - 3, prev.Col);

        eb.SetCursor(prev.Row, prev.Col);
        var prev2 = eb.GetPrevWordBoundary();
        Assert.Equal(0u, prev2.Col);
    }

    [Fact]
    public void WordBoundary_KeepsHangulRunGrouped()
    {
        using var eb = EditBuffer.Create();
        eb.SetText("테스트test");

        var eol = eb.GetEOL();
        Assert.True(eol.Col >= 4);

        eb.SetCursor(0, 0);
        var next = eb.GetNextWordBoundary();
        Assert.Equal(eol.Col - 4, next.Col);

        eb.SetCursor(next.Row, next.Col);
        var next2 = eb.GetNextWordBoundary();
        Assert.Equal(eol.Col, next2.Col);

        eb.SetCursor(0, eol.Col);
        var prev = eb.GetPrevWordBoundary();
        Assert.Equal(eol.Col - 4, prev.Col);

        eb.SetCursor(prev.Row, prev.Col);
        var prev2 = eb.GetPrevWordBoundary();
        Assert.Equal(0u, prev2.Col);
    }

    [Fact]
    public void WordBoundary_CJKPunctuationBeforeASCII()
    {
        using var eb = EditBuffer.Create();
        eb.SetText("日本語。abc");

        var eol = eb.GetEOL();
        Assert.True(eol.Col >= 5);

        eb.SetCursor(0, 0);
        var next = eb.GetNextWordBoundary();
        Assert.Equal(eol.Col - 3, next.Col);

        eb.SetCursor(next.Row, next.Col);
        var next2 = eb.GetNextWordBoundary();
        Assert.Equal(eol.Col, next2.Col);

        eb.SetCursor(0, eol.Col);
        var prev = eb.GetPrevWordBoundary();
        Assert.Equal(eol.Col - 3, prev.Col);

        eb.SetCursor(prev.Row, prev.Col);
        var prev2 = eb.GetPrevWordBoundary();
        Assert.Equal(0u, prev2.Col);
    }

    [Fact]
    public void WordBoundary_CompatIdeographAndASCII()
    {
        using var eb = EditBuffer.Create();
        eb.SetText("丽abc");

        var eol = eb.GetEOL();
        Assert.True(eol.Col >= 3);

        eb.SetCursor(0, 0);
        var next = eb.GetNextWordBoundary();
        Assert.Equal(eol.Col - 3, next.Col);

        eb.SetCursor(next.Row, next.Col);
        var next2 = eb.GetNextWordBoundary();
        Assert.Equal(eol.Col, next2.Col);

        eb.SetCursor(0, eol.Col);
        var prev = eb.GetPrevWordBoundary();
        Assert.Equal(eol.Col - 3, prev.Col);

        eb.SetCursor(prev.Row, prev.Col);
        var prev2 = eb.GetPrevWordBoundary();
        Assert.Equal(0u, prev2.Col);
    }

    [Fact]
    public void WordBoundary_SingleCharScriptTransitions()
    {
        using var eb = EditBuffer.Create();

        // "a日" - ASCII then CJK
        eb.SetText("a日");

        var eol = eb.GetEOL();
        Assert.Equal(3u, eol.Col);

        eb.SetCursor(0, 0);
        var next = eb.GetNextWordBoundary();
        Assert.Equal(1u, next.Col);

        eb.SetCursor(next.Row, next.Col);
        var next2 = eb.GetNextWordBoundary();
        Assert.Equal(3u, next2.Col);

        eb.SetCursor(0, eol.Col);
        var prev = eb.GetPrevWordBoundary();
        Assert.Equal(1u, prev.Col);

        eb.SetCursor(prev.Row, prev.Col);
        var prev2 = eb.GetPrevWordBoundary();
        Assert.Equal(0u, prev2.Col);

        // "日a" - CJK then ASCII
        eb.SetText("日a");

        eol = eb.GetEOL();
        Assert.Equal(3u, eol.Col);

        eb.SetCursor(0, 0);
        next = eb.GetNextWordBoundary();
        Assert.Equal(2u, next.Col);

        eb.SetCursor(next.Row, next.Col);
        next2 = eb.GetNextWordBoundary();
        Assert.Equal(3u, next2.Col);

        eb.SetCursor(0, eol.Col);
        prev = eb.GetPrevWordBoundary();
        Assert.Equal(2u, prev.Col);

        eb.SetCursor(prev.Row, prev.Col);
        prev2 = eb.GetPrevWordBoundary();
        Assert.Equal(0u, prev2.Col);
    }

    [Fact]
    public void WordBoundary_WithEmoji()
    {
        using var eb = EditBuffer.Create();
        // "🌟" = 2 cols, " " = 1 col, "ok" = 2 cols
        eb.InsertText("🌟 ok");
        eb.SetCursor(0, 0);

        var next = eb.GetNextWordBoundary();
        Assert.Equal(3u, next.Col);

        eb.SetCursor(0, 5);
        var prev = eb.GetPrevWordBoundary();
        Assert.Equal(3u, prev.Col);
    }

    // ===== Tab Cursor Movement Tests =====

    [Fact]
    public void MoveRight_PastTabAtStartOfLine()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("\tHello");
        eb.SetCursor(0, 0);

        eb.MoveCursorRight();
        var cursor = eb.GetCursorPosition();
        Assert.True(cursor.Col > 0);

        eb.MoveCursorRight();
        var cursor2 = eb.GetCursorPosition();
        Assert.True(cursor2.Col > cursor.Col);
    }

    [Fact]
    public void MoveRight_AfterTypingBeforeTab()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("\tWorld");
        eb.SetCursor(0, 0);
        eb.InsertText("Hi");

        var cursorAfterInsert = eb.GetCursorPosition();
        Assert.Equal(0u, cursorAfterInsert.Row);

        eb.MoveCursorRight();
        var cursorAfterMove1 = eb.GetCursorPosition();
        Assert.True(cursorAfterMove1.Col > cursorAfterInsert.Col);

        eb.MoveCursorRight();
        var cursorAfterMove2 = eb.GetCursorPosition();
        Assert.True(cursorAfterMove2.Col > cursorAfterMove1.Col);

        eb.MoveCursorRight();
        var cursorAfterMove3 = eb.GetCursorPosition();
        Assert.True(cursorAfterMove3.Col > cursorAfterMove2.Col);
    }

    [Fact]
    public void MoveRight_BetweenTwoTabs()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("\t\tHello");
        eb.SetCursor(0, 0);

        uint prevCol = 0;
        for (int i = 0; i < 10; i++)
        {
            eb.MoveCursorRight();
            var cursor = eb.GetCursorPosition();
            Assert.True(cursor.Col >= prevCol);
            prevCol = cursor.Col;
        }
    }

    [Fact]
    public void TypeAndMoveAroundSingleTab()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("\t");
        eb.SetCursor(0, 0);
        eb.InsertText("a");

        var cursor1 = eb.GetCursorPosition();
        Assert.Equal(0u, cursor1.Row);

        eb.MoveCursorRight();
        var cursor2 = eb.GetCursorPosition();
        Assert.True(cursor2.Col > cursor1.Col);
    }

    [Fact]
    public void InsertTextBetweenTabsAndMoveRight()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("\t\tx");
        eb.SetCursor(0, 0);

        eb.MoveCursorRight();

        eb.InsertText("A");
        var afterInsert = eb.GetCursorPosition();

        eb.MoveCursorRight();
        var afterMove1 = eb.GetCursorPosition();
        Assert.True(afterMove1.Col > afterInsert.Col);

        eb.MoveCursorRight();
        var afterMove2 = eb.GetCursorPosition();
        Assert.True(afterMove2.Col > afterMove1.Col);

        eb.MoveCursorRight();
        var afterMove3 = eb.GetCursorPosition();
        // Should reach append position and stay there
        Assert.Equal(afterMove2.Col, afterMove3.Col);
    }

    [Fact]
    public void InsertAfterTabAndMoveAround()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("\t");
        var tabWidth = eb.GetCursorPosition().Col;

        eb.InsertText("x");
        var afterX = eb.GetCursorPosition();

        eb.MoveCursorLeft();
        var beforeX = eb.GetCursorPosition();
        Assert.Equal(tabWidth, beforeX.Col);

        eb.MoveCursorRight();
        var backAtX = eb.GetCursorPosition();
        Assert.Equal(afterX.Col, backAtX.Col);

        // Already at append position, can't move further
        eb.MoveCursorRight();
        var stillAtX = eb.GetCursorPosition();
        Assert.Equal(backAtX.Col, stillAtX.Col);
    }

    [Fact]
    public void CursorStuckAfterTypingAroundTab()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("hello\tworld");
        eb.SetCursor(0, 5);

        eb.MoveCursorRight();
        var pos1 = eb.GetCursorPosition();

        eb.MoveCursorRight();
        var pos2 = eb.GetCursorPosition();
        Assert.True(pos2.Col > pos1.Col);
    }

    [Fact]
    public void ComplexTabScenario()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("\tx\ty");
        eb.SetCursor(0, 0);

        eb.MoveCursorRight();
        var p1 = eb.GetCursorPosition();

        eb.MoveCursorRight();
        var p2 = eb.GetCursorPosition();
        Assert.True(p2.Col > p1.Col);

        eb.MoveCursorRight();
        var p3 = eb.GetCursorPosition();
        Assert.True(p3.Col > p2.Col);

        eb.MoveCursorRight();
        var p4 = eb.GetCursorPosition();
        Assert.True(p4.Col > p3.Col);

        // Already at append position, can't move further
        eb.MoveCursorRight();
        var p5 = eb.GetCursorPosition();
        Assert.Equal(p4.Col, p5.Col);
    }

    [Fact]
    public void CursorStuckAtTabInMiddleOfLine()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("a\tb");
        eb.SetCursor(0, 1);

        eb.MoveCursorRight();
        var p1 = eb.GetCursorPosition();

        eb.MoveCursorRight();
        var p2 = eb.GetCursorPosition();
        Assert.True(p2.Col > p1.Col);
    }

    [Fact]
    public void TypeBetweenTabsThenMoveRight()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("\t\t");
        eb.SetCursor(0, 2);
        eb.InsertText("x");

        var afterInsert = eb.GetCursorPosition();

        eb.MoveCursorRight();
        var p1 = eb.GetCursorPosition();
        Assert.True(p1.Col > afterInsert.Col);

        // Already at append position, can't move further
        eb.MoveCursorRight();
        var p2 = eb.GetCursorPosition();
        Assert.Equal(p1.Col, p2.Col);
    }

    [Fact]
    public void TabsOnlyWithCursorMovement()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("\t\t\t");
        eb.SetCursor(0, 0);

        uint prevCol = 0;
        for (int i = 0; i < 5; i++)
        {
            eb.MoveCursorRight();
            var cursor = eb.GetCursorPosition();
            Assert.True(cursor.Col >= prevCol);
            prevCol = cursor.Col;
        }
    }

    // ===== GetTextRange Tests =====

    [Fact]
    public void GetTextRange_BasicASCII()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("Hello World");

        var text = eb.GetTextRange(0, 5);
        Assert.Equal("Hello", text);
    }

    [Fact]
    public void GetTextRange_FullText()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("Hello World");

        var text = eb.GetTextRange(0, 11);
        Assert.Equal("Hello World", text);
    }

    [Fact]
    public void GetTextRange_WithEmojis()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("Hello 👋 World");

        // "Hello " = 6 cols, emoji = 2 cols
        var text = eb.GetTextRange(6, 8);
        Assert.Equal("👋", text);
    }

    [Fact]
    public void GetTextRange_EmojiWithSkinTone()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("Hi 👋🏽 there");

        // "Hi " = 3 cols, emoji = 2 cols
        var text = eb.GetTextRange(3, 5);
        Assert.Equal("👋🏽", text);
    }

    [Fact]
    public void GetTextRange_FlagEmoji()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("Flag: 🇺🇸 here");

        // "Flag: " = 6 cols, flag = 2 cols
        var text = eb.GetTextRange(6, 8);
        Assert.Equal("🇺🇸", text);
    }

    [Fact]
    public void GetTextRange_FamilyEmoji()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("Family: 👨\u200D👩\u200D👧\u200D👦 end");

        // "Family: " = 8 cols, family emoji = 2 cols
        var text = eb.GetTextRange(8, 10);
        Assert.Equal("👨\u200D👩\u200D👧\u200D👦", text);
    }

    [Fact]
    public void GetTextRange_DevanagariWithCombiningMarks()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("Say नमस्ते ok");

        // "Say " = 4 cols, "नमस्ते" = 5 cols (4-8 adjusted)
        var text = eb.GetTextRange(4, 8);
        Assert.Equal("नमस्ते", text);
    }

    [Fact]
    public void GetTextRange_CJKCharacters()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("Say 你好 end");

        // "Say " = 4 cols, 你 = 2 cols, 好 = 2 cols
        var text = eb.GetTextRange(4, 8);
        Assert.Equal("你好", text);
    }

    [Fact]
    public void GetTextRange_SingleCJKCharacter()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("A 日 B");

        // "A " = 2 cols, 日 = 2 cols at offset 2-4
        var text = eb.GetTextRange(2, 4);
        Assert.Equal("日", text);
    }

    [Fact]
    public void GetTextRange_AcrossLines()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("Hello\nWorld");

        var text = eb.GetTextRange(3, 8);
        Assert.Equal("lo\nWo", text);
    }

    [Fact]
    public void GetTextRange_WithTabs()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("A\tB");

        var text = eb.GetTextRange(0, 10);
        Assert.Equal("A\tB", text);
    }

    [Fact]
    public void GetTextRange_PartialGraphemeSnapToStart()
    {
        using var eb = EditBuffer.Create();
        // CJK character is 2 cols wide
        eb.InsertText("A 好 B");

        // Try to get range starting at middle of 好 (offset 3), should snap to start (offset 2)
        var text = eb.GetTextRange(3, 5);
        Assert.Equal("好 ", text);
    }

    [Fact]
    public void GetTextRange_PartialGraphemeSnapToEnd()
    {
        using var eb = EditBuffer.Create();
        // CJK character is 2 cols wide
        eb.InsertText("A 好 B");

        // Try to get range ending at middle of 好 (offset 3), should snap to end (offset 4)
        var text = eb.GetTextRange(0, 3);
        Assert.Equal("A 好", text);
    }

    [Fact]
    public void GetTextRange_EmptyRange()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("Hello");

        var text = eb.GetTextRange(5, 5);
        Assert.Equal("", text);
    }

    [Fact]
    public void GetTextRange_OutOfBounds()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("Hello");

        var text = eb.GetTextRange(0, 1000);
        Assert.Equal("Hello", text);
    }

    [Fact]
    public void GetTextRange_MixedScripts()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("Hi 👋 世界 नमस्ते");

        // Get everything
        var total = eb.GetTextRange(0, 100);
        Assert.Equal("Hi 👋 世界 नमस्ते", total);

        // Get just the emoji
        var emoji = eb.GetTextRange(3, 5);
        Assert.Equal("👋", emoji);

        // Get the CJK part: "Hi " = 3, "👋 " = 3, "世界" = 4 (cols 6-10)
        var cjk = eb.GetTextRange(6, 10);
        Assert.Equal("世界", cjk);
    }

    [Fact]
    public void GetTextRange_BeforeCursor()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("Hello World");
        eb.SetCursor(0, 5);

        var cursor = eb.GetCursorPosition();
        var text = eb.GetTextRange(0, cursor.Offset);
        Assert.Equal("Hello", text);
    }

    [Fact]
    public void GetTextRange_MultilineWithEmojis()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("Line1 👋\nLine2 🎉\nLine3");

        var text = eb.GetTextRange(0, 100);
        Assert.Equal("Line1 👋\nLine2 🎉\nLine3", text);
    }

    // ===== Wcwidth Mode Tests =====

    [Fact]
    public void Wcwidth_MultiCodepointEmojiAsSeparateChars()
    {
        using var eb = EditBuffer.Create(); // Wcwidth is default (0)

        // Hand emoji with skin tone: should be 2 separate chars width 2 each = 4
        eb.SetText("👋🏻");
        eb.SetCursor(0, 0);

        eb.MoveCursorRight();
        var cursor = eb.GetCursorPosition();
        Assert.Equal(2u, cursor.Col);

        eb.MoveCursorRight();
        cursor = eb.GetCursorPosition();
        Assert.Equal(4u, cursor.Col);

        // Family emoji: man + ZWJ + woman + ZWJ + girl = 2+0+2+0+2 = 6
        eb.SetText("👨\u200D👩\u200D👧");
        eb.SetCursor(0, 0);

        eb.MoveCursorRight();
        cursor = eb.GetCursorPosition();
        Assert.Equal(2u, cursor.Col);

        eb.MoveCursorRight();
        cursor = eb.GetCursorPosition();
        Assert.Equal(4u, cursor.Col);

        eb.MoveCursorRight();
        cursor = eb.GetCursorPosition();
        Assert.Equal(6u, cursor.Col);

        // Girl with laptop: woman + ZWJ + laptop = 2+0+2 = 4
        eb.SetText("👩\u200D💻");
        eb.SetCursor(0, 0);

        eb.MoveCursorRight();
        cursor = eb.GetCursorPosition();
        Assert.Equal(2u, cursor.Col);

        eb.MoveCursorRight();
        cursor = eb.GetCursorPosition();
        Assert.Equal(4u, cursor.Col);
    }

    [Fact]
    public void Wcwidth_ComprehensiveEmojiCursorMovementAndBackspace()
    {
        using var eb = EditBuffer.Create();

        // Woman technologist: Woman + skin tone + ZWJ + laptop = 2+2+0+2 = 6
        var womanTech = "👩🏽\u200D💻";
        eb.SetText(womanTech);
        eb.SetCursor(0, 0);

        eb.MoveCursorRight(); // Past woman (width 2)
        var cursor = eb.GetCursorPosition();
        Assert.Equal(2u, cursor.Col);

        eb.MoveCursorRight(); // Past skin tone (width 2)
        cursor = eb.GetCursorPosition();
        Assert.Equal(4u, cursor.Col);

        eb.MoveCursorRight(); // Past laptop (ZWJ is skipped)
        cursor = eb.GetCursorPosition();
        Assert.Equal(6u, cursor.Col);

        // Test moving back left
        eb.MoveCursorLeft();
        cursor = eb.GetCursorPosition();
        Assert.Equal(4u, cursor.Col);

        eb.MoveCursorLeft();
        cursor = eb.GetCursorPosition();
        Assert.Equal(2u, cursor.Col);

        eb.MoveCursorLeft();
        cursor = eb.GetCursorPosition();
        Assert.Equal(0u, cursor.Col);

        // Test backspace from end
        eb.SetCursor(0, 6);
        var text = eb.GetText();
        Assert.Equal(womanTech, text);

        eb.DeleteCharBackward(); // Delete laptop, move to col 4
        cursor = eb.GetCursorPosition();
        Assert.Equal(4u, cursor.Col);

        eb.DeleteCharBackward(); // Delete skin tone (skips ZWJ), move to col 2
        cursor = eb.GetCursorPosition();
        Assert.Equal(2u, cursor.Col);

        eb.DeleteCharBackward(); // Delete woman, move to col 0
        cursor = eb.GetCursorPosition();
        Assert.Equal(0u, cursor.Col);

        text = eb.GetText();
        Assert.Equal("", text);

        // Family emoji: Man + ZWJ + Woman + ZWJ + Girl + ZWJ + Boy = 2+0+2+0+2+0+2 = 8
        var family = "👨\u200D👩\u200D👧\u200D👦";
        eb.SetText(family);
        eb.SetCursor(0, 0);

        eb.MoveCursorRight(); // Man
        cursor = eb.GetCursorPosition();
        Assert.Equal(2u, cursor.Col);

        eb.MoveCursorRight(); // Woman (skips ZWJ)
        cursor = eb.GetCursorPosition();
        Assert.Equal(4u, cursor.Col);

        eb.MoveCursorRight(); // Girl (skips ZWJ)
        cursor = eb.GetCursorPosition();
        Assert.Equal(6u, cursor.Col);

        eb.MoveCursorRight(); // Boy (skips ZWJ)
        cursor = eb.GetCursorPosition();
        Assert.Equal(8u, cursor.Col);

        // Move back
        eb.MoveCursorLeft();
        cursor = eb.GetCursorPosition();
        Assert.Equal(6u, cursor.Col);

        eb.MoveCursorLeft();
        cursor = eb.GetCursorPosition();
        Assert.Equal(4u, cursor.Col);

        eb.MoveCursorLeft();
        cursor = eb.GetCursorPosition();
        Assert.Equal(2u, cursor.Col);

        // Rainbow flag: Flag + VS16 + ZWJ + Rainbow = 1+0+0+2 = 3
        var rainbowFlag = "🏳️\u200D🌈";
        eb.SetText(rainbowFlag);
        eb.SetCursor(0, 0);

        eb.MoveCursorRight(); // White flag (width 1, skips VS16 and ZWJ)
        cursor = eb.GetCursorPosition();
        Assert.Equal(1u, cursor.Col);

        eb.MoveCursorRight(); // Rainbow
        cursor = eb.GetCursorPosition();
        Assert.Equal(3u, cursor.Col);

        // US flag: Regional indicators = 1+1 = 2
        var usFlag = "🇺🇸";
        eb.SetText(usFlag);
        eb.SetCursor(0, 0);

        eb.MoveCursorRight();
        cursor = eb.GetCursorPosition();
        Assert.Equal(1u, cursor.Col);

        eb.MoveCursorRight();
        cursor = eb.GetCursorPosition();
        Assert.Equal(2u, cursor.Col);

        eb.MoveCursorLeft();
        cursor = eb.GetCursorPosition();
        Assert.Equal(1u, cursor.Col);

        eb.MoveCursorLeft();
        cursor = eb.GetCursorPosition();
        Assert.Equal(0u, cursor.Col);

        // Mixed text
        var mixedText = "A 👩🏽\u200D💻 B 👨\u200D👩\u200D👧\u200D👦 C";
        eb.SetText(mixedText);
        eb.SetCursor(0, 0);

        // A(1) + space(1) + woman_tech(6) + space(1) + B(1) + space(1) + family(8) + space(1) + C(1) = 21

        eb.MoveCursorRight(); // 'A'
        cursor = eb.GetCursorPosition();
        Assert.Equal(1u, cursor.Col);

        eb.MoveCursorRight(); // space
        cursor = eb.GetCursorPosition();
        Assert.Equal(2u, cursor.Col);

        eb.MoveCursorRight(); // woman
        cursor = eb.GetCursorPosition();
        Assert.Equal(4u, cursor.Col);

        eb.MoveCursorRight(); // skin tone
        cursor = eb.GetCursorPosition();
        Assert.Equal(6u, cursor.Col);

        eb.MoveCursorRight(); // laptop (ZWJ skipped)
        cursor = eb.GetCursorPosition();
        Assert.Equal(8u, cursor.Col);

        eb.MoveCursorRight(); // space after woman_tech
        cursor = eb.GetCursorPosition();
        Assert.Equal(9u, cursor.Col);
    }

    [Fact]
    public void Wcwidth_ZWJDoesNotAppearInRenderedText()
    {
        using var eb = EditBuffer.Create();

        var womanTech = "👩🏽\u200D💻";
        eb.SetText(womanTech);

        // Verify full text preserved byte-for-byte
        var text = eb.GetText();
        Assert.Equal(womanTech, text);

        // Check ZWJ is present in text
        Assert.Contains("\u200D", text);

        // Cursor movement should skip ZWJ: positions 0, 2, 4, 6
        eb.SetCursor(0, 0);
        eb.MoveCursorRight(); // Woman
        var cursor = eb.GetCursorPosition();
        Assert.Equal(2u, cursor.Col);

        eb.MoveCursorRight(); // Skin tone
        cursor = eb.GetCursorPosition();
        Assert.Equal(4u, cursor.Col);

        eb.MoveCursorRight(); // Laptop (ZWJ skipped)
        cursor = eb.GetCursorPosition();
        Assert.Equal(6u, cursor.Col);
    }

    [Fact]
    public void Wcwidth_EachVisibleEmojiRequiresExactlyOneMove()
    {
        using var eb = EditBuffer.Create();

        // Test 1: Simple laptop emoji (no ZWJ)
        eb.SetText("💻");
        eb.SetCursor(0, 0);
        eb.MoveCursorRight();
        var cursor = eb.GetCursorPosition();
        Assert.Equal(2u, cursor.Col);

        // Test 2: Woman emoji (no modifiers)
        eb.SetText("👩");
        eb.SetCursor(0, 0);
        eb.MoveCursorRight();
        cursor = eb.GetCursorPosition();
        Assert.Equal(2u, cursor.Col);

        // Test 3: Skin tone emoji alone
        eb.SetText("🏽");
        eb.SetCursor(0, 0);
        eb.MoveCursorRight();
        cursor = eb.GetCursorPosition();
        Assert.Equal(2u, cursor.Col);

        // Test 4: Woman + skin (no ZWJ) = width 4
        eb.SetText("👩🏽");
        eb.SetCursor(0, 0);
        eb.MoveCursorRight();
        cursor = eb.GetCursorPosition();
        Assert.Equal(2u, cursor.Col);
        eb.MoveCursorRight();
        cursor = eb.GetCursorPosition();
        Assert.Equal(4u, cursor.Col);

        // Test 5: Woman + skin + ZWJ + laptop = width 6
        eb.SetText("👩🏽\u200D💻");
        eb.SetCursor(0, 0);

        eb.MoveCursorRight(); // Move 1: woman
        cursor = eb.GetCursorPosition();
        Assert.Equal(2u, cursor.Col);

        eb.MoveCursorRight(); // Move 2: skin
        cursor = eb.GetCursorPosition();
        Assert.Equal(4u, cursor.Col);

        eb.MoveCursorRight(); // Move 3: laptop (ZWJ skipped)
        cursor = eb.GetCursorPosition();
        Assert.Equal(6u, cursor.Col);

        // Moving right again should do nothing
        eb.MoveCursorRight();
        cursor = eb.GetCursorPosition();
        Assert.Equal(6u, cursor.Col);

        // Test moving backwards
        eb.MoveCursorLeft();
        cursor = eb.GetCursorPosition();
        Assert.Equal(4u, cursor.Col);

        eb.MoveCursorLeft();
        cursor = eb.GetCursorPosition();
        Assert.Equal(2u, cursor.Col);

        eb.MoveCursorLeft();
        cursor = eb.GetCursorPosition();
        Assert.Equal(0u, cursor.Col);
    }

    // ===== ReplaceText / SetText Tests =====

    [Fact]
    public void ReplaceText_AllowsUndo()
    {
        using var eb = EditBuffer.Create();
        eb.SetText("Initial");
        Assert.Equal("Initial", eb.GetText());

        eb.ReplaceText("Modified");
        Assert.Equal("Modified", eb.GetText());

        Assert.True(eb.CanUndo());
        eb.Undo();

        Assert.Equal("Initial", eb.GetText());
    }

    [Fact]
    public void SetText_ClearsAllHistory()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("Initial");
        Assert.True(eb.CanUndo());
        Assert.Equal("Initial", eb.GetText());

        eb.SetText("New");
        Assert.Equal("New", eb.GetText());
        Assert.False(eb.CanUndo());
    }

    [Fact]
    public void MultipleReplaceText_KeepsAddBufferFunctional()
    {
        using var eb = EditBuffer.Create();

        eb.ReplaceText("Line 1");
        eb.InsertText("\nLine 2");

        // Replace text again (preserves history, sets cursor to 0,0)
        eb.ReplaceText("Reset");

        // Insert at cursor position (0,0)
        eb.InsertText(" and more");

        var text = eb.GetText();
        Assert.Equal(" and moreReset", text);

        Assert.True(eb.CanUndo());
    }

    [Fact]
    public void SetText_ResetsAddBuffer()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("First");
        eb.InsertText(" Second");
        Assert.Equal("First Second", eb.GetText());

        eb.SetText("Reset");
        Assert.Equal("Reset", eb.GetText());
    }

    // ===== History Tests (from edit-buffer-history_test.zig) =====

    [Fact]
    public void History_BasicUndoRedoWithInsertText()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("Hello");
        eb.InsertText(" World");
        Assert.Equal("Hello World", eb.GetText());

        var meta = eb.Undo();
        Assert.Equal("edit", meta);
        Assert.Equal("Hello", eb.GetText());

        var meta2 = eb.Redo();
        Assert.Equal("current", meta2);
        Assert.Equal("Hello World", eb.GetText());
    }

    [Fact]
    public void History_CanUndoCanRedo()
    {
        using var eb = EditBuffer.Create();
        Assert.False(eb.CanUndo());
        Assert.False(eb.CanRedo());

        eb.InsertText("Test");
        Assert.True(eb.CanUndo());
        Assert.False(eb.CanRedo());

        eb.Undo();
        Assert.False(eb.CanUndo());
        Assert.True(eb.CanRedo());

        eb.Redo();
        Assert.True(eb.CanUndo());
        Assert.False(eb.CanRedo());
    }

    [Fact]
    public void History_UndoRedoWithDeleteRange()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("Hello World");

        eb.DeleteRange(0, 5, 0, 11);
        Assert.Equal("Hello", eb.GetText());

        eb.Undo();
        Assert.Equal("Hello World", eb.GetText());

        eb.Redo();
        Assert.Equal("Hello", eb.GetText());
    }

    [Fact]
    public void History_UndoRedoWithBackspace()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("Hello");

        eb.DeleteCharBackward();
        Assert.Equal("Hell", eb.GetText());

        eb.Undo();
        Assert.Equal("Hello", eb.GetText());
    }

    [Fact]
    public void History_UndoRedoWithDeleteForward()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("Hello");
        eb.SetCursor(0, 0);

        eb.DeleteChar();
        Assert.Equal("ello", eb.GetText());

        eb.Undo();
        Assert.Equal("Hello", eb.GetText());
    }

    [Fact]
    public void History_CursorPositionAfterUndo()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("Line 1\nLine 2");

        var cursor = eb.GetCursorPosition();
        Assert.Equal(1u, cursor.Row);
        Assert.Equal(6u, cursor.Col);

        eb.InsertText("\nLine 3");
        cursor = eb.GetCursorPosition();
        Assert.Equal(2u, cursor.Row);

        eb.Undo();
        cursor = eb.GetCursorPosition();
        Assert.Equal(1u, cursor.Row);
        Assert.Equal(6u, cursor.Col);
    }

    [Fact]
    public void History_ClearHistory()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("Hello");
        eb.InsertText(" World");

        Assert.True(eb.CanUndo());

        eb.ClearHistory();

        Assert.False(eb.CanUndo());
        Assert.False(eb.CanRedo());
    }

    [Fact]
    public void History_UndoBranching()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("State A");
        eb.InsertText(" -> B");
        Assert.Equal("State A -> B", eb.GetText());

        eb.Undo();
        Assert.Equal("State A", eb.GetText());

        // Create new branch by editing after undo
        eb.InsertText(" -> C");
        Assert.Equal("State A -> C", eb.GetText());

        eb.Undo();
        Assert.Equal("State A", eb.GetText());

        // Redo should go to state C (the new branch)
        eb.Redo();
        Assert.Equal("State A -> C", eb.GetText());
    }

    [Fact]
    public void History_MultipleUndoRedo()
    {
        using var eb = EditBuffer.Create();
        eb.InsertText("A");
        eb.InsertText("B");
        eb.InsertText("C");
        Assert.Equal("ABC", eb.GetText());

        eb.Undo();
        Assert.Equal("AB", eb.GetText());

        eb.Undo();
        Assert.Equal("A", eb.GetText());

        eb.Redo();
        Assert.Equal("AB", eb.GetText());

        eb.Redo();
        Assert.Equal("ABC", eb.GetText());
    }
}
