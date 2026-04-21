using System.Text;
using OpenTui.Core.Managed;

namespace OpenTui.Core;

/// <summary>
/// Façade over <see cref="ManagedRenderer"/> (double-buffered diff rendering)
/// and <see cref="ManagedTerminal"/> (terminal I/O and capability management).
/// Preserves the same public API surface that callers (e.g. <see cref="CliRenderer"/>) expect.
/// </summary>
public sealed class NativeRenderer : IDisposable
{
    internal ManagedRenderer _managedRenderer;
    internal ManagedTerminal _managedTerminal;
    private readonly ITerminalWriter _writer;
    private bool _disposed;

    // ManagedRenderer uses this value to mark the current buffer so every cell diffs as changed
    // on the first frame. Re-used here for force-full-render support.
    private const uint ClearChar = 0x0A00;

    private NativeRenderer(ManagedRenderer managedRenderer, ManagedTerminal managedTerminal, ITerminalWriter writer)
    {
        _managedRenderer = managedRenderer;
        _managedTerminal = managedTerminal;
        _writer = writer;
    }

    /// <summary>Creates a new renderer with the specified terminal dimensions.</summary>
    public static NativeRenderer Create(uint cols, uint rows, bool testing = false, bool remote = false)
    {
        var managedRenderer = ManagedRenderer.Create(cols, rows, testing: testing, remote: remote);
        var managedTerminal = new ManagedTerminal(new TerminalOptions { Remote = remote });
        ITerminalWriter writer = testing ? NullTerminalWriter.Instance : new StdoutTerminalWriter();
        return new NativeRenderer(managedRenderer, managedTerminal, writer);
    }

    #region Terminal writers

    private sealed class NullTerminalWriter : ITerminalWriter
    {
        public static readonly NullTerminalWriter Instance = new();
        public void Write(ReadOnlySpan<byte> data) { }
        public void Flush() { }
    }

    private sealed class StdoutTerminalWriter : ITerminalWriter
    {
        private readonly Stream _stdout = Console.OpenStandardOutput();
        public void Write(ReadOnlySpan<byte> data) => _stdout.Write(data);
        public void Flush() => _stdout.Flush();
    }

    #endregion

    #region Environment

    /// <summary>Sets a terminal environment variable on the renderer.</summary>
    public bool SetTerminalEnvVar(string key, string value)
    {
        _managedTerminal.SetHostEnvVar(key, value);
        return true;
    }

