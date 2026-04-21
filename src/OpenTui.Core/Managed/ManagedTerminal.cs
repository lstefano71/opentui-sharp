using System.Buffers;
using System.Buffers.Text;
using System.Text;

namespace OpenTui.Core.Managed;

// ── Enums ────────────────────────────────────────────────────────────────

/// <summary>Mouse tracking level. Higher levels include all lower-level events.</summary>
public enum MouseLevel : byte
{
    /// <summary>No mouse tracking.</summary>
    None,
    /// <summary>Click only (xterm ?1000).</summary>
    Basic,
    /// <summary>Click + drag (xterm ?1000 + ?1002).</summary>
    Drag,
    /// <summary>All motion (xterm ?1000 + ?1002 + ?1003).</summary>
    Motion,
    /// <summary>Pixel coordinates (xterm ?1000 + ?1002 + ?1003 + ?1016).</summary>
    Pixels,
}

/// <summary>Target channel for OSC 52 clipboard operations.</summary>
public enum ClipboardTarget : byte
{
    /// <summary>System clipboard ("c").</summary>
    Clipboard,
    /// <summary>Primary selection ("p").</summary>
    Primary,
    /// <summary>Secondary selection ("s").</summary>
    Secondary,
    /// <summary>Query clipboard contents ("q").</summary>
    Query,
}

// ── ANSI constants ───────────────────────────────────────────────────────

/// <summary>
/// Static ANSI escape sequences returned as UTF-8 byte spans.
/// Values match the canonical Zig <c>ansi.zig</c> definitions.
/// </summary>
internal static class AnsiSequences
{
    // ── Screen ───────────────────────────────────────────────────────
    public static ReadOnlySpan<byte> SwitchToAlternateScreen => "\x1b[?1049h"u8;
    public static ReadOnlySpan<byte> SwitchToMainScreen => "\x1b[?1049l"u8;

    // ── Cursor visibility ────────────────────────────────────────────
    public static ReadOnlySpan<byte> HideCursor => "\x1b[?25l"u8;
    public static ReadOnlySpan<byte> ShowCursor => "\x1b[?25h"u8;
    public static ReadOnlySpan<byte> SaveCursorState => "\x1b[s"u8;
    public static ReadOnlySpan<byte> RestoreCursorState => "\x1b[u"u8;
    public static ReadOnlySpan<byte> Home => "\x1b[H"u8;
    public static ReadOnlySpan<byte> DefaultCursorStyle => "\x1b[0 q"u8;
    public static ReadOnlySpan<byte> ReverseIndex => "\x1bM"u8;

    // ── Reset / erase ────────────────────────────────────────────────
    public static ReadOnlySpan<byte> Reset => "\x1b[0m"u8;
    public static ReadOnlySpan<byte> EraseBelowCursor => "\x1b[J"u8;
    public static ReadOnlySpan<byte> ResetMousePointer => "\x1b]22;\x07"u8;
    public static ReadOnlySpan<byte> ResetTerminalBgColor => "\x1b]111\x07"u8;
    public static ReadOnlySpan<byte> ResetCursorColor => "\x1b]112\x07"u8;

    // ── Mouse tracking ───────────────────────────────────────────────
    public static ReadOnlySpan<byte> EnableMouseTracking => "\x1b[?1000h"u8;
    public static ReadOnlySpan<byte> DisableMouseTracking => "\x1b[?1000l"u8;
    public static ReadOnlySpan<byte> EnableButtonEventTracking => "\x1b[?1002h"u8;
    public static ReadOnlySpan<byte> DisableButtonEventTracking => "\x1b[?1002l"u8;
    public static ReadOnlySpan<byte> EnableAnyEventTracking => "\x1b[?1003h"u8;
    public static ReadOnlySpan<byte> DisableAnyEventTracking => "\x1b[?1003l"u8;
    public static ReadOnlySpan<byte> EnableSgrMouseMode => "\x1b[?1006h"u8;
    public static ReadOnlySpan<byte> DisableSgrMouseMode => "\x1b[?1006l"u8;
    public static ReadOnlySpan<byte> EnableSgrPixelMouse => "\x1b[?1016h"u8;
    public static ReadOnlySpan<byte> DisableSgrPixelMouse => "\x1b[?1016l"u8;

    // ── Keyboard ─────────────────────────────────────────────────────
    public static ReadOnlySpan<byte> EnableBracketedPaste => "\x1b[?2004h"u8;
    public static ReadOnlySpan<byte> DisableBracketedPaste => "\x1b[?2004l"u8;
    public static ReadOnlySpan<byte> CsiUPop => "\x1b[<u"u8;
    public static ReadOnlySpan<byte> CsiUQuery => "\x1b[?u"u8;
    public static ReadOnlySpan<byte> ModifyOtherKeysSet => "\x1b[>4;1m"u8;
    public static ReadOnlySpan<byte> ModifyOtherKeysReset => "\x1b[>4;0m"u8;

    // ── Focus ────────────────────────────────────────────────────────
    public static ReadOnlySpan<byte> EnableFocusTracking => "\x1b[?1004h"u8;
    public static ReadOnlySpan<byte> DisableFocusTracking => "\x1b[?1004l"u8;

    // ── Sync ─────────────────────────────────────────────────────────
    public static ReadOnlySpan<byte> BeginSync => "\x1b[?2026h"u8;
    public static ReadOnlySpan<byte> EndSync => "\x1b[?2026l"u8;

    // ── Color scheme ─────────────────────────────────────────────────
    public static ReadOnlySpan<byte> EnableColorSchemeUpdates => "\x1b[?2031h"u8;
    public static ReadOnlySpan<byte> DisableColorSchemeUpdates => "\x1b[?2031l"u8;
    public static ReadOnlySpan<byte> ColorSchemeRequest => "\x1b[?996n"u8;

    // ── Unicode ──────────────────────────────────────────────────────
    public static ReadOnlySpan<byte> UnicodeSet => "\x1b[?2027h"u8;

    // ── Queries ──────────────────────────────────────────────────────
    public static ReadOnlySpan<byte> CursorPositionRequest => "\x1b[6n"u8;
    public static ReadOnlySpan<byte> Xtversion => "\x1b[>0q"u8;

    // DECRQM queries
    public static ReadOnlySpan<byte> DecrqmSgrPixels => "\x1b[?1016$p"u8;
    public static ReadOnlySpan<byte> DecrqmUnicode => "\x1b[?2027$p"u8;
    public static ReadOnlySpan<byte> DecrqmColorScheme => "\x1b[?2031$p"u8;
    public static ReadOnlySpan<byte> DecrqmFocus => "\x1b[?1004$p"u8;
    public static ReadOnlySpan<byte> DecrqmBracketedPaste => "\x1b[?2004$p"u8;
    public static ReadOnlySpan<byte> DecrqmSync => "\x1b[?2026$p"u8;

