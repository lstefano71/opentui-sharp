namespace OpenTui;

/// <summary>Terminal cursor visual style.</summary>
public enum CursorStyle : byte
{
    /// <summary>Block cursor (█).</summary>
    Block = 0,
    /// <summary>Vertical line cursor (|).</summary>
    Line = 1,
    /// <summary>Underline cursor (_).</summary>
    Underline = 2,
    /// <summary>Terminal default cursor style.</summary>
    Default = 255,
}
