namespace OpenTui;

/// <summary>Mouse pointer visual style for the terminal.</summary>
public enum MousePointerStyle : byte
{
    /// <summary>Default arrow pointer.</summary>
    Default = 0,
    /// <summary>Hand/pointer cursor for clickable elements.</summary>
    Pointer = 1,
    /// <summary>I-beam cursor for text selection.</summary>
    Text = 2,
    /// <summary>Crosshair cursor for precise selection.</summary>
    Crosshair = 3,
    /// <summary>Move cursor for drag operations.</summary>
    Move = 4,
    /// <summary>Not-allowed cursor indicating a disabled action.</summary>
    NotAllowed = 5,
}
