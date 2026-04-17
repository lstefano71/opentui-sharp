namespace OpenTui;

/// <summary>Terminal capabilities detected via capability queries.</summary>
public sealed class TerminalCapabilities
{
    /// <summary>Whether the terminal supports the Kitty keyboard protocol.</summary>
    public bool KittyKeyboard { get; init; }

    /// <summary>Whether the terminal supports Kitty graphics protocol.</summary>
    public bool KittyGraphics { get; init; }

    /// <summary>Whether the terminal supports RGB (true color).</summary>
    public bool Rgb { get; init; }

    /// <summary>The Unicode width calculation method to use.</summary>
    public WidthMethod Unicode { get; init; }

    /// <summary>Whether the terminal supports SGR pixel positioning.</summary>
    public bool SgrPixels { get; init; }

    /// <summary>Whether the terminal supports color scheme update notifications.</summary>
    public bool ColorSchemeUpdates { get; init; }

    /// <summary>Whether the terminal supports explicit character width.</summary>
    public bool ExplicitWidth { get; init; }

    /// <summary>Whether the terminal supports scaled text rendering.</summary>
    public bool ScaledText { get; init; }

    /// <summary>Whether the terminal supports Sixel graphics.</summary>
    public bool Sixel { get; init; }

    /// <summary>Whether the terminal supports focus tracking events.</summary>
    public bool FocusTracking { get; init; }

    /// <summary>Whether the terminal supports synchronized output.</summary>
    public bool Sync { get; init; }

    /// <summary>Whether the terminal supports bracketed paste mode.</summary>
    public bool BracketedPaste { get; init; }

    /// <summary>Whether the terminal supports hyperlinks (OSC 8).</summary>
    public bool Hyperlinks { get; init; }

    /// <summary>Whether the terminal supports OSC 52 clipboard access.</summary>
    public bool Osc52 { get; init; }

    /// <summary>Whether the terminal supports explicit cursor positioning.</summary>
    public bool ExplicitCursorPositioning { get; init; }

    /// <summary>The terminal name, if detected.</summary>
    public string? TermName { get; init; }

    /// <summary>The terminal version, if detected.</summary>
    public string? TermVersion { get; init; }

    /// <summary>Whether the terminal name was detected via XTVERSION.</summary>
    public bool TermFromXtVersion { get; init; }
}
