using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenTui.Core;

/// <summary>
/// The terminal renderer — concrete implementation of <see cref="IRenderContext"/>.
/// Matches TypeScript CliRenderer (renderer.ts L487-2726).
///
/// Owns the native renderer, two OptimizedBuffers (double-buffering), the
/// root renderable, and the input/render loop.
/// </summary>
public sealed class CliRenderer : EventEmitter, IRenderContext, IDisposable
{
    #region Win32 Console Mode (Windows only)

    private const int STD_INPUT_HANDLE = -10;
    private const uint ENABLE_PROCESSED_INPUT = 0x0001;
    private const uint ENABLE_LINE_INPUT = 0x0002;
    private const uint ENABLE_ECHO_INPUT = 0x0004;
    private const uint ENABLE_VIRTUAL_TERMINAL_INPUT = 0x0200;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(nint hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(nint hConsoleHandle, uint dwMode);

    #endregion

    private const int IdleRenderCoalesceWindowMs = 5;

    #region Default env keys

    private static readonly string[] DefaultForwardedEnvKeys =
    [
        "TMUX", "TERM", "OPENTUI_GRAPHICS", "TERM_PROGRAM", "TERM_PROGRAM_VERSION",
        "ALACRITTY_SOCKET", "ALACRITTY_LOG", "COLORTERM", "TERMUX_VERSION",
        "VHS_RECORD", "OPENTUI_FORCE_WCWIDTH", "OPENTUI_FORCE_UNICODE",
        "OPENTUI_FORCE_NOZWJ", "OPENTUI_FORCE_EXPLICIT_WIDTH", "WT_SESSION",
        "STY", "WSL_DISTRO_NAME", "WSL_INTEROP",
    ];

    #endregion

    #region Fields

    private readonly NativeRenderer _nativeRenderer;
    private readonly CliRendererConfig _config;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private bool _isDestroyed;
    private bool _rendering;
    private bool _immediateRerenderRequested;
    private bool _updateScheduled;
    private bool _isRunning;
    private int _renderRequestSuspensionCount;
    private bool _deferredRenderRequested;
    private Timer? _renderTimer;
    private int _renderScheduleVersion;
    private long _lastTimeMs;
    private int _frameCount;
    private long _lastFpsTimeMs;
    private int _currentFps;
    private int _frameId;

    private readonly double _targetFrameTimeMs;
    private readonly double _minTargetFrameTimeMs;
    private int _liveRequestCounter;
    private int _latestPointerX;
    private int _latestPointerY;

    private Renderable? _currentFocusedRenderable;
    private Renderable? _capturedRenderable;
    private Renderable? _lastOverRenderable;
    private readonly HashSet<Renderable> _lifecyclePasses = [];
    private readonly List<Func<float, Task>> _frameCallbacks = [];
    private List<Renderable> _selectionContainers = [];

    private readonly KeyHandler _keyHandler;
    private StdinParser? _stdinParser;
    private readonly object _stdinLock = new();
    private CancellationTokenSource? _inputCts;
    private Thread? _inputThread;

    private bool _useMouse;
    private readonly bool _autoFocus;
    private readonly bool _enableMouseMovement;
    private readonly List<Action<OptimizedBuffer, float>> _postProcessFns;
    private ScreenMode _screenMode;
    private int _footerHeight;
    private ExternalOutputMode _externalOutputMode;
    private int _splitHeight;
    private int _renderOffset;
    private int _terminalWidth;
    private int _terminalHeight;
    private Rgba _backgroundColor = Rgba.Transparent;
    private readonly StringBuilder _capturedStdout = new();
    private readonly object _capturedStdoutLock = new();
    private readonly TextWriter _originalStdout;
    private readonly TextWriter _originalStderr;
    private readonly InterceptingTextWriter _interceptingStdout;
    private readonly InterceptingTextWriter _interceptingStderr;
    private bool _stdoutInterceptInstalled;
    private bool _stderrInterceptInstalled;
    private readonly object _interceptedOutputListenerLock = new();
    private readonly List<Action<string>> _stdoutInterceptionListeners = [];
    private readonly List<Action<string>> _stderrInterceptionListeners = [];

    private bool _terminalIsSetup;
    private bool? _terminalFocusState;
    private ThemeMode? _themeMode;
    private bool _shouldRestoreModesOnNextFocus;
    private uint _savedConsoleMode;
    private bool _hasConsoleMode;
    private volatile bool _exitOnDestroy;
    private bool _destroyRequested;
    private TerminalCapabilities? _capabilities;
    private readonly object _responseListenerLock = new();
    private readonly List<Action<StdinEvent.Response>> _responseListeners = [];
    private readonly List<Func<string, bool>> _sequenceHandlers = [];
    private readonly List<DebugInputRecord> _debugInputs = [];
    private readonly object _debugInputsLock = new();
    private bool _debugModeEnabled;
    private TerminalColors? _cachedPalette;
    private Task<TerminalColors>? _paletteDetectionTask;

    // Resize detection (polled from a timer, applied on the render thread)
    private Timer? _resizeTimer;
    private volatile int _pendingResizeWidth;
    private volatile int _pendingResizeHeight;
    private volatile bool _hasPendingResize;

    #endregion

    #region Public properties

    /// <summary>The native renderer handle.</summary>
    public NativeRenderer Native => _nativeRenderer;

    /// <summary>The buffer renderables draw into this frame.</summary>
    public OptimizedBuffer NextRenderBuffer { get; private set; }

    /// <summary>The buffer that was last sent to the terminal.</summary>
    public OptimizedBuffer CurrentRenderBuffer { get; private set; }

    /// <summary>Root of the renderable tree.</summary>
    public RootRenderable Root { get; }

    /// <inheritdoc/>
    public int Width { get; private set; }

    /// <inheritdoc/>
    public int Height { get; private set; }

    /// <inheritdoc/>
    public int FrameId => _frameId;

    public bool IsDestroyed => _isDestroyed;

    public bool IsRunning => _isRunning;

    public int LiveRequestCount => _liveRequestCounter;

    public string CurrentControlState => _liveRequestCounter > 0 ? "auto_started" : "idle";

    public ThemeMode? ThemeMode => _themeMode;

    public ScreenMode ScreenMode
    {
        get => _screenMode;
        set
        {
            if (_externalOutputMode == ExternalOutputMode.CaptureStdout && value != ScreenMode.SplitFooter)
                throw new InvalidOperationException("externalOutputMode \"CaptureStdout\" requires screenMode \"SplitFooter\".");

            ApplyScreenMode(value);
        }
    }

    public int FooterHeight
    {
        get => _footerHeight;
        set
        {
            int normalized = NormalizeFooterHeight(value);
            if (normalized == _footerHeight)
                return;

            _footerHeight = normalized;
            if (_screenMode == ScreenMode.SplitFooter)
                ApplyScreenMode(ScreenMode.SplitFooter);
        }
    }

    public ExternalOutputMode ExternalOutputMode
    {
        get => _externalOutputMode;
        set
        {
            if (value == ExternalOutputMode.CaptureStdout && _screenMode != ScreenMode.SplitFooter)
                throw new InvalidOperationException("externalOutputMode \"CaptureStdout\" requires screenMode \"SplitFooter\".");

            if (_externalOutputMode == value)
                return;

            _externalOutputMode = value;
            UpdateStdoutInterception();
        }
    }

    public bool UseMouse
    {
        get => _useMouse;
        set
        {
            if (_useMouse == value)
                return;

            _useMouse = value;
            SetCapturedRenderable(null);
            _stdinParser?.ResetMouseState();

            if (_terminalIsSetup)
            {
                if (_useMouse)
                    _nativeRenderer.EnableMouse();
                else
                    _nativeRenderer.DisableMouse();
            }
        }
    }

    public int TerminalWidth => _terminalWidth;

    public int TerminalHeight => _terminalHeight;

    public bool UseKittyKeyboard => _config.UseKittyKeyboard is not null;

    public TerminalCapabilities? TerminalCapabilities => _capabilities;

    public TerminalConsole Console { get; }

    public string PaletteDetectionStatus =>
        _cachedPalette is not null ? "cached" :
        _paletteDetectionTask is not null ? "detecting" :
        "idle";

    /// <summary>
    /// Advances the frame counter and renders the tree. For use in tests only —
    /// mirrors the essential steps of the real render loop (frame ID increment +
    /// Root.Render) without timer/input/native plumbing.
    /// </summary>
    internal void RenderTestFrame(float deltaTime = 16f)
    {
        _frameId++;
        Root.Render(NextRenderBuffer, deltaTime);
    }

    internal void DispatchTestMouseEvent(RawMouseEvent mouseEvent) => ProcessSingleMouseEvent(mouseEvent);

    internal void DispatchTestResponse(string sequence, string protocol = "csi") =>
        HandleStdinEvent(new StdinEvent.Response(protocol, sequence));

    internal void DispatchTestKeyInput(ParsedKey parsedKey, string? raw = null) =>
        HandleStdinEvent(new StdinEvent.Key(raw ?? parsedKey.Raw, parsedKey));

    internal bool? TerminalFocusState => _terminalFocusState;

    internal ThemeMode? TerminalThemeMode => _themeMode;

    internal bool ShouldRestoreModesOnNextFocus => _shouldRestoreModesOnNextFocus;

    internal bool HasDeferredRenderRequest => _deferredRenderRequested;

    internal int RenderRequestSuspensionCount => _renderRequestSuspensionCount;

    internal int CapturedOutputLength
    {
        get
        {
            lock (_capturedStdoutLock)
                return _capturedStdout.Length;
        }
    }

    internal void PresentTestFrame(float deltaTime = 16f)
    {
        _frameId++;
        if (_splitHeight > 0 && _externalOutputMode == ExternalOutputMode.CaptureStdout)
            FlushCapturedStdout(_splitHeight);

        Root.Render(NextRenderBuffer, deltaTime);
        foreach (var fn in _postProcessFns)
            fn(NextRenderBuffer, deltaTime);
        _nativeRenderer.Render();
    }

    #endregion

    #region Constructor

    private CliRenderer(
        NativeRenderer nativeRenderer,
        int renderWidth,
        int renderHeight,
        int terminalWidth,
        int terminalHeight,
        ScreenMode screenMode,
        int footerHeight,
        ExternalOutputMode externalOutputMode,
        CliRendererConfig config)
    {
        _nativeRenderer = nativeRenderer;
        _config = config;
        Width = renderWidth;
        Height = renderHeight;
        _terminalWidth = terminalWidth;
        _terminalHeight = terminalHeight;
        _screenMode = screenMode;
        _footerHeight = footerHeight;
        _externalOutputMode = externalOutputMode;
        _splitHeight = screenMode == ScreenMode.SplitFooter ? footerHeight : 0;
        _renderOffset = _splitHeight > 0 ? terminalHeight - _splitHeight : 0;

        _targetFrameTimeMs = 1000.0 / config.TargetFps;
        _minTargetFrameTimeMs = 1000.0 / config.MaxFps;
        _useMouse = config.UseMouse;
        _autoFocus = config.AutoFocus;
        _enableMouseMovement = config.EnableMouseMovement;
        _postProcessFns = config.PostProcessFns ?? [];
        _originalStdout = System.Console.Out;
        _originalStderr = System.Console.Error;
        _interceptingStdout = new InterceptingTextWriter(_originalStdout.Encoding, CaptureExternalOutput);
        _interceptingStderr = new InterceptingTextWriter(_originalStderr.Encoding, CaptureErrorOutput);

        // Forward env vars to native
        var envKeys = config.ForwardEnvKeys ?? DefaultForwardedEnvKeys;
        foreach (var key in envKeys)
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (value is not null)
                _nativeRenderer.SetTerminalEnvVar(key, value);
        }

        // Kitty keyboard
        var kittyConfig = config.UseKittyKeyboard;
        if (kittyConfig is not null)
            _nativeRenderer.SetKittyKeyboardFlags(kittyConfig.BuildFlags());

        // Background color
        if (config.BackgroundColor is { } bg)
        {
            _backgroundColor = bg;
            _nativeRenderer.SetBackgroundColor(bg);
        }

        _capabilities = _nativeRenderer.GetTerminalCapabilities();

        // Threading
        _nativeRenderer.SetUseThread(config.UseThread);
        _nativeRenderer.SetRenderOffset((uint)_renderOffset);

        // Buffers (wrapped, non-owning — the native renderer owns these)
        NextRenderBuffer = OptimizedBuffer.WrapExisting(_nativeRenderer.GetNextBuffer());
        CurrentRenderBuffer = OptimizedBuffer.WrapExisting(_nativeRenderer.GetCurrentBuffer());

        // Key handler
        _keyHandler = new KeyHandler();
        _keyHandler.On("keypress", (KeyEvent e) =>
        {
            if (config.ExitOnCtrlC && e.Name == "c" && e.Ctrl)
            {
                _exitOnDestroy = true;
                Destroy();
            }
        });

        // Stdin parser
        var useKitty = kittyConfig is not null;
        _stdinParser = new StdinParser(new StdinParserOptions
        {
            UseKittyKeyboard = useKitty,
            TimeoutMs = 20,
            ProtocolContext = new ProtocolContext
            {
                PrivateCapabilityRepliesActive = true,
            },
        });

        // Root renderable
        Root = new RootRenderable(this);
        Console = new TerminalConsole(this);
        UpdateStdoutInterception();
    }

