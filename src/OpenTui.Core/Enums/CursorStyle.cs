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
    /// <summary>
    /// Represents the Default option.
    /// </summary>
    Default = 0,
    /// <summary>
    /// Represents the Pointer option.
    /// </summary>
    Pointer = 1,
    /// <summary>
    /// Represents the Text option.
    /// </summary>
    Text = 2,
    /// <summary>
    /// Represents the Crosshair option.
    /// </summary>
    Crosshair = 3,
    /// <summary>
    /// Represents the Move option.
    /// </summary>
    Move = 4,
    /// <summary>
    /// Represents the Not Allowed option.
    /// </summary>
    NotAllowed = 5,
}

/// <summary>Debug overlay placement corner.</summary>
public enum DebugOverlayCorner : byte
{
    /// <summary>
    /// Represents the Top Left option.
    /// </summary>
    TopLeft = 0,
    /// <summary>
    /// Represents the Top Right option.
    /// </summary>
    TopRight = 1,
    /// <summary>
    /// Represents the Bottom Left option.
    /// </summary>
    BottomLeft = 2,
    /// <summary>
    /// Represents the Bottom Right option.
    /// </summary>
    BottomRight = 3,
}

/// <summary>Target channel for color operations (foreground, background, or both).</summary>
public enum TargetChannel : byte
{
    /// <summary>
    /// Represents the Fg option.
    /// </summary>
    Fg = 1,
    /// <summary>
    /// Represents the Bg option.
    /// </summary>
    Bg = 2,
    /// <summary>
    /// Represents the Both option.
    /// </summary>
    Both = 3,
}

/// <summary>Width calculation method: wcwidth (C locale) or full Unicode grapheme.</summary>
public enum WidthMethod : byte
{
    /// <summary>
    /// Represents the Wcwidth option.
    /// </summary>
    Wcwidth = 0,
    /// <summary>
    /// Represents the Unicode option.
    /// </summary>
    Unicode = 1,
}

/// <summary>Theme mode (dark or light).</summary>
public enum ThemeMode : byte
{
    /// <summary>
    /// Represents the Dark option.
    /// </summary>
    Dark = 0,
    /// <summary>
    /// Represents the Light option.
    /// </summary>
    Light = 1,
}