    // Kitty graphics query (i=31337 is our sentinel ID)
    public static ReadOnlySpan<byte> KittyGraphicsQuery => "\x1b_Gi=31337,s=1,v=1,a=q,t=d,f=24;AAAA\x1b\\\x1b[c"u8;

    // Explicit width / scaled text
    public static ReadOnlySpan<byte> ExplicitWidthQuery => "\x1b]66;w=1; \x1b\\"u8;
    public static ReadOnlySpan<byte> ScaledTextQuery => "\x1b]66;s=2; \x1b\\"u8;

    // OSC theme queries (BEL terminated)
    public static ReadOnlySpan<byte> OscThemeQueries => "\x1b]10;?\x07\x1b]11;?\x07"u8;

    // tmux DCS passthrough
    public static ReadOnlySpan<byte> TmuxDcsStart => "\x1bPtmux;"u8;
    public static ReadOnlySpan<byte> TmuxDcsEnd => "\x1b\\"u8;

    // GNU Screen DCS passthrough
    public static ReadOnlySpan<byte> ScreenDcsStart => "\x1bP"u8;
    public static ReadOnlySpan<byte> ScreenDcsEnd => "\x1b\\"u8;

    // OSC 8 hyperlink end
    public static ReadOnlySpan<byte> HyperlinkEnd => "\x1b]8;;\x07"u8;
}

// ── Supporting types ─────────────────────────────────────────────────────

/// <summary>Terminal capabilities detected via query responses.</summary>
public sealed class TerminalCapabilities
{
    public bool KittyKeyboard { get; set; }
    public bool KittyGraphics { get; set; }
    public bool Rgb { get; set; }
    public WidthMethod Unicode { get; set; } = WidthMethod.Unicode;
    public bool SgrPixels { get; set; }
    public bool ColorSchemeUpdates { get; set; }
    public bool ExplicitWidth { get; set; }
    public bool ScaledText { get; set; }
    public bool Sixel { get; set; }
    public bool FocusTracking { get; set; }
    public bool Sync { get; set; }
    public bool BracketedPaste { get; set; }
    public bool Hyperlinks { get; set; }
    public bool Osc52 { get; set; }
    public bool ExplicitCursorPositioning { get; set; }
}

/// <summary>Terminal mode and cursor state tracked by the managed terminal.</summary>
public sealed class TerminalState
{
    public bool AltScreen { get; set; }
    public bool KittyKeyboard { get; set; }
    public byte KittyKeyboardFlags { get; set; }
    public bool BracketedPaste { get; set; }
    public bool Mouse { get; set; }
    public bool MouseMovement { get; set; } = true;
    public bool MouseWasEnabled { get; set; }
    public bool PixelMouse { get; set; }
    public bool ColorSchemeUpdates { get; set; }
    public bool ThemeQueriesSent { get; set; }
    public bool FocusTracking { get; set; }
    public bool ModifyOtherKeys { get; set; }
    public MousePointerStyle MousePointer { get; set; } = MousePointerStyle.Default;

    // Cursor state
    public ushort CursorRow { get; set; }
    public ushort CursorCol { get; set; }
    public uint CursorX { get; set; } = 1;
    public uint CursorY { get; set; } = 1;
    public bool CursorVisible { get; set; } = true;
    public CursorStyle CursorStyle { get; set; } = CursorStyle.Default;
    public bool CursorBlinking { get; set; }
    public Rgba CursorColor { get; set; } = Rgba.White;
}

/// <summary>Terminal name and version parsed from xtversion or environment.</summary>
public sealed class TerminalInfo
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool FromXtversion { get; set; }
}

/// <summary>Options for constructing a <see cref="ManagedTerminal"/>.</summary>
public sealed class TerminalOptions
{
    /// <summary>
    /// Kitty keyboard protocol flags (progressive enhancement).
    /// Default 0b00101 (5) = disambiguate + alternate keys.
    /// </summary>
    public byte KittyKeyboardFlags { get; init; } = 0b00101;

    /// <summary>When true, skip environment-based overrides (remote session).</summary>
    public bool Remote { get; init; }
}

// ── Main class ───────────────────────────────────────────────────────────

/// <summary>
/// Pure C# implementation of the Zig terminal management module (<c>terminal.zig</c>).
/// Manages terminal capabilities, mode switching (alternate screen, mouse tracking, etc.),
/// cursor state, and ANSI escape sequence generation.
/// </summary>
public sealed class ManagedTerminal : IDisposable
{
    private readonly TerminalOptions _options;
    private Dictionary<string, string>? _hostEnvMap;

    // Internal query state
    private bool _inTmux;
    private bool _skipGraphicsQuery;
    private bool _skipExplicitWidthQuery;
    private bool _graphicsQueryPending;
    private bool _capabilityQueriesPending;
    private bool _themeQueriesPending;

    /// <summary>Terminal capabilities detected via query responses.</summary>
    public TerminalCapabilities Capabilities { get; } = new();

    /// <summary>Current terminal mode and cursor state.</summary>
    public TerminalState State { get; } = new();

    /// <summary>Terminal name/version from xtversion or environment.</summary>
    public TerminalInfo TermInfo { get; } = new();

    public ManagedTerminal(TerminalOptions? options = null)
    {
        _options = options ?? new TerminalOptions();
        CheckEnvironmentOverrides();
    }

    // ── Screen ───────────────────────────────────────────────────────

    public void EnterAltScreen(ITerminalWriter writer)
    {
        writer.Write(AnsiSequences.SwitchToAlternateScreen);
        State.AltScreen = true;
    }

    public void ExitAltScreen(ITerminalWriter writer)
    {
        writer.Write(AnsiSequences.SwitchToMainScreen);
        State.AltScreen = false;
    }

    /// <summary>
    /// Reset all terminal modes to defaults, undoing everything that was enabled.
    /// Matches the Zig <c>resetState</c> method.
    /// </summary>
    public void ResetState(ITerminalWriter writer)
    {
        writer.Write(AnsiSequences.ShowCursor);
        writer.Write(AnsiSequences.Reset);
        writer.Write(AnsiSequences.ResetMousePointer);
        State.MousePointer = MousePointerStyle.Default;

        if (State.KittyKeyboard)
            SetKittyKeyboard(writer, false, 0);

        if (State.ModifyOtherKeys)
            SetModifyOtherKeys(writer, false);

        if (State.MouseWasEnabled)
            ForceDisableMouseMode(writer);

        if (State.BracketedPaste)
            SetBracketedPaste(writer, false);

        if (State.FocusTracking)
            SetFocusTracking(writer, false);

        if (State.AltScreen)
        {
            ExitAltScreen(writer);
        }
        else
        {
            // Windows: move cursor back up and erase below
            if (OperatingSystem.IsWindows())
            {
                writer.Write("\r"u8);
                for (int i = 0; i < State.CursorRow; i++)
                    writer.Write(AnsiSequences.ReverseIndex);
                writer.Write(AnsiSequences.EraseBelowCursor);
            }
        }

        if (State.ColorSchemeUpdates)
            SetColorSchemeUpdates(writer, false);

        SetTerminalTitle(writer, "");

        writer.Write(AnsiSequences.ResetTerminalBgColor);
    }

