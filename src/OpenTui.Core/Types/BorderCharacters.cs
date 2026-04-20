namespace OpenTui.Core;

/// <summary>
/// Border drawing style. Each style maps to a set of 11 Unicode box-drawing characters.
/// </summary>
public enum BorderStyle
{
    /// <summary>
    /// Represents the Single option.
    /// </summary>
    Single,
    /// <summary>
    /// Represents the Double option.
    /// </summary>
    Double,
    /// <summary>
    /// Represents the Rounded option.
    /// </summary>
    Rounded,
    /// <summary>
    /// Represents the Heavy option.
    /// </summary>
    Heavy,
}

/// <summary>Which sides of a box have borders.</summary>
[Flags]
public enum BorderSides
{
    /// <summary>
    /// Represents the None option.
    /// </summary>
    None = 0,
    /// <summary>
    /// Represents the Left option.
    /// </summary>
    Left = 1 << 0,     // bit 0
    /// <summary>
    /// Represents the Bottom option.
    /// </summary>
    Bottom = 1 << 1,   // bit 1
    /// <summary>
    /// Represents the Right option.
    /// </summary>
    Right = 1 << 2,    // bit 2
    /// <summary>
    /// Represents the Top option.
    /// </summary>
    Top = 1 << 3,      // bit 3
    /// <summary>
    /// Represents the All option.
    /// </summary>
    All = Top | Right | Bottom | Left, // 0b1111 = 15
}

/// <summary>
/// The 11 Unicode box-drawing codepoints for a border style.
/// Order matches the native drawBox convention:
/// TopLeft, TopRight, BottomLeft, BottomRight, Horizontal, Vertical, TopT, BottomT, LeftT, RightT, Cross
/// </summary>
public readonly struct BorderCharacters
{
    /// <summary>
    /// Gets or sets the top left.
    /// </summary>
    public char TopLeft { get; init; }
    /// <summary>
    /// Gets or sets the top right.
    /// </summary>
    public char TopRight { get; init; }
    /// <summary>
    /// Gets or sets the bottom left.
    /// </summary>
    public char BottomLeft { get; init; }
    /// <summary>
    /// Gets or sets the bottom right.
    /// </summary>
    public char BottomRight { get; init; }
    /// <summary>
    /// Gets or sets the horizontal.
    /// </summary>
    public char Horizontal { get; init; }
    /// <summary>
    /// Gets or sets the vertical.
    /// </summary>
    public char Vertical { get; init; }
    /// <summary>
    /// Gets or sets the top t.
    /// </summary>
    public char TopT { get; init; }
    /// <summary>
    /// Gets or sets the bottom t.
    /// </summary>
    public char BottomT { get; init; }
    /// <summary>
    /// Gets or sets the left t.
    /// </summary>
    public char LeftT { get; init; }
    /// <summary>
    /// Gets or sets the right t.
    /// </summary>
    public char RightT { get; init; }
    /// <summary>
    /// Gets or sets the cross.
    /// </summary>
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

    /// <summary>
    /// Stores the single.
    /// </summary>
    public static readonly BorderCharacters Single = new()
    {
        TopLeft = '┌', TopRight = '┐', BottomLeft = '└', BottomRight = '┘',
        Horizontal = '─', Vertical = '│',
        TopT = '┬', BottomT = '┴', LeftT = '├', RightT = '┤', Cross = '┼',
    };

    /// <summary>
    /// Stores the double.
    /// </summary>
    public static readonly BorderCharacters Double = new()
    {
        TopLeft = '╔', TopRight = '╗', BottomLeft = '╚', BottomRight = '╝',
        Horizontal = '═', Vertical = '║',
        TopT = '╦', BottomT = '╩', LeftT = '╠', RightT = '╣', Cross = '╬',
    };

    /// <summary>
    /// Stores the rounded.
    /// </summary>
    public static readonly BorderCharacters Rounded = new()
    {
        TopLeft = '╭', TopRight = '╮', BottomLeft = '╰', BottomRight = '╯',
        Horizontal = '─', Vertical = '│',
        TopT = '┬', BottomT = '┴', LeftT = '├', RightT = '┤', Cross = '┼',
    };

    /// <summary>
    /// Stores the heavy.
    /// </summary>
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
    /// <summary>
    /// Represents the Left option.
    /// </summary>
    Left = 0,
    /// <summary>
    /// Represents the Center option.
    /// </summary>
    Center = 1,
    /// <summary>
    /// Represents the Right option.
    /// </summary>
    Right = 2,
}