    /// <summary>
    /// Create and optionally set up the terminal renderer.
    /// </summary>
    public static CliRenderer Create(CliRendererConfig? config = null)
    {
        config ??= new CliRendererConfig();

        int terminalWidth = config.Width ?? TryGetConsoleWidth();
        int terminalHeight = config.Height ?? TryGetConsoleHeight();
        var resolvedModes = ResolveModes(config);
        int renderHeight = resolvedModes.ScreenMode == ScreenMode.SplitFooter
            ? Math.Min(resolvedModes.FooterHeight, terminalHeight)
            : terminalHeight;

        var nativeRenderer = NativeRenderer.Create(
            (uint)terminalWidth, (uint)renderHeight,
            testing: config.Testing,
            remote: config.Remote
        );

        var renderer = new CliRenderer(
            nativeRenderer,
            terminalWidth,
            renderHeight,
            terminalWidth,
            terminalHeight,
            resolvedModes.ScreenMode,
            resolvedModes.FooterHeight,
            resolvedModes.ExternalOutputMode,
            config);

        if (!config.Testing)
            renderer.SetupTerminal();

        return renderer;
    }

    private static int TryGetConsoleWidth()
    {
        try { return System.Console.WindowWidth; }
        catch { return 80; }
    }

    private static int TryGetConsoleHeight()
    {
        try { return System.Console.WindowHeight; }
        catch { return 24; }
    }

    #endregion

    #region Terminal Setup / Teardown

    private void SetupTerminal()
    {
        if (_terminalIsSetup) return;
        _terminalIsSetup = true;

        bool useAlternateScreen = _screenMode == ScreenMode.AlternateScreen;
        _nativeRenderer.SetupTerminal(useAlternateScreen);

        if (_useMouse)
            _nativeRenderer.EnableMouse();

        var kittyConfig = _config.UseKittyKeyboard;
        if (kittyConfig is not null)
            _nativeRenderer.EnableKittyKeyboard(kittyConfig.BuildFlags());

        // Set up console for raw input on Windows
        SetupRawInput();

        // Handle Ctrl+C
        System.Console.CancelKeyPress += OnCancelKeyPress;

        // Start input reading thread
        StartInputLoop();

        // Start resize polling timer
        StartResizeWatcher();

        WriteRaw("\x1b[?2031h\x1b[?2031$p");
    }

    private void SetupRawInput()
    {
        if (OperatingSystem.IsWindows())
        {
            try { System.Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }
            try { System.Console.InputEncoding = System.Text.Encoding.UTF8; } catch { }

            // Enable raw console mode: disable line buffering, echo, and Ctrl+C signal
            // so stdin reads return individual keystrokes silently.
            var handle = GetStdHandle(STD_INPUT_HANDLE);
            if (handle != nint.Zero && handle != (nint)(-1) &&
                GetConsoleMode(handle, out uint mode))
            {
                _savedConsoleMode = mode;
                _hasConsoleMode = true;
                mode &= ~(ENABLE_PROCESSED_INPUT | ENABLE_LINE_INPUT | ENABLE_ECHO_INPUT);
                mode |= ENABLE_VIRTUAL_TERMINAL_INPUT;
                SetConsoleMode(handle, mode);
            }
        }
    }

