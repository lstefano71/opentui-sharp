namespace OpenTui;

/// <summary>Bitfield flags for text rendering attributes.</summary>
[Flags]
public enum TextAttribute : uint
{
    /// <summary>No attributes applied.</summary>
    None = 0,
    /// <summary>Bold / increased intensity.</summary>
    Bold = 1 << 0,
    /// <summary>Dim / decreased intensity.</summary>
    Dim = 1 << 1,
    /// <summary>Italic text.</summary>
    Italic = 1 << 2,
    /// <summary>Underlined text.</summary>
    Underline = 1 << 3,
    /// <summary>Blinking text.</summary>
    Blink = 1 << 4,
    /// <summary>Swapped foreground and background colors.</summary>
    Inverse = 1 << 5,
    /// <summary>Hidden / invisible text.</summary>
    Hidden = 1 << 6,
    /// <summary>Strikethrough text.</summary>
    Strikethrough = 1 << 7,
}