    /// <summary>
    /// Forwards terminal-related environment variables so the renderer
    /// can detect capabilities (Unicode support, color depth, etc.).
    /// Call after construction, before <see cref="SetupTerminal"/>.
    /// </summary>
    public void ForwardEnvironment()
    {
        foreach (var key in ForwardedEnvKeys)
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (value is not null)
                _managedTerminal.SetHostEnvVar(key, value);
        }
        _managedTerminal.CheckEnvironmentOverrides();
    }

    private static readonly string[] ForwardedEnvKeys =
    [
        "TMUX", "TERM", "OPENTUI_GRAPHICS", "TERM_PROGRAM", "TERM_PROGRAM_VERSION",
        "ALACRITTY_SOCKET", "ALACRITTY_LOG", "COLORTERM", "TERMUX_VERSION",
        "OPENTUI_FORCE_WCWIDTH", "OPENTUI_FORCE_UNICODE", "OPENTUI_FORCE_NOZWJ",
        "OPENTUI_FORCE_EXPLICIT_WIDTH", "WT_SESSION", "STY", "WSL_DISTRO_NAME", "WSL_INTEROP",
    ];

    #endregion

    #region Settings

    /// <summary>Enables or disables threaded rendering (no-op in managed mode).</summary>
    public void SetUseThread(bool useThread) { }

    /// <summary>Sets the renderer background color.</summary>
    public void SetBackgroundColor(Rgba color) =>
        _managedRenderer.BackgroundColor = color;

    /// <summary>Sets the vertical render offset in rows.</summary>
    public void SetRenderOffset(uint offset) =>
        _managedRenderer.RenderOffset = offset;

    /// <summary>Updates the renderer performance statistics.</summary>
    public void UpdateStats(double frameTime, uint fps, double frameCallbackTime)
    {
        _managedRenderer.Stats.LastFrameTime = frameTime;
        _managedRenderer.Stats.Fps = fps;
    }

    /// <summary>Updates the renderer memory usage statistics (no-op in managed mode).</summary>
    public void UpdateMemoryStats(uint heapUsed, uint heapTotal, uint external) { }

    #endregion

    #region Rendering

    /// <summary>Renders the current frame to the terminal.</summary>
    public void Render(bool forceFullRender = false)
    {
        if (forceFullRender)
            _managedRenderer.GetCurrentBuffer().Clear(Rgba.Black, ClearChar);
        _managedRenderer.Render(_writer);
    }

    /// <summary>Gets the next (back) buffer for double-buffered rendering.</summary>
    public OptimizedBuffer GetNextBuffer() =>
        OptimizedBuffer.WrapExisting(_managedRenderer.GetNextBuffer());

    /// <summary>Gets the current (front) buffer showing what's on screen.</summary>
    public OptimizedBuffer GetCurrentBuffer() =>
        OptimizedBuffer.WrapExisting(_managedRenderer.GetCurrentBuffer());

    #endregion

    #region Resize

    /// <summary>Resizes the renderer to new column/row dimensions.</summary>
    public void Resize(uint cols, uint rows) =>
        _managedRenderer.Resize(cols, rows);

    #endregion

    #region Cursor

    /// <summary>Sets the cursor position and visibility.</summary>
    public void SetCursorPosition(int x, int y, bool visible)
    {
        _managedRenderer.SetCursorPosition((uint)Math.Max(0, x), (uint)Math.Max(0, y));
        _managedRenderer.SetCursorVisible(visible);
    }

    /// <summary>Sets the cursor color.</summary>
    public void SetCursorColor(Rgba color) =>
        _managedRenderer.SetCursorColor(color);

    /// <summary>Gets the current cursor state from the renderer.</summary>
    public CursorState GetCursorState() =>
        _managedRenderer.GetCursorState();

    /// <summary>Sets cursor style options.</summary>
    public void SetCursorStyleOptions(CursorStyleOptions options)
    {
        if (options.Style != 255)
        {
            bool blinking = options.Blinking != 255 && options.Blinking != 0;
            _managedRenderer.SetCursorStyle((CursorStyle)options.Style, blinking);
        }
        else if (options.Blinking != 255)
        {
            // Only blinking changed — re-apply current style with new blinking value
            var state = _managedRenderer.GetCursorState();
            _managedRenderer.SetCursorStyle((CursorStyle)state.Style, options.Blinking != 0);
        }
    }

    #endregion

    #region Debug

    /// <summary>Enables or disables the debug overlay (no-op in managed mode).</summary>
    public void SetDebugOverlay(bool enabled, DebugOverlayCorner corner = DebugOverlayCorner.TopLeft) { }

    /// <summary>Clears the entire terminal screen.</summary>
    public void ClearTerminal()
    {
        _writer.Write("\x1b[2J\x1b[H"u8);
        _writer.Flush();
    }

    #endregion

    #region Terminal

    /// <summary>Sets the terminal window title.</summary>
    public void SetTerminalTitle(string title) =>
        _managedTerminal.SetTerminalTitle(_writer, title);

    /// <summary>Copies text to the system clipboard via OSC 52.</summary>
    public bool CopyToClipboard(string text, byte register = 0)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        _managedTerminal.SetClipboard(_writer, ClipboardTarget.Clipboard, bytes);
        return true;
    }

    /// <summary>Clears the system clipboard via OSC 52.</summary>
    public bool ClearClipboard(byte register = 0)
    {
        _managedTerminal.SetClipboard(_writer, ClipboardTarget.Clipboard, ReadOnlySpan<byte>.Empty);
        return true;
    }

    /// <summary>Restores the terminal to its original mode settings.</summary>
    public void RestoreTerminalModes() =>
        _managedTerminal.RestoreTerminalModes(_writer);

    /// <summary>Enables mouse tracking.</summary>
    public void EnableMouse(bool sgr = true) =>
        _managedTerminal.SetMouseMode(_writer, sgr ? MouseLevel.Motion : MouseLevel.Basic);

    /// <summary>Disables mouse tracking.</summary>
    public void DisableMouse() =>
        _managedTerminal.SetMouseMode(_writer, MouseLevel.None);

    /// <summary>Enables the Kitty keyboard protocol with the specified flags.</summary>
    public void EnableKittyKeyboard(byte flags) =>
        _managedTerminal.SetKittyKeyboard(_writer, true, flags);

    /// <summary>Disables the Kitty keyboard protocol.</summary>
    public void DisableKittyKeyboard() =>
        _managedTerminal.SetKittyKeyboard(_writer, false, 0);

    /// <summary>Sets the Kitty keyboard protocol flags.</summary>
    public void SetKittyKeyboardFlags(byte flags) =>
        _managedTerminal.SetKittyKeyboardFlags(flags);

    /// <summary>Gets the current Kitty keyboard protocol flags.</summary>
    public byte GetKittyKeyboardFlags() =>
        _managedTerminal.State.KittyKeyboardFlags;

    /// <summary>Sets up the terminal for rendering.</summary>
    public void SetupTerminal(bool alternateBuffer = true)
    {
        _managedRenderer.UseAlternateScreen = alternateBuffer;
        _managedRenderer.SetupTerminal(_writer);
    }

    /// <summary>Suspends the renderer, restoring the terminal to a normal state.</summary>
    public void Suspend() =>
        _managedRenderer.ShutdownTerminal(_writer);

    /// <summary>Resumes the renderer after a suspension.</summary>
    public void Resume() =>
        _managedRenderer.SetupTerminal(_writer);

    /// <summary>Writes raw bytes to the terminal output.</summary>
    public void WriteOut(ReadOnlySpan<byte> data)
    {
        _writer.Write(data);
        _writer.Flush();
    }

    /// <summary>Retrieves terminal capabilities.</summary>
    public TerminalCapabilities GetTerminalCapabilities()
    {
        var mc = _managedTerminal.Capabilities;
        var ti = _managedTerminal.TermInfo;
        return new TerminalCapabilities
        {
            KittyKeyboard = mc.KittyKeyboard,
            KittyGraphics = mc.KittyGraphics,
            Rgb = mc.Rgb,
            Unicode = mc.Unicode,
            SgrPixels = mc.SgrPixels,
            ColorSchemeUpdates = mc.ColorSchemeUpdates,
            ExplicitWidth = mc.ExplicitWidth,
            ScaledText = mc.ScaledText,
            Sixel = mc.Sixel,
            FocusTracking = mc.FocusTracking,
            Sync = mc.Sync,
            BracketedPaste = mc.BracketedPaste,
            Hyperlinks = mc.Hyperlinks,
            Osc52 = mc.Osc52,
            ExplicitCursorPositioning = mc.ExplicitCursorPositioning,
            TermName = ti.Name,
            TermVersion = ti.Version,
            TermFromXtversion = ti.FromXtversion,
        };
    }

    /// <summary>Processes a terminal capability response.</summary>
    public void ProcessCapabilityResponse(ReadOnlySpan<byte> data)
    {
        // ManagedTerminal expects chars (Latin-1 encoding: each byte maps 1:1 to char)
        Span<char> chars = data.Length <= 512 ? stackalloc char[data.Length] : new char[data.Length];
        for (int i = 0; i < data.Length; i++)
            chars[i] = (char)data[i];
        _managedTerminal.ProcessCapabilityResponse(chars);
    }

    /// <summary>Queries the terminal for pixel resolution (no-op in managed mode).</summary>
    public void QueryPixelResolution() { }

    /// <summary>Dumps internal buffers to a file for debugging (no-op in managed mode).</summary>
    public void DumpBuffers(long? timestamp = null) { }

    /// <summary>Dumps the stdout buffer to a file for debugging (no-op in managed mode).</summary>
    public void DumpStdoutBuffer(long? timestamp = null) { }

    /// <summary>Returns the raw ANSI bytes from the last Render() call (testing mode only).</summary>
    public string GetLastOutputForTest()
    {
        var output = _managedRenderer.LastOutputForTest;
        if (output.IsEmpty) return string.Empty;
        return Encoding.UTF8.GetString(output);
    }

    #endregion

    #region Hit Grid

    /// <summary>Adds a rectangular hit region to the hit grid.</summary>
    public void AddToHitGrid(int x, int y, uint w, uint h, uint id) =>
        _managedRenderer.AddToHitGrid(id, x, y, w, h);

    /// <summary>Clears all hit regions from the current hit grid.</summary>
    public void ClearCurrentHitGrid() =>
        _managedRenderer.ClearCurrentHitGrid();

    /// <summary>Pushes a scissor (clipping) rectangle onto the hit grid's clip stack.</summary>
    public void HitGridPushScissorRect(int x, int y, uint w, uint h) =>
        _managedRenderer.PushHitScissor(x, y, w, h);

    /// <summary>Pops the most recent scissor rectangle from the hit grid's clip stack.</summary>
    public void HitGridPopScissorRect() =>
        _managedRenderer.PopHitScissor();

    /// <summary>Clears all scissor rectangles from the hit grid's clip stack.</summary>
    public void HitGridClearScissorRects() =>
        _managedRenderer.ClearHitScissors();

    /// <summary>Adds a hit region to the current hit grid, clipped by active scissor rectangles.</summary>
    public void AddToCurrentHitGridClipped(int x, int y, uint w, uint h, uint id) =>
        _managedRenderer.AddToCurrentHitGridClipped(id, x, y, w, h);

    /// <summary>Tests whether the given coordinates hit any region.</summary>
    /// <returns>The hit region ID, or 0 if no hit.</returns>
    public uint CheckHit(uint x, uint y) =>
        _managedRenderer.CheckHit(x, y);

    /// <summary>Gets whether the hit grid has been modified since the last check.</summary>
    public bool GetHitGridDirty() =>
        _managedRenderer.GetHitGridDirty();

    /// <summary>Dumps the hit grid contents for debugging (no-op in managed mode).</summary>
    public void DumpHitGrid() { }

    #endregion

    #region Static Callbacks

    /// <summary>Sets the global log callback function pointer (no-op in managed mode).</summary>
    public static void SetLogCallback(nint callback) { }

    /// <summary>Sets the global event callback function pointer (no-op in managed mode).</summary>
    public static void SetEventCallback(nint callback) { }

    #endregion

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _managedRenderer.Dispose();
            _managedTerminal.Dispose();
        }
    }
}
