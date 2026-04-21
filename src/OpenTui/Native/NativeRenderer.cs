using System.Runtime.CompilerServices;
using CoreRenderer = OpenTui.Core.NativeRenderer;

namespace OpenTui;

/// <summary>High-level wrapper around the native OpenTUI renderer.</summary>
public sealed class NativeRenderer : IDisposable
{
    private readonly CoreRenderer _core;
    private bool _disposed;

    /// <summary>Creates a new terminal renderer with the specified dimensions.</summary>
    /// <param name="cols">Number of terminal columns.</param>
    /// <param name="rows">Number of terminal rows.</param>
    /// <param name="testing">When true, renderer operates in test mode (no terminal I/O).</param>
    /// <param name="remote">When true, renderer operates in remote mode.</param>
    public NativeRenderer(uint cols, uint rows, bool testing = false, bool remote = false)
    {
        _core = CoreRenderer.Create(cols, rows, testing: testing, remote: remote);
    }

    /// <summary>Sets whether to use threaded rendering (no-op in managed mode).</summary>
    public void SetUseThread(bool useThread) { }

    /// <summary>Sets the renderer background color.</summary>
    public void SetBackgroundColor(Rgba color) =>
        _core.SetBackgroundColor(ToCore(color));

    /// <summary>Sets the vertical render offset in rows.</summary>
    public void SetRenderOffset(uint offset) =>
        _core.SetRenderOffset(offset);

    /// <summary>Updates the renderer performance statistics (no-op in managed mode).</summary>
    public void UpdateStats(double frameTime, uint nodeCount, double layoutTime) { }

    /// <summary>Updates the renderer memory usage statistics (no-op in managed mode).</summary>
    public void UpdateMemoryStats(uint heapUsed, uint heapTotal, uint external) { }

    /// <summary>Renders the current frame to the terminal.</summary>
    public void Render(bool forceFullRender = false) =>
        _core.Render(forceFullRender);

    /// <summary>Gets the next (back) buffer for double-buffered rendering. The renderer owns this buffer.</summary>
    public NativeBuffer GetNextBuffer() =>
        new(_core.GetNextBuffer());

    /// <summary>Gets the current (front) buffer. The renderer owns this buffer.</summary>
    public NativeBuffer GetCurrentBuffer() =>
        new(_core.GetCurrentBuffer());

    /// <summary>Queries the terminal for pixel resolution (no-op in managed mode).</summary>
    public void QueryPixelResolution() { }

    /// <summary>Resizes the renderer to new column/row dimensions.</summary>
    public void Resize(uint cols, uint rows) =>
        _core.Resize(cols, rows);

    /// <summary>Sets the cursor position and visibility.</summary>
    public void SetCursorPosition(int x, int y, bool visible) =>
        _core.SetCursorPosition(x, y, visible);

    /// <summary>Sets the cursor color.</summary>
    public void SetCursorColor(Rgba color) =>
        _core.SetCursorColor(ToCore(color));

    /// <summary>Enables or disables the debug overlay (no-op in managed mode).</summary>
    public void SetDebugOverlay(bool enabled, byte corner = 0) { }

    /// <summary>Clears the entire terminal screen.</summary>
    public void ClearTerminal() =>
        _core.ClearTerminal();

    /// <summary>Sets the terminal window title.</summary>
    public void SetTitle(string title) =>
        _core.SetTerminalTitle(title);

    /// <summary>Copies data to the system clipboard via OSC 52.</summary>
    public bool CopyToClipboard(byte register, ReadOnlySpan<byte> data) =>
        _core.CopyToClipboard(System.Text.Encoding.UTF8.GetString(data), register);

    /// <summary>Clears the system clipboard via OSC 52.</summary>
    public bool ClearClipboard(byte register) =>
        _core.ClearClipboard(register);

    /// <summary>Restores the terminal to its original mode settings.</summary>
    public void RestoreTerminalModes() =>
        _core.RestoreTerminalModes();

    /// <summary>Enables mouse tracking.</summary>
    public void EnableMouse(bool sgrMode = true) =>
        _core.EnableMouse(sgrMode);

    /// <summary>Disables mouse tracking.</summary>
    public void DisableMouse() =>
        _core.DisableMouse();

    /// <summary>Enables the Kitty keyboard protocol with the specified flags.</summary>
    public void EnableKittyKeyboard(byte flags) =>
        _core.EnableKittyKeyboard(flags);

    /// <summary>Disables the Kitty keyboard protocol.</summary>
    public void DisableKittyKeyboard() =>
        _core.DisableKittyKeyboard();

    /// <summary>Gets or sets the Kitty keyboard protocol flags.</summary>
    public byte KittyKeyboardFlags
    {
        get => _core.GetKittyKeyboardFlags();
        set => _core.SetKittyKeyboardFlags(value);
    }

    /// <summary>Sets up the terminal for rendering.</summary>
    public void SetupTerminal(bool alternateScreen = true) =>
        _core.SetupTerminal(alternateScreen);

    /// <summary>Suspends the renderer, restoring the terminal to a normal state.</summary>
    public void Suspend() =>
        _core.Suspend();

    /// <summary>Resumes the renderer after a suspension.</summary>
    public void Resume() =>
        _core.Resume();

    /// <summary>Writes raw bytes to the terminal output.</summary>
    public void WriteOut(ReadOnlySpan<byte> data) =>
        _core.WriteOut(data);

    /// <summary>Sets a terminal environment variable (no-op in managed mode).</summary>
    public bool SetTerminalEnvVar(string key, string value) =>
        _core.SetTerminalEnvVar(key, value);

    // Environment variable keys the native renderer needs to detect terminal capabilities.
    private static readonly string[] ForwardedEnvKeys =
    [
        "TMUX", "TERM", "OPENTUI_GRAPHICS", "TERM_PROGRAM", "TERM_PROGRAM_VERSION",
        "ALACRITTY_SOCKET", "ALACRITTY_LOG", "COLORTERM", "TERMUX_VERSION",
        "OPENTUI_FORCE_WCWIDTH", "OPENTUI_FORCE_UNICODE", "OPENTUI_FORCE_NOZWJ",
        "OPENTUI_FORCE_EXPLICIT_WIDTH", "WT_SESSION", "STY", "WSL_DISTRO_NAME", "WSL_INTEROP",
    ];

    /// <summary>
    /// Forwards terminal-related environment variables to the native renderer
    /// so it can detect capabilities (Unicode support, color depth, etc.).
    /// Call this after construction, before <see cref="SetupTerminal"/>.
    /// </summary>
    public void ForwardEnvironment() =>
        _core.ForwardEnvironment();

    /// <summary>Dumps internal buffers to a file named with the given timestamp for debugging (no-op in managed mode).</summary>
    public void DumpBuffers(long? timestamp = null) { }

    /// <summary>Dumps the stdout buffer to a file named with the given timestamp for debugging (no-op in managed mode).</summary>
    public void DumpStdoutBuffer(long? timestamp = null) { }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _core.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static OpenTui.Core.Rgba ToCore(Rgba c) => new(c.R, c.G, c.B, c.A);
}