    // ── Mouse ────────────────────────────────────────────────────────

    /// <summary>
    /// Set mouse tracking mode. Maps <see cref="MouseLevel"/> to the appropriate
    /// combination of xterm mouse tracking escape sequences.
    /// </summary>
    public void SetMouseMode(ITerminalWriter writer, MouseLevel level)
    {
        bool enable = level != MouseLevel.None;
        bool enableMovement = level >= MouseLevel.Motion;

        if (enable)
        {
            if (State.Mouse && State.MouseMovement == enableMovement) return;
        }
        else if (!State.Mouse)
        {
            return;
        }

        if (enable)
        {
            State.Mouse = true;
            State.MouseMovement = enableMovement;
            State.MouseWasEnabled = true;
            State.PixelMouse = level == MouseLevel.Pixels;

            if (!enableMovement)
                writer.Write(AnsiSequences.DisableAnyEventTracking);

            writer.Write(AnsiSequences.EnableMouseTracking);
            writer.Write(AnsiSequences.EnableButtonEventTracking);

            if (enableMovement)
                writer.Write(AnsiSequences.EnableAnyEventTracking);

            if (level == MouseLevel.Pixels)
                writer.Write(AnsiSequences.EnableSgrPixelMouse);
            else
                writer.Write(AnsiSequences.EnableSgrMouseMode);
        }
        else
        {
            State.Mouse = false;
            State.PixelMouse = false;
            WriteMouseDisableSequences(writer);
        }
    }

    /// <summary>
    /// Force-disable all mouse tracking modes. Used during shutdown cleanup
    /// to emit sequences even if tracked state has drifted.
    /// </summary>
    public void ForceDisableMouseMode(ITerminalWriter writer)
    {
        State.Mouse = false;
        State.PixelMouse = false;
        WriteMouseDisableSequences(writer);
    }

    private static void WriteMouseDisableSequences(ITerminalWriter writer)
    {
        writer.Write(AnsiSequences.DisableAnyEventTracking);
        writer.Write(AnsiSequences.DisableButtonEventTracking);
        writer.Write(AnsiSequences.DisableMouseTracking);
        writer.Write(AnsiSequences.DisableSgrMouseMode);
    }

    // ── Keyboard ─────────────────────────────────────────────────────

    /// <summary>
    /// Push or pop kitty keyboard protocol mode.
    /// Enable: <c>CSI &gt; {flags} u</c>.  Disable: <c>CSI &lt; u</c>.
    /// </summary>
    public void SetKittyKeyboard(ITerminalWriter writer, bool enable, byte flags)
    {
        if (enable)
        {
            if (!State.KittyKeyboard)
            {
                // CSI > {flags} u — push keyboard mode
                Span<byte> buf = stackalloc byte[16];
                buf[0] = 0x1b; // ESC
                buf[1] = (byte)'[';
                buf[2] = (byte)'>';
                int pos = 3;
                Utf8Formatter.TryFormat(flags, buf[pos..], out int written);
                pos += written;
                buf[pos++] = (byte)'u';
                writer.Write(buf[..pos]);
                State.KittyKeyboard = true;
                State.KittyKeyboardFlags = flags;
            }
        }
        else
        {
            if (State.KittyKeyboard)
            {
                writer.Write(AnsiSequences.CsiUPop);
                State.KittyKeyboard = false;
                State.KittyKeyboardFlags = 0;
            }
        }
    }

    public void SetBracketedPaste(ITerminalWriter writer, bool enable)
    {
        writer.Write(enable ? AnsiSequences.EnableBracketedPaste : AnsiSequences.DisableBracketedPaste);
        State.BracketedPaste = enable;
    }

    public void SetModifyOtherKeys(ITerminalWriter writer, bool enable)
    {
        writer.Write(enable ? AnsiSequences.ModifyOtherKeysSet : AnsiSequences.ModifyOtherKeysReset);
        State.ModifyOtherKeys = enable;
    }

    // ── Focus ────────────────────────────────────────────────────────

    public void SetFocusTracking(ITerminalWriter writer, bool enable)
    {
        writer.Write(enable ? AnsiSequences.EnableFocusTracking : AnsiSequences.DisableFocusTracking);
        State.FocusTracking = enable;
    }

    // ── Cursor ───────────────────────────────────────────────────────

    /// <summary>
    /// Update the tracked cursor position. The 1-based coordinates are stored
    /// in <see cref="TerminalState.CursorX"/>/<see cref="TerminalState.CursorY"/>,
    /// and the ANSI <c>CUP</c> sequence is written to the terminal.
    /// </summary>
    public void SetCursorPosition(ITerminalWriter writer, uint x, uint y)
    {
        uint clampedX = Math.Max(1, x);
        uint clampedY = Math.Max(1, y);
        State.CursorX = clampedX;
        State.CursorY = clampedY;
        State.CursorCol = (ushort)Math.Max(0, clampedX - 1);
        State.CursorRow = (ushort)Math.Max(0, clampedY - 1);

        // CSI {y} ; {x} H
        Span<byte> buf = stackalloc byte[24];
        buf[0] = 0x1b;
        buf[1] = (byte)'[';
        int pos = 2;
        Utf8Formatter.TryFormat(clampedY, buf[pos..], out int w1);
        pos += w1;
        buf[pos++] = (byte)';';
        Utf8Formatter.TryFormat(clampedX, buf[pos..], out int w2);
        pos += w2;
        buf[pos++] = (byte)'H';
        writer.Write(buf[..pos]);
    }

