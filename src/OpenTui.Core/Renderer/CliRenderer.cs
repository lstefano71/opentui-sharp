using System.Diagnostics;

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

    private Renderable? _currentFocusedRenderable;
    private readonly HashSet<Renderable> _lifecyclePasses = [];
    private readonly List<Func<float, Task>> _frameCallbacks = [];

    private readonly KeyHandler _keyHandler;
    private StdinParser? _stdinParser;
    private CancellationTokenSource? _inputCts;
    private Thread? _inputThread;

    private readonly bool _useMouse;
    private readonly bool _autoFocus;
    private readonly bool _enableMouseMovement;
    private readonly List<Action<OptimizedBuffer, float>> _postProcessFns;

    private bool _terminalIsSetup;

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
    }

    private void SetupRawInput()
    {
        if (OperatingSystem.IsWindows())
        {
            // Windows code page 65001 for UTF-8
            try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }
            try { Console.InputEncoding = System.Text.Encoding.UTF8; } catch { }
        }
    }

    private void TeardownTerminal()
    {
        if (!_terminalIsSetup) return;
        _terminalIsSetup = false;

        _inputCts?.Cancel();

        if (_useMouse)
            _nativeRenderer.DisableMouse();

        var kittyConfig = _config.UseKittyKeyboard;
        if (kittyConfig is not null)
            _nativeRenderer.DisableKittyKeyboard();

        _nativeRenderer.RestoreTerminalModes();

        Console.CancelKeyPress -= OnCancelKeyPress;
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        if (_config.ExitOnCtrlC)
        {
            e.Cancel = true;
            Destroy();
        }
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
                    _stdinParser.Push(buffer.AsSpan(0, bytesRead));
                    DrainStdinParser();
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
        if (_stdinParser is null) return;

        _stdinParser.Drain(HandleStdinEvent);
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
        }
    }

    #endregion

    #region Mouse Dispatch

    private void ProcessSingleMouseEvent(RawMouseEvent mouseEvent)
    {
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

        if (mouseEvent.Type == MouseEventType.Down)
        {
            var targetId = HitTest(mouseEvent.X, mouseEvent.Y);
            var target = Renderable.GetByNumber((int)targetId);
            if (target is not null)
            {
                var evt = CreateMouseEvent(target, mouseEvent);
                target.ProcessMouseEvent(evt);

                if (_autoFocus && mouseEvent.Button == 0 && !evt.IsDefaultPrevented)
                {
                    Renderable? current = target;
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
            }
            return;
        }

        // Move / up / drag events
        if (mouseEvent.Type is MouseEventType.Move or MouseEventType.Up or MouseEventType.Drag)
        {
            var targetId = HitTest(mouseEvent.X, mouseEvent.Y);
            var target = Renderable.GetByNumber((int)targetId);
            if (target is not null)
            {
                var evt = CreateMouseEvent(target, mouseEvent);
                target.ProcessMouseEvent(evt);
            }
        }
    }

    private static UiMouseEvent CreateMouseEvent(Renderable target, RawMouseEvent raw) => new()
    {
        Type = raw.Type,
        Button = raw.Button,
        X = raw.X,
        Y = raw.Y,
        Modifiers = raw.Modifiers,
        Scroll = raw.Scroll,
        Target = target,
    };

    private uint HitTest(int x, int y) =>
        _nativeRenderer.CheckHit((uint)Math.Max(0, x), (uint)Math.Max(0, y));

    #endregion

    #region Render Loop

    /// <inheritdoc/>
    public void RequestRender()
    {
        if (_isDestroyed) return;

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

    /// <inheritdoc/>
    public void RequestSelectionUpdate() { /* TODO */ }

    /// <inheritdoc/>
    public void ClearSelection()
    {
        _currentSelection = null;
        Emit(RendererEventNames.Selection);
    }

    /// <inheritdoc/>
    public void StartSelection(Renderable renderable, int x, int y)
    {
        _currentSelection = new Selection(renderable, x, y);
        Emit(RendererEventNames.Selection);
    }

    /// <inheritdoc/>
    public void UpdateSelection(Renderable? currentRenderable, int x, int y, bool finishDragging = false)
    {
        _currentSelection?.UpdateFocus(x, y);
        Emit(RendererEventNames.Selection);
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
    }

    public void Dispose() => Destroy();

    #endregion
}
