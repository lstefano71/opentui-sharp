using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace OpenTui.Core.Managed.Unicode;

/// <summary>
/// Identifies word-wrap break opportunities in text.
/// Matches the Zig reference <c>findWrapBreaks</c> in <c>utf8.zig</c>.
/// </summary>
public static class WrapBreaks
{
    /// <summary>
    /// A single wrap-break opportunity, recording both byte and char (grapheme) offsets.
    /// </summary>
    public readonly record struct WrapBreak(uint ByteOffset, uint CharOffset);

    /// <summary>
    /// Find all wrap-break opportunities in UTF-8 text.
    /// </summary>
    /// <param name="utf8Text">UTF-8 encoded text (line content, no CR/LF).</param>
    /// <param name="results">List to append break opportunities to (cleared first).</param>
    /// <param name="widthMethod">Width method (currently unused, kept for API consistency).</param>
    public static void FindWrapBreaks(ReadOnlySpan<byte> utf8Text, List<WrapBreak> results, WidthMethod widthMethod)
    {
        _ = widthMethod; // Reserved for future use, matching Zig API
        results.Clear();
        if (utf8Text.IsEmpty) return;

        int pos = 0;
        uint charOffset = 0;
        WordClass prevClass = WordClass.Other;
        bool havePrevGrapheme = false;
        uint prevGraphemeByteOffset = 0;
        uint prevGraphemeCharOffset = 0;

        while (pos + 16 <= utf8Text.Length)
        {
            // Check if the entire 16-byte chunk is ASCII (< 0x80)
            if (IsChunkAscii(utf8Text.Slice(pos, 16)))
            {
                // Fast ASCII path
                var firstClass = WordBoundary.ClassifyWord(new Rune(utf8Text[pos]));
                if (havePrevGrapheme && IsCjkAsciiTransition(prevClass, firstClass))
                {
                    results.Add(new WrapBreak(prevGraphemeByteOffset, prevGraphemeCharOffset));
                }

                ProcessAsciiBreaks(utf8Text.Slice(pos, 16), (uint)pos, charOffset, results);

                uint blockStartCharOffset = charOffset;
                charOffset += 16;
                pos += 16;

                // Update previous grapheme tracking to the last byte in the block
                havePrevGrapheme = true;
                prevGraphemeByteOffset = (uint)(pos - 1);
                prevGraphemeCharOffset = blockStartCharOffset + 15;
                prevClass = WordBoundary.ClassifyWord(new Rune(utf8Text[pos - 1]));
                continue;
            }

            // Slow path: mixed ASCII/non-ASCII — process byte by byte within the chunk
            int chunkEnd = pos + 16;
            while (pos < chunkEnd && pos < utf8Text.Length)
            {
                var (rune, bytesConsumed) = TextWidth.DecodeUtf8(utf8Text, pos);
                int cp = rune.Value;

                var currentClass = WordBoundary.ClassifyWord(rune);
                if (havePrevGrapheme && IsCjkAsciiTransition(prevClass, currentClass))
                {
                    results.Add(new WrapBreak(prevGraphemeByteOffset, prevGraphemeCharOffset));
                }

                if (cp < 0x80)
                {
                    if (IsAsciiWrapBreak((byte)cp))
                        results.Add(new WrapBreak((uint)pos, charOffset));
                }
                else
                {
                    if (IsUnicodeWrapBreak(cp))
                        results.Add(new WrapBreak((uint)pos, charOffset));
                }

                havePrevGrapheme = true;
                prevGraphemeByteOffset = (uint)pos;
                prevGraphemeCharOffset = charOffset;
                prevClass = currentClass;

                pos += bytesConsumed;
                charOffset++;
            }
        }

        // Remainder (< 16 bytes)
        while (pos < utf8Text.Length)
        {
            var (rune, bytesConsumed) = TextWidth.DecodeUtf8(utf8Text, pos);
            int cp = rune.Value;

            var currentClass = WordBoundary.ClassifyWord(rune);
            if (havePrevGrapheme && IsCjkAsciiTransition(prevClass, currentClass))
            {
                results.Add(new WrapBreak(prevGraphemeByteOffset, prevGraphemeCharOffset));
            }

            if (cp < 0x80)
            {
                if (IsAsciiWrapBreak((byte)cp))
                    results.Add(new WrapBreak((uint)pos, charOffset));
            }
            else
            {
                if (IsUnicodeWrapBreak(cp))
                    results.Add(new WrapBreak((uint)pos, charOffset));
            }

            havePrevGrapheme = true;
            prevGraphemeByteOffset = (uint)pos;
            prevGraphemeCharOffset = charOffset;
            prevClass = currentClass;

            pos += bytesConsumed;
            charOffset++;
        }
    }

    #region ASCII break detection

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAsciiWrapBreak(byte b) => b switch
    {
        (byte)' ' or (byte)'\t' => true,
        (byte)'-' => true,
        (byte)'/' or (byte)'\\' => true,
        (byte)'.' or (byte)',' or (byte)';' or (byte)':' or (byte)'!' or (byte)'?' => true,
        (byte)'(' or (byte)')' or (byte)'[' or (byte)']' or (byte)'{' or (byte)'}' => true,
        _ => false,
    };