    private void TeardownTerminal()
    {
        if (!_terminalIsSetup) return;
        _terminalIsSetup = false;

        _inputCts?.Cancel();
        _resizeTimer?.Dispose();
        _resizeTimer = null;

        if (_useMouse)
            _nativeRenderer.DisableMouse();

        var kittyConfig = _config.UseKittyKeyboard;
        if (kittyConfig is not null)
            _nativeRenderer.DisableKittyKeyboard();

        RestoreConsoleMode();
        WriteRaw("\x1b[?2031l");
        _nativeRenderer.RestoreTerminalModes();

        System.Console.CancelKeyPress -= OnCancelKeyPress;
    }

    private void RestoreConsoleMode()
    {
        if (_hasConsoleMode && OperatingSystem.IsWindows())
        {
            var handle = GetStdHandle(STD_INPUT_HANDLE);
            if (handle != nint.Zero && handle != (nint)(-1))
                SetConsoleMode(handle, _savedConsoleMode);
            _hasConsoleMode = false;
        }
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        if (_config.ExitOnCtrlC)
        {
            e.Cancel = true;
            _exitOnDestroy = true;
            Destroy();
        }
    }

    #endregion

    #region Resize Detection

    private void StartResizeWatcher()
    {
        // Poll terminal size at regular intervals.
        // When a change is detected, store it and trigger a render.
        // The actual Resize() call happens on the render thread in Loop().
        _resizeTimer = new Timer(_ =>
        {
            if (_isDestroyed) return;
            try
            {
                int w = System.Console.WindowWidth;
                int h = System.Console.WindowHeight;
                if (w > 0 && h > 0 && (w != _terminalWidth || h != _terminalHeight))
                {
                    _pendingResizeWidth = w;
                    _pendingResizeHeight = h;
                    _hasPendingResize = true;
                    RequestRender();
                }
            }
            catch
            {
                // Console may not be available (e.g., piped stdin)
            }
        }, null, _config.DebounceDelay, _config.DebounceDelay);
    }

    /// <summary>
    /// Apply any pending resize on the render thread.
    /// Called at the start of Loop() before processing events.
    /// </summary>
    private void ApplyPendingResize()
    {
        if (!_hasPendingResize) return;
        _hasPendingResize = false;
        Resize(_pendingResizeWidth, _pendingResizeHeight);
    }

    #endregion

    #region Input Loop

    private void StartInputLoop()
    {
        _inputCts = new CancellationTokenSource();
        var token = _inputCts.Token;

        _inputThread = new Thread(() => ReadInputLoop(token))
        {
            IsBackground = true,
            Name = "OpenTUI-StdinReader",
        };
        _inputThread.Start();
    }

    private void ReadInputLoop(CancellationToken ct)
    {
        var stdin = System.Console.OpenStandardInput();
        var buffer = new byte[4096];

        try
        {
            while (!ct.IsCancellationRequested)
            {
                int bytesRead;
                try
                {
                    bytesRead = stdin.Read(buffer, 0, buffer.Length);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    break;
                }

                if (bytesRead <= 0) break;

                if (_stdinParser is not null)
                {
                    lock (_stdinLock)
                    {
                        _stdinParser.Push(buffer.AsSpan(0, bytesRead));
                    }
                    RequestRender();
                }
            }
        }
        catch
        {
            // Input loop terminated
        }
    }

    private void DrainStdinParser()
    {
        var parser = _stdinParser;
        if (parser is null) return;

        // Collect events under lock to avoid racing with Push on the input thread
        List<StdinEvent>? events = null;
        lock (_stdinLock)
        {
            StdinEvent? evt;
            while ((evt = parser.Read()) is not null)
            {
                events ??= new();
                events.Add(evt);
            }
        }

        // Dispatch outside lock — handlers may modify the Yoga tree, call RequestRender, etc.
        if (events is not null)
        {
            foreach (var evt in events)
                HandleStdinEvent(evt);
        }
    }

    private void HandleStdinEvent(StdinEvent evt)
    {
        switch (evt)
        {
            case StdinEvent.Key keyEvt:
                if (DispatchSequenceHandlers(keyEvt.Raw))
                    break;
                if (Console.HandleKey(keyEvt.ParsedKey))
                    break;
                _keyHandler.ProcessParsedKey(keyEvt.ParsedKey);
                break;
            case StdinEvent.Mouse mouseEvt:
                if (_useMouse)
                    ProcessSingleMouseEvent(mouseEvt.MouseEvent);
                else
                    DispatchSequenceHandlers(mouseEvt.Raw);
                break;
            case StdinEvent.Paste pasteEvt:
                _keyHandler.ProcessPaste(pasteEvt.Bytes, pasteEvt.Metadata);
                break;
            case StdinEvent.Response responseEvt:
                HandleResponseEvent(responseEvt);
                DispatchSequenceHandlers(responseEvt.Sequence);
                break;
        }
    }

    private bool DispatchSequenceHandlers(string sequence)
    {
        if (_debugModeEnabled)
        {
            lock (_debugInputsLock)
            {
                _debugInputs.Add(new DebugInputRecord
                {
                    Timestamp = DateTime.UtcNow.ToString("O"),
                    Sequence = sequence,
                });
            }
        }

        if (_sequenceHandlers.Count == 0)
            return false;

        foreach (var handler in _sequenceHandlers.ToArray())
        {
            if (handler(sequence))
                return true;
        }

        return false;
    }

    private void HandleResponseEvent(StdinEvent.Response evt)
    {
        _nativeRenderer.ProcessCapabilityResponse(Encoding.Latin1.GetBytes(evt.Sequence));
        _capabilities = _nativeRenderer.GetTerminalCapabilities();

        if (TryHandleFocusResponse(evt.Sequence))
            return;

        if (TryHandleThemeModeResponse(evt.Sequence))
            return;

        NotifyResponseListeners(evt);
    }

    private bool TryHandleFocusResponse(string sequence)
    {
        if (sequence == "\x1b[I")
        {
            if (_shouldRestoreModesOnNextFocus)
            {
                _nativeRenderer.RestoreTerminalModes();
                _shouldRestoreModesOnNextFocus = false;
            }

            if (_terminalFocusState != true)
            {
                _terminalFocusState = true;
                Emit(RendererEventNames.Focus);
            }

            return true;
        }

        if (sequence == "\x1b[O")
        {
            _shouldRestoreModesOnNextFocus = true;

            if (_terminalFocusState != false)
            {
                _terminalFocusState = false;
                Emit(RendererEventNames.Blur);
            }

            return true;
        }

        return false;
    }

    private bool TryHandleThemeModeResponse(string sequence)
    {
        if (sequence == "\x1b[?997;1n")
        {
            if (_themeMode != OpenTui.Core.ThemeMode.Dark)
            {
                _themeMode = OpenTui.Core.ThemeMode.Dark;
                Emit(RendererEventNames.ThemeMode, OpenTui.Core.ThemeMode.Dark);
            }

            return true;
        }

        if (sequence == "\x1b[?997;2n")
        {
            if (_themeMode != OpenTui.Core.ThemeMode.Light)
            {
                _themeMode = OpenTui.Core.ThemeMode.Light;
                Emit(RendererEventNames.ThemeMode, OpenTui.Core.ThemeMode.Light);
            }

            return true;
        }

        return false;
    }

    private IDisposable SubscribeResponses(Action<StdinEvent.Response> listener)
    {
        lock (_responseListenerLock)
            _responseListeners.Add(listener);

        return new ResponseUnsubscriber(this, listener);
    }

    private void NotifyResponseListeners(StdinEvent.Response response)
    {
        Action<StdinEvent.Response>[] listeners;
        lock (_responseListenerLock)
            listeners = _responseListeners.ToArray();

        foreach (var listener in listeners)
            listener(response);
    }

