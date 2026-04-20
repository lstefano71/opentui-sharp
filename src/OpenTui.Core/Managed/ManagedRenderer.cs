using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using OpenTui.Core.Managed.Unicode;

namespace OpenTui.Core.Managed;

/// <summary>
/// Clip rectangle for hit grid scissor stack.
/// </summary>
public readonly record struct ClipRect(int X, int Y, uint Width, uint Height);

/// <summary>
/// Render performance statistics.
/// </summary>
public sealed class RenderStats
{
    public double LastFrameTime { get; set; }
    public double AverageFrameTime { get; set; }
    public ulong FrameCount { get; set; }
    public uint Fps { get; set; }
    public uint CellsUpdated { get; set; }
}

/// <summary>
/// Interface for terminal output sinks. Used by <see cref="ManagedRenderer"/> to write
/// ANSI output in a single batch. Implementations may write to stdout, a stream, or memory.
/// </summary>
public interface ITerminalWriter
{
    void Write(ReadOnlySpan<byte> data);
    void Flush();
}

/// <summary>
/// Pure C# double-buffered diff-based ANSI terminal renderer.
/// Port of the Zig <c>renderer.zig</c> (<c>CliRenderer</c>).
///
/// <para>Maintains two cell buffers (<c>current</c> = what's on screen, <c>next</c> = what to
/// render). Each <see cref="Render"/> call diffs the two, emits only changed cells as ANSI
/// escape sequences, then swaps buffers and clears next for the following frame.</para>
///
/// <para>Also maintains a double-buffered hit grid for mouse event dispatch and a scissor
/// stack for clipping hit regions to overflow-hidden containers.</para>
/// </summary>
public sealed class ManagedRenderer : IDisposable
{
    #region Constants

    private const uint ClearChar = 0x0A00;
    private const float ColorEpsilon = 0.00001f;
    private const int OutputBufferInitialSize = 1024 * 1024 * 2; // 2 MB

    // ANSI escape sequences (UTF-8 byte literals)
    private static ReadOnlySpan<byte> SyncSet => "\x1b[?2026h"u8;
    private static ReadOnlySpan<byte> SyncReset => "\x1b[?2026l"u8;
    private static ReadOnlySpan<byte> HideCursor => "\x1b[?25l"u8;
    private static ReadOnlySpan<byte> ShowCursor => "\x1b[?25h"u8;
    private static ReadOnlySpan<byte> Reset => "\x1b[0m"u8;
    private static ReadOnlySpan<byte> DefaultBg => "\x1b[49m"u8;
    private static ReadOnlySpan<byte> Bold => "\x1b[1m"u8;
    private static ReadOnlySpan<byte> Dim => "\x1b[2m"u8;
    private static ReadOnlySpan<byte> Italic => "\x1b[3m"u8;
    private static ReadOnlySpan<byte> Underline => "\x1b[4m"u8;
    private static ReadOnlySpan<byte> Blink => "\x1b[5m"u8;
    private static ReadOnlySpan<byte> Inverse => "\x1b[7m"u8;
    private static ReadOnlySpan<byte> Hidden => "\x1b[8m"u8;
    private static ReadOnlySpan<byte> Strikethrough => "\x1b[9m"u8;
    private static ReadOnlySpan<byte> DefaultCursorStyle => "\x1b[0 q"u8;
    private static ReadOnlySpan<byte> CursorBlock => "\x1b[2 q"u8;
    private static ReadOnlySpan<byte> CursorBlockBlink => "\x1b[1 q"u8;
    private static ReadOnlySpan<byte> CursorLineSeq => "\x1b[6 q"u8;
    private static ReadOnlySpan<byte> CursorLineBlink => "\x1b[5 q"u8;
    private static ReadOnlySpan<byte> CursorUnderlineSeq => "\x1b[4 q"u8;
    private static ReadOnlySpan<byte> CursorUnderlineBlink => "\x1b[3 q"u8;

