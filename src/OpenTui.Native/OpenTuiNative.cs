using System.Runtime.InteropServices;

namespace OpenTui.Native;

/// <summary>
/// Raw P/Invoke bindings for the opentui native library.
/// All pointers are <see cref="nint"/> — use the SafeHandle types in <see cref="OpenTui.Native"/> for safe resource management.
/// </summary>
internal static partial class OpenTuiNative
{
    private const string LibName = "opentui";

    #region Callbacks

    /// <summary>Sets the global log callback function pointer.</summary>
    [LibraryImport(LibName, EntryPoint = "setLogCallback")]
    internal static partial void SetLogCallback(nint callback);

    /// <summary>Sets the global event callback function pointer.</summary>
    [LibraryImport(LibName, EntryPoint = "setEventCallback")]
    internal static partial void SetEventCallback(nint callback);

    #endregion

    #region Renderer

    /// <summary>Creates a new terminal renderer with the specified dimensions.</summary>
    [LibraryImport(LibName, EntryPoint = "createRenderer")]
    internal static partial nint CreateRenderer(uint cols, uint rows, [MarshalAs(UnmanagedType.U1)] bool useStdout, [MarshalAs(UnmanagedType.U1)] bool useAlt);

    /// <summary>Sets a terminal environment variable on the renderer (UTF-8 key/value as pointer+length).</summary>
    [LibraryImport(LibName, EntryPoint = "setTerminalEnvVar")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool SetTerminalEnvVar(nint renderer, nint key, nuint keyLen, nint value, nuint valueLen);

    /// <summary>Destroys a renderer and releases all associated resources.</summary>
    [LibraryImport(LibName, EntryPoint = "destroyRenderer")]
    internal static partial void RendererDestroy(nint renderer);

    /// <summary>Enables or disables threaded rendering.</summary>
    [LibraryImport(LibName, EntryPoint = "setUseThread")]
    internal static partial void SetUseThread(nint renderer, [MarshalAs(UnmanagedType.U1)] bool useThread);

    /// <summary>Sets the renderer background color (pointer to 4×float RGBA array).</summary>
    [LibraryImport(LibName, EntryPoint = "setBackgroundColor")]
    internal static partial void SetBackgroundColor(nint renderer, nint rgba);

    /// <summary>Sets the vertical render offset in rows.</summary>
    [LibraryImport(LibName, EntryPoint = "setRenderOffset")]
    internal static partial void SetRenderOffset(nint renderer, uint offset);

    /// <summary>Updates the renderer performance statistics.</summary>
    [LibraryImport(LibName, EntryPoint = "updateStats")]
    internal static partial void UpdateStats(nint renderer, double frameTime, uint nodeCount, double layoutTime);

    /// <summary>Updates the renderer memory usage statistics.</summary>
    [LibraryImport(LibName, EntryPoint = "updateMemoryStats")]
    internal static partial void UpdateMemoryStats(nint renderer, uint heapUsed, uint heapTotal, uint external);

    /// <summary>Renders the current frame to the terminal.</summary>
    [LibraryImport(LibName, EntryPoint = "render")]
    internal static partial void Render(nint renderer, [MarshalAs(UnmanagedType.U1)] bool forceFullRender);

    /// <summary>Gets the next (back) buffer for double-buffered rendering.</summary>
    [LibraryImport(LibName, EntryPoint = "getNextBuffer")]
    internal static partial nint GetNextBuffer(nint renderer);

    /// <summary>Gets the current (front) buffer.</summary>
    [LibraryImport(LibName, EntryPoint = "getCurrentBuffer")]
    internal static partial nint GetCurrentBuffer(nint renderer);

    /// <summary>Queries the terminal for pixel resolution (asynchronous response via event callback).</summary>
    [LibraryImport(LibName, EntryPoint = "queryPixelResolution")]
    internal static partial void QueryPixelResolution(nint renderer);

    /// <summary>Resizes the renderer to new column/row dimensions.</summary>
    [LibraryImport(LibName, EntryPoint = "resizeRenderer")]
    internal static partial void ResizeRenderer(nint renderer, uint cols, uint rows);

    /// <summary>Sets the cursor position and visibility.</summary>
    [LibraryImport(LibName, EntryPoint = "setCursorPosition")]
    internal static partial void SetCursorPosition(nint renderer, int x, int y, [MarshalAs(UnmanagedType.U1)] bool visible);

    /// <summary>Sets the cursor color (pointer to 4×float RGBA array).</summary>
    [LibraryImport(LibName, EntryPoint = "setCursorColor")]
    internal static partial void SetCursorColor(nint renderer, nint rgba);

    /// <summary>Gets the current cursor state into the provided output struct.</summary>
    [LibraryImport(LibName, EntryPoint = "getCursorState")]
    internal static partial void GetCursorState(nint renderer, nint outState);

    /// <summary>Sets cursor style options from the provided options struct.</summary>
    [LibraryImport(LibName, EntryPoint = "setCursorStyleOptions")]
    internal static partial void SetCursorStyleOptions(nint renderer, nint options);

    /// <summary>Enables or disables the debug overlay in the specified corner.</summary>
    [LibraryImport(LibName, EntryPoint = "setDebugOverlay")]
    internal static partial void SetDebugOverlay(nint renderer, [MarshalAs(UnmanagedType.U1)] bool enabled, byte corner);

    /// <summary>Clears the entire terminal screen.</summary>
    [LibraryImport(LibName, EntryPoint = "clearTerminal")]
    internal static partial void ClearTerminal(nint renderer);

    /// <summary>Sets the terminal window title (UTF-8 pointer + byte length).</summary>
    [LibraryImport(LibName, EntryPoint = "setTerminalTitle")]
    internal static partial void SetTerminalTitle(nint renderer, nint title, nuint titleLen);

    /// <summary>Copies data to the system clipboard via OSC 52 escape sequence.</summary>
    [LibraryImport(LibName, EntryPoint = "copyToClipboardOSC52")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool CopyToClipboardOSC52(nint renderer, byte reg, nint data, nuint dataLen);

    /// <summary>Clears the system clipboard via OSC 52 escape sequence.</summary>
    [LibraryImport(LibName, EntryPoint = "clearClipboardOSC52")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool ClearClipboardOSC52(nint renderer, byte reg);

    /// <summary>Restores the terminal to its original mode settings.</summary>
    [LibraryImport(LibName, EntryPoint = "restoreTerminalModes")]
    internal static partial void RestoreTerminalModes(nint renderer);

    /// <summary>Enables mouse tracking, optionally using SGR extended mode.</summary>
    [LibraryImport(LibName, EntryPoint = "enableMouse")]
    internal static partial void EnableMouse(nint renderer, [MarshalAs(UnmanagedType.U1)] bool sgr);

    /// <summary>Disables mouse tracking.</summary>
    [LibraryImport(LibName, EntryPoint = "disableMouse")]
    internal static partial void DisableMouse(nint renderer);

    /// <summary>Enables the Kitty keyboard protocol with the specified flags.</summary>
    [LibraryImport(LibName, EntryPoint = "enableKittyKeyboard")]
    internal static partial void EnableKittyKeyboard(nint renderer, byte flags);

    /// <summary>Disables the Kitty keyboard protocol.</summary>
    [LibraryImport(LibName, EntryPoint = "disableKittyKeyboard")]
    internal static partial void DisableKittyKeyboard(nint renderer);

    /// <summary>Sets the Kitty keyboard protocol flags.</summary>
    [LibraryImport(LibName, EntryPoint = "setKittyKeyboardFlags")]
    internal static partial void SetKittyKeyboardFlags(nint renderer, byte flags);

    /// <summary>Gets the current Kitty keyboard protocol flags.</summary>
    [LibraryImport(LibName, EntryPoint = "getKittyKeyboardFlags")]
    internal static partial byte GetKittyKeyboardFlags(nint renderer);

    /// <summary>Sets up the terminal for rendering, optionally using the alternate screen buffer.</summary>
    [LibraryImport(LibName, EntryPoint = "setupTerminal")]
    internal static partial void SetupTerminal(nint renderer, [MarshalAs(UnmanagedType.U1)] bool alternateBuf);

    /// <summary>Suspends the renderer, restoring the terminal to a normal state.</summary>
    [LibraryImport(LibName, EntryPoint = "suspendRenderer")]
    internal static partial void SuspendRenderer(nint renderer);

    /// <summary>Resumes the renderer after a suspension.</summary>
    [LibraryImport(LibName, EntryPoint = "resumeRenderer")]
    internal static partial void ResumeRenderer(nint renderer);

    /// <summary>Writes raw bytes to the terminal output.</summary>
    [LibraryImport(LibName, EntryPoint = "writeOut")]
    internal static partial void WriteOut(nint renderer, nint data, ulong len);

    /// <summary>Retrieves terminal capabilities into the provided output struct.</summary>
    [LibraryImport(LibName, EntryPoint = "getTerminalCapabilities")]
    internal static partial void GetTerminalCapabilities(nint renderer, nint outCaps);

    /// <summary>Processes a terminal capability response.</summary>
    [LibraryImport(LibName, EntryPoint = "processCapabilityResponse")]
    internal static partial void ProcessCapabilityResponse(nint renderer, nint data, nuint len);

    /// <summary>Dumps the internal buffers to the specified file descriptor for debugging.</summary>
    [LibraryImport(LibName, EntryPoint = "dumpBuffers")]
    internal static partial void DumpBuffers(nint renderer, long fd);

    /// <summary>Dumps the stdout buffer to the specified file descriptor for debugging.</summary>
    [LibraryImport(LibName, EntryPoint = "dumpStdoutBuffer")]
    internal static partial void DumpStdoutBuffer(nint renderer, long fd);

    #endregion

    #region Buffer (Optimized Buffer)

    /// <summary>Creates an optimized buffer with the specified dimensions and width calculation method.</summary>
    [LibraryImport(LibName, EntryPoint = "createOptimizedBuffer")]
    internal static partial nint CreateOptimizedBuffer(uint w, uint h, [MarshalAs(UnmanagedType.U1)] bool respectAlpha, byte widthMethod, nint id, nuint idLen);

    /// <summary>Destroys an optimized buffer and frees its memory.</summary>
    [LibraryImport(LibName, EntryPoint = "destroyOptimizedBuffer")]
    internal static partial void BufferDestroy(nint buffer);

    /// <summary>Draws a region from a source buffer into this buffer at the specified position.</summary>
    [LibraryImport(LibName, EntryPoint = "drawFrameBuffer")]
    internal static partial void DrawFrameBuffer(nint buffer, int x, int y, nint srcBuffer, uint srcX, uint srcY, uint w, uint h);

    /// <summary>Gets the width of the buffer in columns.</summary>
    [LibraryImport(LibName, EntryPoint = "getBufferWidth")]
    internal static partial uint GetBufferWidth(nint buffer);

    /// <summary>Gets the height of the buffer in rows.</summary>
    [LibraryImport(LibName, EntryPoint = "getBufferHeight")]
    internal static partial uint GetBufferHeight(nint buffer);

    /// <summary>Clears the entire buffer, optionally filling with the specified background color.</summary>
    [LibraryImport(LibName, EntryPoint = "bufferClear")]
    internal static partial void BufferClear(nint buffer, nint bgColor);

    /// <summary>Gets a pointer to the buffer's character data array.</summary>
    [LibraryImport(LibName, EntryPoint = "bufferGetCharPtr")]
    internal static partial nint BufferGetCharPtr(nint buffer);

    /// <summary>Gets a pointer to the buffer's foreground color data array.</summary>
    [LibraryImport(LibName, EntryPoint = "bufferGetFgPtr")]
    internal static partial nint BufferGetFgPtr(nint buffer);

    /// <summary>Gets a pointer to the buffer's background color data array.</summary>
    [LibraryImport(LibName, EntryPoint = "bufferGetBgPtr")]
    internal static partial nint BufferGetBgPtr(nint buffer);

    /// <summary>Gets a pointer to the buffer's cell attributes array.</summary>
    [LibraryImport(LibName, EntryPoint = "bufferGetAttributesPtr")]
    internal static partial nint BufferGetAttributesPtr(nint buffer);

    /// <summary>Gets whether the buffer respects alpha transparency.</summary>
    [LibraryImport(LibName, EntryPoint = "bufferGetRespectAlpha")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool BufferGetRespectAlpha(nint buffer);

    /// <summary>Sets whether the buffer respects alpha transparency.</summary>
    [LibraryImport(LibName, EntryPoint = "bufferSetRespectAlpha")]
    internal static partial void BufferSetRespectAlpha(nint buffer, [MarshalAs(UnmanagedType.U1)] bool value);

    /// <summary>Gets the buffer's identifier string into the output buffer. Returns the actual byte length.</summary>
    [LibraryImport(LibName, EntryPoint = "bufferGetId")]
    internal static partial nuint BufferGetId(nint buffer, nint outId, nuint maxLen);

    /// <summary>Gets the real character size accounting for wide/combining characters.</summary>
    [LibraryImport(LibName, EntryPoint = "bufferGetRealCharSize")]
    internal static partial uint BufferGetRealCharSize(nint buffer);

    /// <summary>Writes pre-resolved character data into the buffer.</summary>
    [LibraryImport(LibName, EntryPoint = "bufferWriteResolvedChars")]
    internal static partial uint BufferWriteResolvedChars(nint buffer, nint chars, nuint len, [MarshalAs(UnmanagedType.U1)] bool append);

    /// <summary>Draws text from a text view into the buffer at the given position with styling.</summary>
    [LibraryImport(LibName, EntryPoint = "bufferDrawText")]
    internal static partial void BufferDrawText(nint buffer, nint textView, uint x, uint y, uint maxWidth, nint fg, nint bg, uint attrs);

    /// <summary>Sets a single cell with alpha blending applied.</summary>
    [LibraryImport(LibName, EntryPoint = "bufferSetCellWithAlphaBlending")]
    internal static partial void BufferSetCellWithAlphaBlending(nint buffer, uint x, uint y, uint codepoint, nint fg, nint bg, uint attrs);

    /// <summary>Sets a single cell in the buffer.</summary>
    [LibraryImport(LibName, EntryPoint = "bufferSetCell")]
    internal static partial void BufferSetCell(nint buffer, uint x, uint y, uint codepoint, nint fg, nint bg, uint attrs);

    /// <summary>Fills a rectangular region with the specified color.</summary>
    [LibraryImport(LibName, EntryPoint = "bufferFillRect")]
    internal static partial void BufferFillRect(nint buffer, uint x, uint y, uint w, uint h, nint color);

    /// <summary>Applies a color matrix transformation to the buffer within a region.</summary>
    [LibraryImport(LibName, EntryPoint = "bufferColorMatrix")]
    internal static partial void BufferColorMatrix(nint buffer, nint matrix, nint region, nuint regionLen, float opacity, byte channel);

    /// <summary>Applies a uniform color matrix transformation to the entire buffer.</summary>
    [LibraryImport(LibName, EntryPoint = "bufferColorMatrixUniform")]
    internal static partial void BufferColorMatrixUniform(nint buffer, nint matrix, float opacity, byte channel);

    /// <summary>Resizes the buffer to new dimensions.</summary>
    [LibraryImport(LibName, EntryPoint = "bufferResize")]
    internal static partial void BufferResize(nint buffer, uint w, uint h);

    /// <summary>Draws a super-sampled buffer at the specified position.</summary>
    [LibraryImport(LibName, EntryPoint = "bufferDrawSuperSampleBuffer")]
    internal static partial void BufferDrawSuperSampleBuffer(nint buffer, uint x, uint y, nint data, nuint dataLen, byte sampleFactor, uint width);

    /// <summary>Draws a packed pixel buffer at the specified position and dimensions.</summary>
    [LibraryImport(LibName, EntryPoint = "bufferDrawPackedBuffer")]
    internal static partial void BufferDrawPackedBuffer(nint buffer, nint data, nuint dataLen, uint x, uint y, uint w, uint h);

    /// <summary>Draws a grayscale pixel buffer using the specified foreground/background colors.</summary>
    [LibraryImport(LibName, EntryPoint = "bufferDrawGrayscaleBuffer")]
    internal static partial void BufferDrawGrayscaleBuffer(nint buffer, int x, int y, nint data, uint w, uint h, nint fg, nint bg);

    /// <summary>Draws a supersampled grayscale buffer using the specified foreground/background colors.</summary>
    [LibraryImport(LibName, EntryPoint = "bufferDrawGrayscaleBufferSupersampled")]
    internal static partial void BufferDrawGrayscaleBufferSupersampled(nint buffer, int x, int y, nint data, uint w, uint h, nint fg, nint bg);

    /// <summary>Draws a grid with specified dimensions, colors, and border characters.</summary>
    [LibraryImport(LibName, EntryPoint = "bufferDrawGrid")]
    internal static partial void BufferDrawGrid(nint buffer, nint gridDef, nint widths, nint heights, nint colors, uint colorCount, nint borderChars, uint borderCharCount, nint borderColor);

    /// <summary>Draws a box with borders and an optional title.</summary>
    [LibraryImport(LibName, EntryPoint = "bufferDrawBox")]
    internal static partial void BufferDrawBox(nint buffer, int x, int y, uint w, uint h, nint borderChars, uint borderCharCount, nint borderColor, nint titlePtr, nint titleColor, uint titleLen, nint titleAlign, uint titleAlignVal);

    /// <summary>Pushes a scissor (clipping) rectangle onto the buffer's clip stack.</summary>
    [LibraryImport(LibName, EntryPoint = "bufferPushScissorRect")]
    internal static partial void BufferPushScissorRect(nint buffer, int x, int y, uint w, uint h);

    /// <summary>Pops the most recent scissor rectangle from the buffer's clip stack.</summary>
    [LibraryImport(LibName, EntryPoint = "bufferPopScissorRect")]
    internal static partial void BufferPopScissorRect(nint buffer);

    /// <summary>Clears all scissor rectangles from the buffer's clip stack.</summary>
    [LibraryImport(LibName, EntryPoint = "bufferClearScissorRects")]
    internal static partial void BufferClearScissorRects(nint buffer);

    /// <summary>Pushes an opacity value onto the buffer's opacity stack.</summary>
    [LibraryImport(LibName, EntryPoint = "bufferPushOpacity")]
    internal static partial void BufferPushOpacity(nint buffer, float opacity);

    /// <summary>Pops the most recent opacity value from the buffer's opacity stack.</summary>
    [LibraryImport(LibName, EntryPoint = "bufferPopOpacity")]
    internal static partial void BufferPopOpacity(nint buffer);

    /// <summary>Gets the current effective opacity value.</summary>
    [LibraryImport(LibName, EntryPoint = "bufferGetCurrentOpacity")]
    internal static partial float BufferGetCurrentOpacity(nint buffer);

    /// <summary>Clears the entire opacity stack, resetting to full opacity.</summary>
    [LibraryImport(LibName, EntryPoint = "bufferClearOpacity")]
    internal static partial void BufferClearOpacity(nint buffer);

    /// <summary>Draws a single character at the specified cell position with styling.</summary>
    [LibraryImport(LibName, EntryPoint = "bufferDrawChar")]
    internal static partial void BufferDrawChar(nint buffer, uint x, uint y, uint codepoint, nint fg, nint bg, uint attrs);

    /// <summary>Draws a text buffer view into the optimized buffer at the specified position.</summary>
    [LibraryImport(LibName, EntryPoint = "bufferDrawTextBufferView")]
    internal static partial void BufferDrawTextBufferView(nint buffer, nint textBufferView, int x, int y);

    /// <summary>Draws an editor view into the optimized buffer at the specified position.</summary>
    [LibraryImport(LibName, EntryPoint = "bufferDrawEditorView")]
    internal static partial void BufferDrawEditorView(nint buffer, nint editorView, int x, int y);

    #endregion

    #region Link

    // NOTE: The native API does not expose an explicit link store destroy function.
    // Link stores may be owned and freed by the renderer. The LinkHandle SafeHandle
    // calls LinkDestroy, which is declared here for compilation but may be a no-op
    // or internally managed.

    /// <summary>Allocates a new link entry in the link store for the given URL hash.</summary>
    [LibraryImport(LibName, EntryPoint = "linkAlloc")]
    internal static partial uint LinkAlloc(nint linkStore, uint urlHash);

    /// <summary>Gets the URL string for a link ID into the output buffer. Returns the actual byte length.</summary>
    [LibraryImport(LibName, EntryPoint = "linkGetUrl")]
    internal static partial uint LinkGetUrl(uint linkId, nint outBuf, uint maxLen);

    /// <summary>Returns a new attributes value with the specified link ID embedded.</summary>
    [LibraryImport(LibName, EntryPoint = "attributesWithLink")]
    internal static partial uint AttributesWithLink(uint attrs, uint linkId);

    /// <summary>Extracts the link ID from a cell attributes value.</summary>
    [LibraryImport(LibName, EntryPoint = "attributesGetLinkId")]
    internal static partial uint AttributesGetLinkId(uint attrs);

    #endregion

    #region Hit Grid

    // NOTE: The native API does not expose an explicit hit grid destroy function.
    // Hit grids may be owned and freed by the renderer. The HitGridHandle SafeHandle
    // calls HitGridDestroy, which is declared here for compilation but may be a no-op
    // or internally managed.

    /// <summary>Adds a rectangular hit region to the hit grid.</summary>
    [LibraryImport(LibName, EntryPoint = "addToHitGrid")]
    internal static partial void AddToHitGrid(nint hitGrid, int x, int y, uint w, uint h, uint id);

    /// <summary>Clears all hit regions from the current hit grid.</summary>
    [LibraryImport(LibName, EntryPoint = "clearCurrentHitGrid")]
    internal static partial void ClearCurrentHitGrid(nint hitGrid);

    /// <summary>Pushes a scissor (clipping) rectangle onto the hit grid's clip stack.</summary>
    [LibraryImport(LibName, EntryPoint = "hitGridPushScissorRect")]
    internal static partial void HitGridPushScissorRect(nint hitGrid, int x, int y, uint w, uint h);

    /// <summary>Pops the most recent scissor rectangle from the hit grid's clip stack.</summary>
    [LibraryImport(LibName, EntryPoint = "hitGridPopScissorRect")]
    internal static partial void HitGridPopScissorRect(nint hitGrid);

    /// <summary>Clears all scissor rectangles from the hit grid's clip stack.</summary>
    [LibraryImport(LibName, EntryPoint = "hitGridClearScissorRects")]
    internal static partial void HitGridClearScissorRects(nint hitGrid);

    /// <summary>Adds a hit region to the current hit grid, clipped by the active scissor rectangles.</summary>
    [LibraryImport(LibName, EntryPoint = "addToCurrentHitGridClipped")]
    internal static partial void AddToCurrentHitGridClipped(nint hitGrid, int x, int y, uint w, uint h, uint id);

    /// <summary>Tests whether the given coordinates hit any region, returning the region ID or 0.</summary>
    [LibraryImport(LibName, EntryPoint = "checkHit")]
    internal static partial uint CheckHit(nint hitGrid, uint x, uint y);

    /// <summary>Gets whether the hit grid has been modified since the last check.</summary>
    [LibraryImport(LibName, EntryPoint = "getHitGridDirty")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool GetHitGridDirty(nint hitGrid);

    /// <summary>Dumps the hit grid contents for debugging.</summary>
    [LibraryImport(LibName, EntryPoint = "dumpHitGrid")]
    internal static partial void DumpHitGrid(nint hitGrid);

    #endregion

    #region Text Buffer

    /// <summary>Creates a new text buffer with the specified width calculation method.</summary>
    [LibraryImport(LibName, EntryPoint = "createTextBuffer")]
    internal static partial nint CreateTextBuffer(byte widthMethod);

    /// <summary>Destroys a text buffer and frees its memory.</summary>
    [LibraryImport(LibName, EntryPoint = "destroyTextBuffer")]
    internal static partial void TextBufferDestroy(nint textBuffer);

    /// <summary>Gets the character length of the text buffer contents.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferGetLength")]
    internal static partial uint TextBufferGetLength(nint textBuffer);

    /// <summary>Gets the byte size of the text buffer contents.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferGetByteSize")]
    internal static partial uint TextBufferGetByteSize(nint textBuffer);

    /// <summary>Resets the text buffer to its initial state.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferReset")]
    internal static partial void TextBufferReset(nint textBuffer);

    /// <summary>Clears all content from the text buffer.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferClear")]
    internal static partial void TextBufferClear(nint textBuffer);

    /// <summary>Sets the default foreground color (pointer to 4×float RGBA array).</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferSetDefaultFg")]
    internal static partial void TextBufferSetDefaultFg(nint textBuffer, nint rgba);

    /// <summary>Sets the default background color (pointer to 4×float RGBA array).</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferSetDefaultBg")]
    internal static partial void TextBufferSetDefaultBg(nint textBuffer, nint rgba);

    /// <summary>Sets the default cell attributes.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferSetDefaultAttributes")]
    internal static partial void TextBufferSetDefaultAttributes(nint textBuffer, nint attrs);

    /// <summary>Resets all default styling to initial values.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferResetDefaults")]
    internal static partial void TextBufferResetDefaults(nint textBuffer);

    /// <summary>Gets the current tab display width.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferGetTabWidth")]
    internal static partial byte TextBufferGetTabWidth(nint textBuffer);

    /// <summary>Sets the tab display width.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferSetTabWidth")]
    internal static partial void TextBufferSetTabWidth(nint textBuffer, byte width);

    /// <summary>Registers a memory buffer and returns its ID.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferRegisterMemBuffer")]
    internal static partial ushort TextBufferRegisterMemBuffer(nint textBuffer, nint data, nuint len, [MarshalAs(UnmanagedType.U1)] bool copy);

    /// <summary>Replaces an existing registered memory buffer by ID.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferReplaceMemBuffer")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool TextBufferReplaceMemBuffer(nint textBuffer, byte id, nint data, nuint len, [MarshalAs(UnmanagedType.U1)] bool copy);

    /// <summary>Clears all registered memory buffers.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferClearMemRegistry")]
    internal static partial void TextBufferClearMemRegistry(nint textBuffer);

    /// <summary>Sets the text buffer content from a registered memory buffer.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferSetTextFromMem")]
    internal static partial void TextBufferSetTextFromMem(nint textBuffer, byte memId);

    /// <summary>Appends UTF-8 text (pointer + byte length) to the text buffer.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferAppend")]
    internal static partial void TextBufferAppend(nint textBuffer, nint text, nuint len);

    /// <summary>Appends text from a registered memory buffer.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferAppendFromMemId")]
    internal static partial void TextBufferAppendFromMemId(nint textBuffer, byte memId);

    /// <summary>Loads content from a file into the text buffer (UTF-8 path + byte length).</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferLoadFile")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool TextBufferLoadFile(nint textBuffer, nint path, nuint pathLen);

    /// <summary>Sets styled text content from a serialized data buffer.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferSetStyledText")]
    internal static partial void TextBufferSetStyledText(nint textBuffer, nint data, nuint len);

    /// <summary>Gets the number of lines in the text buffer.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferGetLineCount")]
    internal static partial uint TextBufferGetLineCount(nint textBuffer);

    /// <summary>Gets the plain text content into the output buffer. Returns the actual byte length.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferGetPlainText")]
    internal static partial nuint TextBufferGetPlainText(nint textBuffer, nint outBuf, nuint maxLen);

    /// <summary>Adds a highlight by character range using the provided highlight definition.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferAddHighlightByCharRange")]
    internal static partial void TextBufferAddHighlightByCharRange(nint textBuffer, nint highlight);

    /// <summary>Adds a highlight to a specific line using the provided highlight definition.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferAddHighlight")]
    internal static partial void TextBufferAddHighlight(nint textBuffer, uint line, nint highlight);

    /// <summary>Removes all highlights that match the given reference ID.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferRemoveHighlightsByRef")]
    internal static partial void TextBufferRemoveHighlightsByRef(nint textBuffer, ushort hlRef);

    /// <summary>Clears all highlights from a specific line.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferClearLineHighlights")]
    internal static partial void TextBufferClearLineHighlights(nint textBuffer, uint line);

    /// <summary>Clears all highlights from all lines.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferClearAllHighlights")]
    internal static partial void TextBufferClearAllHighlights(nint textBuffer);

    /// <summary>Sets the syntax style used for rendering.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferSetSyntaxStyle")]
    internal static partial void TextBufferSetSyntaxStyle(nint textBuffer, nint syntaxStyle);

    /// <summary>Gets a pointer to the highlight array for a specific line. Writes the count to <paramref name="outCount"/>.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferGetLineHighlightsPtr")]
    internal static partial nint TextBufferGetLineHighlightsPtr(nint textBuffer, uint line, nint outCount);

    /// <summary>Frees a previously obtained line highlights array.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferFreeLineHighlights")]
    internal static partial void TextBufferFreeLineHighlights(nint data, nuint count);

    /// <summary>Gets the total number of highlights across all lines.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferGetHighlightCount")]
    internal static partial uint TextBufferGetHighlightCount(nint textBuffer);

    /// <summary>Gets a range of text by character offset into the output buffer. Returns the actual byte length.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferGetTextRange")]
    internal static partial nuint TextBufferGetTextRange(nint textBuffer, uint start, uint end, nint outBuf, nuint maxLen);

    /// <summary>Gets a range of text by row/column coordinates into the output buffer. Returns the actual byte length.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferGetTextRangeByCoords")]
    internal static partial nuint TextBufferGetTextRangeByCoords(nint textBuffer, uint startRow, uint startCol, uint endRow, uint endCol, nint outBuf, nuint maxLen);

    #endregion

    #region Text Buffer View

    /// <summary>Creates a new text buffer view for the given text buffer.</summary>
    [LibraryImport(LibName, EntryPoint = "createTextBufferView")]
    internal static partial nint CreateTextBufferView(nint textBuffer);

    /// <summary>Destroys a text buffer view and frees its memory.</summary>
    [LibraryImport(LibName, EntryPoint = "destroyTextBufferView")]
    internal static partial void TextBufferViewDestroy(nint view);

    /// <summary>Sets the text selection by character offsets with selection colors.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferViewSetSelection")]
    internal static partial void TextBufferViewSetSelection(nint view, uint startOffset, uint endOffset, nint selFg, nint selBg);

    /// <summary>Resets (clears) the current text selection.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferViewResetSelection")]
    internal static partial void TextBufferViewResetSelection(nint view);

    /// <summary>Gets the current selection info as a packed 64-bit value.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferViewGetSelectionInfo")]
    internal static partial ulong TextBufferViewGetSelectionInfo(nint view);

    /// <summary>Sets a local (visual coordinate) selection with selection colors.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferViewSetLocalSelection")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool TextBufferViewSetLocalSelection(nint view, int startX, int startY, int endX, int endY, nint selFg, nint selBg);

    /// <summary>Updates the end offset of the current selection.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferViewUpdateSelection")]
    internal static partial void TextBufferViewUpdateSelection(nint view, uint newEnd, nint selFg, nint selBg);

    /// <summary>Updates the local (visual coordinate) selection extent.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferViewUpdateLocalSelection")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool TextBufferViewUpdateLocalSelection(nint view, int startX, int startY, int endX, int endY, nint selFg, nint selBg);

    /// <summary>Resets the local (visual coordinate) selection.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferViewResetLocalSelection")]
    internal static partial void TextBufferViewResetLocalSelection(nint view);

    /// <summary>Sets the wrap width for line wrapping.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferViewSetWrapWidth")]
    internal static partial void TextBufferViewSetWrapWidth(nint view, uint width);

    /// <summary>Sets the line wrap mode.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferViewSetWrapMode")]
    internal static partial void TextBufferViewSetWrapMode(nint view, byte mode);

    /// <summary>Sets the viewport size in columns and rows.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferViewSetViewportSize")]
    internal static partial void TextBufferViewSetViewportSize(nint view, uint w, uint h);

    /// <summary>Sets the viewport position and size.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferViewSetViewport")]
    internal static partial void TextBufferViewSetViewport(nint view, uint x, uint y, uint w, uint h);

    /// <summary>Gets the number of virtual (wrapped) lines.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferViewGetVirtualLineCount")]
    internal static partial uint TextBufferViewGetVirtualLineCount(nint view);

    /// <summary>Gets line information directly into the output struct.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferViewGetLineInfoDirect")]
    internal static partial void TextBufferViewGetLineInfoDirect(nint view, nint outInfo);

    /// <summary>Gets logical line information directly into the output struct.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferViewGetLogicalLineInfoDirect")]
    internal static partial void TextBufferViewGetLogicalLineInfoDirect(nint view, nint outInfo);

    /// <summary>Gets the selected text into the output buffer. Returns the actual byte length.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferViewGetSelectedText")]
    internal static partial nuint TextBufferViewGetSelectedText(nint view, nint outBuf, nuint maxLen);

    /// <summary>Gets the plain text visible in the view into the output buffer. Returns the actual byte length.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferViewGetPlainText")]
    internal static partial nuint TextBufferViewGetPlainText(nint view, nint outBuf, nuint maxLen);

    /// <summary>Sets the Unicode codepoint used to display tab indicators.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferViewSetTabIndicator")]
    internal static partial void TextBufferViewSetTabIndicator(nint view, uint codepoint);

    /// <summary>Sets the color used for tab indicator characters (pointer to 4×float RGBA).</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferViewSetTabIndicatorColor")]
    internal static partial void TextBufferViewSetTabIndicatorColor(nint view, nint rgba);

    /// <summary>Enables or disables line truncation instead of wrapping.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferViewSetTruncate")]
    internal static partial void TextBufferViewSetTruncate(nint view, [MarshalAs(UnmanagedType.U1)] bool truncate);

    /// <summary>Measures text content to fit within the given dimensions. Writes results to <paramref name="outResult"/>.</summary>
    [LibraryImport(LibName, EntryPoint = "textBufferViewMeasureForDimensions")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool TextBufferViewMeasureForDimensions(nint view, uint w, uint h, nint outResult);

    #endregion

    #region Editor View

    /// <summary>Creates a new editor view for the given edit buffer with the specified dimensions.</summary>
    [LibraryImport(LibName, EntryPoint = "createEditorView")]
    internal static partial nint CreateEditorView(nint editBuffer, uint w, uint h);

    /// <summary>Destroys an editor view and frees its memory.</summary>
    [LibraryImport(LibName, EntryPoint = "destroyEditorView")]
    internal static partial void EditorViewDestroy(nint editorView);

    /// <summary>Sets the viewport size of the editor view.</summary>
    [LibraryImport(LibName, EntryPoint = "editorViewSetViewportSize")]
    internal static partial void EditorViewSetViewportSize(nint ev, uint w, uint h);

    /// <summary>Sets the viewport position and size, optionally clamping to content bounds.</summary>
    [LibraryImport(LibName, EntryPoint = "editorViewSetViewport")]
    internal static partial void EditorViewSetViewport(nint ev, uint x, uint y, uint w, uint h, [MarshalAs(UnmanagedType.U1)] bool clamp);

    /// <summary>Gets the current viewport position and size.</summary>
    [LibraryImport(LibName, EntryPoint = "editorViewGetViewport")]
    internal static partial void EditorViewGetViewport(nint ev, nint outX, nint outY, nint outW, nint outH);

    /// <summary>Sets the scroll margin as a fraction of viewport height.</summary>
    [LibraryImport(LibName, EntryPoint = "editorViewSetScrollMargin")]
    internal static partial void EditorViewSetScrollMargin(nint ev, float margin);

    /// <summary>Sets the line wrap mode for the editor view.</summary>
    [LibraryImport(LibName, EntryPoint = "editorViewSetWrapMode")]
    internal static partial void EditorViewSetWrapMode(nint ev, byte mode);

    /// <summary>Gets the number of virtual (wrapped) lines visible in the viewport.</summary>
    [LibraryImport(LibName, EntryPoint = "editorViewGetVirtualLineCount")]
    internal static partial uint EditorViewGetVirtualLineCount(nint ev);

    /// <summary>Gets the total number of virtual (wrapped) lines in the entire document.</summary>
    [LibraryImport(LibName, EntryPoint = "editorViewGetTotalVirtualLineCount")]
    internal static partial uint EditorViewGetTotalVirtualLineCount(nint ev);

    /// <summary>Gets the underlying text buffer view from the editor view.</summary>
    [LibraryImport(LibName, EntryPoint = "editorViewGetTextBufferView")]
    internal static partial nint EditorViewGetTextBufferView(nint ev);

    /// <summary>Gets line information directly into the output struct.</summary>
    [LibraryImport(LibName, EntryPoint = "editorViewGetLineInfoDirect")]
    internal static partial void EditorViewGetLineInfoDirect(nint ev, nint outInfo);

    /// <summary>Gets logical line information directly into the output struct.</summary>
    [LibraryImport(LibName, EntryPoint = "editorViewGetLogicalLineInfoDirect")]
    internal static partial void EditorViewGetLogicalLineInfoDirect(nint ev, nint outInfo);

    /// <summary>Sets the text selection by character offsets with selection colors.</summary>
    [LibraryImport(LibName, EntryPoint = "editorViewSetSelection")]
    internal static partial void EditorViewSetSelection(nint ev, uint start, uint end, nint selFg, nint selBg);

    /// <summary>Resets (clears) the current text selection.</summary>
    [LibraryImport(LibName, EntryPoint = "editorViewResetSelection")]
    internal static partial void EditorViewResetSelection(nint ev);

    /// <summary>Gets the current selection as a packed 64-bit value.</summary>
    [LibraryImport(LibName, EntryPoint = "editorViewGetSelection")]
    internal static partial ulong EditorViewGetSelection(nint ev);

    /// <summary>Sets a local (visual coordinate) selection with options for extend and visual mode.</summary>
    [LibraryImport(LibName, EntryPoint = "editorViewSetLocalSelection")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool EditorViewSetLocalSelection(nint ev, int sx, int sy, int ex, int ey, nint selFg, nint selBg, [MarshalAs(UnmanagedType.U1)] bool extend, [MarshalAs(UnmanagedType.U1)] bool visual);

    /// <summary>Updates the end offset of the current selection.</summary>
    [LibraryImport(LibName, EntryPoint = "editorViewUpdateSelection")]
    internal static partial void EditorViewUpdateSelection(nint ev, uint newEnd, nint selFg, nint selBg);

    /// <summary>Updates the local (visual coordinate) selection extent with extend/visual options.</summary>
    [LibraryImport(LibName, EntryPoint = "editorViewUpdateLocalSelection")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool EditorViewUpdateLocalSelection(nint ev, int sx, int sy, int ex, int ey, nint selFg, nint selBg, [MarshalAs(UnmanagedType.U1)] bool extend, [MarshalAs(UnmanagedType.U1)] bool visual);

    /// <summary>Resets the local (visual coordinate) selection.</summary>
    [LibraryImport(LibName, EntryPoint = "editorViewResetLocalSelection")]
    internal static partial void EditorViewResetLocalSelection(nint ev);

    /// <summary>Gets the selected text as bytes into the output buffer. Returns the actual byte length.</summary>
    [LibraryImport(LibName, EntryPoint = "editorViewGetSelectedTextBytes")]
    internal static partial nuint EditorViewGetSelectedTextBytes(nint ev, nint outBuf, nuint maxLen);

    /// <summary>Gets the logical and visual cursor positions.</summary>
    [LibraryImport(LibName, EntryPoint = "editorViewGetCursor")]
    internal static partial void EditorViewGetCursor(nint ev, nint outLogical, nint outVisual);

    /// <summary>Gets the editor text into the output buffer. Returns the actual byte length.</summary>
    [LibraryImport(LibName, EntryPoint = "editorViewGetText")]
    internal static partial nuint EditorViewGetText(nint ev, nint outBuf, nuint maxLen);

    /// <summary>Gets the visual cursor position into the output struct.</summary>
    [LibraryImport(LibName, EntryPoint = "editorViewGetVisualCursor")]
    internal static partial void EditorViewGetVisualCursor(nint ev, nint outVisualCursor);

    /// <summary>Moves the cursor up one visual line.</summary>
    [LibraryImport(LibName, EntryPoint = "editorViewMoveUpVisual")]
    internal static partial void EditorViewMoveUpVisual(nint ev);

    /// <summary>Moves the cursor down one visual line.</summary>
    [LibraryImport(LibName, EntryPoint = "editorViewMoveDownVisual")]
    internal static partial void EditorViewMoveDownVisual(nint ev);

    /// <summary>Deletes the currently selected text.</summary>
    [LibraryImport(LibName, EntryPoint = "editorViewDeleteSelectedText")]
    internal static partial void EditorViewDeleteSelectedText(nint ev);

    /// <summary>Sets the cursor position by character offset.</summary>
    [LibraryImport(LibName, EntryPoint = "editorViewSetCursorByOffset")]
    internal static partial void EditorViewSetCursorByOffset(nint ev, uint offset);

    /// <summary>Gets the next word boundary cursor position.</summary>
    [LibraryImport(LibName, EntryPoint = "editorViewGetNextWordBoundary")]
    internal static partial void EditorViewGetNextWordBoundary(nint ev, nint outCursor);

    /// <summary>Gets the previous word boundary cursor position.</summary>
    [LibraryImport(LibName, EntryPoint = "editorViewGetPrevWordBoundary")]
    internal static partial void EditorViewGetPrevWordBoundary(nint ev, nint outCursor);

    /// <summary>Gets the end-of-line cursor position.</summary>
    [LibraryImport(LibName, EntryPoint = "editorViewGetEOL")]
    internal static partial void EditorViewGetEOL(nint ev, nint outCursor);

    /// <summary>Gets the visual start-of-line cursor position.</summary>
    [LibraryImport(LibName, EntryPoint = "editorViewGetVisualSOL")]
    internal static partial void EditorViewGetVisualSOL(nint ev, nint outCursor);

    /// <summary>Gets the visual end-of-line cursor position.</summary>
    [LibraryImport(LibName, EntryPoint = "editorViewGetVisualEOL")]
    internal static partial void EditorViewGetVisualEOL(nint ev, nint outCursor);

    /// <summary>Sets placeholder styled text from a serialized data buffer.</summary>
    [LibraryImport(LibName, EntryPoint = "editorViewSetPlaceholderStyledText")]
    internal static partial void EditorViewSetPlaceholderStyledText(nint ev, nint data, nuint len);

    /// <summary>Sets the Unicode codepoint used to display tab indicators.</summary>
    [LibraryImport(LibName, EntryPoint = "editorViewSetTabIndicator")]
    internal static partial void EditorViewSetTabIndicator(nint ev, uint codepoint);

    /// <summary>Sets the color for tab indicator characters (pointer to 4×float RGBA).</summary>
    [LibraryImport(LibName, EntryPoint = "editorViewSetTabIndicatorColor")]
    internal static partial void EditorViewSetTabIndicatorColor(nint ev, nint rgba);

    #endregion

    #region Edit Buffer

    /// <summary>Creates a new edit buffer with the specified width calculation method.</summary>
    [LibraryImport(LibName, EntryPoint = "createEditBuffer")]
    internal static partial nint CreateEditBuffer(byte widthMethod);

    /// <summary>Destroys an edit buffer and frees its memory.</summary>
    [LibraryImport(LibName, EntryPoint = "destroyEditBuffer")]
    internal static partial void EditBufferDestroy(nint editBuffer);

    /// <summary>Sets the entire text content of the edit buffer (UTF-8 pointer + byte length).</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferSetText")]
    internal static partial void EditBufferSetText(nint eb, nint text, nuint len);

    /// <summary>Sets the text content from a registered memory buffer.</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferSetTextFromMem")]
    internal static partial void EditBufferSetTextFromMem(nint eb, byte memId);

    /// <summary>Replaces the current text content (UTF-8 pointer + byte length).</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferReplaceText")]
    internal static partial void EditBufferReplaceText(nint eb, nint text, nuint len);

    /// <summary>Replaces the current text content from a registered memory buffer.</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferReplaceTextFromMem")]
    internal static partial void EditBufferReplaceTextFromMem(nint eb, byte memId);

    /// <summary>Gets the text content into the output buffer. Returns the actual byte length.</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferGetText")]
    internal static partial nuint EditBufferGetText(nint eb, nint outBuf, nuint maxLen);

    /// <summary>Inserts a character at the cursor position (UTF-8 pointer + byte length).</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferInsertChar")]
    internal static partial void EditBufferInsertChar(nint eb, nint charData, nuint charLen);

    /// <summary>Inserts text at the cursor position (UTF-8 pointer + byte length).</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferInsertText")]
    internal static partial void EditBufferInsertText(nint eb, nint text, nuint len);

    /// <summary>Deletes the character at the cursor position (forward delete).</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferDeleteChar")]
    internal static partial void EditBufferDeleteChar(nint eb);

    /// <summary>Deletes the character before the cursor position (backspace).</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferDeleteCharBackward")]
    internal static partial void EditBufferDeleteCharBackward(nint eb);

    /// <summary>Deletes a range of text specified by row/column coordinates.</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferDeleteRange")]
    internal static partial void EditBufferDeleteRange(nint eb, uint startRow, uint startCol, uint endRow, uint endCol);

    /// <summary>Inserts a newline at the cursor position.</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferNewLine")]
    internal static partial void EditBufferNewLine(nint eb);

    /// <summary>Deletes the current line.</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferDeleteLine")]
    internal static partial void EditBufferDeleteLine(nint eb);

    /// <summary>Moves the cursor one position to the left.</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferMoveCursorLeft")]
    internal static partial void EditBufferMoveCursorLeft(nint eb);

    /// <summary>Moves the cursor one position to the right.</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferMoveCursorRight")]
    internal static partial void EditBufferMoveCursorRight(nint eb);

    /// <summary>Moves the cursor one line up.</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferMoveCursorUp")]
    internal static partial void EditBufferMoveCursorUp(nint eb);

    /// <summary>Moves the cursor one line down.</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferMoveCursorDown")]
    internal static partial void EditBufferMoveCursorDown(nint eb);

    /// <summary>Moves the cursor to the specified line.</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferGotoLine")]
    internal static partial void EditBufferGotoLine(nint eb, uint line);

    /// <summary>Sets the cursor to the specified row and column.</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferSetCursor")]
    internal static partial void EditBufferSetCursor(nint eb, uint row, uint col);

    /// <summary>Sets the cursor to the specified line and column.</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferSetCursorToLineCol")]
    internal static partial void EditBufferSetCursorToLineCol(nint eb, uint line, uint col);

    /// <summary>Sets the cursor position by character offset.</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferSetCursorByOffset")]
    internal static partial void EditBufferSetCursorByOffset(nint eb, uint offset);

    /// <summary>Gets the current cursor position into the output struct.</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferGetCursorPosition")]
    internal static partial void EditBufferGetCursorPosition(nint eb, nint outCursor);

    /// <summary>Gets the unique identifier of the edit buffer.</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferGetId")]
    internal static partial ushort EditBufferGetId(nint eb);

    /// <summary>Gets the underlying text buffer from the edit buffer.</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferGetTextBuffer")]
    internal static partial nint EditBufferGetTextBuffer(nint eb);

    /// <summary>Dumps the internal rope structure for debugging.</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferDebugLogRope")]
    internal static partial void EditBufferDebugLogRope(nint eb);

    /// <summary>Undoes the last edit operation. Returns the undo description byte length.</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferUndo")]
    internal static partial nuint EditBufferUndo(nint eb, nint outBuf, nuint maxLen);

    /// <summary>Redoes the last undone operation. Returns the redo description byte length.</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferRedo")]
    internal static partial nuint EditBufferRedo(nint eb, nint outBuf, nuint maxLen);

    /// <summary>Gets whether an undo operation is available.</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferCanUndo")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool EditBufferCanUndo(nint eb);

    /// <summary>Gets whether a redo operation is available.</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferCanRedo")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool EditBufferCanRedo(nint eb);

    /// <summary>Clears the entire undo/redo history.</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferClearHistory")]
    internal static partial void EditBufferClearHistory(nint eb);

    /// <summary>Clears all content from the edit buffer.</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferClear")]
    internal static partial void EditBufferClear(nint eb);

    /// <summary>Gets the next word boundary cursor position.</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferGetNextWordBoundary")]
    internal static partial void EditBufferGetNextWordBoundary(nint eb, nint outCursor);

    /// <summary>Gets the previous word boundary cursor position.</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferGetPrevWordBoundary")]
    internal static partial void EditBufferGetPrevWordBoundary(nint eb, nint outCursor);

    /// <summary>Gets the end-of-line cursor position.</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferGetEOL")]
    internal static partial void EditBufferGetEOL(nint eb, nint outCursor);

    /// <summary>Converts a character offset to a row/column position.</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferOffsetToPosition")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool EditBufferOffsetToPosition(nint eb, uint offset, nint outCursor);

    /// <summary>Converts a row/column position to a character offset.</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferPositionToOffset")]
    internal static partial uint EditBufferPositionToOffset(nint eb, uint row, uint col);

    /// <summary>Gets the byte offset of the start of the specified line.</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferGetLineStartOffset")]
    internal static partial uint EditBufferGetLineStartOffset(nint eb, uint line);

    /// <summary>Gets a range of text by character offset into the output buffer. Returns the actual byte length.</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferGetTextRange")]
    internal static partial nuint EditBufferGetTextRange(nint eb, uint start, uint end, nint outBuf, nuint maxLen);

    /// <summary>Gets a range of text by row/column coordinates into the output buffer. Returns the actual byte length.</summary>
    [LibraryImport(LibName, EntryPoint = "editBufferGetTextRangeByCoords")]
    internal static partial nuint EditBufferGetTextRangeByCoords(nint eb, uint startRow, uint startCol, uint endRow, uint endCol, nint outBuf, nuint maxLen);

    #endregion

    #region Syntax Style

    /// <summary>Creates a new syntax style registry.</summary>
    [LibraryImport(LibName, EntryPoint = "createSyntaxStyle")]
    internal static partial nint CreateSyntaxStyle();

    /// <summary>Destroys a syntax style registry and frees its memory.</summary>
    [LibraryImport(LibName, EntryPoint = "destroySyntaxStyle")]
    internal static partial void SyntaxStyleDestroy(nint style);

    /// <summary>Registers a named syntax style with colors and attributes. Returns the style ID.</summary>
    [LibraryImport(LibName, EntryPoint = "syntaxStyleRegister")]
    internal static partial uint SyntaxStyleRegister(nint style, nint name, nuint nameLen, nint fg, nint bg, byte attrs);

    /// <summary>Resolves a syntax style ID by name (UTF-8 pointer + byte length).</summary>
    [LibraryImport(LibName, EntryPoint = "syntaxStyleResolveByName")]
    internal static partial uint SyntaxStyleResolveByName(nint style, nint name, nuint nameLen);

    /// <summary>Gets the total number of registered syntax styles.</summary>
    [LibraryImport(LibName, EntryPoint = "syntaxStyleGetStyleCount")]
    internal static partial nuint SyntaxStyleGetStyleCount(nint style);

    #endregion

    #region Unicode

    /// <summary>Encodes text using the specified Unicode width method. Writes output to <paramref name="outBuf"/> and length to <paramref name="outLen"/>.</summary>
    [LibraryImport(LibName, EntryPoint = "encodeUnicode")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool EncodeUnicode(nint input, nuint inputLen, nint outBuf, nint outLen, byte method);

    /// <summary>Frees a Unicode buffer previously allocated by <see cref="EncodeUnicode"/>.</summary>
    [LibraryImport(LibName, EntryPoint = "freeUnicode")]
    internal static partial void FreeUnicode(nint data, nuint len);

    #endregion

    #region Native Span Feed

    /// <summary>Creates a new native span feed with the specified options.</summary>
    [LibraryImport(LibName, EntryPoint = "createNativeSpanFeed")]
    internal static partial nint CreateNativeSpanFeed(nint options);

    /// <summary>Attaches a span feed to the processing pipeline. Returns a status code.</summary>
    [LibraryImport(LibName, EntryPoint = "attachNativeSpanFeed")]
    internal static partial int AttachNativeSpanFeed(nint spanFeed);

    /// <summary>Destroys a native span feed and frees its resources.</summary>
    [LibraryImport(LibName, EntryPoint = "destroyNativeSpanFeed")]
    internal static partial void SpanFeedDestroy(nint spanFeed);

    /// <summary>Writes data to the stream. Returns a status code.</summary>
    [LibraryImport(LibName, EntryPoint = "streamWrite")]
    internal static partial int StreamWrite(nint spanFeed, nint data, ulong len);

    /// <summary>Commits pending stream data. Returns a status code.</summary>
    [LibraryImport(LibName, EntryPoint = "streamCommit")]
    internal static partial int StreamCommit(nint spanFeed);

    /// <summary>Drains completed spans from the feed. Returns the number of spans written.</summary>
    [LibraryImport(LibName, EntryPoint = "streamDrainSpans")]
    internal static partial uint StreamDrainSpans(nint spanFeed, nint outSpans, uint maxSpans);

    /// <summary>Closes the stream. Returns a status code.</summary>
    [LibraryImport(LibName, EntryPoint = "streamClose")]
    internal static partial int StreamClose(nint spanFeed);

    /// <summary>Reserves space in the stream. Writes reservation info to <paramref name="outReserveInfo"/>. Returns a status code.</summary>
    [LibraryImport(LibName, EntryPoint = "streamReserve")]
    internal static partial int StreamReserve(nint spanFeed, uint len, nint outReserveInfo);

    /// <summary>Commits a previously reserved region of the given length. Returns a status code.</summary>
    [LibraryImport(LibName, EntryPoint = "streamCommitReserved")]
    internal static partial int StreamCommitReserved(nint spanFeed, uint len);

    /// <summary>Updates the stream processing options. Returns a status code.</summary>
    [LibraryImport(LibName, EntryPoint = "streamSetOptions")]
    internal static partial int StreamSetOptions(nint spanFeed, nint options);

    /// <summary>Gets stream statistics into the output struct. Returns a status code.</summary>
    [LibraryImport(LibName, EntryPoint = "streamGetStats")]
    internal static partial int StreamGetStats(nint spanFeed, nint outStats);

    /// <summary>Sets a callback function for stream events.</summary>
    [LibraryImport(LibName, EntryPoint = "streamSetCallback")]
    internal static partial void StreamSetCallback(nint spanFeed, nint callback);

    #endregion

    #region Diagnostics

    /// <summary>Gets the total bytes allocated by the arena allocator.</summary>
    [LibraryImport(LibName, EntryPoint = "getArenaAllocatedBytes")]
    internal static partial nuint GetArenaAllocatedBytes();

    /// <summary>Gets the native library build options into the output struct.</summary>
    [LibraryImport(LibName, EntryPoint = "getBuildOptions")]
    internal static partial void GetBuildOptions(nint outOptions);

    /// <summary>Gets the allocator statistics into the output struct.</summary>
    [LibraryImport(LibName, EntryPoint = "getAllocatorStats")]
    internal static partial void GetAllocatorStats(nint outStats);

    #endregion

    #region Handle Destroy Stubs

    // The following destroy methods are referenced by SafeHandle types but do not have
    // corresponding exports in the documented native API. They may exist as internal
    // native exports or may need to be added. Declared here for compilation.

    /// <summary>Destroys a hit grid (may be internally managed by the renderer).</summary>
    [LibraryImport(LibName, EntryPoint = "destroyHitGrid")]
    internal static partial void HitGridDestroy(nint hitGrid);

    /// <summary>Destroys a link store (may be internally managed by the renderer).</summary>
    [LibraryImport(LibName, EntryPoint = "destroyLinkStore")]
    internal static partial void LinkDestroy(nint linkStore);

    #endregion
}
