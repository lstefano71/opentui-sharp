using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace OpenTui.Core;

/// <summary>
/// RGBA color with float components (0.0–1.0).
/// Layout-compatible with the native 4×float array expected by opentui.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct Rgba(float R, float G, float B, float A = 1f)
{
    // Common constants
    public static readonly Rgba Transparent = new(0f, 0f, 0f, 0f);
    public static readonly Rgba White = new(1f, 1f, 1f, 1f);
    public static readonly Rgba Black = new(0f, 0f, 0f, 1f);
    public static readonly Rgba Red = new(1f, 0f, 0f, 1f);
    public static readonly Rgba Green = new(0f, 128f / 255f, 0f, 1f);
    public static readonly Rgba Blue = new(0f, 0f, 1f, 1f);
    public static readonly Rgba Yellow = new(1f, 1f, 0f, 1f);
    public static readonly Rgba Cyan = new(0f, 1f, 1f, 1f);
    public static readonly Rgba Magenta = new(1f, 0f, 1f, 1f);

    // Factory methods

    public static Rgba FromValues(float r, float g, float b, float a = 1f)
        => new(r, g, b, a);

    public static Rgba FromInts(int r, int g, int b, int a = 255)
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

    /// <summary>Convert to integer tuple (0–255).</summary>
    public (byte R, byte G, byte B, byte A) ToInts()
        => ((byte)(R * 255f + 0.5f), (byte)(G * 255f + 0.5f), (byte)(B * 255f + 0.5f), (byte)(A * 255f + 0.5f));

    /// <summary>Returns the hex string representation (#RRGGBB or #RRGGBBAA if alpha &lt; 1).</summary>
    public string ToHex()
    {
        var (r, g, b, a) = ToInts();
        return a == 255
            ? $"#{r:X2}{g:X2}{b:X2}"
            : $"#{r:X2}{g:X2}{b:X2}{a:X2}";
    }

    public override string ToString() => ToHex();

    // HSV conversion (matching reference hsvToRgb)
    public static Rgba FromHsv(float h, float s, float v)
    {
        float r = 0, g = 0, b = 0;
        int i = (int)MathF.Floor(h / 60) % 6;
        float f = h / 60 - MathF.Floor(h / 60);
        float p = v * (1 - s);
        float q = v * (1 - f * s);
        float t = v * (1 - (1 - f) * s);
        switch (i)
        {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            case 5: r = v; g = p; b = q; break;
        }
        return new Rgba(r, g, b, 1f);
    }

    /// <summary>Parse a color from a string (hex, name, or "transparent").</summary>
    public static Rgba Parse(string color)
    {
        var lower = color.ToLowerInvariant();
        if (lower == "transparent")
            return Transparent;
        if (CssColors.TryGetValue(lower, out var hex))
            return FromHex(hex);
        return FromHex(color);
    }

    /// <summary>Try to resolve a CSS color name to Rgba.</summary>
    public static Rgba? FromName(string name)
    {
        if (CssColors.TryGetValue(name.ToLowerInvariant(), out var hex))
            return FromHex(hex);
        return null;
    }

    private static float ParseNibble(char c) =>
        int.Parse(stackalloc char[] { c }, NumberStyles.HexNumber);

    private static float ParseByte(ReadOnlySpan<char> span) =>
        int.Parse(span, NumberStyles.HexNumber);

    private static readonly Dictionary<string, string> CssColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = "#000000",
        ["white"] = "#FFFFFF",
        ["red"] = "#FF0000",
        ["green"] = "#008000",
        ["blue"] = "#0000FF",
        ["yellow"] = "#FFFF00",
        ["cyan"] = "#00FFFF",
        ["magenta"] = "#FF00FF",
        ["silver"] = "#C0C0C0",
        ["gray"] = "#808080",
        ["grey"] = "#808080",
        ["maroon"] = "#800000",
        ["olive"] = "#808000",
        ["lime"] = "#00FF00",
        ["aqua"] = "#00FFFF",
        ["teal"] = "#008080",
        ["navy"] = "#000080",
        ["fuchsia"] = "#FF00FF",
        ["purple"] = "#800080",
        ["orange"] = "#FFA500",
        ["brightblack"] = "#666666",
        ["brightred"] = "#FF6666",
        ["brightgreen"] = "#66FF66",
        ["brightblue"] = "#6666FF",
        ["brightyellow"] = "#FFFF66",
        ["brightcyan"] = "#66FFFF",
        ["brightmagenta"] = "#FF66FF",
        ["brightwhite"] = "#FFFFFF",
    };
}