    #endregion

    #region State — buffers

    // TODO: depends on ManagedBuffer — for now these are typed as ManagedBuffer.
    // Once ManagedBuffer lands, wire up properly.
    private ManagedBuffer _currentRenderBuffer;
    private ManagedBuffer _nextRenderBuffer;

    #endregion

    #region State — hit grid

    private uint[] _currentHitGrid;
    private uint[] _nextHitGrid;
    private uint _hitGridWidth;
    private uint _hitGridHeight;
    private readonly List<ClipRect> _hitScissorStack = new();
    private bool _hitGridDirty;

    #endregion

    #region State — output buffer

    private byte[] _outputBuffer;
    private int _outputLen;

    #endregion

    #region State — cursor tracking (for diff output)

    private byte? _lastCursorStyleTag;
    private bool? _lastCursorBlinking;
    private (byte R, byte G, byte B)? _lastCursorColorRgb;
    private MousePointerStyle _lastMousePointerStyle = MousePointerStyle.Default;

    #endregion

    #region State — cursor position/style (managed terminal state)

    private uint _cursorX;
    private uint _cursorY;
    private bool _cursorVisible;
    private CursorStyle _cursorStyle = CursorStyle.Default;
    private bool _cursorBlinking;
    private Rgba _cursorColor = Rgba.White;
    private MousePointerStyle _mousePointerStyle = MousePointerStyle.Default;

    #endregion

    #region State — grapheme pool

    private readonly ManagedGraphemePool _graphemePool;
    private readonly bool _ownsGraphemePool;

    #endregion

    #region Public properties

    /// <summary>Width in columns.</summary>
    public uint Width { get; private set; }

    /// <summary>Height in rows.</summary>
    public uint Height { get; private set; }

    /// <summary>Background color used to clear the next buffer after each frame.</summary>
    public Rgba BackgroundColor { get; set; }

    /// <summary>Vertical row offset for non-alternate-screen rendering.</summary>
    public uint RenderOffset { get; set; }

    /// <summary>Whether this renderer is in testing mode (no actual terminal I/O).</summary>
    public bool Testing { get; }

    /// <summary>Whether to use the alternate screen buffer.</summary>
    public bool UseAlternateScreen { get; set; } = true;

    /// <summary>Render performance statistics.</summary>
    public RenderStats Stats { get; } = new();

    /// <summary>Terminal capabilities (for managed renderer, returns a default set).</summary>
    public TerminalCapabilities Capabilities { get; } = new()
    {
        Rgb = true,
        Sync = true,
        Unicode = WidthMethod.Unicode,
    };

    /// <summary>The grapheme pool used by this renderer's buffers.</summary>
    public ManagedGraphemePool GraphemePool => _graphemePool;

    /// <summary>Last rendered output bytes (testing mode). Empty when not in testing mode.</summary>
    public ReadOnlySpan<byte> LastOutputForTest => _outputBuffer.AsSpan(0, _outputLen);

    #endregion

    #region Construction

    private ManagedRenderer(uint width, uint height, ManagedGraphemePool pool, bool ownsPool, bool testing)
    {
        Width = width;
        Height = height;
        Testing = testing;
        _graphemePool = pool;
        _ownsGraphemePool = ownsPool;

        BackgroundColor = Rgba.Transparent;

        _currentRenderBuffer = ManagedBuffer.Create(width, height);
        _nextRenderBuffer = ManagedBuffer.Create(width, height);

        // Clear current buffer with ClearChar so first frame always diffs as changed
        _currentRenderBuffer.Clear(Rgba.Black, ClearChar);
        _nextRenderBuffer.Clear(BackgroundColor, null);

        uint gridSize = width * height;
        _currentHitGrid = new uint[gridSize];
        _nextHitGrid = new uint[gridSize];
        _hitGridWidth = width;
        _hitGridHeight = height;

        _outputBuffer = new byte[OutputBufferInitialSize];
        _outputLen = 0;
    }

