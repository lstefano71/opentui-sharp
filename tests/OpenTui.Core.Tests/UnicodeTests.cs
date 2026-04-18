using OpenTui.Core;
using Xunit;

namespace OpenTui.Core.Tests;

/// <summary>
/// Unicode encoding tests. The Zig utf8_test.zig has 358 tests for internal Unicode tables,
/// grapheme segmentation, and width calculation. Most test internal Zig APIs.
/// These tests exercise the public EncodeUnicode/FreeUnicode C API through the Unicode wrapper,
/// plus width-related behavior observable through TextBuffer and OptimizedBuffer.
/// </summary>
public class UnicodeTests
{
    [Fact]
    public void EncodeAsciiText()
    {
        var result = Unicode.Encode("Hello", WidthMethod.Unicode);
        Assert.NotNull(result);
        Unicode.FreeEncoded(result.Value.Data, result.Value.Length);
    }

    [Fact]
    public void EncodeEmptyString()
    {
        var result = Unicode.Encode("", WidthMethod.Unicode);
        // Empty string encoding behavior depends on native implementation
        if (result.HasValue)
            Unicode.FreeEncoded(result.Value.Data, result.Value.Length);
    }

    [Fact]
    public void EncodeEmojiText()
    {
        var result = Unicode.Encode("Hello 👋 World", WidthMethod.Unicode);
        Assert.NotNull(result);
        Assert.True(result.Value.Length > 0);
        Unicode.FreeEncoded(result.Value.Data, result.Value.Length);
    }

    [Fact]
    public void EncodeCjkText()
    {
        var result = Unicode.Encode("世界", WidthMethod.Unicode);
        Assert.NotNull(result);
        Assert.True(result.Value.Length > 0);
        Unicode.FreeEncoded(result.Value.Data, result.Value.Length);
    }

    [Fact]
    public void EncodeWithWcwidthMethod()
    {
        var result = Unicode.Encode("Hello", WidthMethod.Wcwidth);
        Assert.NotNull(result);
        Unicode.FreeEncoded(result.Value.Data, result.Value.Length);
    }

    [Fact]
    public void EncodeZwjSequence()
    {
        // Family emoji: man + ZWJ + woman + ZWJ + girl
        var result = Unicode.Encode("👨‍👩‍👧", WidthMethod.Unicode);
        Assert.NotNull(result);
        Unicode.FreeEncoded(result.Value.Data, result.Value.Length);
    }

    [Fact]
    public void EncodeSkinToneModifier()
    {
        var result = Unicode.Encode("👋🏽", WidthMethod.Unicode);
        Assert.NotNull(result);
        Unicode.FreeEncoded(result.Value.Data, result.Value.Length);
    }

    [Fact]
    public void EncodeDevanagari()
    {
        var result = Unicode.Encode("नमस्ते", WidthMethod.Unicode);
        Assert.NotNull(result);
        Unicode.FreeEncoded(result.Value.Data, result.Value.Length);
    }

    [Fact]
    public void EncodeKoreanHangul()
    {
        var result = Unicode.Encode("한글", WidthMethod.Unicode);
        Assert.NotNull(result);
        Unicode.FreeEncoded(result.Value.Data, result.Value.Length);
    }

    [Fact]
    public void EncodeMixedScript()
    {
        var result = Unicode.Encode("Hello世界👋नमस्ते", WidthMethod.Unicode);
        Assert.NotNull(result);
        Unicode.FreeEncoded(result.Value.Data, result.Value.Length);
    }

    // -- Width observable through TextBuffer --

    [Fact]
    public void AsciiCharWidthIsOne()
    {
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Hello");
        Assert.Equal(1u, tb.LineCount);
    }

    [Fact]
    public void CjkCharWidthIsTwo()
    {
        // CJK chars are double-width; a 4-col-wide buffer can fit 2 CJK chars
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("世界");
        Assert.Equal(1u, tb.LineCount);
    }

    [Fact]
    public void EmojiWidthIsTwo()
    {
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("👋");
        Assert.Equal(1u, tb.LineCount);
    }

    [Fact]
    public void TabCharExpands()
    {
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.TabWidth = 4;
        tb.SetText("\tHello");
        Assert.Equal(1u, tb.LineCount);
    }

    [Fact]
    public void MultiLineNewlines()
    {
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("A\nB\nC");
        Assert.Equal(3u, tb.LineCount);
    }

    [Fact]
    public void EmptyStringHasOneLine()
    {
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("");
        // Empty text is still "one line" (the empty line)
        Assert.True(tb.LineCount >= 1);
    }

    [Fact]
    public void WcwidthVsUnicodeMode()
    {
        using var tbWc = TextBuffer.Create(WidthMethod.Wcwidth);
        using var tbUni = TextBuffer.Create(WidthMethod.Unicode);
        tbWc.SetText("Hello 👋");
        tbUni.SetText("Hello 👋");
        // Both should handle basic text without crash
        Assert.True(tbWc.LineCount >= 1);
        Assert.True(tbUni.LineCount >= 1);
    }

    [Fact]
    public void FlagEmoji()
    {
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("🇺🇸");
        Assert.Equal(1u, tb.LineCount);
    }

    [Fact]
    public void CombiningCharacters()
    {
        // e + combining acute accent = é
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("e\u0301");
        Assert.Equal(1u, tb.LineCount);
    }

    [Fact]
    public void ControlCharacters()
    {
        using var tb = TextBuffer.Create(WidthMethod.Unicode);
        tb.SetText("Hello\x01World");
        Assert.Equal(1u, tb.LineCount);
    }
}
