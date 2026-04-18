using System.Diagnostics;
using System.Runtime.InteropServices;

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
    private Timer? _renderTimer;
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

    private readonly bool _useMouse;
    private readonly bool _autoFocus;
    private readonly bool _enableMouseMovement;
    private readonly List<Action<OptimizedBuffer, float>> _postProcessFns;

    private bool _terminalIsSetup;
    private bool? _terminalFocusState;
    private bool _shouldRestoreModesOnNextFocus;
    private uint _savedConsoleMode;
    private bool _hasConsoleMode;
    private volatile bool _exitOnDestroy;
    private bool _destroyRequested;

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

    internal bool? TerminalFocusState => _terminalFocusState;

    internal bool ShouldRestoreModesOnNextFocus => _shouldRestoreModesOnNextFocus;

    internal void PresentTestFrame(float deltaTime = 16f)
    {
        _frameId++;
        Root.Render(NextRenderBuffer, deltaTime);
        foreach (var fn in _postProcessFns)
            fn(NextRenderBuffer, deltaTime);
        _nativeRenderer.Render();
    }

    #endregion

    #region Constructor

    private CliRenderer(NativeRenderer nativeRenderer, int width, int height, CliRendererConfig config)
    {
        _nativeRenderer = nativeRenderer;
        _config = config;
        Width = width;
        Height = height;

        _targetFrameTimeMs = 1000.0 / config.TargetFps;
        _minTargetFrameTimeMs = 1000.0 / config.MaxFps;
        _useMouse = config.UseMouse;
        _autoFocus = config.AutoFocus;
        _enableMouseMovement = config.EnableMouseMovement;
        _postProcessFns = config.PostProcessFns ?? [];

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
            _nativeRenderer.SetBackgroundColor(bg);

        // Threading
        _nativeRenderer.SetUseThread(config.UseThread);

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
        });

        // Root renderable
        Root = new RootRenderable(this);
    }

    /// <summary>
    /// Create and optionally set up the terminal renderer.
    /// </summary>
    public static CliRenderer Create(CliRendererConfig? config = null)
    {
        config ??= new CliRendererConfig();

        int width = config.Width ?? TryGetConsoleWidth();
        int height = config.Height ?? TryGetConsoleHeight();

        var nativeRenderer = NativeRenderer.Create(
            (uint)width, (uint)height,
            testing: config.Testing,
            remote: config.Remote
        );

        var renderer = new CliRenderer(nativeRenderer, width, height, config);

        if (!config.Testing)
            renderer.SetupTerminal();

        return renderer;
    }

    private static int TryGetConsoleWidth()
    {
        try { return Console.WindowWidth; }
        catch { return 80; }
    }

    private static int TryGetConsoleHeight()
    {
        try { return Console.WindowHeight; }
        catch { return 24; }
    }

    #endregion

    #region Terminal Setup / Teardown

    private void SetupTerminal()
    {
        if (_terminalIsSetup) return;
        _terminalIsSetup = true;

        bool useAlternateScreen = _config.ScreenMode == ScreenMode.AlternateScreen;
        _nativeRenderer.SetupTerminal(useAlternateScreen);

        if (_useMouse)
            _nativeRenderer.EnableMouse();

        var kittyConfig = _config.UseKittyKeyboard;
        if (kittyConfig is not null)
            _nativeRenderer.EnableKittyKeyboard(kittyConfig.BuildFlags());

        // Set up console for raw input on Windows
        SetupRawInput();

        // Handle Ctrl+C
        Console.CancelKeyPress += OnCancelKeyPress;

        // Start input reading thread
        StartInputLoop();

        // Start resize polling timer
        StartResizeWatcher();
    }

    private void SetupRawInput()
    {
        if (OperatingSystem.IsWindows())
        {
            try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }
            try { Console.InputEncoding = System.Text.Encoding.UTF8; } catch { }

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
        _nativeRenderer.RestoreTerminalModes();

        Console.CancelKeyPress -= OnCancelKeyPress;
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
                int w = Console.WindowWidth;
                int h = Console.WindowHeight;
                if (w > 0 && h > 0 && (w != Width || h != Height))
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
        var stdin = Console.OpenStandardInput();
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
                _keyHandler.ProcessParsedKey(keyEvt.ParsedKey);
                break;
            case StdinEvent.Mouse mouseEvt:
                if (_useMouse)
                    ProcessSingleMouseEvent(mouseEvt.MouseEvent);
                break;
            case StdinEvent.Paste pasteEvt:
                _keyHandler.ProcessPaste(pasteEvt.Bytes, pasteEvt.Metadata);
                break;
            case StdinEvent.Response responseEvt:
                HandleResponseEvent(responseEvt);
                break;
        }
    }

    private void HandleResponseEvent(StdinEvent.Response evt)
    {
        if (TryHandleFocusResponse(evt.Sequence))
            return;
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

    #endregion

    #region Mouse Dispatch

    private void ProcessSingleMouseEvent(RawMouseEvent mouseEvent)
    {
        _latestPointerX = mouseEvent.X;
        _latestPointerY = mouseEvent.Y;

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

        if (!_updateScheduled && _renderTimer is null)
        {
            _updateScheduled = true;
            var nowMs = _clock.ElapsedMilliseconds;
            var elapsed = nowMs - _lastTimeMs;
            var delay = Math.Max(_minTargetFrameTimeMs - elapsed, 0);

            if (delay <= 0)
            {
                ThreadPool.QueueUserWorkItem(_ => ActivateFrame());
            }
            else
            {
                _renderTimer = new Timer(
                    _ => ActivateFrame(),
                    null,
                    (int)delay,
                    Timeout.Infinite
                );
            }
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
    public object? Capabilities => null; // TODO: terminal capability detection

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
        if (width == Width && height == Height) return;
        Width = width;
        Height = height;

        _nativeRenderer.Resize((uint)width, (uint)height);

        // Re-acquire buffer wrappers (non-owning) after native resize
        NextRenderBuffer.Dispose();
        CurrentRenderBuffer.Dispose();
        NextRenderBuffer = OptimizedBuffer.WrapExisting(_nativeRenderer.GetNextBuffer());
        CurrentRenderBuffer = OptimizedBuffer.WrapExisting(_nativeRenderer.GetCurrentBuffer());

        Root.Resize(width, height);
        Emit<(int Width, int Height)>(RendererEventNames.Resize, (width, height));
        RequestRender();
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
        Root.DestroyRecursively();

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
}