    /// <summary>Creates a new managed renderer with the specified terminal dimensions.</summary>
    public static ManagedRenderer Create(uint width, uint height, bool testing = false, bool remote = false)
    {
        return Create(width, height, new ManagedGraphemePool(), ownsPool: true, testing);
    }

    /// <summary>Creates a new managed renderer sharing an existing grapheme pool.</summary>
    public static ManagedRenderer Create(uint width, uint height, ManagedGraphemePool pool, bool ownsPool, bool testing = false)
    {
        return new ManagedRenderer(width, height, pool, ownsPool, testing);
    }

    #endregion

    #region Buffer access

    /// <summary>Gets the next (back) buffer to draw the upcoming frame into.</summary>
    public ManagedBuffer GetNextBuffer() => _nextRenderBuffer;

    /// <summary>Gets the current (front) buffer showing what's on screen.</summary>
    public ManagedBuffer GetCurrentBuffer() => _currentRenderBuffer;

    #endregion

    #region Hit grid

    /// <summary>
    /// Writes a renderable's bounds to the next hit grid for the upcoming frame.
    /// Clipped to the current hit scissor stack.
    /// </summary>
    public void AddToHitGrid(uint renderableId, int x, int y, uint width, uint height)
    {
        var clipped = ClipRectToHitScissor(x, y, width, height);
        if (clipped is null) return;
        var cr = clipped.Value;

        int startX = Math.Max(0, cr.X);
        int startY = Math.Max(0, cr.Y);
        int endX = Math.Min((int)_hitGridWidth, cr.X + (int)cr.Width);
        int endY = Math.Min((int)_hitGridHeight, cr.Y + (int)cr.Height);

        if (startX >= endX || startY >= endY) return;

        for (int row = startY; row < endY; row++)
        {
            int rowStart = row * (int)_hitGridWidth;
            Array.Fill(_nextHitGrid, renderableId, rowStart + startX, endX - startX);
        }
    }

    /// <summary>
    /// Writes directly to the current hit grid with scissor clipping.
    /// Used for immediate hit grid sync when scroll/translate changes.
    /// </summary>
    public void AddToCurrentHitGridClipped(uint renderableId, int x, int y, uint width, uint height)
    {
        var clipped = ClipRectToHitScissor(x, y, width, height);
        if (clipped is null) return;
        var cr = clipped.Value;

        int startX = Math.Max(0, cr.X);
        int startY = Math.Max(0, cr.Y);
        int endX = Math.Min((int)_hitGridWidth, cr.X + (int)cr.Width);
        int endY = Math.Min((int)_hitGridHeight, cr.Y + (int)cr.Height);

        if (startX >= endX || startY >= endY) return;

        for (int row = startY; row < endY; row++)
        {
            int rowStart = row * (int)_hitGridWidth;
            Array.Fill(_currentHitGrid, renderableId, rowStart + startX, endX - startX);
        }
    }

    /// <summary>Returns the renderable ID at screen position (x, y), or 0 if none.</summary>
    public uint CheckHit(uint x, uint y)
    {
        if (x >= _hitGridWidth || y >= _hitGridHeight) return 0;
        return _currentHitGrid[y * _hitGridWidth + x];
    }

    /// <summary>Clears the current hit grid.</summary>
    public void ClearCurrentHitGrid() => Array.Clear(_currentHitGrid);

    /// <summary>Returns whether the hit grid changed during the last render.</summary>
    public bool GetHitGridDirty() => _hitGridDirty;

    /// <summary>Pushes a scissor rect for hit grid clipping. Intersected with any existing scissor.</summary>
    public void PushHitScissor(int x, int y, uint width, uint height)
    {
        var rect = new ClipRect(x, y, width, height);

        if (_hitScissorStack.Count > 0)
        {
            var intersected = ClipRectToHitScissor(rect.X, rect.Y, rect.Width, rect.Height);
            rect = intersected ?? new ClipRect(0, 0, 0, 0);
        }

        _hitScissorStack.Add(rect);
    }