    /// <summary>
    /// Process a 16-byte ASCII chunk, appending break positions using SIMD when available.
    /// </summary>
    private static void ProcessAsciiBreaks(ReadOnlySpan<byte> chunk, uint byteBase, uint charBase, List<WrapBreak> results)
    {
        if (Sse2.IsSupported)
        {
            Vector128<byte> vec = Vector128.Create(chunk);
            Vector128<byte> mask = Vector128<byte>.Zero;

            // Whitespace
            mask = Sse2.Or(mask, Sse2.CompareEqual(vec, Vector128.Create((byte)' ')));
            mask = Sse2.Or(mask, Sse2.CompareEqual(vec, Vector128.Create((byte)'\t')));
            // Dashes / slashes
            mask = Sse2.Or(mask, Sse2.CompareEqual(vec, Vector128.Create((byte)'-')));
            mask = Sse2.Or(mask, Sse2.CompareEqual(vec, Vector128.Create((byte)'/')));
            mask = Sse2.Or(mask, Sse2.CompareEqual(vec, Vector128.Create((byte)'\\')));
            // Punctuation
            mask = Sse2.Or(mask, Sse2.CompareEqual(vec, Vector128.Create((byte)'.')));
            mask = Sse2.Or(mask, Sse2.CompareEqual(vec, Vector128.Create((byte)',')));
            mask = Sse2.Or(mask, Sse2.CompareEqual(vec, Vector128.Create((byte)';')));
            mask = Sse2.Or(mask, Sse2.CompareEqual(vec, Vector128.Create((byte)':')));
            mask = Sse2.Or(mask, Sse2.CompareEqual(vec, Vector128.Create((byte)'!')));
            mask = Sse2.Or(mask, Sse2.CompareEqual(vec, Vector128.Create((byte)'?')));
            // Brackets
            mask = Sse2.Or(mask, Sse2.CompareEqual(vec, Vector128.Create((byte)'(')));
            mask = Sse2.Or(mask, Sse2.CompareEqual(vec, Vector128.Create((byte)')')));
            mask = Sse2.Or(mask, Sse2.CompareEqual(vec, Vector128.Create((byte)'[')));
            mask = Sse2.Or(mask, Sse2.CompareEqual(vec, Vector128.Create((byte)']')));
            mask = Sse2.Or(mask, Sse2.CompareEqual(vec, Vector128.Create((byte)'{')));
            mask = Sse2.Or(mask, Sse2.CompareEqual(vec, Vector128.Create((byte)'}')));

            int bitmask = Sse2.MoveMask(mask);
            while (bitmask != 0)
            {
                int bit = int.TrailingZeroCount(bitmask);
                results.Add(new WrapBreak(byteBase + (uint)bit, charBase + (uint)bit));
                bitmask &= bitmask - 1;
            }
        }
        else
        {
            // Scalar fallback
            for (int i = 0; i < chunk.Length; i++)
            {
                if (IsAsciiWrapBreak(chunk[i]))
                    results.Add(new WrapBreak(byteBase + (uint)i, charBase + (uint)i));
            }
        }
    }

    #endregion

    #region Unicode break detection

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsUnicodeWrapBreak(int cp) => cp switch
    {
        0x00A0 => true,   // NBSP
        0x1680 => true,   // OGHAM SPACE MARK
        >= 0x2000 and <= 0x200A => true, // En quad .. Hair space
        0x202F => true,   // NARROW NO-BREAK SPACE
        0x205F => true,   // MEDIUM MATHEMATICAL SPACE
        0x3000 => true,   // IDEOGRAPHIC SPACE
        0x200B => true,   // ZERO WIDTH SPACE
        0x00AD => true,   // SOFT HYPHEN
        0x2010 => true,   // HYPHEN
        0x3001 => true,   // IDEOGRAPHIC COMMA
        0x3002 => true,   // IDEOGRAPHIC FULL STOP
        0xFF01 => true,   // FULLWIDTH EXCLAMATION MARK
        0xFF1F => true,   // FULLWIDTH QUESTION MARK
        _ => false,
    };

    #endregion

    #region Helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsChunkAscii(ReadOnlySpan<byte> chunk)
    {
        if (Sse2.IsSupported)
        {
            Vector128<byte> vec = Vector128.Create(chunk);
            Vector128<byte> threshold = Vector128.Create((byte)0x80);
            Vector128<byte> highBits = Sse2.SubtractSaturate(vec, Vector128.Create((byte)0x7F));
            return Sse2.MoveMask(Sse2.CompareEqual(highBits, Vector128<byte>.Zero)) == 0xFFFF;
        }

        // Scalar fallback
        for (int i = 0; i < chunk.Length; i++)
        {
            if (chunk[i] >= 0x80) return false;
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsCjkAsciiTransition(WordClass prev, WordClass curr)
        => (prev == WordClass.CjkWord && curr == WordClass.AsciiWord)
        || (prev == WordClass.AsciiWord && curr == WordClass.CjkWord);

    #endregion
}