    private void RemoveResponseListener(Action<StdinEvent.Response> listener)
    {
        lock (_responseListenerLock)
            _responseListeners.Remove(listener);
    }

    #endregion

    #region Mouse Dispatch

    private void ProcessSingleMouseEvent(RawMouseEvent mouseEvent)
    {
        _latestPointerX = mouseEvent.X;
        _latestPointerY = mouseEvent.Y;

        if (Console.HandleMouse(mouseEvent))
            return;

        if (mouseEvent.Type == MouseEventType.Scroll)
        {
            var maybeId = HitTest(mouseEvent.X, mouseEvent.Y);
            var target = Renderable.GetByNumber((int)maybeId);
            var fallback = _currentFocusedRenderable is { IsDestroyed: false, Focused: true }
                ? _currentFocusedRenderable
                : null;
            var scrollTarget = target ?? fallback;
            if (scrollTarget is not null)
            {
                var evt = CreateMouseEvent(scrollTarget, mouseEvent);
                scrollTarget.ProcessMouseEvent(evt);
            }
            return;
        }

        var targetId = HitTest(mouseEvent.X, mouseEvent.Y);
        var hitTarget = Renderable.GetByNumber((int)targetId);

        if (mouseEvent.Type == MouseEventType.Down
            && mouseEvent.Button == (int)MouseButton.Left
            && !(_currentSelection?.IsDragging ?? false)
            && !mouseEvent.Modifiers.Ctrl)
        {
            bool canStartSelection =
                hitTarget is { Selectable: true, IsDestroyed: false }
                && hitTarget.ShouldStartSelection(mouseEvent.X, mouseEvent.Y);

            if (canStartSelection)
            {
                StartSelection(hitTarget!, mouseEvent.X, mouseEvent.Y);
                hitTarget!.ProcessMouseEvent(CreateMouseEvent(hitTarget, mouseEvent));
                return;
            }
        }

        if (mouseEvent.Type == MouseEventType.Drag && _currentSelection?.IsDragging == true)
        {
            UpdateSelection(hitTarget, mouseEvent.X, mouseEvent.Y);

            if (hitTarget is not null)
                hitTarget.ProcessMouseEvent(CreateMouseEvent(hitTarget, mouseEvent, isDragging: true));

            return;
        }

        if (mouseEvent.Type == MouseEventType.Up && _currentSelection?.IsDragging == true)
        {
            if (hitTarget is not null)
                hitTarget.ProcessMouseEvent(CreateMouseEvent(hitTarget, mouseEvent, isDragging: true));

            FinishSelection();
            return;
        }

        if (mouseEvent.Type == MouseEventType.Down
            && mouseEvent.Button == (int)MouseButton.Left
            && _currentSelection is not null
            && mouseEvent.Modifiers.Ctrl)
        {
            _currentSelection.IsDragging = true;
            UpdateSelection(hitTarget, mouseEvent.X, mouseEvent.Y);
            return;
        }

        bool sameElement = ReferenceEquals(_lastOverRenderable, hitTarget);
        if (!sameElement && mouseEvent.Type is MouseEventType.Drag or MouseEventType.Move)
        {
            if (_lastOverRenderable is { IsDestroyed: false } previousOver
                && !ReferenceEquals(previousOver, _capturedRenderable))
            {
                previousOver.ProcessMouseEvent(CreateMouseEvent(previousOver, mouseEvent, overrideType: MouseEventType.Out));
            }

            _lastOverRenderable = hitTarget;
            if (hitTarget is not null)
            {
                hitTarget.ProcessMouseEvent(CreateMouseEvent(
                    hitTarget,
                    mouseEvent,
                    overrideType: MouseEventType.Over,
                    sourceId: _capturedRenderable?.Id));
            }
        }

        if (_capturedRenderable is { IsDestroyed: false } captured && mouseEvent.Type != MouseEventType.Up)
        {
            captured.ProcessMouseEvent(CreateMouseEvent(captured, mouseEvent));
            return;
        }

        if (_capturedRenderable is { IsDestroyed: false } capturedUp && mouseEvent.Type == MouseEventType.Up)
        {
            capturedUp.ProcessMouseEvent(CreateMouseEvent(capturedUp, mouseEvent, overrideType: MouseEventType.DragEnd));
            capturedUp.ProcessMouseEvent(CreateMouseEvent(capturedUp, mouseEvent));

            if (hitTarget is not null)
            {
                hitTarget.ProcessMouseEvent(CreateMouseEvent(
                    hitTarget,
                    mouseEvent,
                    overrideType: MouseEventType.Drop,
                    sourceId: capturedUp.Id));
            }

            _lastOverRenderable = capturedUp;
            SetCapturedRenderable(null);
            RequestRender();
        }

        UiMouseEvent? dispatchedEvent = null;

        if (hitTarget is not null)
        {
            if (mouseEvent.Type == MouseEventType.Drag && mouseEvent.Button == (int)MouseButton.Left)
            {
                SetCapturedRenderable(hitTarget);
            }
            else
            {
                SetCapturedRenderable(null);
            }

            dispatchedEvent = CreateMouseEvent(hitTarget, mouseEvent);
            hitTarget.ProcessMouseEvent(dispatchedEvent);
        }
        else
        {
            SetCapturedRenderable(null);
            _lastOverRenderable = null;
        }

        if (mouseEvent.Type == MouseEventType.Down
            && hitTarget is not null
            && _autoFocus
            && mouseEvent.Button == 0
            && !(dispatchedEvent?.IsDefaultPrevented ?? false))
        {
            Renderable? current = hitTarget;
            while (current is not null)
            {
                if (current.Focusable)
                {
                    current.Focus();
                    break;
                }
                current = current.Parent;
            }
        }

        if (_currentSelection is not null
            && mouseEvent.Type == MouseEventType.Down
            && !(dispatchedEvent?.IsDefaultPrevented ?? false))
        {
            ClearSelection();
        }
    }

    private void SetCapturedRenderable(Renderable? renderable) => _capturedRenderable = renderable is { IsDestroyed: false } ? renderable : null;

    private static UiMouseEvent CreateMouseEvent(
        Renderable target,
        RawMouseEvent raw,
        bool isDragging = false,
        MouseEventType? overrideType = null,
        string? sourceId = null) => new()
    {
        Type = overrideType ?? raw.Type,
        Button = raw.Button,
        X = raw.X,
        Y = raw.Y,
        Modifiers = raw.Modifiers,
        Scroll = raw.Scroll,
        IsDragging = isDragging,
        Target = target,
        Source = sourceId,
    };

    private uint HitTest(int x, int y) =>
        _nativeRenderer.CheckHit((uint)Math.Max(0, x), (uint)Math.Max(0, y));

    #endregion

    #region Render Loop

    /// <inheritdoc/>
    public void RequestRender()
    {
        if (_isDestroyed) return;

        if (_renderRequestSuspensionCount > 0)
        {
            _deferredRenderRequested = true;
            return;
        }

        _deferredRenderRequested = false;

        // In testing mode, don't schedule async renders — tests call RenderFrame()
        // explicitly. Scheduling background renders causes data races with native
        // handles when the test also renders on its own thread.
        if (_config.Testing) return;

        if (_isRunning) return;

        if (_rendering)
        {
            _immediateRerenderRequested = true;
            return;
        }

        if (_updateScheduled)
        {
            if (_renderTimer is not null)
                ScheduleIdleFrame();
            return;
        }

        _updateScheduled = true;
        ScheduleIdleFrame();
    }

    private void ScheduleIdleFrame()
    {
        // Upstream requestRender() runs later on the same event-loop thread, so
        // a burst of synchronous tree mutations naturally coalesces into one
        // render. In C#, timer/thread-pool callbacks can interleave with the
        // caller, so we wait for a short quiet window before rendering idle
        // frames. This keeps startup tree construction from racing layout.
        _renderTimer?.Dispose();
        _renderTimer = null;

        var nowMs = _clock.ElapsedMilliseconds;
        var elapsed = nowMs - _lastTimeMs;
        var frameBudgetDelayMs = Math.Max(_minTargetFrameTimeMs - elapsed, 0);
        var delayMs = Math.Max(frameBudgetDelayMs, IdleRenderCoalesceWindowMs);
        var dueTimeMs = Math.Max(1, (int)Math.Ceiling(delayMs));
        var scheduleVersion = ++_renderScheduleVersion;

        _renderTimer = new Timer(
            _ =>
            {
                if (scheduleVersion != _renderScheduleVersion)
                    return;

                ActivateFrame();
            },
            null,
            dueTimeMs,
            Timeout.Infinite
        );
    }