    /// <summary>Pops the most recent scissor rectangle from the hit grid's clip stack.</summary>
    public void PopHitScissor()
    {
        if (_hitScissorStack.Count > 0)
            _hitScissorStack.RemoveAt(_hitScissorStack.Count - 1);
    }

    /// <summary>Clears all hit grid scissors.</summary>
    public void ClearHitScissors() => _hitScissorStack.Clear();

    private ClipRect? ClipRectToHitScissor(int x, int y, uint width, uint height)
    {
        if (_hitScissorStack.Count == 0)
            return new ClipRect(x, y, width, height);

        var scissor = _hitScissorStack[^1];

        int rectEndX = x + (int)width;
        int rectEndY = y + (int)height;
        int scissorEndX = scissor.X + (int)scissor.Width;
        int scissorEndY = scissor.Y + (int)scissor.Height;

        int ix = Math.Max(x, scissor.X);
        int iy = Math.Max(y, scissor.Y);
        int iex = Math.Min(rectEndX, scissorEndX);
        int iey = Math.Min(rectEndY, scissorEndY);

        if (ix >= iex || iy >= iey) return null;

        return new ClipRect(ix, iy, (uint)(iex - ix), (uint)(iey - iy));
    }

    #endregion

    #region Cursor / terminal state

    /// <summary>Sets the cursor position.</summary>
    public void SetCursorPosition(uint x, uint y)
    {
        _cursorX = x;
        _cursorY = y;
    }

    /// <summary>Sets the cursor style.</summary>
    public void SetCursorStyle(CursorStyle style, bool blinking)
    {
        _cursorStyle = style;
        _cursorBlinking = blinking;
    }

    /// <summary>Sets the cursor color.</summary>
    public void SetCursorColor(Rgba color)
    {
        _cursorColor = color;
    }

    /// <summary>Sets the cursor visibility.</summary>
    public void SetCursorVisible(bool visible)
    {
        _cursorVisible = visible;
    }

    /// <summary>Sets the mouse pointer style.</summary>
    public void SetMousePointerStyle(MousePointerStyle style)
    {
        _mousePointerStyle = style;
    }

    /// <summary>Gets the current cursor state.</summary>
    public CursorState GetCursorState() => new()
    {
        X = _cursorX,
        Y = _cursorY,
        Visible = _cursorVisible,
        Style = (byte)_cursorStyle,
        Blinking = _cursorBlinking,
        R = _cursorColor.R,
        G = _cursorColor.G,
        B = _cursorColor.B,
        A = _cursorColor.A,
    };

    /// <summary>Sets up the terminal for rendering (writes setup sequences to writer).</summary>
    public void SetupTerminal(ITerminalWriter writer)
    {
        if (Testing) return;
        // Save cursor, enter alt screen, hide cursor
        writer.Write("\x1b[s"u8);
        if (UseAlternateScreen)
            writer.Write("\x1b[?1049h"u8);
        writer.Write(HideCursor);
        writer.Flush();
    }

    /// <summary>Shuts down the terminal (writes restore sequences to writer).</summary>
    public void ShutdownTerminal(ITerminalWriter writer)
    {
        if (Testing) return;
        writer.Write(Reset);
        if (UseAlternateScreen)
            writer.Write("\x1b[?1049l"u8);
        writer.Write("\x1b]112\x07"u8); // reset cursor color
        writer.Write(DefaultCursorStyle);
        writer.Write(ShowCursor);
        writer.Flush();
    }

    #endregion

    #region Resize

