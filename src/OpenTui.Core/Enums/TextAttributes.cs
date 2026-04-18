namespace OpenTui.Core;

/// <summary>
/// Text rendering attribute flags. Matches the native opentui TextAttributes bit layout.
/// </summary>
[Flags]
public enum TextAttributes : uint
{
    None = 0,
    Bold = 1 << 0,          // 1
    Dim = 1 << 1,           // 2
    Italic = 1 << 2,        // 4
    Underline = 1 << 3,     // 8
    Blink = 1 << 4,         // 16
    Inverse = 1 << 5,       // 32
    Hidden = 1 << 6,        // 64
    Strikethrough = 1 << 7, // 128
}

/// <summary>
/// Utility for working with attribute values that carry additional data in upper bits.
/// The lower 8 bits are the base TextAttributes. Upper bits may encode link IDs etc.
/// </summary>
public static class TextAttributeUtils
{
    public const int BaseBits = 8;
    public const uint BaseMask = 0xFF;

    /// <summary>Extract the base TextAttributes from a packed attribute value.</summary>
    public static TextAttributes GetBase(uint packed) => (TextAttributes)(packed & BaseMask);

    /// <summary>Create a packed attribute value combining base attributes with upper bits.</summary>
    public static uint Pack(TextAttributes baseAttrs, uint upper) => (uint)baseAttrs | (upper << BaseBits);

    /// <summary>Build a TextAttributes value from individual flags.</summary>
    public static TextAttributes Create(
        bool bold = false,
        bool dim = false,
        bool italic = false,
        bool underline = false,
        bool blink = false,
        bool inverse = false,
        bool hidden = false,
        bool strikethrough = false)
    {
        var attrs = TextAttributes.None;
        if (bold) attrs |= TextAttributes.Bold;
        if (dim) attrs |= TextAttributes.Dim;
        if (italic) attrs |= TextAttributes.Italic;
        if (underline) attrs |= TextAttributes.Underline;
        if (blink) attrs |= TextAttributes.Blink;
        if (inverse) attrs |= TextAttributes.Inverse;
        if (hidden) attrs |= TextAttributes.Hidden;
        if (strikethrough) attrs |= TextAttributes.Strikethrough;
        return attrs;
    }
}
