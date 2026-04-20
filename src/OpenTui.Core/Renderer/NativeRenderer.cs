using System.Runtime.InteropServices;
using System.Text;
using OpenTui.Core.Native;
using OpenTui.Native;

namespace OpenTui.Core;

/// <summary>
/// Safe managed wrapper around the native OpenTUI renderer and its hit grid.
/// Wraps the P/Invoke surface from <see cref="OpenTuiNative"/> (Renderer + Hit Grid regions).
/// </summary>
public sealed class NativeRenderer : IDisposable
{
    private nint _handle;
    private bool _disposed;

    private NativeRenderer(nint handle)
    {
        _handle = handle;
    }

    /// <summary>Creates a new native renderer with the specified terminal dimensions.</summary>
    public static NativeRenderer Create(uint cols, uint rows, bool testing = false, bool remote = false)
    {
        nint ptr = OpenTuiNative.CreateRenderer(cols, rows, testing, remote);
        return new NativeRenderer(ptr);
    }

    /// <summary>Gets the raw native handle. Throws if the renderer has been disposed.</summary>
    internal nint Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _handle;
        }
    }

    #region Environment

    /// <summary>Sets a terminal environment variable on the renderer.</summary>
    public bool SetTerminalEnvVar(string key, string value)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        byte[] valueBytes = Encoding.UTF8.GetBytes(value);
        unsafe
        {
            fixed (byte* kPtr = keyBytes)
            fixed (byte* vPtr = valueBytes)
            {
                return OpenTuiNative.SetTerminalEnvVar(
                    Handle, (nint)kPtr, (nuint)keyBytes.Length,
                    (nint)vPtr, (nuint)valueBytes.Length);
            }
        }
    }

    /// <summary>
    /// Forwards terminal-related environment variables to the native renderer
    /// so it can detect capabilities (Unicode support, color depth, etc.).
    /// Call after construction, before <see cref="SetupTerminal"/>.
    /// </summary>
    public void ForwardEnvironment()
    {
        foreach (var key in ForwardedEnvKeys)
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (value is not null)
                SetTerminalEnvVar(key, value);
        }
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

    /// <summary>Enables or disables threaded rendering.</summary>
    public void SetUseThread(bool useThread) =>
        OpenTuiNative.SetUseThread(Handle, useThread);

    /// <summary>Sets the renderer background color.</summary>
    public void SetBackgroundColor(Rgba color) =>
        RgbaMarshalling.WithColorPtr(color, ptr => OpenTuiNative.SetBackgroundColor(Handle, ptr));

    /// <summary>Sets the vertical render offset in rows.</summary>
    public void SetRenderOffset(uint offset) =>
        OpenTuiNative.SetRenderOffset(Handle, offset);

    /// <summary>Updates the renderer performance statistics.</summary>
    public void UpdateStats(double frameTime, uint fps, double frameCallbackTime) =>
        OpenTuiNative.UpdateStats(Handle, frameTime, fps, frameCallbackTime);

    /// <summary>Updates the renderer memory usage statistics.</summary>
    public void UpdateMemoryStats(uint heapUsed, uint heapTotal, uint external) =>
        OpenTuiNative.UpdateMemoryStats(Handle, heapUsed, heapTotal, external);

    #endregion

    #region Rendering

    /// <summary>Renders the current frame to the terminal.</summary>
    public void Render(bool forceFullRender = false) =>
        OpenTuiNative.Render(Handle, forceFullRender);

    /// <summary>Gets the next (back) buffer handle for double-buffered rendering. The renderer owns this buffer.</summary>
    public nint GetNextBuffer() =>
        OpenTuiNative.GetNextBuffer(Handle);

    /// <summary>Gets the current (front) buffer handle. The renderer owns this buffer.</summary>
    public nint GetCurrentBuffer() =>
        OpenTuiNative.GetCurrentBuffer(Handle);

    #endregion

    #region Resize

    /// <summary>Resizes the renderer to new column/row dimensions.</summary>
    public void Resize(uint cols, uint rows) =>
        OpenTuiNative.ResizeRenderer(Handle, cols, rows);

    #endregion

    #region Cursor

    /// <summary>Sets the cursor position and visibility.</summary>
    public void SetCursorPosition(int x, int y, bool visible) =>
        OpenTuiNative.SetCursorPosition(Handle, x, y, visible);

    /// <summary>Sets the cursor color.</summary>
    public void SetCursorColor(Rgba color) =>
        RgbaMarshalling.WithColorPtr(color, ptr => OpenTuiNative.SetCursorColor(Handle, ptr));

    /// <summary>Gets the current cursor state from the renderer.</summary>
    public CursorState GetCursorState()
    {
        CursorState state = default;
        unsafe
        {
            OpenTuiNative.GetCursorState(Handle, (nint)(&state));
        }
        return state;
    }

    /// <summary>Sets cursor style options.</summary>
    public void SetCursorStyleOptions(CursorStyleOptions options)
    {
        unsafe
        {
            OpenTuiNative.SetCursorStyleOptions(Handle, (nint)(&options));
        }
    }

    #endregion

    #region Debug

    /// <summary>Enables or disables the debug overlay in the specified corner.</summary>
    public void SetDebugOverlay(bool enabled, DebugOverlayCorner corner = DebugOverlayCorner.TopLeft) =>
        OpenTuiNative.SetDebugOverlay(Handle, enabled, (byte)corner);

    /// <summary>Clears the entire terminal screen.</summary>
    public void ClearTerminal() =>
        OpenTuiNative.ClearTerminal(Handle);

    #endregion

    #region Terminal

    /// <summary>Sets the terminal window title.</summary>
    public void SetTerminalTitle(string title)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(title);
        unsafe
        {
            fixed (byte* ptr = bytes)
            {
                OpenTuiNative.SetTerminalTitle(Handle, (nint)ptr, (nuint)bytes.Length);
            }
        }
    }

    /// <summary>Copies text to the system clipboard via OSC 52.</summary>
    public bool CopyToClipboard(string text, byte register = 0)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        unsafe
        {
            fixed (byte* ptr = bytes)
            {
                return OpenTuiNative.CopyToClipboardOSC52(Handle, register, (nint)ptr, (nuint)bytes.Length);
            }
        }
    }

    /// <summary>Clears the system clipboard via OSC 52.</summary>
    public bool ClearClipboard(byte register = 0) =>
        OpenTuiNative.ClearClipboardOSC52(Handle, register);

    /// <summary>Restores the terminal to its original mode settings.</summary>
    public void RestoreTerminalModes() =>
        OpenTuiNative.RestoreTerminalModes(Handle);

    /// <summary>Enables mouse tracking.</summary>
    public void EnableMouse(bool sgr = true) =>
        OpenTuiNative.EnableMouse(Handle, sgr);

    /// <summary>Disables mouse tracking.</summary>
    public void DisableMouse() =>
        OpenTuiNative.DisableMouse(Handle);

    /// <summary>Enables the Kitty keyboard protocol with the specified flags.</summary>
    public void EnableKittyKeyboard(byte flags) =>
        OpenTuiNative.EnableKittyKeyboard(Handle, flags);

    /// <summary>Disables the Kitty keyboard protocol.</summary>
    public void DisableKittyKeyboard() =>
        OpenTuiNative.DisableKittyKeyboard(Handle);

    /// <summary>Sets the Kitty keyboard protocol flags.</summary>
    public void SetKittyKeyboardFlags(byte flags) =>
        OpenTuiNative.SetKittyKeyboardFlags(Handle, flags);

    /// <summary>Gets the current Kitty keyboard protocol flags.</summary>
    public byte GetKittyKeyboardFlags() =>
        OpenTuiNative.GetKittyKeyboardFlags(Handle);

    /// <summary>Sets up the terminal for rendering.</summary>
    public void SetupTerminal(bool alternateBuffer = true) =>
        OpenTuiNative.SetupTerminal(Handle, alternateBuffer);

    /// <summary>Suspends the renderer, restoring the terminal to a normal state.</summary>
    public void Suspend() =>
        OpenTuiNative.SuspendRenderer(Handle);

    /// <summary>Resumes the renderer after a suspension.</summary>
    public void Resume() =>
        OpenTuiNative.ResumeRenderer(Handle);

    /// <summary>Writes raw bytes to the terminal output.</summary>
    public void WriteOut(ReadOnlySpan<byte> data)
    {
        unsafe
        {
            fixed (byte* ptr = data)
            {
                OpenTuiNative.WriteOut(Handle, (nint)ptr, (ulong)data.Length);
            }
        }
    }

    /// <summary>Retrieves terminal capabilities from the renderer.</summary>
    public TerminalCapabilities GetTerminalCapabilities()
    {
        // Native struct: 15 bools (byte each) + padding + 2×(nint ptr, nuint len) + 1 bool
        const int BufSize = 256;
        unsafe
        {
            byte* buf = stackalloc byte[BufSize];
            OpenTuiNative.GetTerminalCapabilities(Handle, (nint)buf);

            int i = 0;
            bool kittyKeyboard = buf[i++] != 0;
            bool kittyGraphics = buf[i++] != 0;
            bool rgb = buf[i++] != 0;
            var unicode = (WidthMethod)buf[i++];
            bool sgrPixels = buf[i++] != 0;
            bool colorSchemeUpdates = buf[i++] != 0;
            bool explicitWidth = buf[i++] != 0;
            bool scaledText = buf[i++] != 0;
            bool sixel = buf[i++] != 0;
            bool focusTracking = buf[i++] != 0;
            bool sync = buf[i++] != 0;
            bool bracketedPaste = buf[i++] != 0;
            bool hyperlinks = buf[i++] != 0;
            bool osc52 = buf[i++] != 0;
            bool explicitCursorPositioning = buf[i++] != 0;

            // Align to pointer boundary for string pointer/length pairs
            int ptrSize = nint.Size;
            i = (i + ptrSize - 1) / ptrSize * ptrSize;

            string termName = "";
            string termVersion = "";

            nint namePtr = *(nint*)(buf + i);
            i += ptrSize;
            nuint nameLen = *(nuint*)(buf + i);
            i += ptrSize;
            nint versionPtr = *(nint*)(buf + i);
            i += ptrSize;
            nuint versionLen = *(nuint*)(buf + i);
            i += ptrSize;

            if (namePtr != nint.Zero && nameLen > 0)
                termName = Encoding.UTF8.GetString((byte*)namePtr, (int)nameLen);
            if (versionPtr != nint.Zero && versionLen > 0)
                termVersion = Encoding.UTF8.GetString((byte*)versionPtr, (int)versionLen);
            bool termFromXtversion = buf[i] != 0;

            return new TerminalCapabilities
            {
                KittyKeyboard = kittyKeyboard,
                KittyGraphics = kittyGraphics,
                Rgb = rgb,
                Unicode = unicode,
                SgrPixels = sgrPixels,
                ColorSchemeUpdates = colorSchemeUpdates,
                ExplicitWidth = explicitWidth,
                ScaledText = scaledText,
                Sixel = sixel,
                FocusTracking = focusTracking,
                Sync = sync,
                BracketedPaste = bracketedPaste,
                Hyperlinks = hyperlinks,
                Osc52 = osc52,
                ExplicitCursorPositioning = explicitCursorPositioning,
                TermName = termName,
                TermVersion = termVersion,
                TermFromXtversion = termFromXtversion,
            };
        }
    }

    /// <summary>Processes a terminal capability response.</summary>
    public void ProcessCapabilityResponse(ReadOnlySpan<byte> data)
    {
        unsafe
        {
            fixed (byte* ptr = data)
            {
                OpenTuiNative.ProcessCapabilityResponse(Handle, (nint)ptr, (nuint)data.Length);
            }
        }
    }

    /// <summary>Queries the terminal for pixel resolution (async response via event callback).</summary>
    public void QueryPixelResolution() =>
        OpenTuiNative.QueryPixelResolution(Handle);

    /// <summary>Dumps internal buffers to a file for debugging.</summary>
    public void DumpBuffers(long? timestamp = null) =>
        OpenTuiNative.DumpBuffers(Handle, timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    /// <summary>Dumps the stdout buffer to a file for debugging.</summary>
    public void DumpStdoutBuffer(long? timestamp = null) =>
        OpenTuiNative.DumpStdoutBuffer(Handle, timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    /// <summary>Returns the raw ANSI bytes from the last Render() call (testing mode only).</summary>
    public string GetLastOutputForTest()
    {
        OpenTuiNative.GetLastOutputForTest(Handle, out var slice);
        if (slice.Ptr == 0 || slice.Len == 0) return string.Empty;
        unsafe
        {
            return System.Text.Encoding.UTF8.GetString((byte*)slice.Ptr, (int)slice.Len);
        }
    }

    #endregion

    #region Hit Grid

    /// <summary>Adds a rectangular hit region to the hit grid.</summary>
    public void AddToHitGrid(int x, int y, uint w, uint h, uint id) =>
        OpenTuiNative.AddToHitGrid(Handle, x, y, w, h, id);

    /// <summary>Clears all hit regions from the current hit grid.</summary>
    public void ClearCurrentHitGrid() =>
        OpenTuiNative.ClearCurrentHitGrid(Handle);

    /// <summary>Pushes a scissor (clipping) rectangle onto the hit grid's clip stack.</summary>
    public void HitGridPushScissorRect(int x, int y, uint w, uint h) =>
        OpenTuiNative.HitGridPushScissorRect(Handle, x, y, w, h);

    /// <summary>Pops the most recent scissor rectangle from the hit grid's clip stack.</summary>
    public void HitGridPopScissorRect() =>
        OpenTuiNative.HitGridPopScissorRect(Handle);

    /// <summary>Clears all scissor rectangles from the hit grid's clip stack.</summary>
    public void HitGridClearScissorRects() =>
        OpenTuiNative.HitGridClearScissorRects(Handle);

    /// <summary>Adds a hit region to the current hit grid, clipped by active scissor rectangles.</summary>
    public void AddToCurrentHitGridClipped(int x, int y, uint w, uint h, uint id) =>
        OpenTuiNative.AddToCurrentHitGridClipped(Handle, x, y, w, h, id);

    /// <summary>Tests whether the given coordinates hit any region.</summary>
    /// <returns>The hit region ID, or 0 if no hit.</returns>
    public uint CheckHit(uint x, uint y) =>
        OpenTuiNative.CheckHit(Handle, x, y);

    /// <summary>Gets whether the hit grid has been modified since the last check.</summary>
    public bool GetHitGridDirty() =>
        OpenTuiNative.GetHitGridDirty(Handle);

    /// <summary>Dumps the hit grid contents for debugging.</summary>
    public void DumpHitGrid() =>
        OpenTuiNative.DumpHitGrid(Handle);

    #endregion

    #region Static Callbacks

    /// <summary>Sets the global log callback function pointer.</summary>
    public static void SetLogCallback(nint callback) =>
        OpenTuiNative.SetLogCallback(callback);

    /// <summary>Sets the global event callback function pointer.</summary>
    public static void SetEventCallback(nint callback) =>
        OpenTuiNative.SetEventCallback(callback);

    #endregion

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            OpenTuiNative.RendererDestroy(_handle);
            _handle = nint.Zero;
        }
    }
}
