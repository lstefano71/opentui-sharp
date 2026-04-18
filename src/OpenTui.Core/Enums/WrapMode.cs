namespace OpenTui.Core;

/// <summary>
/// Wrap mode for text rendering. Matches the native byte values.
/// </summary>
public enum WrapMode : byte
{
    /// <summary>No wrapping — text extends beyond viewport.</summary>
    None = 0,
    /// <summary>Wrap at character boundaries.</summary>
    Char = 1,
    /// <summary>Wrap at word boundaries.</summary>
    Word = 2,
}