    /// <summary>
    /// Set cursor style and blink state. Writes the DECSCUSR sequence.
    /// </summary>
    public void SetCursorStyle(ITerminalWriter writer, CursorStyle style, bool blinking)
    {
        State.CursorStyle = style;
        State.CursorBlinking = blinking;

        byte decscusr = style switch
        {
            CursorStyle.Default => 0,
            CursorStyle.BlinkingBlock or CursorStyle.SteadyBlock =>
                blinking ? (byte)1 : (byte)2,
            CursorStyle.BlinkingUnderline or CursorStyle.SteadyUnderline =>
                blinking ? (byte)3 : (byte)4,
            CursorStyle.BlinkingBar or CursorStyle.SteadyBar =>
                blinking ? (byte)5 : (byte)6,
            _ => 0,
        };

        // CSI {n} SP q
        Span<byte> buf = stackalloc byte[8];
        buf[0] = 0x1b;
        buf[1] = (byte)'[';
        int pos = 2;
        Utf8Formatter.TryFormat(decscusr, buf[pos..], out int written);
        pos += written;
        buf[pos++] = (byte)' ';
        buf[pos++] = (byte)'q';
        writer.Write(buf[..pos]);
    }

    /// <summary>Write OSC 12 cursor color sequence.</summary>
    public void SetCursorColor(ITerminalWriter writer, Rgba color)
    {
        State.CursorColor = color;
        var (r, g, b, _) = color.ToInts();

        // OSC 12 ; #{rr}{gg}{bb} BEL
        Span<byte> buf = stackalloc byte[20];
        buf[0] = 0x1b;
        buf[1] = (byte)']';
        buf[2] = (byte)'1';
        buf[3] = (byte)'2';
        buf[4] = (byte)';';
        buf[5] = (byte)'#';
        HexByte(r, buf[6..]);
        HexByte(g, buf[8..]);
        HexByte(b, buf[10..]);
        buf[12] = 0x07; // BEL
        writer.Write(buf[..13]);
    }

    public void SetCursorVisible(ITerminalWriter writer, bool visible)
    {
        State.CursorVisible = visible;
        writer.Write(visible ? AnsiSequences.ShowCursor : AnsiSequences.HideCursor);
    }

    // ── Terminal title ───────────────────────────────────────────────