    /// <summary>Resizes the renderer to new column/row dimensions.</summary>
    public void Resize(uint width, uint height)
    {
        if (Width == width && Height == height) return;

        Width = width;
        Height = height;

        _currentRenderBuffer.Resize(width, height);
        _nextRenderBuffer.Resize(width, height);

        _currentRenderBuffer.Clear(Rgba.Black, ClearChar);
        _nextRenderBuffer.Clear(BackgroundColor, null);

        uint newGridSize = width * height;
        uint oldGridSize = _hitGridWidth * _hitGridHeight;
        if (newGridSize > oldGridSize)
        {
            _currentHitGrid = new uint[newGridSize];
            _nextHitGrid = new uint[newGridSize];
        }
        else
        {
            Array.Clear(_currentHitGrid, 0, (int)newGridSize);
            Array.Clear(_nextHitGrid, 0, (int)newGridSize);
        }

        _hitGridWidth = width;
        _hitGridHeight = height;

        _cursorX = Math.Min(_cursorX, width);
        _cursorY = Math.Min(_cursorY, height);
    }

    #endregion

    #region Render — the core diff loop

    /// <summary>
    /// Renders a frame by diffing next vs current buffer, emitting only changed cells
    /// as ANSI escape sequences, and flushing to the writer.
    /// </summary>
    public void Render(ITerminalWriter writer)
    {
        long renderStart = Stopwatch.GetTimestamp();
        uint cellsUpdated = 0;

        // Reset output buffer
        _outputLen = 0;

        // Begin synchronized update
        WriteToOutput(SyncSet);
        WriteToOutput(HideCursor);

        // Tracking state for the diff loop
        Rgba? currentFg = null;
        Rgba? currentBg = null;
        int currentAttributes = -1;
        uint currentLinkId = 0;
        Span<byte> utf8Buf = stackalloc byte[4];

        int runStart = -1;
        uint runLength = 0;

        for (uint y = 0; y < Height; y++)
        {
            runStart = -1;
            runLength = 0;

            for (uint x = 0; x < Width; x++)
            {
                var currentCell = _currentRenderBuffer.Get(x, y);
                var nextCell = _nextRenderBuffer.Get(x, y);

                if (currentCell is null || nextCell is null) continue;

                var cc = currentCell.Value;
                var nc = nextCell.Value;

                // Skip unchanged cells
                if (cc.Char == nc.Char &&
                    cc.Attributes == nc.Attributes &&
                    RgbaEqual(cc.Fg, nc.Fg) &&
                    RgbaEqual(cc.Bg, nc.Bg))
                {
                    if (runLength > 0)
                    {
                        WriteToOutput(Reset);
                        runStart = -1;
                        runLength = 0;
                    }
                    continue;
                }

                // Check if fg/bg/attrs match what we've already emitted
                bool fgMatch = currentFg.HasValue && RgbaEqual(currentFg.Value, nc.Fg);
                bool bgMatch = currentBg.HasValue && RgbaEqual(currentBg.Value, nc.Bg);
                bool sameAttributes = fgMatch && bgMatch && (int)nc.Attributes == currentAttributes;

                // Hyperlink handling
                uint linkId = TextAttributeUtils.GetBase(nc.Attributes) != 0
                    ? 0 // simplified — link support can be added later
                    : 0;

                if (!sameAttributes || runStart == -1)
                {
                    if (runLength > 0)
                    {
                        WriteToOutput(Reset);
                    }

                    runStart = (int)x;
                    runLength = 0;

                    currentFg = nc.Fg;
                    currentBg = nc.Bg;
                    currentAttributes = (int)nc.Attributes;

                    // Move cursor to position (1-based)
                    WriteCursorPosition(x + 1, y + 1 + RenderOffset);

                    // Foreground color
                    byte fgR = RgbaComponentToU8(nc.Fg.R);
                    byte fgG = RgbaComponentToU8(nc.Fg.G);
                    byte fgB = RgbaComponentToU8(nc.Fg.B);
                    WriteFgColor(fgR, fgG, fgB);

                    // Background color — transparent bg uses terminal default
                    if (nc.Bg.A < 0.001f)
                    {
                        WriteToOutput(DefaultBg);
                    }
                    else
                    {
                        byte bgR = RgbaComponentToU8(nc.Bg.R);
                        byte bgG = RgbaComponentToU8(nc.Bg.G);
                        byte bgB = RgbaComponentToU8(nc.Bg.B);
                        WriteBgColor(bgR, bgG, bgB);
                    }

                    // Text attributes
                    WriteTextAttributes(nc.Attributes);
                }

                // Write the character
                if (IsGraphemeChar(nc.Char))
                {
                    uint gid = GraphemeIdFromChar(nc.Char);
                    var bytes = _graphemePool.Get(gid);
                    if (bytes.Length > 0)
                    {
                        WriteToOutput(bytes);
                    }
                }
                else if (IsContinuationChar(nc.Char))
                {
                    // Skip continuation cells — they are handled by the start cell
                }
                else
                {
                    // Regular codepoint → UTF-8
                    var rune = new Rune((int)nc.Char);
                    int len = rune.EncodeToUtf8(utf8Buf);
                    WriteToOutput(utf8Buf[..len]);
                }

                runLength++;

                // Sync cell to current buffer so next frame's diff is correct
                _currentRenderBuffer.SyncCell(x, y, nc);

                cellsUpdated++;
            }
        }

        // End-of-frame reset
        WriteToOutput(Reset);

        // Cursor handling
        if (_cursorVisible)
        {
            byte cursorR = RgbaComponentToU8(_cursorColor.R);
            byte cursorG = RgbaComponentToU8(_cursorColor.G);
            byte cursorB = RgbaComponentToU8(_cursorColor.B);

            byte styleTag = (byte)_cursorStyle;
            bool styleChanged = !_lastCursorStyleTag.HasValue || _lastCursorStyleTag.Value != styleTag ||
                                !_lastCursorBlinking.HasValue || _lastCursorBlinking.Value != _cursorBlinking;
            bool colorChanged = !_lastCursorColorRgb.HasValue ||
                                _lastCursorColorRgb.Value.R != cursorR ||
                                _lastCursorColorRgb.Value.G != cursorG ||
                                _lastCursorColorRgb.Value.B != cursorB;

            if (colorChanged)
            {
                WriteCursorColor(cursorR, cursorG, cursorB);
                _lastCursorColorRgb = (cursorR, cursorG, cursorB);
            }

            if (styleChanged)
            {
                WriteCursorStyleSequence(_cursorStyle, _cursorBlinking);
                _lastCursorStyleTag = styleTag;
                _lastCursorBlinking = _cursorBlinking;
            }

            WriteCursorPosition(_cursorX, _cursorY + RenderOffset);
            WriteToOutput(ShowCursor);
        }
        else
        {
            WriteToOutput(HideCursor);
            _lastCursorStyleTag = null;
            _lastCursorBlinking = null;
            _lastCursorColorRgb = null;
        }

        // Mouse pointer style
        if (_mousePointerStyle != _lastMousePointerStyle)
        {
            WriteMousePointerStyle(_mousePointerStyle);
            _lastMousePointerStyle = _mousePointerStyle;
        }

        // End synchronized update
        WriteToOutput(SyncReset);

        // Update stats
        Stats.CellsUpdated = cellsUpdated;
        Stats.FrameCount++;

        double elapsedUs = Stopwatch.GetElapsedTime(renderStart).TotalMicroseconds;
        Stats.LastFrameTime = elapsedUs / 1000.0; // ms

        // Flush output to writer
        if (!Testing)
        {
            writer.Write(_outputBuffer.AsSpan(0, _outputLen));
            writer.Flush();
        }

        // Compare hit grids to detect changes
        _hitGridDirty = !_currentHitGrid.AsSpan(0, (int)(_hitGridWidth * _hitGridHeight))
            .SequenceEqual(_nextHitGrid.AsSpan(0, (int)(_hitGridWidth * _hitGridHeight)));

        // Swap hit grids
        (_currentHitGrid, _nextHitGrid) = (_nextHitGrid, _currentHitGrid);
        Array.Clear(_nextHitGrid, 0, (int)(_hitGridWidth * _hitGridHeight));

        // Clear next buffer for the next frame
        _nextRenderBuffer.Clear(BackgroundColor, null);
    }

