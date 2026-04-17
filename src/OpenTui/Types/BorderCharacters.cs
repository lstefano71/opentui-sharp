namespace OpenTui;

/// <summary>Characters used for drawing box borders and grid lines.</summary>
public sealed record BorderCharacters(
    char TopLeft, char TopRight,
    char BottomLeft, char BottomRight,
    char Horizontal, char Vertical,
    char TopT, char BottomT,
    char LeftT, char RightT,
    char Cross)
{
    /// <summary>Single-line border characters: ┌─┐│└┘</summary>
    public static readonly BorderCharacters Single = new('┌', '┐', '└', '┘', '─', '│', '┬', '┴', '├', '┤', '┼');

    /// <summary>Double-line border characters: ╔═╗║╚╝</summary>
    public static readonly BorderCharacters Double = new('╔', '╗', '╚', '╝', '═', '║', '╦', '╩', '╠', '╣', '╬');

    /// <summary>Rounded border characters: ╭─╮│╰╯</summary>
    public static readonly BorderCharacters Rounded = new('╭', '╮', '╰', '╯', '─', '│', '┬', '┴', '├', '┤', '┼');

    /// <summary>Heavy border characters: ┏━┓┃┗┛</summary>
    public static readonly BorderCharacters Heavy = new('┏', '┓', '┗', '┛', '━', '┃', '┳', '┻', '┣', '┫', '╋');

    /// <summary>Gets the predefined characters for a given border style.</summary>
    public static BorderCharacters ForStyle(BorderStyle style) => style switch
    {
        BorderStyle.Single => Single,
        BorderStyle.Double => Double,
        BorderStyle.Rounded => Rounded,
        BorderStyle.Heavy => Heavy,
        _ => Single,
    };

    /// <summary>Converts to a uint array (codepoints) for native interop.</summary>
    internal uint[] ToCodePoints() =>
    [
        TopLeft, TopRight, BottomLeft, BottomRight,
        Horizontal, Vertical, TopT, BottomT,
        LeftT, RightT, Cross
    ];
}