    /// <summary>Set terminal window title via OSC 0.</summary>
    public void SetTerminalTitle(ITerminalWriter writer, string title)
    {
        // OSC 0 ; {title} BEL
        int maxLen = 5 + Encoding.UTF8.GetMaxByteCount(title.Length) + 1;
        byte[]? rented = null;
        Span<byte> buf = maxLen <= 256
            ? stackalloc byte[256]
            : (rented = ArrayPool<byte>.Shared.Rent(maxLen));
        try
        {
            buf[0] = 0x1b;
            buf[1] = (byte)']';
            buf[2] = (byte)'0';
            buf[3] = (byte)';';
            int titleBytes = Encoding.UTF8.GetBytes(title.AsSpan(), buf[4..]);
            buf[4 + titleBytes] = 0x07;
            writer.Write(buf[..(5 + titleBytes)]);
        }
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    // ── Color scheme ─────────────────────────────────────────────────

    public void SetColorSchemeUpdates(ITerminalWriter writer, bool enable)
    {
        writer.Write(enable ? AnsiSequences.EnableColorSchemeUpdates : AnsiSequences.DisableColorSchemeUpdates);
        State.ColorSchemeUpdates = enable;
    }

    /// <summary>Set terminal background color via OSC 11.</summary>
    public void SetTerminalBackgroundColor(ITerminalWriter writer, Rgba color)
    {
        var (r, g, b, _) = color.ToInts();

        // OSC 11 ; rgb:{rr}/{gg}/{bb} BEL
        Span<byte> buf = stackalloc byte[24];
        buf[0] = 0x1b;
        buf[1] = (byte)']';
        buf[2] = (byte)'1';
        buf[3] = (byte)'1';
        buf[4] = (byte)';';
        buf[5] = (byte)'r';
        buf[6] = (byte)'g';
        buf[7] = (byte)'b';
        buf[8] = (byte)':';
        HexByte(r, buf[9..]);
        buf[11] = (byte)'/';
        HexByte(g, buf[12..]);
        buf[14] = (byte)'/';
        HexByte(b, buf[15..]);
        buf[17] = 0x07;
        writer.Write(buf[..18]);
    }

    // ── Clipboard (OSC 52) ───────────────────────────────────────────

    /// <summary>
    /// Write OSC 52 clipboard sequence, with tmux/screen passthrough when needed.
    /// <paramref name="data"/> is the raw Base64-encoded payload.
    /// </summary>
    public void SetClipboard(ITerminalWriter writer, ClipboardTarget target, ReadOnlySpan<byte> data)
    {
        if (!Capabilities.Osc52) return;

        byte targetChar = target switch
        {
            ClipboardTarget.Clipboard => (byte)'c',
            ClipboardTarget.Primary => (byte)'p',
            ClipboardTarget.Secondary => (byte)'s',
            ClipboardTarget.Query => (byte)'q',
            _ => (byte)'c',
        };

        // Build the inner OSC 52 sequence: ESC ] 52 ; {t} ; {data} ESC \
        int innerLen = 5 + 1 + 1 + data.Length + 2; // ESC]52; + target + ; + data + ESC\
        byte[]? rented = null;
        Span<byte> osc52 = innerLen <= 1024
            ? stackalloc byte[1024]
            : (rented = ArrayPool<byte>.Shared.Rent(innerLen));
        try
        {
            osc52[0] = 0x1b;
            osc52[1] = (byte)']';
            osc52[2] = (byte)'5';
            osc52[3] = (byte)'2';
            osc52[4] = (byte)';';
            osc52[5] = targetChar;
            osc52[6] = (byte)';';
            data.CopyTo(osc52[7..]);
            int pos = 7 + data.Length;
            osc52[pos++] = 0x1b;
            osc52[pos++] = (byte)'\\';
            var inner = osc52[..pos];

            bool isTmux = _inTmux || IsXtversionTmux();

            if (isTmux)
            {
                WriteTmuxWrapped(writer, inner);
            }
            else if (_options.Remote)
            {
                writer.Write(inner);
            }
            else
            {
                // Check for GNU Screen (STY env var)
                string? sty = GetEnvVar("STY");
                if (sty is not null)
                    WriteScreenWrapped(writer, inner);
                else
                    writer.Write(inner);
            }
        }
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    // ── Mouse pointer ────────────────────────────────────────────────

    /// <summary>Set mouse pointer shape via OSC 22.</summary>
    public void SetMousePointerStyle(ITerminalWriter writer, MousePointerStyle style)
    {
        State.MousePointer = style;

        ReadOnlySpan<byte> name = style switch
        {
            MousePointerStyle.Default => "default"u8,
            MousePointerStyle.Pointer => "pointer"u8,
            MousePointerStyle.Text => "text"u8,
            MousePointerStyle.Crosshair => "crosshair"u8,
            MousePointerStyle.Move => "move"u8,
            MousePointerStyle.NotAllowed => "not-allowed"u8,
            _ => "default"u8,
        };

        // OSC 22 ; {name} BEL
        Span<byte> buf = stackalloc byte[32];
        buf[0] = 0x1b;
        buf[1] = (byte)']';
        buf[2] = (byte)'2';
        buf[3] = (byte)'2';
        buf[4] = (byte)';';
        name.CopyTo(buf[5..]);
        buf[5 + name.Length] = 0x07;
        writer.Write(buf[..(6 + name.Length)]);
    }

    // ── Hyperlinks (OSC 8) ───────────────────────────────────────────

    /// <summary>Begin a hyperlink region via OSC 8.</summary>
    public void BeginHyperlink(ITerminalWriter writer, string url, string? id = null)
    {
        // OSC 8 ; {params} ; {url} BEL
        string paramStr = id is not null ? $"id={id}" : "";
        int maxLen = 5 + Encoding.UTF8.GetMaxByteCount(paramStr.Length)
            + 1 + Encoding.UTF8.GetMaxByteCount(url.Length) + 1;
        byte[]? rented = null;
        Span<byte> buf = maxLen <= 512
            ? stackalloc byte[512]
            : (rented = ArrayPool<byte>.Shared.Rent(maxLen));
        try
        {
            buf[0] = 0x1b;
            buf[1] = (byte)']';
            buf[2] = (byte)'8';
            buf[3] = (byte)';';
            int pos = 4;
            pos += Encoding.UTF8.GetBytes(paramStr.AsSpan(), buf[pos..]);
            buf[pos++] = (byte)';';
            pos += Encoding.UTF8.GetBytes(url.AsSpan(), buf[pos..]);
            buf[pos++] = 0x07;
            writer.Write(buf[..pos]);
        }
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>End a hyperlink region via OSC 8.</summary>
    public void EndHyperlink(ITerminalWriter writer)
    {
        writer.Write(AnsiSequences.HyperlinkEnd);
    }

    // ── Capability queries ───────────────────────────────────────────

    /// <summary>
    /// Send initial capability query sequences to the terminal.
    /// Matches the Zig <c>queryTerminalSend</c> method.
    /// </summary>
    public void QueryTerminalSend(ITerminalWriter writer)
    {
        CheckEnvironmentOverrides();
        _graphicsQueryPending = !_skipGraphicsQuery;
        _capabilityQueriesPending = false;
        _themeQueriesPending = false;

        SetColorSchemeUpdates(writer, true);
        writer.Write(AnsiSequences.ColorSchemeRequest);

        if (_inTmux)
        {
            WriteTmuxWrapped(writer, AnsiSequences.OscThemeQueries);
        }
        else
        {
            writer.Write(AnsiSequences.OscThemeQueries);
            _themeQueriesPending = true;
        }
        State.ThemeQueriesSent = true;

        // Xtversion + hide cursor + save cursor state
        writer.Write(AnsiSequences.Xtversion);
        writer.Write(AnsiSequences.HideCursor);
        writer.Write(AnsiSequences.SaveCursorState);

        // Capability queries (DECRQM batch)
        if (_inTmux)
        {
            WriteCapabilityQueriesTmux(writer);
        }
        else
        {
            WriteCapabilityQueries(writer);
            _capabilityQueriesPending = true;
        }

        if (!_skipExplicitWidthQuery)
        {
            writer.Write(AnsiSequences.Home);
            writer.Write(AnsiSequences.ExplicitWidthQuery);
            writer.Write(AnsiSequences.CursorPositionRequest);
            writer.Write(AnsiSequences.Home);
            writer.Write(AnsiSequences.ScaledTextQuery);
            writer.Write(AnsiSequences.CursorPositionRequest);
        }

        writer.Write(AnsiSequences.RestoreCursorState);
    }

    /// <summary>
    /// Send pending queries that were deferred until xtversion was received.
    /// Returns true if any queries were sent.
    /// </summary>
    public bool SendPendingQueries(ITerminalWriter writer)
    {
        bool sent = false;
        bool isTmux = _inTmux || IsXtversionTmux();

        if (_capabilityQueriesPending)
        {
            if (TermInfo.FromXtversion && isTmux)
            {
                WriteCapabilityQueriesTmux(writer);
                sent = true;
            }
            _capabilityQueriesPending = false;
        }

        if (_graphicsQueryPending && !_skipGraphicsQuery)
        {
            if (isTmux)
                WriteTmuxWrapped(writer, AnsiSequences.KittyGraphicsQuery);
            else
                writer.Write(AnsiSequences.KittyGraphicsQuery);
            _graphicsQueryPending = false;
            sent = true;
        }

        if (_themeQueriesPending)
        {
            if (TermInfo.FromXtversion && isTmux)
            {
                WriteTmuxWrapped(writer, AnsiSequences.OscThemeQueries);
                sent = true;
            }
            _themeQueriesPending = false;
        }

        return sent;
    }

    /// <summary>
    /// Enable features that were detected via capability queries.
    /// Matches the Zig <c>enableDetectedFeatures</c> method.
    /// </summary>
    public void EnableDetectedFeatures(ITerminalWriter writer, bool useKittyKeyboard)
    {
        if (OperatingSystem.IsWindows())
        {
            Capabilities.Rgb = true;
            Capabilities.BracketedPaste = true;
        }

        CheckEnvironmentOverrides();

        if (!State.ModifyOtherKeys && !State.KittyKeyboard)
            SetModifyOtherKeys(writer, true);

        if (Capabilities.KittyKeyboard && useKittyKeyboard)
        {
            if (State.ModifyOtherKeys)
                SetModifyOtherKeys(writer, false);
            SetKittyKeyboard(writer, true, _options.KittyKeyboardFlags);
        }

        if (Capabilities.Unicode == WidthMethod.Unicode && !Capabilities.ExplicitWidth)
            writer.Write(AnsiSequences.UnicodeSet);

        if (Capabilities.BracketedPaste)
            SetBracketedPaste(writer, true);

        if (Capabilities.FocusTracking)
            SetFocusTracking(writer, true);

        if (!State.ColorSchemeUpdates)
        {
            SetColorSchemeUpdates(writer, true);
            writer.Write(AnsiSequences.ColorSchemeRequest);
        }

        bool isTmux = _inTmux || IsXtversionTmux();
        if (!State.ThemeQueriesSent)
        {
            if (isTmux)
            {
                WriteTmuxWrapped(writer, AnsiSequences.OscThemeQueries);
                _themeQueriesPending = false;
            }
            else
            {
                writer.Write(AnsiSequences.OscThemeQueries);
                _themeQueriesPending = true;
            }
            State.ThemeQueriesSent = true;
        }
    }

    /// <summary>
    /// Re-send all currently-active terminal mode escape sequences unconditionally.
    /// Call in response to a focus-in event to restore modes that the terminal
    /// emulator may have silently disabled.
    /// </summary>
    public void RestoreTerminalModes(ITerminalWriter writer)
    {
        if (State.Mouse)
        {
            if (!State.MouseMovement)
                writer.Write(AnsiSequences.DisableAnyEventTracking);
            writer.Write(AnsiSequences.EnableMouseTracking);
            writer.Write(AnsiSequences.EnableButtonEventTracking);
            if (State.MouseMovement)
                writer.Write(AnsiSequences.EnableAnyEventTracking);
            writer.Write(AnsiSequences.EnableSgrMouseMode);
        }

        if (State.FocusTracking)
            writer.Write(AnsiSequences.EnableFocusTracking);

        if (State.BracketedPaste)
            writer.Write(AnsiSequences.EnableBracketedPaste);

        // Pop stale entry then re-push kitty keyboard to avoid stack growth
        if (State.KittyKeyboard)
        {
            writer.Write(AnsiSequences.CsiUPop);
            Span<byte> buf = stackalloc byte[16];
            buf[0] = 0x1b;
            buf[1] = (byte)'[';
            buf[2] = (byte)'>';
            int pos = 3;
            Utf8Formatter.TryFormat(State.KittyKeyboardFlags, buf[pos..], out int written);
            pos += written;
            buf[pos++] = (byte)'u';
            writer.Write(buf[..pos]);
        }

        if (State.ModifyOtherKeys)
            writer.Write(AnsiSequences.ModifyOtherKeysSet);
    }

    /// <summary>
    /// Parse a raw capability response from the terminal and update
    /// <see cref="Capabilities"/> accordingly.
    /// </summary>
    public void ProcessCapabilityResponse(ReadOnlySpan<char> response)
    {
        // DECRPM responses
        if (response.Contains("1016;2$y", StringComparison.Ordinal))
            Capabilities.SgrPixels = true;
        if (response.Contains("2027;2$y", StringComparison.Ordinal))
            Capabilities.Unicode = WidthMethod.Unicode;
        if (response.Contains("2031;1$y", StringComparison.Ordinal) ||
            response.Contains("2031;2$y", StringComparison.Ordinal))
            Capabilities.ColorSchemeUpdates = true;
        if (response.Contains("1004;1$y", StringComparison.Ordinal) ||
            response.Contains("1004;2$y", StringComparison.Ordinal))
            Capabilities.FocusTracking = true;
        if (response.Contains("2026;1$y", StringComparison.Ordinal) ||
            response.Contains("2026;2$y", StringComparison.Ordinal))
            Capabilities.Sync = true;
        if (response.Contains("2004;1$y", StringComparison.Ordinal) ||
            response.Contains("2004;2$y", StringComparison.Ordinal))
            Capabilities.BracketedPaste = true;

        // Explicit width detection via CPR
        int cprIdx = response.IndexOf("\x1b[1;", StringComparison.Ordinal);
        if (cprIdx >= 0)
        {
            var after = response[(cprIdx + 4)..];
            int end = 0;
            while (end < after.Length && after[end] >= '0' && after[end] <= '9') end++;
            if (end > 0 && end < after.Length && after[end] == 'R')
            {
                if (ushort.TryParse(after[..end], out ushort col))
                {
                    if (col >= 2) Capabilities.ExplicitWidth = true;
                    if (col >= 3) Capabilities.ScaledText = true;
                }
            }
        }

        // Parse xtversion response: ESC P > | {name} ESC \
        int xtIdx = response.IndexOf("\x1bP>|", StringComparison.Ordinal);
        if (xtIdx >= 0)
        {
            var start = response[(xtIdx + 4)..];
            int stEnd = start.IndexOf("\x1b\\", StringComparison.Ordinal);
            if (stEnd >= 0)
                ParseXtversion(start[..stEnd]);
        }

        // Terminal-specific capability upgrades
        if (response.Contains("kitty", StringComparison.OrdinalIgnoreCase))
        {
            Capabilities.KittyKeyboard = true;
            Capabilities.KittyGraphics = true;
            Capabilities.Unicode = WidthMethod.Unicode;
            Capabilities.Rgb = true;
            Capabilities.Sixel = true;
            Capabilities.BracketedPaste = true;
            Capabilities.Hyperlinks = true;
        }

        // Kitty keyboard protocol detection via CSI ? N u
        if (response.Contains("\x1b[?", StringComparison.Ordinal) &&
            response.Contains("u", StringComparison.Ordinal))
        {
            for (int i = 0; i + 4 < response.Length; i++)
            {
                if (response[i] == '\x1b' && response[i + 1] == '[' && response[i + 2] == '?')
                {
                    int numEnd = i + 3;
                    while (numEnd < response.Length && response[numEnd] >= '0' && response[numEnd] <= '9')
                        numEnd++;
                    if (numEnd > i + 3 && numEnd < response.Length && response[numEnd] == 'u')
                    {
                        Capabilities.KittyKeyboard = true;
                        break;
                    }
                }
            }
        }

        if (response.Contains("tmux", StringComparison.OrdinalIgnoreCase))
        {
            Capabilities.Unicode = WidthMethod.Wcwidth;
            Capabilities.ExplicitCursorPositioning = true;
        }

        if (response.Contains("alacritty", StringComparison.OrdinalIgnoreCase))
            Capabilities.ExplicitCursorPositioning = true;

        // Sixel detection via DA1 response (capability 4)
        int daEnd = response.IndexOf(";c", StringComparison.Ordinal);
        if (daEnd >= 4)
        {
            int daStart = daEnd;
            while (daStart > 0 && response[daStart] != '\x1b') daStart--;
            var daResponse = response[daStart..(daEnd + 2)];
            if (daResponse.StartsWith("\x1b[?", StringComparison.Ordinal))
            {
                if (daResponse.Contains("4;", StringComparison.Ordinal) ||
                    daResponse.Contains(";4;", StringComparison.Ordinal) ||
                    daResponse.Contains(";4c", StringComparison.Ordinal))
                    Capabilities.Sixel = true;
            }
        }

        // Kitty graphics response
        if (response.Contains("\x1b_G", StringComparison.Ordinal) &&
            response.Contains("i=31337", StringComparison.Ordinal))
            Capabilities.KittyGraphics = true;

        if (!Capabilities.Osc52 && IsOsc52Term(response))
            Capabilities.Osc52 = true;

        if (!Capabilities.Hyperlinks && IsHyperlinkTerm(response))
            Capabilities.Hyperlinks = true;
    }

    // ── Environment ──────────────────────────────────────────────────

    /// <summary>Set a host environment variable override used during capability detection.</summary>
    public void SetHostEnvVar(string key, string value)
    {
        _hostEnvMap ??= new(StringComparer.Ordinal);
        _hostEnvMap[key] = value;
        CheckEnvironmentOverrides();
    }

    /// <summary>
    /// Check environment variables and update capabilities/flags accordingly.
    /// Matches the Zig <c>checkEnvironmentOverrides</c>.
    /// </summary>
    public void CheckEnvironmentOverrides()
    {
        _inTmux = false;
        _skipGraphicsQuery = false;
        _skipExplicitWidthQuery = false;

        Capabilities.BracketedPaste = true;

        if (Capabilities.Rgb)
            Capabilities.Hyperlinks = true;

        if (_options.Remote) return;

        if (!TermInfo.FromXtversion)
        {
            string? tmux = GetEnvVar("TMUX");
            if (tmux is not null)
            {
                _inTmux = true;
                Capabilities.Unicode = WidthMethod.Wcwidth;
                Capabilities.ExplicitCursorPositioning = true;
            }
            else
            {
                string? term = GetEnvVar("TERM");
                if (term is not null)
                {
                    if (term.StartsWith("tmux", StringComparison.Ordinal))
                    {
                        _inTmux = true;
                        Capabilities.Unicode = WidthMethod.Wcwidth;
                        Capabilities.ExplicitCursorPositioning = true;
                    }
                    else if (term.StartsWith("screen", StringComparison.Ordinal))
                    {
                        _skipGraphicsQuery = true;
                        Capabilities.Unicode = WidthMethod.Wcwidth;
                        Capabilities.ExplicitCursorPositioning = true;
                    }
                    if (term.Contains("alacritty", StringComparison.OrdinalIgnoreCase))
                        Capabilities.ExplicitCursorPositioning = true;
                }
            }
        }

        string? opentui_graphics = GetEnvVar("OPENTUI_GRAPHICS");
        if (opentui_graphics is not null)
        {
            if (opentui_graphics is "false" or "0")
                _skipGraphicsQuery = true;
            else if (opentui_graphics is "true" or "1")
                _skipGraphicsQuery = false;
        }

        if (!TermInfo.FromXtversion)
        {
            string? termProgram = GetEnvVar("TERM_PROGRAM");
            if (termProgram is not null)
            {
                if (TermInfo.Name.Length == 0)
                    TermInfo.Name = termProgram;

                string? termProgramVersion = GetEnvVar("TERM_PROGRAM_VERSION");
                if (termProgramVersion is not null && TermInfo.Version.Length == 0)
                    TermInfo.Version = termProgramVersion;

                if (termProgram == "vscode")
                {
                    Capabilities.KittyKeyboard = false;
                    Capabilities.KittyGraphics = false;
                    Capabilities.Unicode = WidthMethod.Unicode;
                }
                else if (termProgram == "Apple_Terminal")
                {
                    Capabilities.Unicode = WidthMethod.Wcwidth;
                }
                else if (termProgram == "Alacritty")
                {
                    Capabilities.ExplicitCursorPositioning = true;
                }
            }

            if (GetEnvVar("ALACRITTY_SOCKET") is not null || GetEnvVar("ALACRITTY_LOG") is not null)
            {
                Capabilities.ExplicitCursorPositioning = true;
                if (TermInfo.Name.Length == 0)
                    TermInfo.Name = "Alacritty";
            }
        }

        string? colorterm = GetEnvVar("COLORTERM");
        if (colorterm is "truecolor" or "24bit")
            Capabilities.Rgb = true;

        if (!TermInfo.FromXtversion)
        {
            if (GetEnvVar("TERMUX_VERSION") is not null)
                Capabilities.Unicode = WidthMethod.Wcwidth;

            if (GetEnvVar("VHS_RECORD") is not null)
            {
                Capabilities.Unicode = WidthMethod.Wcwidth;
                Capabilities.KittyKeyboard = false;
                Capabilities.KittyGraphics = false;
            }
        }

        if (GetEnvVar("OPENTUI_FORCE_WCWIDTH") is not null)
            Capabilities.Unicode = WidthMethod.Wcwidth;
        if (GetEnvVar("OPENTUI_FORCE_UNICODE") is not null)
            Capabilities.Unicode = WidthMethod.Unicode;
        if (GetEnvVar("OPENTUI_FORCE_NOZWJ") is not null)
            Capabilities.Unicode = WidthMethod.NoZwj;

        string? explicitWidth = GetEnvVar("OPENTUI_FORCE_EXPLICIT_WIDTH");
        if (explicitWidth is not null)
        {
            if (explicitWidth is "true" or "1")
                Capabilities.ExplicitWidth = true;
            else if (explicitWidth is "false" or "0")
            {
                Capabilities.ExplicitWidth = false;
                _skipExplicitWidthQuery = true;
            }
        }

        if (!Capabilities.Hyperlinks && TermInfo.FromXtversion)
        {
            if (IsHyperlinkTerm(TermInfo.Name.AsSpan()))
                Capabilities.Hyperlinks = true;
        }

        if (!Capabilities.Hyperlinks && !TermInfo.FromXtversion)
        {
            string? term2 = GetEnvVar("TERM");
            if (term2 is not null && IsHyperlinkTerm(term2.AsSpan()))
                Capabilities.Hyperlinks = true;
        }

        if (!Capabilities.Hyperlinks && !TermInfo.FromXtversion)
        {
            bool isWsl = GetEnvVar("WSL_DISTRO_NAME") is not null || GetEnvVar("WSL_INTEROP") is not null;
            bool hasWtSession = GetEnvVar("WT_SESSION") is not null;
            if (isWsl && hasWtSession)
            {
                string? term3 = GetEnvVar("TERM");
                if (term3 is not null && term3.StartsWith("xterm", StringComparison.Ordinal))
                    Capabilities.Hyperlinks = true;
            }
        }

        if (!Capabilities.Osc52 && !TermInfo.FromXtversion)
        {
            if (GetEnvVar("WT_SESSION") is not null)
                Capabilities.Osc52 = true;

            if (!Capabilities.Osc52 && (_inTmux || GetEnvVar("STY") is not null))
                Capabilities.Osc52 = true;

            if (!Capabilities.Osc52)
            {
                string? prog = GetEnvVar("TERM_PROGRAM");
                if (prog is not null && IsOsc52Term(prog.AsSpan()))
                    Capabilities.Osc52 = true;
            }

            if (!Capabilities.Osc52)
            {
                string? term4 = GetEnvVar("TERM");
                if (term4 is not null)
                {
                    if (IsOsc52Term(term4.AsSpan()) ||
                        term4.Contains("256color", StringComparison.OrdinalIgnoreCase) ||
                        term4.Contains("xterm", StringComparison.OrdinalIgnoreCase))
                        Capabilities.Osc52 = true;
                }
            }
        }
    }

    /// <summary>Set the kitty keyboard flags for future enable calls.</summary>
    public void SetKittyKeyboardFlags(byte flags)
    {
        // Stored via a mutable copy to match Zig's opts mutation.
        // _options is sealed, so we track this separately.
        _kittyKeyboardFlagsOverride = flags;
    }

    // We keep an override so SetKittyKeyboardFlags works without mutating the init-only TerminalOptions.
    private byte? _kittyKeyboardFlagsOverride;
    private byte EffectiveKittyKeyboardFlags => _kittyKeyboardFlagsOverride ?? _options.KittyKeyboardFlags;

    /// <summary>Whether the xtversion response identified tmux.</summary>
    public bool IsXtversionTmux() =>
        TermInfo.FromXtversion &&
        TermInfo.Name.Equals("tmux", StringComparison.OrdinalIgnoreCase);

    public void Dispose()
    {
        _hostEnvMap = null;
    }

    // ── Private helpers ──────────────────────────────────────────────

    /// <summary>Get an environment variable, checking host overrides first.</summary>
    private string? GetEnvVar(string key)
    {
        if (_hostEnvMap is not null && _hostEnvMap.TryGetValue(key, out var val))
            return val;
        return Environment.GetEnvironmentVariable(key);
    }

    /// <summary>Write the DECRQM capability query batch (unwrapped).</summary>
    private static void WriteCapabilityQueries(ITerminalWriter writer)
    {
        writer.Write(AnsiSequences.DecrqmSgrPixels);
        writer.Write(AnsiSequences.DecrqmUnicode);
        writer.Write(AnsiSequences.DecrqmColorScheme);
        writer.Write(AnsiSequences.DecrqmFocus);
        writer.Write(AnsiSequences.DecrqmBracketedPaste);
        writer.Write(AnsiSequences.DecrqmSync);
        writer.Write(AnsiSequences.CsiUQuery);
    }

    /// <summary>Write the DECRQM capability query batch wrapped for tmux DCS passthrough.</summary>
    private static void WriteCapabilityQueriesTmux(ITerminalWriter writer)
    {
        // Base queries (without CsiUQuery) wrapped for tmux, then CsiUQuery unwrapped
        Span<byte> baseQueries = stackalloc byte[256];
        int pos = 0;
        AnsiSequences.DecrqmSgrPixels.CopyTo(baseQueries[pos..]);
        pos += AnsiSequences.DecrqmSgrPixels.Length;
        AnsiSequences.DecrqmUnicode.CopyTo(baseQueries[pos..]);
        pos += AnsiSequences.DecrqmUnicode.Length;
        AnsiSequences.DecrqmColorScheme.CopyTo(baseQueries[pos..]);
        pos += AnsiSequences.DecrqmColorScheme.Length;
        AnsiSequences.DecrqmFocus.CopyTo(baseQueries[pos..]);
        pos += AnsiSequences.DecrqmFocus.Length;
        AnsiSequences.DecrqmBracketedPaste.CopyTo(baseQueries[pos..]);
        pos += AnsiSequences.DecrqmBracketedPaste.Length;
        AnsiSequences.DecrqmSync.CopyTo(baseQueries[pos..]);
        pos += AnsiSequences.DecrqmSync.Length;

        WriteTmuxWrapped(writer, baseQueries[..pos]);
        writer.Write(AnsiSequences.CsiUQuery);
    }

    /// <summary>
    /// Wrap a byte sequence for tmux DCS passthrough (doubling ESC bytes).
    /// </summary>
    private static void WriteTmuxWrapped(ITerminalWriter writer, ReadOnlySpan<byte> data)
    {
        writer.Write(AnsiSequences.TmuxDcsStart);

        // Double every ESC (0x1b) byte in the payload
        int lastStart = 0;
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] == 0x1b)
            {
                if (i > lastStart)
                    writer.Write(data[lastStart..i]);
                writer.Write("\x1b\x1b"u8);
                lastStart = i + 1;
            }
        }
        if (lastStart < data.Length)
            writer.Write(data[lastStart..]);

        writer.Write(AnsiSequences.TmuxDcsEnd);
    }

