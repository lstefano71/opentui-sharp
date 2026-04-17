namespace OpenTui;

/// <summary>Specifies which color channel(s) to target in color operations.</summary>
[Flags]
public enum TargetChannel : byte
{
    /// <summary>Foreground color channel.</summary>
    Fg = 1,
    /// <summary>Background color channel.</summary>
    Bg = 2,
    /// <summary>Both foreground and background channels.</summary>
    Both = 3,
}
