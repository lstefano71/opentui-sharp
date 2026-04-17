using OpenTui.Native;

namespace OpenTui;

/// <summary>High-level wrapper around the native OpenTUI renderer.</summary>
public sealed class NativeRenderer : IDisposable
{
    private readonly RendererHandle _handle;
    private bool _disposed;

    /// <summary>Creates a new terminal renderer with the specified dimensions.</summary>
    public NativeRenderer(uint cols, uint rows, bool useStdout = true, bool useAlternateScreen = true)
    {
        nint ptr = OpenTuiNative.CreateRenderer(cols, rows, useStdout, useAlternateScreen);
        _handle = new RendererHandle();
        _handle.SetHandleValue(ptr);
    }

    internal nint Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _handle.DangerousGetHandle();
        }
    }

    /// <summary>Sets whether to use threaded rendering.</summary>
    public void SetUseThread(bool useThread) =>
        OpenTuiNative.SetUseThread(Handle, useThread);

    /// <summary>Sets the renderer background color.</summary>
    public void SetBackgroundColor(Rgba color) =>
        RgbaMarshalling.WithColorPtr(color, ptr => OpenTuiNative.SetBackgroundColor(Handle, ptr));

    /// <summary>Sets the vertical render offset in rows.</summary>
    public void SetRenderOffset(uint offset) =>
        OpenTuiNative.SetRenderOffset(Handle, offset);

    /// <summary>Updates the renderer performance statistics.</summary>
    public void UpdateStats(double frameTime, uint nodeCount, double layoutTime) =>
        OpenTuiNative.UpdateStats(Handle, frameTime, nodeCount, layoutTime);

    /// <summary>Updates the renderer memory usage statistics.</summary>
    public void UpdateMemoryStats(uint heapUsed, uint heapTotal, uint external) =>
        OpenTuiNative.UpdateMemoryStats(Handle, heapUsed, heapTotal, external);

    /// <summary>Renders the current frame to the terminal.</summary>
    public void Render(bool forceFullRender = false) =>
        OpenTuiNative.Render(Handle, forceFullRender);

    /// <summary>Gets the next (back) buffer for double-buffered rendering. The renderer owns this buffer.</summary>
    public NativeBuffer GetNextBuffer() =>
        new(OpenTuiNative.GetNextBuffer(Handle));

    /// <summary>Gets the current (front) buffer. The renderer owns this buffer.</summary>
    public NativeBuffer GetCurrentBuffer() =>
        new(OpenTuiNative.GetCurrentBuffer(Handle));

    /// <summary>Queries the terminal for pixel resolution (async response via event callback).</summary>
    public void QueryPixelResolution() =>
        OpenTuiNative.QueryPixelResolution(Handle);

    /// <summary>Resizes the renderer to new column/row dimensions.</summary>
    public void Resize(uint cols, uint rows) =>
        OpenTuiNative.ResizeRenderer(Handle, cols, rows);

    /// <summary>Sets the cursor position and visibility.</summary>
    public void SetCursorPosition(int x, int y, bool visible) =>
        OpenTuiNative.SetCursorPosition(Handle, x, y, visible);

    /// <summary>Sets the cursor color.</summary>
    public void SetCursorColor(Rgba color) =>
        RgbaMarshalling.WithColorPtr(color, ptr => OpenTuiNative.SetCursorColor(Handle, ptr));

    /// <summary>Enables or disables the debug overlay.</summary>
    public void SetDebugOverlay(bool enabled, byte corner = 0) =>
        OpenTuiNative.SetDebugOverlay(Handle, enabled, corner);

    /// <summary>Clears the entire terminal screen.</summary>
    public void ClearTerminal() =>
        OpenTuiNative.ClearTerminal(Handle);

    /// <summary>Sets the terminal window title.</summary>
    public void SetTitle(string title)
    {
        var utf8 = new Utf8String(title);
        utf8.WithPtr((ptr, len) => OpenTuiNative.SetTerminalTitle(Handle, ptr, len));
    }

    /// <summary>Copies data to the system clipboard via OSC 52.</summary>
    public bool CopyToClipboard(byte register, ReadOnlySpan<byte> data)
    {
        unsafe
        {
            fixed (byte* ptr = data)
            {
                return OpenTuiNative.CopyToClipboardOSC52(Handle, register, (nint)ptr, (nuint)data.Length);
            }
        }
    }

    /// <summary>Clears the system clipboard via OSC 52.</summary>
    public bool ClearClipboard(byte register) =>
        OpenTuiNative.ClearClipboardOSC52(Handle, register);

    /// <summary>Restores the terminal to its original mode settings.</summary>
    public void RestoreTerminalModes() =>
        OpenTuiNative.RestoreTerminalModes(Handle);

    /// <summary>Enables mouse tracking.</summary>
    public void EnableMouse(bool sgrMode = true) =>
        OpenTuiNative.EnableMouse(Handle, sgrMode);

    /// <summary>Disables mouse tracking.</summary>
    public void DisableMouse() =>
        OpenTuiNative.DisableMouse(Handle);

    /// <summary>Enables the Kitty keyboard protocol with the specified flags.</summary>
    public void EnableKittyKeyboard(byte flags) =>
        OpenTuiNative.EnableKittyKeyboard(Handle, flags);

    /// <summary>Disables the Kitty keyboard protocol.</summary>
    public void DisableKittyKeyboard() =>
        OpenTuiNative.DisableKittyKeyboard(Handle);

    /// <summary>Gets or sets the Kitty keyboard protocol flags.</summary>
    public byte KittyKeyboardFlags
    {
        get => OpenTuiNative.GetKittyKeyboardFlags(Handle);
        set => OpenTuiNative.SetKittyKeyboardFlags(Handle, value);
    }

    /// <summary>Sets up the terminal for rendering.</summary>
    public void SetupTerminal(bool alternateScreen = true) =>
        OpenTuiNative.SetupTerminal(Handle, alternateScreen);

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

    /// <summary>Sets a terminal environment variable.</summary>
    public bool SetTerminalEnvVar(string key, string value)
    {
        byte[] keyBytes = System.Text.Encoding.UTF8.GetBytes(key);
        byte[] valueBytes = System.Text.Encoding.UTF8.GetBytes(value);
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

    /// <summary>Dumps internal buffers to a file named with the given timestamp for debugging.</summary>
    public void DumpBuffers(long? timestamp = null) =>
        OpenTuiNative.DumpBuffers(Handle, timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    /// <summary>Dumps the stdout buffer to a file named with the given timestamp for debugging.</summary>
    public void DumpStdoutBuffer(long? timestamp = null) =>
        OpenTuiNative.DumpStdoutBuffer(Handle, timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _handle.Dispose();
        }
    }
}
