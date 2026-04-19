using System.Text.RegularExpressions;

namespace OpenTui.Core;

internal static partial class TerminalPaletteParser
{
    [GeneratedRegex(@"\x1b]4;(\d+);(?:(?:rgb:)([0-9a-fA-F]+)\/([0-9a-fA-F]+)\/([0-9a-fA-F]+)|#([0-9a-fA-F]{6}))(?:\x07|\x1b\\)")]
    private static partial Regex Osc4ResponseRe();

    [GeneratedRegex(@"\x1b](\d+);(?:(?:rgb:)([0-9a-fA-F]+)\/([0-9a-fA-F]+)\/([0-9a-fA-F]+)|#([0-9a-fA-F]{6}))(?:\x07|\x1b\\)")]
    private static partial Regex OscSpecialResponseRe();

    public static bool TryApplyPaletteResponse(string sequence, IDictionary<int, string?> palette)
    {
        bool updated = false;
        foreach (Match match in Osc4ResponseRe().Matches(sequence))
        {
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out int index) || !palette.ContainsKey(index))
                continue;

            palette[index] = ToHex(match.Groups[2].Value, match.Groups[3].Value, match.Groups[4].Value, match.Groups[5].Value);
            updated = true;
        }

        return updated;
    }

    public static bool TryApplySpecialResponse(string sequence, IDictionary<int, string?> specialColors)
    {
        bool updated = false;
        foreach (Match match in OscSpecialResponseRe().Matches(sequence))
        {
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out int index) || !specialColors.ContainsKey(index))
                continue;

            specialColors[index] = ToHex(match.Groups[2].Value, match.Groups[3].Value, match.Groups[4].Value, match.Groups[5].Value);
            updated = true;
        }

        return updated;
    }

    private static string ToHex(string r, string g, string b, string hex6)
    {
        if (!string.IsNullOrEmpty(hex6))
            return $"#{hex6.ToLowerInvariant()}";

        return $"#{ScaleComponent(r)}{ScaleComponent(g)}{ScaleComponent(b)}";
    }

    private static string ScaleComponent(string component)
    {
        if (string.IsNullOrEmpty(component))
            return "00";

        int value = Convert.ToInt32(component, 16);
        int maxInput = (1 << (4 * component.Length)) - 1;
        int scaled = maxInput == 0 ? 0 : (int)Math.Round((value / (double)maxInput) * 255);
        return scaled.ToString("x2");
    }
}