    #endregion

    #region Output buffer helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteToOutput(ReadOnlySpan<byte> data)
    {
        EnsureOutputCapacity(data.Length);
        data.CopyTo(_outputBuffer.AsSpan(_outputLen));
        _outputLen += data.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteToOutput(byte b)
    {
        EnsureOutputCapacity(1);
        _outputBuffer[_outputLen++] = b;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void EnsureOutputCapacity(int additional)
    {
        if (_outputLen + additional <= _outputBuffer.Length) return;
        int newSize = _outputBuffer.Length;
        while (newSize < _outputLen + additional) newSize *= 2;
        Array.Resize(ref _outputBuffer, newSize);
    }

    #endregion

    #region ANSI sequence writers

    /// <summary>Writes \x1b[{row};{col}H to the output buffer.</summary>
    private void WriteCursorPosition(uint col, uint row)
    {
        // \x1b[{row};{col}H
        WriteToOutput((byte)0x1B);
        WriteToOutput((byte)'[');
        WriteUIntAscii(row);
        WriteToOutput((byte)';');
        WriteUIntAscii(col);
        WriteToOutput((byte)'H');
    }

    /// <summary>Writes \x1b[38;2;{r};{g};{b}m to the output buffer.</summary>
    private void WriteFgColor(byte r, byte g, byte b)
    {
        WriteToOutput((byte)0x1B);
        WriteToOutput((byte)'[');
        WriteToOutput("38;2;"u8);
        WriteByteAscii(r);
        WriteToOutput((byte)';');
        WriteByteAscii(g);
        WriteToOutput((byte)';');
        WriteByteAscii(b);
        WriteToOutput((byte)'m');
    }

    /// <summary>Writes \x1b[48;2;{r};{g};{b}m to the output buffer.</summary>
    private void WriteBgColor(byte r, byte g, byte b)
    {
        WriteToOutput((byte)0x1B);
        WriteToOutput((byte)'[');
        WriteToOutput("48;2;"u8);
        WriteByteAscii(r);
        WriteToOutput((byte)';');
        WriteByteAscii(g);
        WriteToOutput((byte)';');
        WriteByteAscii(b);
        WriteToOutput((byte)'m');
    }

    /// <summary>Writes applicable text attribute sequences (bold, italic, etc.).</summary>
    private void WriteTextAttributes(uint attributes)
    {
        uint baseAttrs = attributes & TextAttributeUtils.BaseMask;
        if ((baseAttrs & (uint)TextAttributes.Bold) != 0) WriteToOutput(Bold);
        if ((baseAttrs & (uint)TextAttributes.Dim) != 0) WriteToOutput(Dim);
        if ((baseAttrs & (uint)TextAttributes.Italic) != 0) WriteToOutput(Italic);
        if ((baseAttrs & (uint)TextAttributes.Underline) != 0) WriteToOutput(Underline);
        if ((baseAttrs & (uint)TextAttributes.Blink) != 0) WriteToOutput(Blink);
        if ((baseAttrs & (uint)TextAttributes.Inverse) != 0) WriteToOutput(Inverse);
        if ((baseAttrs & (uint)TextAttributes.Hidden) != 0) WriteToOutput(Hidden);
        if ((baseAttrs & (uint)TextAttributes.Strikethrough) != 0) WriteToOutput(Strikethrough);
    }

    /// <summary>Writes \x1b]12;#RRGGBB\x07 to the output buffer.</summary>
    private void WriteCursorColor(byte r, byte g, byte b)
    {
        WriteToOutput("\x1b]12;#"u8);
        WriteHexByte(r);
        WriteHexByte(g);
        WriteHexByte(b);
        WriteToOutput((byte)0x07);
    }

    private void WriteCursorStyleSequence(CursorStyle style, bool blinking)
    {
        ReadOnlySpan<byte> seq = style switch
        {
            CursorStyle.SteadyBlock => CursorBlock,
            CursorStyle.BlinkingBlock => CursorBlockBlink,
            CursorStyle.SteadyBar => CursorLineSeq,
            CursorStyle.BlinkingBar => CursorLineBlink,
            CursorStyle.SteadyUnderline => CursorUnderlineSeq,
            CursorStyle.BlinkingUnderline => CursorUnderlineBlink,
            _ => DefaultCursorStyle,
        };
        WriteToOutput(seq);
    }

    /// <summary>Writes \x1b]22;{name}\x07 to the output buffer.</summary>
    private void WriteMousePointerStyle(MousePointerStyle style)
    {
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
        WriteToOutput("\x1b]22;"u8);
        WriteToOutput(name);
        WriteToOutput((byte)0x07);
    }

    #endregion

    #region Number formatting (hot path)

    /// <summary>Writes an unsigned integer as ASCII decimal digits to the output buffer.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteUIntAscii(uint value)
    {
        if (value < 10)
        {
            WriteToOutput((byte)('0' + value));
            return;
        }
        if (value < 100)
        {
            WriteToOutput((byte)('0' + value / 10));
            WriteToOutput((byte)('0' + value % 10));
            return;
        }
        // General case — use stack buffer
        Span<byte> buf = stackalloc byte[10];
        Utf8Formatter.TryFormat(value, buf, out int written);
        WriteToOutput(buf[..written]);
    }

    /// <summary>Writes a byte as ASCII decimal digits to the output buffer.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteByteAscii(byte value)
    {
        if (value < 10)
        {
            WriteToOutput((byte)('0' + value));
        }
        else if (value < 100)
        {
            WriteToOutput((byte)('0' + value / 10));
            WriteToOutput((byte)('0' + value % 10));
        }
        else
        {
            WriteToOutput((byte)('0' + value / 100));
            WriteToOutput((byte)('0' + (value / 10) % 10));
            WriteToOutput((byte)('0' + value % 10));
        }
    }

    private static ReadOnlySpan<byte> HexChars => "0123456789abcdef"u8;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteHexByte(byte value)
    {
        WriteToOutput(HexChars[value >> 4]);
        WriteToOutput(HexChars[value & 0xF]);
    }

    #endregion

    #region Color / char helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte RgbaComponentToU8(float component)
    {
        if (!float.IsFinite(component)) return 0;
        return (byte)MathF.Round(Math.Clamp(component, 0f, 1f) * 255f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool RgbaEqual(Rgba a, Rgba b)
    {
        return MathF.Abs(a.R - b.R) < ColorEpsilon &&
               MathF.Abs(a.G - b.G) < ColorEpsilon &&
               MathF.Abs(a.B - b.B) < ColorEpsilon &&
               MathF.Abs(a.A - b.A) < ColorEpsilon;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsGraphemeChar(uint c)
        => (c & 0xC000_0000) == ManagedGraphemePool.CharFlagGrapheme;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsContinuationChar(uint c)
        => (c & 0xC000_0000) == ManagedGraphemePool.CharFlagContinuation;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GraphemeIdFromChar(uint c)
        => c & ManagedGraphemePool.GraphemeIdMask;

    #endregion

    #region IDisposable

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _currentRenderBuffer.Dispose();
        _nextRenderBuffer.Dispose();

        if (_ownsGraphemePool && _graphemePool is IDisposable disposablePool)
            disposablePool.Dispose();
    }

    #endregion
}