    public IDisposable SuspendRenderRequests()
    {
        _renderRequestSuspensionCount++;
        return new RenderRequestSuspension(this);
    }

    private void ResumeRenderRequests()
    {
        if (_renderRequestSuspensionCount == 0)
            return;

        _renderRequestSuspensionCount--;
        if (_renderRequestSuspensionCount == 0 && _deferredRenderRequested)
        {
            _deferredRenderRequested = false;
            RequestRender();
        }
    }

    private void ActivateFrame()
    {
        if (!_updateScheduled)
            return;

        try
        {
            Loop();
        }
        finally
        {
            _updateScheduled = false;
        }
    }

    private void Loop()
    {
        if (_rendering || _isDestroyed) return;
        _renderTimer?.Dispose();
        _renderTimer = null;

        _rendering = true;
        try
        {
            // Apply any terminal resize detected by the watcher timer.
            // This runs on the render thread so tree mutations are safe.
            ApplyPendingResize();

            // Process stdin events on the render thread before anything else.
            // The input thread only pushes raw bytes; we drain parsed events here
            // so all Yoga tree mutations happen on a single thread.
            DrainStdinParser();

            _frameId++;

            var nowMs = _clock.ElapsedMilliseconds;
            var elapsed = nowMs - _lastTimeMs;
            var deltaTime = (float)elapsed;
            _lastTimeMs = nowMs;

            _frameCount++;
            if (nowMs - _lastFpsTimeMs >= 1000)
            {
                _currentFps = _frameCount;
                _frameCount = 0;
                _lastFpsTimeMs = nowMs;
            }

            // Frame callbacks
            foreach (var cb in _frameCallbacks)
            {
                try { cb(deltaTime).GetAwaiter().GetResult(); }
                catch { /* swallow */ }
            }

            if (_splitHeight > 0 && _externalOutputMode == ExternalOutputMode.CaptureStdout)
                FlushCapturedStdout(_splitHeight);

            // Render the tree
            Root.Render(NextRenderBuffer, deltaTime);

            // Post-process hooks
            foreach (var fn in _postProcessFns)
                fn(NextRenderBuffer, deltaTime);

            // Native render
            if (!_isDestroyed)
            {
                _nativeRenderer.Render();

                // Recheck hover if hit grid changed
                if (_useMouse && _nativeRenderer.GetHitGridDirty())
                    RecheckHoverState();

                // Schedule next frame if running or immediate requested
                if (_isRunning || _immediateRerenderRequested)
                {
                    var targetMs = _immediateRerenderRequested ? _minTargetFrameTimeMs : _targetFrameTimeMs;
                    var frameTimeMs = _clock.ElapsedMilliseconds - nowMs;
                    var delay = Math.Max(1, targetMs - frameTimeMs);
                    _immediateRerenderRequested = false;

                    _renderTimer = new Timer(
                        _ => Loop(),
                        null,
                        (int)delay,
                        Timeout.Infinite
                    );
                }
            }
        }
        finally
        {
            _rendering = false;
            if (_destroyRequested)
                Destroy();
        }
    }

    private void RecheckHoverState()
    {
        // TODO: implement hover recheck using hit grid + last pointer position
    }

    private sealed class RenderRequestSuspension : IDisposable
    {
        private CliRenderer? _renderer;

        public RenderRequestSuspension(CliRenderer renderer) => _renderer = renderer;

        public void Dispose()
        {
            _renderer?.ResumeRenderRequests();
            _renderer = null;
        }
    }

    #endregion

    #region Live Mode

    /// <inheritdoc/>
    public void RequestLive()
    {
        _liveRequestCounter++;
        if (_liveRequestCounter == 1)
            StartRunning();
    }

    /// <inheritdoc/>
    public void DropLive()
    {
        _liveRequestCounter = Math.Max(0, _liveRequestCounter - 1);
        if (_liveRequestCounter == 0)
            StopRunning();
    }

    private void StartRunning()
    {
        if (_isRunning || _isDestroyed) return;
        _isRunning = true;
        RequestRender();
    }

    private void StopRunning()
    {
        _isRunning = false;
        _renderTimer?.Dispose();
        _renderTimer = null;
    }

    #endregion

    #region Frame Callbacks

    public void AddFrameCallback(Func<float, Task> callback) =>
        _frameCallbacks.Add(callback);

    public void ClearFrameCallbacks() =>
        _frameCallbacks.Clear();

    public bool RemoveFrameCallback(Func<float, Task> callback) =>
        _frameCallbacks.Remove(callback);

    public void AddPostProcessFn(Action<OptimizedBuffer, float> callback) =>
        _postProcessFns.Add(callback);

    public bool RemovePostProcessFn(Action<OptimizedBuffer, float> callback) =>
        _postProcessFns.Remove(callback);

    public void ClearPostProcessFns() =>
        _postProcessFns.Clear();

    #endregion

    #region Focus

    /// <inheritdoc/>
    public Renderable? CurrentFocusedRenderable => _currentFocusedRenderable;

    /// <inheritdoc/>
    public void FocusRenderable(Renderable renderable)
    {
        if (_currentFocusedRenderable == renderable) return;
        _currentFocusedRenderable?.Blur();
        _currentFocusedRenderable = renderable;
    }

    /// <inheritdoc/>
    public void BlurRenderable(Renderable renderable)
    {
        if (_currentFocusedRenderable == renderable)
            _currentFocusedRenderable = null;
    }

    #endregion

    #region Hit Grid (delegated to native)

    /// <inheritdoc/>
    public void AddToHitGrid(int x, int y, uint width, uint height, uint id) =>
        _nativeRenderer.AddToHitGrid(x, y, width, height, id);

    /// <inheritdoc/>
    public void PushHitGridScissorRect(int x, int y, uint width, uint height) =>
        _nativeRenderer.HitGridPushScissorRect(x, y, width, height);

    /// <inheritdoc/>
    public void PopHitGridScissorRect() =>
        _nativeRenderer.HitGridPopScissorRect();

    /// <inheritdoc/>
    public void ClearHitGridScissorRects() =>
        _nativeRenderer.HitGridClearScissorRects();

    #endregion

    #region Cursor

    /// <inheritdoc/>
    public void SetCursorPosition(int x, int y, bool visible) =>
        _nativeRenderer.SetCursorPosition(x, y, visible);

    /// <inheritdoc/>
    public void SetCursorStyle(CursorStyleOptions options) =>
        _nativeRenderer.SetCursorStyleOptions(options);

    /// <inheritdoc/>
    public void SetCursorColor(Rgba color) =>
        _nativeRenderer.SetCursorColor(color);

    /// <inheritdoc/>
    public void SetMousePointer(MousePointerStyle shape)
    {
        // Mouse pointer shape is a terminal hint, not always supported
        // Stored for potential OSC sequence emission
    }

    #endregion

    #region Width Method & Capabilities

    /// <inheritdoc/>
    public WidthMethod WidthMethod => WidthMethod.Unicode;

    /// <inheritdoc/>
    public object? Capabilities => _capabilities;

    public void SetDebugMode(bool enabled) => _debugModeEnabled = enabled;

    public IReadOnlyList<DebugInputRecord> GetDebugInputs()
    {
        lock (_debugInputsLock)
            return _debugInputs.Select(record => new DebugInputRecord
            {
                Timestamp = record.Timestamp,
                Sequence = record.Sequence,
            }).ToArray();
    }

    public void AddInputHandler(Func<string, bool> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _sequenceHandlers.Add(handler);
    }

    public void PrependInputHandler(Func<string, bool> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _sequenceHandlers.Insert(0, handler);
    }

