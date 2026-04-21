using OpenTui.Core.Managed;
using OpenTui.Core.Managed.Unicode;
using Xunit;

namespace OpenTui.Core.Tests;

/// <summary>
/// Regression tests for ManagedBuffer.DrawText Unicode / grapheme cluster support.
/// </summary>
public class ManagedBufferDrawTextTests : IDisposable
{
    private static readonly Rgba Black = new(0f, 0f, 0f, 1f);
    private static readonly Rgba White = new(1f, 1f, 1f, 1f);

    private ManagedBuffer _buf = ManagedBuffer.Create(40, 5);

    public void Dispose() => _buf.Dispose();

    #region ASCII

    [Fact]
    public void DrawText_Ascii_WritesCorrectCodepoints()
    {
        _buf.DrawText("Hi", 0, 0, White, Black);

        var h = _buf.Get(0, 0);
        var i = _buf.Get(1, 0);
        Assert.Equal((uint)'H', h!.Value.Char);
        Assert.Equal((uint)'i', i!.Value.Char);
        // Next cell should still be the default space
        Assert.Equal(ManagedBuffer.DefaultSpaceChar, _buf.Get(2, 0)!.Value.Char);
    }

    [Fact]
    public void DrawText_ClipsAtRightEdge()
    {
        using var buf = ManagedBuffer.Create(3, 1);
        buf.DrawText("ABCDE", 0, 0, White, Black);

        Assert.Equal((uint)'A', buf.Get(0, 0)!.Value.Char);
        Assert.Equal((uint)'B', buf.Get(1, 0)!.Value.Char);
        Assert.Equal((uint)'C', buf.Get(2, 0)!.Value.Char);
    }

    #endregion

    #region CJK / Wide characters

    [Fact]
    public void DrawText_CjkCharacter_OccupiesTwoCells()
    {
        _buf.DrawText("测", 0, 0, White, Black);

        var first = _buf.Get(0, 0)!.Value;
        var second = _buf.Get(1, 0)!.Value;

        // First cell: the codepoint for '测' (U+6D4B)
        Assert.Equal(0x6D4Bu, first.Char);
        // Second cell: continuation flag | codepoint
        Assert.Equal(ManagedGraphemePool.CharFlagContinuation | 0x6D4Bu, second.Char);
    }

    [Fact]
    public void DrawText_CjkString_CorrectWidth()
    {
        _buf.DrawText("测试", 0, 0, White, Black);

        // '测' at cols 0-1, '试' at cols 2-3
        Assert.Equal(0x6D4Bu, _buf.Get(0, 0)!.Value.Char); // 测
        Assert.Equal(ManagedGraphemePool.CharFlagContinuation | 0x6D4Bu, _buf.Get(1, 0)!.Value.Char);
        Assert.Equal(0x8BD5u, _buf.Get(2, 0)!.Value.Char); // 试
        Assert.Equal(ManagedGraphemePool.CharFlagContinuation | 0x8BD5u, _buf.Get(3, 0)!.Value.Char);
        // Col 4 should be untouched
        Assert.Equal(ManagedBuffer.DefaultSpaceChar, _buf.Get(4, 0)!.Value.Char);
    }

    [Fact]
    public void DrawText_WideCharDoesNotFit_Stops()
    {
        using var buf = ManagedBuffer.Create(3, 1);
        // '测试' needs 4 cols but only 3 available; second char shouldn't be written
        buf.DrawText("测试", 0, 0, White, Black);

        Assert.Equal(0x6D4Bu, buf.Get(0, 0)!.Value.Char);
        Assert.Equal(ManagedGraphemePool.CharFlagContinuation | 0x6D4Bu, buf.Get(1, 0)!.Value.Char);
        // Third cell: wide '试' needs 2 cols starting at col 2 but only 1 col left → not written
        Assert.Equal(ManagedBuffer.DefaultSpaceChar, buf.Get(2, 0)!.Value.Char);
    }

    #endregion

    #region Emoji (wide)

    [Fact]
    public void DrawText_Emoji_OccupiesTwoCells()
    {
        _buf.DrawText("🌟", 0, 0, White, Black);

        var first = _buf.Get(0, 0)!.Value;
        var second = _buf.Get(1, 0)!.Value;

        // U+1F31F = 🌟
        Assert.Equal(0x1F31Fu, first.Char);
        Assert.Equal(ManagedGraphemePool.CharFlagContinuation | 0x1F31Fu, second.Char);
    }

