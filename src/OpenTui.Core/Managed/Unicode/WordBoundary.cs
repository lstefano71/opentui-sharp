using System.Runtime.CompilerServices;
using System.Text;

namespace OpenTui.Core.Managed.Unicode;

/// <summary>
/// Word classification for cursor movement and text segmentation.
/// </summary>
public enum WordClass : byte
{
    /// <summary>ASCII alphanumeric + underscore.</summary>
    AsciiWord,
    /// <summary>CJK ideograph, Hiragana, Katakana, or Hangul.</summary>
    CjkWord,
    /// <summary>Everything else (punctuation, whitespace, symbols).</summary>
    Other,
}

/// <summary>
/// Word boundary detection for cursor movement.
/// CJK ranges match the Zig reference in <c>utf8.zig</c>.
/// </summary>
public static class WordBoundary
{
    /// <summary>
    /// Check if a codepoint is a "word" character (alphanumeric + underscore, or CJK).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsWordCodepoint(Rune rune) => ClassifyWord(rune) != WordClass.Other;

    /// <summary>
    /// Check if a codepoint falls within a CJK range
    /// (Han, Hiragana, Katakana, Hangul).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCjkCodepoint(Rune rune) => IsCjkCodepoint(rune.Value);

    /// <summary>
    /// Classify a codepoint into a word class for boundary detection.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static WordClass ClassifyWord(Rune rune)
    {
        int cp = rune.Value;

        if (cp <= 0x7F)
            return IsAsciiWordByte((byte)cp) ? WordClass.AsciiWord : WordClass.Other;

        if (IsCjkCodepoint(cp))
            return WordClass.CjkWord;

        return WordClass.Other;
    }

    #region Private helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAsciiWordByte(byte b)
        => (b >= (byte)'a' && b <= (byte)'z')
        || (b >= (byte)'A' && b <= (byte)'Z')
        || (b >= (byte)'0' && b <= (byte)'9')
        || b == (byte)'_';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsCjkCodepoint(int cp)
    {
        // Han ideographs
        if (cp >= 0x3400 && cp <= 0x4DBF) return true;
        if (cp >= 0x4E00 && cp <= 0x9FFF) return true;
        if (cp >= 0xF900 && cp <= 0xFAFF) return true;
        if (cp >= 0x20000 && cp <= 0x2A6DF) return true;
        if (cp >= 0x2A700 && cp <= 0x2B73F) return true;
        if (cp >= 0x2B740 && cp <= 0x2B81F) return true;
        if (cp >= 0x2B820 && cp <= 0x2CEAF) return true;
        if (cp >= 0x2CEB0 && cp <= 0x2EBEF) return true;
        if (cp >= 0x2EBF0 && cp <= 0x2EE5D) return true;
        if (cp >= 0x2F800 && cp <= 0x2FA1F) return true;

        // Hiragana + Katakana
        if (cp >= 0x3040 && cp <= 0x309F) return true;
        if (cp >= 0x30A0 && cp <= 0x30FF) return true;
        if (cp >= 0x31F0 && cp <= 0x31FF) return true;
        if (cp >= 0xFF66 && cp <= 0xFF9D) return true;

        // Hangul
        if (cp >= 0x1100 && cp <= 0x11FF) return true;
        if (cp >= 0x3130 && cp <= 0x318F) return true;
        if (cp >= 0xA960 && cp <= 0xA97F) return true;
        if (cp >= 0xAC00 && cp <= 0xD7AF) return true;
        if (cp >= 0xD7B0 && cp <= 0xD7FF) return true;

        return false;
    }

    #endregion
}
