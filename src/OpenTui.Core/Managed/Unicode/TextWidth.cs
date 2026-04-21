using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace OpenTui.Core.Managed.Unicode;

/// <summary>
/// High-performance text width calculation for terminal display.
/// Determines how many terminal columns a string occupies, handling
/// ASCII, East Asian Wide characters, combining marks, and grapheme clusters.
/// </summary>
public static class TextWidth
{
    /// <summary>
    /// Calculate the display width (terminal columns) of UTF-8 text.
    /// </summary>
    /// <param name="utf8Text">UTF-8 encoded text.</param>
    /// <param name="tabWidth">Number of columns a tab character occupies.</param>
    /// <param name="isAsciiOnly">If true, assumes all bytes are printable ASCII for a fast path.</param>
    /// <param name="widthMethod">Width calculation method to use.</param>
    /// <returns>Display width in terminal columns.</returns>
    public static uint CalculateTextWidth(ReadOnlySpan<byte> utf8Text, byte tabWidth, bool isAsciiOnly, WidthMethod widthMethod)
    {
        if (utf8Text.IsEmpty) return 0;

        // ASCII-only fast path: each byte is one column
        if (isAsciiOnly) return (uint)utf8Text.Length;

        return widthMethod switch
        {
            WidthMethod.Wcwidth => CalculateWidthWcwidth(utf8Text, tabWidth),
            _ => CalculateWidthUnicode(utf8Text, tabWidth, widthMethod),
        };
    }

