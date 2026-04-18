namespace OpenTui.Core;

/// <summary>
/// Border drawing style. Each style maps to a set of 11 Unicode box-drawing characters.
/// </summary>
public enum BorderStyle
{
    Single,
    Double,
    Rounded,
    Heavy,
}

/// <summary>Which sides of a box have borders.</summary>
[Flags]
public enum BorderSides
{
    None = 0,
    Left = 1 << 0,     // bit 0
    Bottom = 1 << 1,   // bit 1
    Right = 1 << 2,    // bit 2
    Top = 1 << 3,      // bit 3
    All = Top | Right | Bottom | Left, // 0b1111 = 15
}

/// <summary>
/// The 11 Unicode box-drawing codepoints for a border style.
/// Order matches the native drawBox convention:
/// TopLeft, TopRight, BottomLeft, BottomRight, Horizontal, Vertical, TopT, BottomT, LeftT, RightT, Cross
/// </summary>
public readonly struct BorderCharacters
{
    public char TopLeft { get; init; }
    public char TopRight { get; init; }
    public char BottomLeft { get; init; }
    public char BottomRight { get; init; }
    public char Horizontal { get; init; }
    public char Vertical { get; init; }
    public char TopT { get; init; }
    public char BottomT { get; init; }
    public char LeftT { get; init; }
    public char RightT { get; init; }
    public char Cross { get; init; }

    /// <summary>Convert to a uint[11] codepoint array for native calls.</summary>
    public uint[] ToCodePoints() =>
    [
        TopLeft, TopRight, BottomLeft, BottomRight,
        Horizontal, Vertical,
        TopT, BottomT, LeftT, RightT, Cross,
    ];

    /// <summary>Get the border characters for a given style.</summary>
    public static BorderCharacters ForStyle(BorderStyle style) => style switch
    {
        BorderStyle.Single => Single,
        BorderStyle.Double => Double,
        BorderStyle.Rounded => Rounded,
        BorderStyle.Heavy => Heavy,
        _ => Single,
    };

    public static readonly BorderCharacters Single = new()
    {
        TopLeft = '┌', TopRight = '┐', BottomLeft = '└', BottomRight = '┘',
        Horizontal = '─', Vertical = '│',
        TopT = '┬', BottomT = '┴', LeftT = '├', RightT = '┤', Cross = '┼',
    };

    public static readonly BorderCharacters Double = new()
    {
        TopLeft = '╔', TopRight = '╗', BottomLeft = '╚', BottomRight = '╝',
        Horizontal = '═', Vertical = '║',
        TopT = '╦', BottomT = '╩', LeftT = '╠', RightT = '╣', Cross = '╬',
    };

    public static readonly BorderCharacters Rounded = new()
    {
        TopLeft = '╭', TopRight = '╮', BottomLeft = '╰', BottomRight = '╯',
        Horizontal = '─', Vertical = '│',
        TopT = '┬', BottomT = '┴', LeftT = '├', RightT = '┤', Cross = '┼',
    };

    public static readonly BorderCharacters Heavy = new()
    {
        TopLeft = '┏', TopRight = '┓', BottomLeft = '┗', BottomRight = '┛',
        Horizontal = '━', Vertical = '┃',
        TopT = '┳', BottomT = '┻', LeftT = '┣', RightT = '┫', Cross = '╋',
    };
}

/// <summary>Title alignment for box titles.</summary>
public enum TitleAlignment : byte
{
    Left = 0,
    Center = 1,
    Right = 2,
}
