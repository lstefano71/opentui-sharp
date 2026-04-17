namespace OpenTui;

/// <summary>Text wrapping mode for text buffers and views.</summary>
public enum WrapMode : byte
{
    /// <summary>No wrapping; lines extend beyond the viewport.</summary>
    None = 0,
    /// <summary>Wrap at word boundaries.</summary>
    Word = 1,
    /// <summary>Wrap at character boundaries.</summary>
    Char = 2,
}