    public void RemoveInputHandler(Func<string, bool> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _sequenceHandlers.RemoveAll(candidate => ReferenceEquals(candidate, handler));
    }

    public bool CopyToClipboardOSC52(string text, byte register = 0) =>
        _nativeRenderer.CopyToClipboard(text, register);

    public bool ClearClipboardOSC52(byte register = 0) =>
        _nativeRenderer.ClearClipboard(register);

    public void SetBackgroundColor(Rgba color)
    {
        _backgroundColor = color;
        _nativeRenderer.SetBackgroundColor(color);
        NextRenderBuffer.Clear(color);
        RequestRender();
    }

    internal void CaptureExternalOutput(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        bool splitCaptureActive = _splitHeight > 0 && _externalOutputMode == ExternalOutputMode.CaptureStdout;
        if (splitCaptureActive)
        {
            lock (_capturedStdoutLock)
                _capturedStdout.Append(text);
        }

        NotifyInterceptedOutputListeners(text, error: false);

        if (splitCaptureActive)
            RequestRender();
    }

    private void CaptureErrorOutput(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        NotifyInterceptedOutputListeners(text, error: true);
    }

    private void UpdateStdoutInterception()
    {
        bool shouldInterceptStdout;
        bool shouldInterceptStderr;
        lock (_interceptedOutputListenerLock)
        {
            shouldInterceptStdout = _externalOutputMode == ExternalOutputMode.CaptureStdout || _stdoutInterceptionListeners.Count > 0;
            shouldInterceptStderr = _stderrInterceptionListeners.Count > 0;
        }

        if (_config.Testing)
            return;

        if (shouldInterceptStdout != _stdoutInterceptInstalled)
        {
            if (shouldInterceptStdout)
            {
                System.Console.SetOut(_interceptingStdout);
                _stdoutInterceptInstalled = true;
            }
            else
            {
                System.Console.SetOut(_originalStdout);
                _stdoutInterceptInstalled = false;
            }
        }

        if (shouldInterceptStderr != _stderrInterceptInstalled)
        {
            if (shouldInterceptStderr)
            {
                System.Console.SetError(_interceptingStderr);
                _stderrInterceptInstalled = true;
            }
            else
            {
                System.Console.SetError(_originalStderr);
                _stderrInterceptInstalled = false;
            }
        }
    }

    private void FlushCapturedStdout(int space, bool force = false)
    {
        string output;
        lock (_capturedStdoutLock)
        {
            if (_capturedStdout.Length == 0 && !force)
                return;

            output = _capturedStdout.ToString();
            _capturedStdout.Clear();
        }

        CurrentRenderBuffer.Clear(_backgroundColor);

        if (_config.Testing || _isDestroyed)
            return;

        int rendererStartLine = Math.Max(1, _terminalHeight - _splitHeight);
        var builder = new StringBuilder();
        builder.Append(MoveCursorAndClear(rendererStartLine, 1));
        builder.Append(MoveCursor(rendererStartLine, 1));
        builder.Append(output);

        if (space > 0)
            builder.Append(ClearFooterArea(space));

        WriteRaw(builder.ToString());
    }

    private string ClearFooterArea(int space)
    {
        if (space <= 0 || Width <= 0)
            return string.Empty;

        string clearLines = string.Concat(Enumerable.Repeat(new string(' ', Width) + '\n', space));
        if (_backgroundColor.A <= 0f)
            return clearLines;

        var (r, g, b, _) = _backgroundColor.ToInts();
        return $"\x1b[48;2;{r};{g};{b}m{clearLines}\x1b[49m";
    }

    private void ApplyScreenMode(ScreenMode screenMode, bool emitResize = true, bool requestRender = true)
    {
        int nextSplitHeight = screenMode == ScreenMode.SplitFooter
            ? Math.Min(_terminalHeight, _footerHeight)
            : 0;

        bool sameMode = _screenMode == screenMode;
        bool sameSplitHeight = _splitHeight == nextSplitHeight;
        bool sameRenderSize = Width == _terminalWidth && Height == (nextSplitHeight > 0 ? nextSplitHeight : _terminalHeight);
        if (sameMode && sameSplitHeight && sameRenderSize)
            return;

        int previousSplitHeight = _splitHeight;
        bool previousAlternate = _screenMode == ScreenMode.AlternateScreen;
        bool nextAlternate = screenMode == ScreenMode.AlternateScreen;
        bool terminalScreenModeChanged = _terminalIsSetup && previousAlternate != nextAlternate;
        bool leavingSplitFooter = previousSplitHeight > 0 && nextSplitHeight == 0;

        if (leavingSplitFooter)
            FlushCapturedStdout(_terminalHeight, force: true);

        if (_terminalIsSetup && !terminalScreenModeChanged)
        {
            if (previousSplitHeight == 0 && nextSplitHeight > 0)
            {
                WriteRaw(ScrollDown(Math.Max(0, _terminalHeight - nextSplitHeight)));
            }
            else if (previousSplitHeight > nextSplitHeight && nextSplitHeight > 0)
            {
                WriteRaw(ScrollDown(previousSplitHeight - nextSplitHeight));
            }
            else if (previousSplitHeight < nextSplitHeight && previousSplitHeight > 0)
            {
                WriteRaw(ScrollUp(nextSplitHeight - previousSplitHeight));
            }
        }

        _screenMode = screenMode;
        _splitHeight = nextSplitHeight;
        _renderOffset = nextSplitHeight > 0 ? _terminalHeight - nextSplitHeight : 0;
        Width = _terminalWidth;
        Height = nextSplitHeight > 0 ? nextSplitHeight : _terminalHeight;

        _nativeRenderer.SetRenderOffset((uint)_renderOffset);
        _nativeRenderer.Resize((uint)Width, (uint)Height);
        RebindBuffers();
        Root.Resize(Width, Height);

        if (terminalScreenModeChanged)
        {
            _nativeRenderer.Suspend();
            _nativeRenderer.SetupTerminal(nextAlternate);

            if (_useMouse)
                _nativeRenderer.EnableMouse();
        }

        if (emitResize)
            Emit<(int Width, int Height)>(RendererEventNames.Resize, (Width, Height));

        if (requestRender)
            RequestRender();
    }

    private void RebindBuffers()
    {
        NextRenderBuffer.Dispose();
        CurrentRenderBuffer.Dispose();
        NextRenderBuffer = OptimizedBuffer.WrapExisting(_nativeRenderer.GetNextBuffer());
        CurrentRenderBuffer = OptimizedBuffer.WrapExisting(_nativeRenderer.GetCurrentBuffer());
        CurrentRenderBuffer.Clear(_backgroundColor);
        NextRenderBuffer.Clear(_backgroundColor);
    }

    public void ClearPaletteCache() => _cachedPalette = null;

    public Task<TerminalColors> GetPalette(GetPaletteOptions? options = null)
    {
        options ??= new GetPaletteOptions();
        if (options.Size is < 1 or > 256)
            throw new ArgumentOutOfRangeException(nameof(options.Size), "Palette size must be between 1 and 256.");

        if (options.Timeout <= 0)
            throw new ArgumentOutOfRangeException(nameof(options.Timeout), "Timeout must be greater than 0.");

        if (_cachedPalette is not null && _cachedPalette.Palette.Length != options.Size)
            _cachedPalette = null;

        if (_cachedPalette is not null)
            return Task.FromResult(_cachedPalette);

        if (_paletteDetectionTask is not null)
            return _paletteDetectionTask;

        return _paletteDetectionTask = DetectPaletteAsync(options);
    }