    [Fact]
    public void DrawText_MixedAsciiAndEmoji()
    {
        _buf.DrawText("A🌟B", 0, 0, White, Black);

        // A at col 0
        Assert.Equal((uint)'A', _buf.Get(0, 0)!.Value.Char);
        // 🌟 at cols 1-2
        Assert.Equal(0x1F31Fu, _buf.Get(1, 0)!.Value.Char);
        Assert.Equal(ManagedGraphemePool.CharFlagContinuation | 0x1F31Fu, _buf.Get(2, 0)!.Value.Char);
        // B at col 3
        Assert.Equal((uint)'B', _buf.Get(3, 0)!.Value.Char);
    }

    #endregion

    #region Combining characters / zero-width

    [Fact]
    public void DrawText_CombiningAccent_SkippedAsZeroWidth()
    {
        // 'e' + combining acute accent (U+0301)
        _buf.DrawText("e\u0301", 0, 0, White, Black);

        // 'e' is written at col 0, combining mark is zero-width and skipped
        Assert.Equal((uint)'e', _buf.Get(0, 0)!.Value.Char);
    }

    [Fact]
    public void DrawText_ZWJ_IsSkipped()
    {
        // ZWJ (U+200D) has zero width
        _buf.DrawText("A\u200DB", 0, 0, White, Black);

        Assert.Equal((uint)'A', _buf.Get(0, 0)!.Value.Char);
        Assert.Equal((uint)'B', _buf.Get(1, 0)!.Value.Char);
    }

    #endregion

    #region Control characters and newlines

    [Fact]
    public void DrawText_NewlineStopsRendering()
    {
        _buf.DrawText("AB\nCD", 0, 0, White, Black);

        Assert.Equal((uint)'A', _buf.Get(0, 0)!.Value.Char);
        Assert.Equal((uint)'B', _buf.Get(1, 0)!.Value.Char);
        // After newline, rendering stops
        Assert.Equal(ManagedBuffer.DefaultSpaceChar, _buf.Get(2, 0)!.Value.Char);
    }

    [Fact]
    public void DrawText_ControlCharsSkipped()
    {
        // BEL (U+0007) is a control char < 32 and not tab — skipped without advancing cursor
        _buf.DrawText("A\u0007B", 0, 0, White, Black);

        Assert.Equal((uint)'A', _buf.Get(0, 0)!.Value.Char);
        // Control char is skipped (no cell consumed), so B is at col 1
        Assert.Equal((uint)'B', _buf.Get(1, 0)!.Value.Char);
    }

    #endregion

    #region Bullet / non-ASCII single-width

    [Fact]
    public void DrawText_Bullet_SingleWidth()
    {
        // Bullet '•' (U+2022) is single-width
        _buf.DrawText("•X", 0, 0, White, Black);

        Assert.Equal(0x2022u, _buf.Get(0, 0)!.Value.Char);
        Assert.Equal((uint)'X', _buf.Get(1, 0)!.Value.Char);
    }

    #endregion

    #region Repeated rendering (no pool exhaustion)

    [Fact]
    public void DrawText_RepeatedEmoji_DoesNotCrash()
    {
        for (int i = 0; i < 500; i++)
        {
            _buf.Clear(Black);
            _buf.DrawText("🌟🎨🚀", 0, 0, White, Black);
        }
    }

    [Fact]
    public void DrawText_RepeatedCjk_DoesNotCrash()
    {
        for (int i = 0; i < 500; i++)
        {
            _buf.Clear(Black);
            _buf.DrawText("测试文字", 0, 0, White, Black);
        }
    }

    #endregion

    #region Empty / boundary

    [Fact]
    public void DrawText_EmptyString_NoOp()
    {
        _buf.DrawText("", 0, 0, White, Black);
        Assert.Equal(ManagedBuffer.DefaultSpaceChar, _buf.Get(0, 0)!.Value.Char);
    }

    [Fact]
    public void DrawText_OutOfBoundsPosition_NoOp()
    {
        _buf.DrawText("Hello", 100, 0, White, Black);
        Assert.Equal(ManagedBuffer.DefaultSpaceChar, _buf.Get(0, 0)!.Value.Char);
    }

    #endregion

    #region Box title width

    [Fact]
    public void DrawBox_WideTitleUsesUnicodeWidth()
    {
        // Just verify it doesn't crash with CJK title text in DrawBox
        using var buf = ManagedBuffer.Create(30, 5);
        buf.DrawBox(0, 0, 30, 5,
            BorderCharacters.Single, BorderSides.All, shouldFill: true,
            White, Black, title: "测试标题");
    }

    #endregion
}