    /// <summary>
    /// Calculate display width for a C# string (UTF-16).
    /// </summary>
    public static uint CalculateTextWidth(ReadOnlySpan<char> text, byte tabWidth, WidthMethod widthMethod)
    {
        if (text.IsEmpty) return 0;

        // Convert to UTF-8 on stack for short strings, heap for long
        int maxUtf8 = Encoding.UTF8.GetMaxByteCount(text.Length);
        byte[]? rented = null;
        Span<byte> utf8Buffer = maxUtf8 <= 1024
            ? stackalloc byte[maxUtf8]
            : (rented = System.Buffers.ArrayPool<byte>.Shared.Rent(maxUtf8));

        try
        {
            int bytesWritten = Encoding.UTF8.GetBytes(text, utf8Buffer);
            ReadOnlySpan<byte> utf8 = utf8Buffer[..bytesWritten];
            bool ascii = IsAsciiOnly(utf8);
            return CalculateTextWidth(utf8, tabWidth, ascii, widthMethod);
        }
        finally
        {
            if (rented is not null)
                System.Buffers.ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// Get the display width of a single codepoint.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint CharWidth(Rune rune, byte tabWidth)
    {
        int value = rune.Value;

        if (value == '\t') return tabWidth;
        if (value >= 32 && value <= 126) return 1;
        if (value < 32 || (value >= 0x7F && value < 0xA0)) return 0;
        if (value == 0) return 0;

        // Combining marks and zero-width
        if (IsZeroWidth(value)) return 0;

        // East Asian Wide / Fullwidth
        if (IsEastAsianWide(value)) return 2;

        // Emoji and symbols blocks (wide)
        if (IsWideEmoji(value)) return 2;

        return 1;
    }

    /// <summary>
    /// Check if a byte sequence is entirely printable ASCII (32..126).
    /// Uses SIMD when available for 16-byte-at-a-time checking.
    /// </summary>
    public static bool IsAsciiOnly(ReadOnlySpan<byte> text)
    {
        if (text.IsEmpty) return false;

        int pos = 0;

        if (Sse2.IsSupported && text.Length >= Vector128<byte>.Count)
        {
            Vector128<byte> minPrintable = Vector128.Create((byte)32);
            Vector128<byte> maxPrintable = Vector128.Create((byte)126);

            while (pos + Vector128<byte>.Count <= text.Length)
            {
                Vector128<byte> chunk = Vector128.Create(text.Slice(pos, Vector128<byte>.Count));
                // Check < 32: if any byte is less than 32, the unsigned subtraction will have high bits set
                Vector128<byte> tooLow = Sse2.SubtractSaturate(minPrintable, chunk);
                // Check > 126: subtract 126, if result is non-zero then byte > 126
                Vector128<byte> tooHigh = Sse2.SubtractSaturate(chunk, maxPrintable);
                Vector128<byte> outOfRange = Sse2.Or(tooLow, tooHigh);

                if (Sse2.MoveMask(outOfRange) != 0)
                    return false;

                pos += Vector128<byte>.Count;
            }
        }

        // Scalar remainder
        for (int i = pos; i < text.Length; i++)
        {
            byte b = text[i];
            if (b < 32 || b > 126)
                return false;
        }

        return true;
    }

    #region Wcwidth mode — per-codepoint width, no grapheme clustering

    private static uint CalculateWidthWcwidth(ReadOnlySpan<byte> utf8Text, byte tabWidth)
    {
        uint totalWidth = 0;
        int pos = 0;

        while (pos < utf8Text.Length)
        {
            var (rune, bytesConsumed) = DecodeUtf8(utf8Text, pos);
            totalWidth += CharWidth(rune, tabWidth);
            pos += bytesConsumed;
        }

        return totalWidth;
    }

    #endregion

    #region Unicode / NoZwj mode — grapheme-cluster-aware width

    private static uint CalculateWidthUnicode(ReadOnlySpan<byte> utf8Text, byte tabWidth, WidthMethod widthMethod)
    {
        // Decode the full string to a managed string, then use StringInfo
        // for grapheme cluster segmentation.
        string text = Encoding.UTF8.GetString(utf8Text);
        if (text.Length == 0) return 0;

        uint totalWidth = 0;
        TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(text);

        while (enumerator.MoveNext())
        {
            string grapheme = enumerator.GetTextElement();

            if (widthMethod == WidthMethod.NoZwj)
            {
                // In NoZwj mode, split at ZWJ boundaries: each sub-segment
                // gets its own width (ZWJ itself is zero-width).
                totalWidth += CalculateNoZwjGraphemeWidth(grapheme, tabWidth);
            }
            else
            {
                totalWidth += CalculateGraphemeClusterWidth(grapheme, tabWidth);
            }
        }

        return totalWidth;
    }

    /// <summary>
    /// Width of a grapheme cluster in Unicode mode:
    /// the cluster's width is determined by the first codepoint that has a nonzero width,
    /// with special handling for VS16 (widens to 2), regional indicators, and Indic conjuncts.
    /// </summary>
    private static uint CalculateGraphemeClusterWidth(string grapheme, byte tabWidth)
    {
        uint width = 0;
        bool hasWidth = false;
        bool hasVs16 = false;
        bool isRegionalFirst = false;
        bool hasIndicVirama = false;

        foreach (Rune rune in grapheme.EnumerateRunes())
        {
            int cp = rune.Value;
            uint cpWidth = CharWidth(rune, tabWidth);

            // Variation Selector-16 → emoji presentation, widen to 2
            if (cp == 0xFE0F)
            {
                hasVs16 = true;
                if (hasWidth && width == 1) width = 2;
                continue;
            }

            // Combining marks (Mn, Mc, Me) → zero width in cluster context
            if (Rune.GetUnicodeCategory(rune) is
                UnicodeCategory.NonSpacingMark or
                UnicodeCategory.SpacingCombiningMark or
                UnicodeCategory.EnclosingMark)
            {
                // Check for Indic virama (NonSpacingMark)
                if (Rune.GetUnicodeCategory(rune) == UnicodeCategory.NonSpacingMark)
                    hasIndicVirama = true;
                continue;
            }

            // Regional indicator pair
            if (cp >= 0x1F1E6 && cp <= 0x1F1FF)
            {
                if (isRegionalFirst)
                {
                    width += cpWidth;
                    hasWidth = true;
                    isRegionalFirst = false;
                }
                else
                {
                    if (!hasWidth)
                    {
                        width = cpWidth;
                        hasWidth = true;
                    }
                    isRegionalFirst = true;
                }
                continue;
            }

            // Indic conjunct: virama + base consonant → add width
            if (hasWidth && hasIndicVirama && cpWidth > 0 && IsDevanagariBase(cp) && !IsDevanagariRa(cp))
            {
                width += cpWidth;
                hasIndicVirama = false;
                continue;
            }

            if (!hasWidth && cpWidth > 0)
            {
                width = cpWidth;
                hasWidth = true;
            }

            hasIndicVirama = false;
        }

        return width;
    }

    /// <summary>
    /// Width in NoZwj mode: ZWJ (U+200D) does NOT join codepoints — each segment
    /// between ZWJ characters is treated as a separate grapheme for width purposes.
    /// </summary>
    private static uint CalculateNoZwjGraphemeWidth(string grapheme, byte tabWidth)
    {
        const int ZWJ = 0x200D;
        uint total = 0;
        bool inSegment = false;
        uint segmentWidth = 0;
        bool segmentHasWidth = false;

        foreach (Rune rune in grapheme.EnumerateRunes())
        {
            int cp = rune.Value;

            if (cp == ZWJ)
            {
                // Flush current segment
                if (inSegment)
                {
                    total += segmentWidth;
                    segmentWidth = 0;
                    segmentHasWidth = false;
                    inSegment = false;
                }
                // ZWJ itself has width 0
                continue;
            }

            uint cpWidth = CharWidth(rune, tabWidth);

            // Combining marks
            if (Rune.GetUnicodeCategory(rune) is
                UnicodeCategory.NonSpacingMark or
                UnicodeCategory.SpacingCombiningMark or
                UnicodeCategory.EnclosingMark)
            {
                continue;
            }

            // VS16
            if (cp == 0xFE0F)
            {
                if (segmentHasWidth && segmentWidth == 1) segmentWidth = 2;
                continue;
            }

            if (!segmentHasWidth && cpWidth > 0)
            {
                segmentWidth = cpWidth;
                segmentHasWidth = true;
                inSegment = true;
            }
        }

        // Flush final segment
        if (inSegment) total += segmentWidth;

        return total;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsDevanagariBase(int cp)
        => (cp >= 0x0915 && cp <= 0x0939) || (cp >= 0x0958 && cp <= 0x095F);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsDevanagariRa(int cp) => cp == 0x0930;

    #endregion

    #region Character classification helpers

    /// <summary>
    /// Zero-width codepoints: combining marks, ZWJ, ZWNJ, ZWSP, WJ, CGJ, BOM, variation selectors.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsZeroWidth(int cp)
    {
        // General category check for combining marks
        if (Rune.IsValid(cp))
        {
            var cat = Rune.GetUnicodeCategory(new Rune(cp));
            if (cat is UnicodeCategory.NonSpacingMark
                    or UnicodeCategory.SpacingCombiningMark
                    or UnicodeCategory.EnclosingMark)
                return true;
        }

        // Specific zero-width codepoints (matching Zig eawToWidth)
        return cp switch
        {
            0x200B => true, // ZWSP
            0x200C => true, // ZWNJ
            0x200D => true, // ZWJ
            0x2060 => true, // Word Joiner
            0x034F => true, // CGJ
            0xFEFF => true, // BOM / ZWNBSP
            >= 0x180B and <= 0x180D => true,       // Mongolian variation selectors
            >= 0xFE00 and <= 0xFE0F => true,       // Variation selectors
            >= 0xE0100 and <= 0xE01EF => true,     // Variation selectors supplement
            _ => false,
        };
    }

    /// <summary>
    /// East Asian Wide / Fullwidth codepoints — these occupy 2 terminal columns.
    /// Uses Unicode East_Asian_Width property ranges.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsEastAsianWide(int cp)
    {
        // CJK blocks
        if (cp >= 0x1100 && cp <= 0x115F) return true;   // Hangul Jamo
        if (cp >= 0x2E80 && cp <= 0x303E) return true;   // CJK misc
        if (cp >= 0x3041 && cp <= 0x33BF) return true;   // Hiragana, Katakana, etc.
        if (cp >= 0x3400 && cp <= 0x4DBF) return true;   // CJK Unified Extension A
        if (cp >= 0x4E00 && cp <= 0x9FFF) return true;   // CJK Unified
        if (cp >= 0xA000 && cp <= 0xA4CF) return true;   // Yi
        if (cp >= 0xAC00 && cp <= 0xD7AF) return true;   // Hangul Syllables
        if (cp >= 0xF900 && cp <= 0xFAFF) return true;   // CJK Compatibility Ideographs
        if (cp >= 0xFE30 && cp <= 0xFE6F) return true;   // CJK Compatibility Forms
        if (cp >= 0xFF01 && cp <= 0xFF60) return true;   // Fullwidth Forms
        if (cp >= 0xFFE0 && cp <= 0xFFE6) return true;   // Fullwidth Signs
        if (cp >= 0x20000 && cp <= 0x2FFFF) return true; // CJK Extension B+
        if (cp >= 0x30000 && cp <= 0x3FFFF) return true; // CJK Extension G+
        return false;
    }

    /// <summary>
    /// Wide emoji and symbol ranges not covered by East_Asian_Width=W/F.
    /// Matches the specific ranges from the Zig reference implementation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsWideEmoji(int cp)
    {
        // Mahjong Tiles, Domino Tiles, Playing Cards
        if (cp >= 0x1F000 && cp <= 0x1F02B) return true;
        if (cp >= 0x1F030 && cp <= 0x1F093) return true;
        if (cp >= 0x1F0A0 && cp <= 0x1F0AE) return true;
        if (cp >= 0x1F0B1 && cp <= 0x1F0BF) return true;
        if (cp >= 0x1F0C1 && cp <= 0x1F0CF) return true;
        if (cp >= 0x1F0D1 && cp <= 0x1F0F5) return true;

        // Misc symbols with explicit wide rendering
        if (cp is 0x231A or 0x231B) return true;
        if (cp is 0x2329 or 0x232A) return true;
        if (cp >= 0x23E9 && cp <= 0x23EC) return true;
        if (cp is 0x23F0 or 0x23F3) return true;
        if (cp >= 0x25FD && cp <= 0x25FE) return true;
        if (cp >= 0x2614 && cp <= 0x2615) return true;
        if (cp is 0x2622 or 0x2623) return true;
        if (cp >= 0x2630 && cp <= 0x2637) return true;
        if (cp >= 0x2648 && cp <= 0x2653) return true;
        if (cp is 0x267F or 0x2693 or 0x269B) return true;
        if (cp is 0x26A0 or 0x26A1) return true;
        if (cp >= 0x26AA && cp <= 0x26AB) return true;
        if (cp >= 0x26BD && cp <= 0x26BE) return true;
        if (cp >= 0x26C4 && cp <= 0x26C5) return true;
        if (cp is 0x26CE or 0x26D1 or 0x26D4) return true;
        if (cp is 0x26EA or 0x26F2 or 0x26F3) return true;
        if (cp is 0x26F5 or 0x26FA or 0x26FD) return true;
        if (cp is 0x203C or 0x2049) return true;
        if (cp == 0x2705 || (cp >= 0x270A && cp <= 0x270B)) return true;
        if (cp is 0x2728 or 0x274C or 0x274E) return true;
        if (cp >= 0x2753 && cp <= 0x2755) return true;
        if (cp == 0x2757) return true;
        if (cp >= 0x2760 && cp <= 0x2767) return true;
        if (cp >= 0x2795 && cp <= 0x2797) return true;
        if (cp is 0x27B0 or 0x27BF) return true;
        if (cp >= 0x2B1B && cp <= 0x2B1C) return true;
        if (cp == 0x2B50) return true;
        if (cp == 0x2B55) return true;

        // Emoji & Symbols block — detailed ranges matching Zig reference
        if (cp >= 0x1F300 && cp <= 0x1F320) return true;
        if (cp >= 0x1F32D && cp <= 0x1F335) return true;
        if (cp >= 0x1F337 && cp <= 0x1F37C) return true;
        if (cp >= 0x1F37E && cp <= 0x1F393) return true;
        if (cp >= 0x1F3A0 && cp <= 0x1F3CA) return true;
        if (cp >= 0x1F3CF && cp <= 0x1F3D3) return true;
        if (cp >= 0x1F3E0 && cp <= 0x1F3F0) return true;
        if (cp == 0x1F3F4) return true;
        if (cp >= 0x1F3F8 && cp <= 0x1F3FF) return true;
        if (cp >= 0x1F400 && cp <= 0x1F43E) return true;
        if (cp == 0x1F440) return true;
        if (cp >= 0x1F442 && cp <= 0x1F4FC) return true;
        if (cp >= 0x1F4FF && cp <= 0x1F6C5) return true;
        if (cp == 0x1F6CC) return true;
        if (cp >= 0x1F6D0 && cp <= 0x1F6D2) return true;
        if (cp >= 0x1F6D5 && cp <= 0x1F6D7) return true;
        if (cp >= 0x1F6DC && cp <= 0x1F6DF) return true;
        if (cp >= 0x1F6EB && cp <= 0x1F6EC) return true;
        if (cp >= 0x1F6F4 && cp <= 0x1F6FC) return true;
        if (cp >= 0x1F700 && cp <= 0x1F773) return true;
        if (cp >= 0x1F780 && cp <= 0x1F7D8) return true;
        if (cp >= 0x1F7E0 && cp <= 0x1F7EB) return true;
        if (cp >= 0x1F800 && cp <= 0x1F80B) return true;
        if (cp >= 0x1F810 && cp <= 0x1F847) return true;
        if (cp >= 0x1F850 && cp <= 0x1F859) return true;
        if (cp >= 0x1F860 && cp <= 0x1F887) return true;
        if (cp >= 0x1F890 && cp <= 0x1F8AD) return true;
        if (cp >= 0x1F8B0 && cp <= 0x1F8B1) return true;
        if (cp >= 0x1F90C && cp <= 0x1F93A) return true;
        if (cp >= 0x1F93C && cp <= 0x1F945) return true;
        if (cp >= 0x1F947 && cp <= 0x1FA53) return true;
        if (cp >= 0x1FA60 && cp <= 0x1FA6D) return true;
        if (cp >= 0x1FA70 && cp <= 0x1FA74) return true;
        if (cp >= 0x1FA78 && cp <= 0x1FA7C) return true;
        if (cp >= 0x1FA80 && cp <= 0x1FA86) return true;
        if (cp >= 0x1FA90 && cp <= 0x1FAAC) return true;
        if (cp >= 0x1FAB0 && cp <= 0x1FABA) return true;
        if (cp >= 0x1FAC0 && cp <= 0x1FAC5) return true;
        if (cp >= 0x1FAD0 && cp <= 0x1FAD9) return true;
        if (cp >= 0x1FAE0 && cp <= 0x1FAE7) return true;
        if (cp >= 0x1FAF0 && cp <= 0x1FAF8) return true;

        return false;
    }

    #endregion

    #region UTF-8 decoding

    /// <summary>
    /// Decode a single UTF-8 codepoint starting at <paramref name="pos"/>.
    /// Returns the decoded Rune and the number of bytes consumed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static (Rune Rune, int BytesConsumed) DecodeUtf8(ReadOnlySpan<byte> utf8, int pos)
    {
        if (Rune.DecodeFromUtf8(utf8[pos..], out Rune rune, out int bytesConsumed) == System.Buffers.OperationStatus.Done)
            return (rune, bytesConsumed);

        // Invalid sequence — replacement character, consume 1 byte
        return (Rune.ReplacementChar, 1);
    }

    #endregion
}