    private async Task<TerminalColors> DetectPaletteAsync(GetPaletteOptions options)
    {
        try
        {
            var palette = Enumerable.Range(0, options.Size).ToDictionary(i => i, _ => (string?)null);
            var specialColors = new Dictionary<int, string?>
            {
                [10] = null,
                [11] = null,
                [12] = null,
                [13] = null,
                [14] = null,
                [15] = null,
                [16] = null,
                [17] = null,
                [19] = null,
            };

            var completion = new TaskCompletionSource<TerminalColors>(TaskCreationOptions.RunContinuationsAsynchronously);
            object gate = new();
            bool finished = false;
            bool sawPaletteResponse = false;
            Timer? idleTimer = null;
            Timer? timeoutTimer = null;

            TerminalColors BuildResult() => new()
            {
                Palette = Enumerable.Range(0, options.Size).Select(i => palette[i]).ToArray(),
                DefaultForeground = specialColors[10],
                DefaultBackground = specialColors[11],
                CursorColor = specialColors[12],
                MouseForeground = specialColors[13],
                MouseBackground = specialColors[14],
                TekForeground = specialColors[15],
                TekBackground = specialColors[16],
                HighlightBackground = specialColors[17],
                HighlightForeground = specialColors[19],
            };

            void Complete()
            {
                if (finished)
                    return;

                finished = true;
                idleTimer?.Dispose();
                timeoutTimer?.Dispose();
                completion.TrySetResult(BuildResult());
            }

            using var subscription = SubscribeResponses(response =>
            {
                if (!string.Equals(response.Protocol, "osc", StringComparison.Ordinal))
                    return;

                lock (gate)
                {
                    if (finished)
                        return;

                    bool updated =
                        TerminalPaletteParser.TryApplyPaletteResponse(response.Sequence, palette) |
                        TerminalPaletteParser.TryApplySpecialResponse(response.Sequence, specialColors);

                    if (!updated)
                        return;

                    sawPaletteResponse = true;

                    if (palette.Values.All(value => value is not null) && specialColors.Values.All(value => value is not null))
                    {
                        Complete();
                        return;
                    }

                    idleTimer?.Change(150, Timeout.Infinite);
                }
            });

            timeoutTimer = new Timer(_ =>
            {
                lock (gate)
                    Complete();
            }, null, options.Timeout, Timeout.Infinite);

            idleTimer = new Timer(_ =>
            {
                lock (gate)
                {
                    if (sawPaletteResponse)
                        Complete();
                }
            }, null, Timeout.Infinite, Timeout.Infinite);

            WriteRaw(BuildPaletteQuery(options.Size));
            var result = await completion.Task.ConfigureAwait(false);
            _cachedPalette = result;
            return result;
        }
        finally
        {
            _paletteDetectionTask = null;
        }
    }

    private static string BuildPaletteQuery(int size)
    {
        var builder = new StringBuilder(size * 8 + 80);
        for (int i = 0; i < size; i++)
            builder.Append("\x1b]4;").Append(i).Append(";?\x07");

        builder
            .Append("\x1b]10;?\x07")
            .Append("\x1b]11;?\x07")
            .Append("\x1b]12;?\x07")
            .Append("\x1b]13;?\x07")
            .Append("\x1b]14;?\x07")
            .Append("\x1b]15;?\x07")
            .Append("\x1b]16;?\x07")
            .Append("\x1b]17;?\x07")
            .Append("\x1b]19;?\x07");

        return builder.ToString();
    }

    private void WriteRaw(string sequence)
    {
        if (_config.Testing || _isDestroyed || string.IsNullOrEmpty(sequence))
            return;

        _nativeRenderer.WriteOut(GetOutputEncoding().GetBytes(sequence));
    }

    private Encoding GetOutputEncoding()
    {
        try { return System.Console.OutputEncoding; }
        catch { return Encoding.UTF8; }
    }

    private static (ScreenMode ScreenMode, int FooterHeight, ExternalOutputMode ExternalOutputMode) ResolveModes(CliRendererConfig config)
    {
        ScreenMode screenMode = config.ScreenMode;
        var alternateScreenOverride = Environment.GetEnvironmentVariable("OTUI_USE_ALTERNATE_SCREEN");
        if (alternateScreenOverride is not null)
            screenMode = IsTruthy(alternateScreenOverride) ? ScreenMode.AlternateScreen : ScreenMode.MainScreen;

        int footerHeight = screenMode == ScreenMode.SplitFooter
            ? NormalizeFooterHeight(config.FooterHeight)
            : CliRendererConfig.DefaultFooterHeight;

        ExternalOutputMode externalOutputMode = config.ExternalOutputMode;
        if (screenMode == ScreenMode.SplitFooter && externalOutputMode == ExternalOutputMode.Passthrough)
            externalOutputMode = ExternalOutputMode.CaptureStdout;

        var stdoutOverride = Environment.GetEnvironmentVariable("OTUI_OVERRIDE_STDOUT");
        if (stdoutOverride is not null)
            externalOutputMode = IsTruthy(stdoutOverride) && screenMode == ScreenMode.SplitFooter
                ? ExternalOutputMode.CaptureStdout
                : ExternalOutputMode.Passthrough;

        if (externalOutputMode == ExternalOutputMode.CaptureStdout && screenMode != ScreenMode.SplitFooter)
            throw new InvalidOperationException("externalOutputMode \"CaptureStdout\" requires screenMode \"SplitFooter\".");

        return (screenMode, footerHeight, externalOutputMode);
    }

    private static int NormalizeFooterHeight(int footerHeight)
    {
        if (footerHeight <= 0)
            throw new InvalidOperationException("footerHeight must be greater than 0.");

        return footerHeight;
    }

    private static bool IsTruthy(string value) =>
        value.Equals("1", StringComparison.OrdinalIgnoreCase)
        || value.Equals("true", StringComparison.OrdinalIgnoreCase)
        || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
        || value.Equals("on", StringComparison.OrdinalIgnoreCase);

    private static string MoveCursor(int row, int column) => $"\x1b[{Math.Max(1, row)};{Math.Max(1, column)}H";

    private static string MoveCursorAndClear(int row, int column) => MoveCursor(row, column) + "\x1b[J";

    private static string ScrollDown(int lines) => lines > 0 ? $"\x1b[{lines}T" : string.Empty;

    private static string ScrollUp(int lines) => lines > 0 ? $"\x1b[{lines}S" : string.Empty;

    #endregion

    #region Selection

    private Selection? _currentSelection;

    /// <inheritdoc/>
    public bool HasSelection => _currentSelection is not null;

    /// <inheritdoc/>
    public Selection? GetSelection() => _currentSelection;

    public Renderable? GetSelectionContainer() => _selectionContainers.Count > 0 ? _selectionContainers[^1] : null;

    /// <inheritdoc/>
    public void RequestSelectionUpdate()
    {
        if (_currentSelection?.IsDragging != true)
            return;

        var maybeRenderable = Renderable.GetByNumber((int)HitTest(_latestPointerX, _latestPointerY));
        UpdateSelection(maybeRenderable, _latestPointerX, _latestPointerY);
    }

    /// <inheritdoc/>
    public void ClearSelection()
    {
        if (_currentSelection is not null)
        {
            foreach (var renderable in _currentSelection.TouchedRenderables)
            {
                if (renderable.Selectable && !renderable.IsDestroyed)
                    renderable.OnSelectionChanged(null);
            }
        }

        _currentSelection = null;
        _selectionContainers.Clear();
    }

    /// <inheritdoc/>
    public void StartSelection(Renderable renderable, int x, int y)
    {
        if (!renderable.Selectable)
            return;

        ClearSelection();
        _selectionContainers.Add(renderable.Parent ?? Root);
        _currentSelection = new Selection(renderable, x, y) { IsStart = true };
        NotifySelectablesOfSelectionChange();
    }

    /// <inheritdoc/>
    public void UpdateSelection(Renderable? currentRenderable, int x, int y, bool finishDragging = false)
    {
        if (_currentSelection is null)
            return;

        _currentSelection.IsStart = false;
        _currentSelection.UpdateFocus(x, y);

        if (finishDragging)
            _currentSelection.IsDragging = false;

        if (_selectionContainers.Count > 0)
        {
            var currentContainer = _selectionContainers[^1];

            if (currentRenderable is null || !IsWithinContainer(currentRenderable, currentContainer))
            {
                var parentContainer = currentContainer.Parent ?? Root;
                if (!ReferenceEquals(parentContainer, currentContainer))
                    _selectionContainers.Add(parentContainer);
            }
            else if (_selectionContainers.Count > 1)
            {
                int containerIndex = _selectionContainers.IndexOf(currentRenderable);
                if (containerIndex == -1)
                {
                    var immediateParent = currentRenderable.Parent ?? Root;
                    containerIndex = _selectionContainers.IndexOf(immediateParent);
                }

                if (containerIndex != -1 && containerIndex < _selectionContainers.Count - 1)
                    _selectionContainers.RemoveRange(containerIndex + 1, _selectionContainers.Count - containerIndex - 1);
            }
        }

        NotifySelectablesOfSelectionChange();
    }

