using System.Globalization;
using System.Runtime.InteropServices;

namespace OpenTui;

/// <summary>
/// Represents an RGBA color with float components (0.0–1.0).
/// Layout-compatible with the native 4×float array expected by opentui.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct Rgba(float R, float G, float B, float A = 1f)
{
    /// <summary>Fully transparent color (0,0,0,0).</summary>
    public static readonly Rgba Transparent = new(0f, 0f, 0f, 0f);

    /// <summary>White (1,1,1,1).</summary>
    public static readonly Rgba White = new(1f, 1f, 1f, 1f);

    /// <summary>Black (0,0,0,1).</summary>
    public static readonly Rgba Black = new(0f, 0f, 0f, 1f);

    /// <summary>Creates an Rgba from 0–255 integer components.</summary>
    public static Rgba FromInts(byte r, byte g, byte b, byte a = 255)
        => new(r / 255f, g / 255f, b / 255f, a / 255f);

    /// <summary>
    /// Parses a hex color string. Supports #RGB, #RGBA, #RRGGBB, #RRGGBBAA formats.
    /// The leading '#' is optional.
    /// </summary>
    public static Rgba FromHex(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);
        ReadOnlySpan<char> span = hex.AsSpan().TrimStart('#');

        return span.Length switch
        {
            3 => new Rgba(
                ParseNibble(span[0]) / 15f,
                ParseNibble(span[1]) / 15f,
                ParseNibble(span[2]) / 15f),
            4 => new Rgba(
                ParseNibble(span[0]) / 15f,
                ParseNibble(span[1]) / 15f,
                ParseNibble(span[2]) / 15f,
                ParseNibble(span[3]) / 15f),
            6 => new Rgba(
                ParseByte(span[0..2]) / 255f,
                ParseByte(span[2..4]) / 255f,
                ParseByte(span[4..6]) / 255f),
            8 => new Rgba(
                ParseByte(span[0..2]) / 255f,
                ParseByte(span[2..4]) / 255f,
                ParseByte(span[4..6]) / 255f,
                ParseByte(span[6..8]) / 255f),
            _ => throw new FormatException($"Invalid hex color format: '{hex}'"),
        };
    }

    /// <summary>Converts to 0–255 integer components.</summary>
    public (byte R, byte G, byte B, byte A) ToInts()
        => ((byte)(R * 255f + 0.5f), (byte)(G * 255f + 0.5f), (byte)(B * 255f + 0.5f), (byte)(A * 255f + 0.5f));

    /// <summary>Returns the hex string representation (#RRGGBB or #RRGGBBAA if alpha &lt; 1).</summary>
    public override string ToString()
    {
        var (r, g, b, a) = ToInts();
        return a == 255
            ? $"#{r:X2}{g:X2}{b:X2}"
            : $"#{r:X2}{g:X2}{b:X2}{a:X2}";
    }

    private static float ParseNibble(char c) =>
        int.Parse(stackalloc char[] { c }, NumberStyles.HexNumber);

    private static float ParseByte(ReadOnlySpan<char> span) =>
        int.Parse(span, NumberStyles.HexNumber);
}