    /// <summary>
    /// Wrap a byte sequence for GNU Screen DCS passthrough (doubling ESC bytes).
    /// </summary>
    private static void WriteScreenWrapped(ITerminalWriter writer, ReadOnlySpan<byte> data)
    {
        writer.Write(AnsiSequences.ScreenDcsStart);

        int lastStart = 0;
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] == 0x1b)
            {
                if (i > lastStart)
                    writer.Write(data[lastStart..i]);
                writer.Write("\x1b\x1b"u8);
                lastStart = i + 1;
            }
        }
        if (lastStart < data.Length)
            writer.Write(data[lastStart..]);

        writer.Write(AnsiSequences.ScreenDcsEnd);
    }

    /// <summary>Parse xtversion string like "kitty(0.40.1)" or "ghostty 1.1.3".</summary>
    private void ParseXtversion(ReadOnlySpan<char> termStr)
    {
        if (termStr.IsEmpty) return;

        int paren = termStr.IndexOf('(');
        if (paren >= 0)
        {
            TermInfo.Name = termStr[..paren].ToString();
            int closeParen = termStr[paren..].IndexOf(')');
            if (closeParen >= 0)
                TermInfo.Version = termStr[(paren + 1)..(paren + closeParen)].ToString();
        }
        else
        {
            int space = termStr.IndexOf(' ');
            if (space >= 0)
            {
                TermInfo.Name = termStr[..space].ToString();
                TermInfo.Version = termStr[(space + 1)..].ToString();
            }
            else
            {
                TermInfo.Name = termStr.ToString();
                TermInfo.Version = string.Empty;
            }
        }

        TermInfo.FromXtversion = true;
    }

    private static bool IsOsc52Term(ReadOnlySpan<char> value) =>
        value.Contains("iterm", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("kitty", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("alacritty", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("wezterm", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("contour", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("foot", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("rio", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("ghostty", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("tmux", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("screen", StringComparison.OrdinalIgnoreCase);

    private static bool IsHyperlinkTerm(ReadOnlySpan<char> value) =>
        value.Contains("ghostty", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("kitty", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("wezterm", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("alacritty", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("iterm", StringComparison.OrdinalIgnoreCase);

    /// <summary>Write two lowercase hex digits for a byte value into <paramref name="dest"/>.</summary>
    private static void HexByte(byte value, Span<byte> dest)
    {
        static byte HexNibble(int n) => (byte)(n < 10 ? '0' + n : 'a' + n - 10);
        dest[0] = HexNibble(value >> 4);
        dest[1] = HexNibble(value & 0xF);
    }
}