    #endregion

    #region Selection Helpers

    private void FinishSelection()
    {
        if (_currentSelection is null)
            return;

        _currentSelection.IsDragging = false;
        Emit(RendererEventNames.Selection, _currentSelection);
        NotifySelectablesOfSelectionChange();
    }

    private bool IsWithinContainer(Renderable renderable, Renderable container)
    {
        Renderable? current = renderable;
        while (current is not null)
        {
            if (ReferenceEquals(current, container))
                return true;

            current = current.Parent;
        }

        return false;
    }

    private void NotifySelectablesOfSelectionChange()
    {
        if (_currentSelection is null)
            return;

        var selectedRenderables = new List<Renderable>();
        var touchedRenderables = new List<Renderable>();
        var currentContainer = _selectionContainers.Count > 0 ? _selectionContainers[^1] : Root;

        WalkSelectableRenderables(currentContainer, _currentSelection.Bounds, selectedRenderables, touchedRenderables);

        foreach (var renderable in _currentSelection.TouchedRenderables)
        {
            if (!touchedRenderables.Contains(renderable) && !renderable.IsDestroyed)
                renderable.OnSelectionChanged(null);
        }

        _currentSelection.SetSelectedRenderables(selectedRenderables);
        _currentSelection.SetTouchedRenderables(touchedRenderables);
    }

    private void WalkSelectableRenderables(
        Renderable container,
        ViewportBounds selectionBounds,
        List<Renderable> selectedRenderables,
        List<Renderable> touchedRenderables)
    {
        foreach (var child in container.GetChildrenSortedByPrimaryAxis())
        {
            if (child.IsDestroyed || !OverlapsSelection(child, selectionBounds))
                continue;

            if (child.Selectable)
            {
                bool hasSelection = child.OnSelectionChanged(_currentSelection);
                if (hasSelection)
                    selectedRenderables.Add(child);

                touchedRenderables.Add(child);
            }

            if (child.GetChildrenCount() > 0)
                WalkSelectableRenderables(child, selectionBounds, selectedRenderables, touchedRenderables);
        }
    }

    private static bool OverlapsSelection(Renderable renderable, ViewportBounds selectionBounds)
    {
        var renderableBounds = new ViewportBounds((int)renderable.ScreenX, (int)renderable.ScreenY, renderable.Width, renderable.Height);
        if (renderableBounds.Width <= 0 || renderableBounds.Height <= 0)
            return false;

        return renderableBounds.X < selectionBounds.X + selectionBounds.Width
            && renderableBounds.X + renderableBounds.Width > selectionBounds.X
            && renderableBounds.Y < selectionBounds.Y + selectionBounds.Height
            && renderableBounds.Y + renderableBounds.Height > selectionBounds.Y;
    }

    #endregion

    #region Lifecycle

    /// <inheritdoc/>
    public void RegisterLifecyclePass(Renderable renderable) =>
        _lifecyclePasses.Add(renderable);

    /// <inheritdoc/>
    public void UnregisterLifecyclePass(Renderable renderable) =>
        _lifecyclePasses.Remove(renderable);

    /// <inheritdoc/>
    public IReadOnlySet<Renderable> GetLifecyclePasses() => _lifecyclePasses;

    #endregion

    #region Input

    /// <inheritdoc/>
    public KeyHandler KeyInput => _keyHandler;

    /// <inheritdoc/>
    public KeyHandler InternalKeyInput => _keyHandler;

    #endregion

    #region Resize

    public void Resize(int width, int height)
    {
        if (width == _terminalWidth && height == _terminalHeight)
            return;

        int previousTerminalWidth = _terminalWidth;
        _terminalWidth = width;
        _terminalHeight = height;

        SetCapturedRenderable(null);
        _stdinParser?.ResetMouseState();

        if (_splitHeight > 0 && width < previousTerminalWidth)
            WriteRaw(MoveCursorAndClear(Math.Max(1, _terminalHeight - (_splitHeight * 2)), 1));

        Console.Resize(width, height);
        ApplyScreenMode(_screenMode);
    }

    #endregion

    #region Destroy

    public void Destroy()
    {
        if (_isDestroyed) return;

        if (_rendering)
        {
            // Defer destruction until after current frame
            _destroyRequested = true;
            _immediateRerenderRequested = false;
            _isRunning = false;
            return;
        }

        _isDestroyed = true;

        _renderTimer?.Dispose();
        _renderTimer = null;

        StopRunning();
        Console.Dispose();
        Root.DestroyRecursively();
        FlushCapturedStdout(_terminalHeight, force: true);
        if (_stdoutInterceptInstalled)
            System.Console.SetOut(_originalStdout);
        _stdoutInterceptInstalled = false;
        if (_stderrInterceptInstalled)
            System.Console.SetError(_originalStderr);
        _stderrInterceptInstalled = false;

        TeardownTerminal();

        Emit(RendererEventNames.Destroy);
        _config.OnDestroy?.Invoke();

        _stdinParser = null;
        _nativeRenderer.Dispose();

        if (_exitOnDestroy)
            Environment.Exit(0);
    }

    public void Dispose() => Destroy();

    #endregion

    internal IDisposable SubscribeInterceptedOutput(Action<string>? stdoutListener, Action<string>? stderrListener)
    {
        lock (_interceptedOutputListenerLock)
        {
            if (stdoutListener is not null)
                _stdoutInterceptionListeners.Add(stdoutListener);

            if (stderrListener is not null)
                _stderrInterceptionListeners.Add(stderrListener);
        }

        UpdateStdoutInterception();
        return new InterceptedOutputUnsubscriber(this, stdoutListener, stderrListener);
    }

    private void NotifyInterceptedOutputListeners(string text, bool error)
    {
        Action<string>[] listeners;
        lock (_interceptedOutputListenerLock)
        {
            listeners = (error ? _stderrInterceptionListeners : _stdoutInterceptionListeners).ToArray();
        }

        foreach (var listener in listeners)
            listener(text);
    }

    private void RemoveInterceptedOutputListener(Action<string>? stdoutListener, Action<string>? stderrListener)
    {
        lock (_interceptedOutputListenerLock)
        {
            if (stdoutListener is not null)
                _stdoutInterceptionListeners.RemoveAll(candidate => ReferenceEquals(candidate, stdoutListener));

            if (stderrListener is not null)
                _stderrInterceptionListeners.RemoveAll(candidate => ReferenceEquals(candidate, stderrListener));
        }

        UpdateStdoutInterception();
    }

    private sealed class ResponseUnsubscriber(CliRenderer renderer, Action<StdinEvent.Response> listener) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            renderer.RemoveResponseListener(listener);
        }
    }

    private sealed class InterceptedOutputUnsubscriber(
        CliRenderer renderer,
        Action<string>? stdoutListener,
        Action<string>? stderrListener) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            renderer.RemoveInterceptedOutputListener(stdoutListener, stderrListener);
        }
    }

    private sealed class InterceptingTextWriter(Encoding encoding, Action<string> onWrite) : TextWriter
    {
        public override Encoding Encoding => encoding;

        public override void Write(char value) => onWrite(value.ToString());

        public override void Write(string? value)
        {
            if (!string.IsNullOrEmpty(value))
                onWrite(value);
        }

        public override void Write(char[] buffer, int index, int count)
        {
            if (count > 0)
                onWrite(new string(buffer, index, count));
        }

        public override Task WriteAsync(char value)
        {
            Write(value);
            return Task.CompletedTask;
        }

        public override Task WriteAsync(string? value)
        {
            Write(value);
            return Task.CompletedTask;
        }

        public override Task WriteAsync(char[] buffer, int index, int count)
        {
            Write(buffer, index, count);
            return Task.CompletedTask;
        }
    }
}
