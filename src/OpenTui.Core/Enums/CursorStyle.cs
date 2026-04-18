namespace OpenTui.Core;

/// <summary>
/// Terminal cursor style. Values match the ANSI/xterm DECSCUSR parameter values
/// used by the native renderer.
/// </summary>
public enum CursorStyle : byte
{
    /// <summary>Use the terminal default cursor (DECSCUSR 0 / sentinel 255).</summary>
    Default = 255,
    /// <summary>Blinking block cursor (DECSCUSR 1).</summary>
    BlinkingBlock = 1,
    /// <summary>Steady (non-blinking) block cursor (DECSCUSR 2).</summary>
    SteadyBlock = 2,
    /// <summary>Blinking underline cursor (DECSCUSR 3).</summary>
    BlinkingUnderline = 3,
    /// <summary>Steady underline cursor (DECSCUSR 4).</summary>
    SteadyUnderline = 4,
    /// <summary>Blinking bar (vertical line) cursor (DECSCUSR 5).</summary>
    BlinkingBar = 5,
    /// <summary>Steady bar (vertical line) cursor (DECSCUSR 6).</summary>
    SteadyBar = 6,
}

/// <summary>Mouse pointer shape for terminal mouse mode.</summary>
public enum MousePointerStyle : byte
{
    Default = 0,
    Pointer = 1,
    Text = 2,
    Crosshair = 3,
    Move = 4,
    NotAllowed = 5,
}

/// <summary>Debug overlay placement corner.</summary>
public enum DebugOverlayCorner : byte
{
    TopLeft = 0,
    TopRight = 1,
    BottomLeft = 2,
    BottomRight = 3,
}

/// <summary>Target channel for color operations (foreground, background, or both).</summary>
public enum TargetChannel : byte
{
    Fg = 1,
    Bg = 2,
    Both = 3,
}

/// <summary>Width calculation method: wcwidth (C locale) or full Unicode grapheme.</summary>
public enum WidthMethod : byte
{
    Wcwidth = 0,
    Unicode = 1,
}

/// <summary>Theme mode (dark or light).</summary>
public enum ThemeMode : byte
{
    Dark = 0,
    Light = 1,
}
