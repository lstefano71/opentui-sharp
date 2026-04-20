namespace OpenTui.Core;

/// <summary>
/// Text rendering attribute flags. Matches the native opentui TextAttributes bit layout.
/// </summary>
[Flags]
public enum TextAttributes : uint
{
    /// <summary>
    /// Represents the None option.
    /// </summary>
    None = 0,
    /// <summary>
    /// Represents the Bold option.
    /// </summary>
    Bold = 1 << 0,          // 1
    /// <summary>
    /// Represents the Dim option.
    /// </summary>
    Dim = 1 << 1,           // 2
    /// <summary>
    /// Represents the Italic option.
    /// </summary>
    Italic = 1 << 2,        // 4
    /// <summary>
    /// Represents the Underline option.
    /// </summary>
    Underline = 1 << 3,     // 8
    /// <summary>
    /// Represents the Blink option.
    /// </summary>
    Blink = 1 << 4,         // 16
    /// <summary>
    /// Represents the Inverse option.
    /// </summary>
    Inverse = 1 << 5,       // 32
    /// <summary>
    /// Represents the Hidden option.
    /// </summary>
    Hidden = 1 << 6,        // 64
    /// <summary>
    /// Represents the Strikethrough option.
    /// </summary>
    Strikethrough = 1 << 7, // 128
}

/// <summary>
/// Utility for working with attribute values that carry additional data in upper bits.
/// The lower 8 bits are the base TextAttributes. Upper bits may encode link IDs etc.
/// </summary>
public static class TextAttributeUtils
{
    /// <summary>
    /// Stores the base bits.
    /// </summary>
    public const int BaseBits = 8;
    /// <summary>
    /// Stores the base mask.
    /// </summary>
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
