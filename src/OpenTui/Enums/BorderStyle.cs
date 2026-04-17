namespace OpenTui;

/// <summary>Visual style for box borders.</summary>
public enum BorderStyle : byte
{
    /// <summary>Single-line border (─│┌┐└┘).</summary>
    Single = 0,
    /// <summary>Double-line border (═║╔╗╚╝).</summary>
    Double = 1,
    /// <summary>Rounded border (─│╭╮╰╯).</summary>
    Rounded = 2,
    /// <summary>Heavy/thick border (━┃┏┓┗┛).</summary>
    Heavy = 3,
}
