using System.Collections;
using System.Text;

namespace OpenTui.Core;

/// <summary>Specifies which edge of the terminal the overlay console attaches to.</summary>
public enum ConsolePosition
{
    /// <summary>
    /// Represents the Top option.
    /// </summary>
    Top,
    /// <summary>
    /// Represents the Bottom option.
    /// </summary>
    Bottom,
    /// <summary>
    /// Represents the Left option.
    /// </summary>
    Left,
    /// <summary>
    /// Represents the Right option.
    /// </summary>
    Right,
}

/// <summary>Built-in actions that can be triggered by console key bindings.</summary>
public enum ConsoleAction
{
    /// <summary>
    /// Represents the Scroll Up option.
    /// </summary>
    ScrollUp,
    /// <summary>
    /// Represents the Scroll Down option.
    /// </summary>
    ScrollDown,
    /// <summary>
    /// Represents the Scroll To Top option.
    /// </summary>
    ScrollToTop,
    /// <summary>
    /// Represents the Scroll To Bottom option.
    /// </summary>
    ScrollToBottom,
    /// <summary>
    /// Represents the Position Previous option.
    /// </summary>
    PositionPrevious,
    /// <summary>
    /// Represents the Position Next option.
    /// </summary>
    PositionNext,
    /// <summary>
    /// Represents the Size Increase option.
    /// </summary>
    SizeIncrease,
    /// <summary>
    /// Represents the Size Decrease option.
    /// </summary>
    SizeDecrease,
    /// <summary>
    /// Represents the Copy Selection option.
    /// </summary>
    CopySelection,
}

/// <summary>Severity levels for entries written to <see cref="TerminalConsole"/>.</summary>
public enum ConsoleLogLevel
{
    /// <summary>
    /// Represents the Log option.
    /// </summary>
    Log,
    /// <summary>
    /// Represents the Info option.
    /// </summary>
    Info,
    /// <summary>
    /// Represents the Warn option.
    /// </summary>
    Warn,
    /// <summary>
    /// Represents the Error option.
    /// </summary>
    Error,
    /// <summary>
    /// Represents the Debug option.
    /// </summary>
    Debug,
}

/// <summary>Defines a keyboard shortcut that triggers a <see cref="ConsoleAction"/>.</summary>
public sealed class ConsoleKeyBinding
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public required string Name { get; init; }
    /// <summary>
    /// Gets or sets the ctrl.
    /// </summary>
    public bool Ctrl { get; init; }
    /// <summary>
    /// Gets or sets the shift.
    /// </summary>
    public bool Shift { get; init; }
    /// <summary>
    /// Gets or sets the alt.
    /// </summary>
    public bool Alt { get; init; }
    /// <summary>
    /// Gets or sets the meta.
    /// </summary>
    public bool Meta { get; init; }
    /// <summary>
    /// Gets or sets the action.
    /// </summary>
    public required ConsoleAction Action { get; init; }
}

/// <summary>Represents a single log entry stored by <see cref="TerminalConsole"/>.</summary>
public sealed class ConsoleLogEntry
{
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    public required DateTime Timestamp { get; init; }
    /// <summary>
    /// Gets or sets the level.
    /// </summary>
    public required ConsoleLogLevel Level { get; init; }
    /// <summary>
    /// Gets or sets the text.
    /// </summary>
    public required string Text { get; init; }
}

/// <summary>
/// Built-in overlay console for logging, captured stdout, scrolling, and text selection while a
/// <see cref="CliRenderer"/> owns the terminal.
/// </summary>
public sealed class TerminalConsole : IDisposable
{
    private const int MinSizePercent = 10;
    private const int MaxSizePercent = 100;
    private const int SizeStepPercent = 5;
    private const int MaxStoredLogs = 2000;
    private const int IndentWidth = 2;

    private static readonly ConsoleKeyBinding[] DefaultKeyBindings =
    [
        new() { Name = "up", Action = ConsoleAction.ScrollUp },
        new() { Name = "down", Action = ConsoleAction.ScrollDown },
        new() { Name = "up", Shift = true, Action = ConsoleAction.ScrollToTop },
        new() { Name = "down", Shift = true, Action = ConsoleAction.ScrollToBottom },
        new() { Name = "p", Ctrl = true, Action = ConsoleAction.PositionPrevious },
        new() { Name = "o", Ctrl = true, Action = ConsoleAction.PositionNext },
        new() { Name = "+", Action = ConsoleAction.SizeIncrease },
        new() { Name = "=", Shift = true, Action = ConsoleAction.SizeIncrease },
        new() { Name = "-", Action = ConsoleAction.SizeDecrease },
        new() { Name = "c", Ctrl = true, Shift = true, Action = ConsoleAction.CopySelection },
    ];

    private readonly CliRenderer _renderer;
    private readonly object _sync = new();
    private readonly List<ConsoleLogEntry> _entries = [];
    private readonly List<DisplayLine> _displayLines = [];
    private readonly StringBuilder _pendingStdout = new();
    private readonly StringBuilder _pendingStderr = new();
    private readonly ConsolePosition[] _positions =
    [
        ConsolePosition.Top,
        ConsolePosition.Right,
        ConsolePosition.Bottom,
        ConsolePosition.Left,
    ];

    private ConsoleKeyBinding[] _keyBindings = [];
    private IDisposable? _outputSubscription;
    private OptimizedBuffer? _frameBuffer;

    private bool _visible;
    private bool _focused;
    private bool _captureActivated;
    private bool _displayLinesDirty = true;
    private bool _scrollToBottomPending = true;
    private bool _isDragging;

    private ConsolePosition _position = ConsolePosition.Bottom;
    private int _sizePercent = 30;
    private int _consoleX;
    private int _consoleY;
    private int _consoleWidth;
    private int _consoleHeight;
    private int _scrollTopIndex;
    private int _currentLineIndex;

    private SelectionPoint? _selectionStart;
    private SelectionPoint? _selectionEnd;
    private HitBounds _copyButtonBounds;

    private readonly Rgba _infoColor = Rgba.FromHex("#00FFFF");
    private readonly Rgba _warnColor = Rgba.FromHex("#FFFF00");
    private readonly Rgba _errorColor = Rgba.FromHex("#FF0000");
    private readonly Rgba _debugColor = Rgba.FromHex("#808080");
    private readonly Rgba _defaultColor = Rgba.FromHex("#FFFFFF");
    private readonly Rgba _backgroundColor = new(0.1f, 0.1f, 0.1f, 0.7f);
    private readonly Rgba _titleBarColor = new(0.05f, 0.05f, 0.05f, 0.7f);
    private readonly Rgba _titleBarTextColor = Rgba.FromHex("#FFFFFF");
    private readonly Rgba _cursorColor = Rgba.FromHex("#00A0FF");
    private readonly Rgba _selectionColor = new(0.3f, 0.5f, 0.8f, 0.5f);
    private readonly Rgba _copyButtonColor = Rgba.FromHex("#00A0FF");
    private readonly Rgba _disabledColor = Rgba.FromInts(100, 100, 100);

    /// <summary>
    /// Initializes a new instance of the TerminalConsole class.
    /// </summary>
    /// <param name="renderer">The renderer instance.</param>
    public TerminalConsole(CliRenderer renderer)
    {
        _renderer = renderer;
        UpdateDimensions(renderer.TerminalWidth, renderer.TerminalHeight);
        _renderer.AddPostProcessFn(RenderOverlay);
    }

    /// <summary>Gets the currently buffered log entries.</summary>
    public IReadOnlyList<ConsoleLogEntry> Entries
    {
        get
        {
            lock (_sync)
                return _entries.ToArray();
        }
    }

    /// <summary>Gets or sets the keyboard bindings that control the console overlay.</summary>
    public IReadOnlyList<ConsoleKeyBinding> KeyBindings
    {
        get => _keyBindings;
        set
        {
            _keyBindings = value?.ToArray() ?? [];
            RequestRender();
        }
    }

    /// <summary>
    /// Gets or sets the on copy selection.
    /// </summary>
    public Action<string>? OnCopySelection { get; set; }

    /// <summary>
    /// Gets the visible.
    /// </summary>
    public bool Visible => _visible;

    /// <summary>
    /// Gets the focused.
    /// </summary>
    public bool Focused => _focused;

    /// <summary>
    /// Gets the position.
    /// </summary>
    public ConsolePosition Position => _position;

    /// <summary>
    /// Gets the size percent.
    /// </summary>
    public int SizePercent => _sizePercent;

    /// <summary>
    /// Gets the bounds.
    /// </summary>
    public (int X, int Y, int Width, int Height) Bounds => (_consoleX, _consoleY, _consoleWidth, _consoleHeight);

    /// <summary>
    /// Performs show.
    /// </summary>
    public void Show()
    {
        EnsureActivated();
        _visible = true;
        _focused = true;
        UpdateDimensions(_renderer.TerminalWidth, _renderer.TerminalHeight);
        ScrollToBottom(forceCursorToLastLine: true);
        RequestRender();
    }

    /// <summary>
    /// Performs hide.
    /// </summary>
    public void Hide()
    {
        if (!_visible)
            return;

        _visible = false;
        _focused = false;
        _isDragging = false;
        RequestRender();
    }

    /// <summary>
    /// Performs toggle.
    /// </summary>
    public void Toggle()
    {
        if (_visible)
        {
            if (_focused)
                Hide();
            else
                Focus();
            return;
        }

        Show();
    }

    /// <summary>
    /// Gives this instance input focus.
    /// </summary>
    public void Focus()
    {
        EnsureActivated();
        _visible = true;
        _focused = true;
        ScrollToBottom(forceCursorToLastLine: true);
        RequestRender();
    }

    /// <summary>
    /// Removes input focus from this instance.
    /// </summary>
    public void Blur()
    {
        if (!_focused)
            return;

        _focused = false;
        _isDragging = false;
        RequestRender();
    }

    /// <summary>
    /// Performs clear.
    /// </summary>
    public void Clear()
    {
        lock (_sync)
            _entries.Clear();

        _displayLines.Clear();
        _displayLinesDirty = true;
        _scrollTopIndex = 0;
        _currentLineIndex = 0;
        _scrollToBottomPending = true;
        ClearSelection();
        RequestRender();
    }

    /// <summary>
    /// Performs log.
    /// </summary>
    /// <param name="args">The arguments.</param>
    public void Log(params object?[] args) => AddEntry(ConsoleLogLevel.Log, FormatArguments(args));

    /// <summary>
    /// Performs info.
    /// </summary>
    /// <param name="args">The arguments.</param>
    public void Info(params object?[] args) => AddEntry(ConsoleLogLevel.Info, FormatArguments(args));

    /// <summary>
    /// Performs warn.
    /// </summary>
    /// <param name="args">The arguments.</param>
    public void Warn(params object?[] args) => AddEntry(ConsoleLogLevel.Warn, FormatArguments(args));

    /// <summary>
    /// Performs error.
    /// </summary>
    /// <param name="args">The arguments.</param>
    public void Error(params object?[] args) => AddEntry(ConsoleLogLevel.Error, FormatArguments(args));

    /// <summary>
    /// Performs debug.
    /// </summary>
    /// <param name="args">The arguments.</param>
    public void Debug(params object?[] args) => AddEntry(ConsoleLogLevel.Debug, FormatArguments(args));

    internal void Resize(int terminalWidth, int terminalHeight)
    {
        UpdateDimensions(terminalWidth, terminalHeight);
        ClampScrollState();
        RequestRender();
    }

    internal bool HandleKey(ParsedKey key)
    {
        if (!_visible || !_focused || key.EventType == KeyEventType.Release)
            return false;

        if (key.Name == "escape")
        {
            Blur();
            return true;
        }

        if (TryMatchBinding(key, out var action))
        {
            ExecuteAction(action);
            return true;
        }

        return false;
    }

    internal bool HandleMouse(RawMouseEvent mouseEvent)
    {
        if (!_visible)
            return false;

        int localX = mouseEvent.X - _consoleX;
        int localY = mouseEvent.Y - _consoleY;

        if (localX < 0 || localX >= _consoleWidth || localY < 0 || localY >= _consoleHeight)
            return false;

        if (mouseEvent.Type == MouseEventType.Scroll && mouseEvent.Scroll is { } scroll)
        {
            Focus();
            if (scroll.Direction == "up")
                ScrollUp();
            else if (scroll.Direction == "down")
                ScrollDown();
            return true;
        }

        if (mouseEvent.Type == MouseEventType.Down && mouseEvent.Button == (int)MouseButton.Left)
            Focus();

        if (localY == 0)
        {
            if (mouseEvent.Type == MouseEventType.Down
                && mouseEvent.Button == (int)MouseButton.Left
                && _copyButtonBounds.Contains(localX, localY))
            {
                TriggerCopy();
            }

            return true;
        }

        int lineIndex = _scrollTopIndex + Math.Max(0, localY - 1);
        int columnIndex = Math.Max(0, localX - 1);

        if (mouseEvent.Type == MouseEventType.Down && mouseEvent.Button == (int)MouseButton.Left)
        {
            _selectionStart = new SelectionPoint(lineIndex, columnIndex);
            _selectionEnd = new SelectionPoint(lineIndex, columnIndex);
            _isDragging = true;
            RequestRender();
            return true;
        }

        if (mouseEvent.Type == MouseEventType.Drag && _isDragging)
        {
            _selectionEnd = new SelectionPoint(lineIndex, columnIndex);

            if (localY <= 1)
                ScrollUp();
            else if (localY >= _consoleHeight - 1)
                ScrollDown();
            else
                RequestRender();

            return true;
        }

        if (mouseEvent.Type == MouseEventType.Up)
        {
            if (_isDragging)
            {
                _selectionEnd = new SelectionPoint(lineIndex, columnIndex);
                _isDragging = false;
                RequestRender();
            }

            return true;
        }

        return true;
    }

    /// <summary>
    /// Releases the resources used by this instance.
    /// </summary>
    public void Dispose()
    {
        FlushPendingText(_pendingStdout, ConsoleLogLevel.Log);
        FlushPendingText(_pendingStderr, ConsoleLogLevel.Error);

        _renderer.RemovePostProcessFn(RenderOverlay);
        _outputSubscription?.Dispose();
        _outputSubscription = null;

        _frameBuffer?.Dispose();
        _frameBuffer = null;
    }

    private void EnsureActivated()
    {
        if (_captureActivated)
            return;

        _captureActivated = true;
        _outputSubscription = _renderer.SubscribeInterceptedOutput(HandleStdoutChunk, HandleStderrChunk);
    }

    private void HandleStdoutChunk(string text) => AppendCapturedText(_pendingStdout, ConsoleLogLevel.Log, text);

    private void HandleStderrChunk(string text) => AppendCapturedText(_pendingStderr, ConsoleLogLevel.Error, text);

    private void AppendCapturedText(StringBuilder pending, ConsoleLogLevel level, string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        pending.Append(text);

        int start = 0;
        for (int i = 0; i < pending.Length; i++)
        {
            if (pending[i] != '\n')
                continue;

            int length = i - start;
            string line = pending.ToString(start, length).TrimEnd('\r');
            AddEntry(level, line);
            start = i + 1;
        }

        if (start > 0)
            pending.Remove(0, start);
    }

    private void FlushPendingText(StringBuilder pending, ConsoleLogLevel level)
    {
        if (pending.Length == 0)
            return;

        string line = pending.ToString().TrimEnd('\r');
        pending.Clear();
        if (!string.IsNullOrEmpty(line))
            AddEntry(level, line);
    }

    private void AddEntry(ConsoleLogLevel level, string text)
    {
        bool shouldAutoScroll = _scrollToBottomPending;

        lock (_sync)
        {
            _entries.Add(new ConsoleLogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Text = text ?? string.Empty,
            });

            if (_entries.Count > MaxStoredLogs)
                _entries.RemoveAt(0);
        }

        _displayLinesDirty = true;
        _scrollToBottomPending = shouldAutoScroll;
        RequestRender();
    }

    private void RequestRender() => _renderer.RequestRender();

    private bool TryMatchBinding(ParsedKey key, out ConsoleAction action)
    {
        var bindings = GetEffectiveBindings();
        for (int i = bindings.Length - 1; i >= 0; i--)
        {
            var binding = bindings[i];
            if (binding.Name != key.Name
                || binding.Ctrl != key.Ctrl
                || binding.Shift != key.Shift
                || binding.Alt != key.Option
                || binding.Meta != key.Meta)
            {
                continue;
            }

            action = binding.Action;
            return true;
        }

        action = default;
        return false;
    }

    private ConsoleKeyBinding[] GetEffectiveBindings()
    {
        if (_keyBindings.Length == 0)
            return DefaultKeyBindings;

        var effective = new ConsoleKeyBinding[DefaultKeyBindings.Length + _keyBindings.Length];
        DefaultKeyBindings.CopyTo(effective, 0);
        _keyBindings.CopyTo(effective, DefaultKeyBindings.Length);
        return effective;
    }

    private void ExecuteAction(ConsoleAction action)
    {
        switch (action)
        {
            case ConsoleAction.ScrollUp:
                ScrollUp();
                break;
            case ConsoleAction.ScrollDown:
                ScrollDown();
                break;
            case ConsoleAction.ScrollToTop:
                ScrollToTop();
                break;
            case ConsoleAction.ScrollToBottom:
                ScrollToBottom(forceCursorToLastLine: true);
                RequestRender();
                break;
            case ConsoleAction.PositionPrevious:
                RotatePosition(-1);
                break;
            case ConsoleAction.PositionNext:
                RotatePosition(1);
                break;
            case ConsoleAction.SizeIncrease:
                ChangeSize(SizeStepPercent);
                break;
            case ConsoleAction.SizeDecrease:
                ChangeSize(-SizeStepPercent);
                break;
            case ConsoleAction.CopySelection:
                TriggerCopy();
                break;
        }
    }

    private void ScrollUp()
    {
        if (_currentLineIndex > 0)
        {
            _currentLineIndex--;
            RequestRender();
            return;
        }

        if (_scrollTopIndex > 0)
        {
            _scrollTopIndex--;
            _scrollToBottomPending = false;
            RequestRender();
        }
    }

    private void ScrollDown()
    {
        EnsureDisplayLines();
        int visibleHeight = Math.Max(1, _consoleHeight - 1);
        int maxScrollTop = Math.Max(0, _displayLines.Count - visibleHeight);
        bool canMoveCursor = _currentLineIndex < visibleHeight - 1
            && _scrollTopIndex + _currentLineIndex < _displayLines.Count - 1;

        if (canMoveCursor)
        {
            _currentLineIndex++;
            RequestRender();
            return;
        }

        if (_scrollTopIndex < maxScrollTop)
        {
            _scrollTopIndex++;
            _scrollToBottomPending = _scrollTopIndex == maxScrollTop;
            RequestRender();
        }
    }

    private void ScrollToTop()
    {
        _scrollTopIndex = 0;
        _currentLineIndex = 0;
        _scrollToBottomPending = _displayLines.Count <= Math.Max(1, _consoleHeight - 1);
        RequestRender();
    }

    private void ScrollToBottom(bool forceCursorToLastLine)
    {
        EnsureDisplayLines();

        int visibleHeight = Math.Max(1, _consoleHeight - 1);
        int maxScrollTop = Math.Max(0, _displayLines.Count - visibleHeight);
        _scrollTopIndex = maxScrollTop;
        _scrollToBottomPending = true;

        int visibleLineCount = Math.Min(visibleHeight, Math.Max(0, _displayLines.Count - _scrollTopIndex));
        if (forceCursorToLastLine || _currentLineIndex >= visibleLineCount)
            _currentLineIndex = Math.Max(0, visibleLineCount - 1);
    }

    private void RotatePosition(int delta)
    {
        int currentIndex = Array.IndexOf(_positions, _position);
        int nextIndex = (currentIndex + delta + _positions.Length) % _positions.Length;
        _position = _positions[nextIndex];
        UpdateDimensions(_renderer.TerminalWidth, _renderer.TerminalHeight);
        ClampScrollState();
        RequestRender();
    }

    private void ChangeSize(int delta)
    {
        _sizePercent = Math.Clamp(_sizePercent + delta, MinSizePercent, MaxSizePercent);
        UpdateDimensions(_renderer.TerminalWidth, _renderer.TerminalHeight);
        ClampScrollState();
        RequestRender();
    }

    private void TriggerCopy()
    {
        if (!HasSelection())
            return;

        string text = GetSelectedText();
        if (text.Length == 0)
            return;

        OnCopySelection?.Invoke(text);
        ClearSelection();
        RequestRender();
    }

    private bool HasSelection()
    {
        if (_selectionStart is not { } start || _selectionEnd is not { } end)
            return false;

        return start.Line != end.Line || start.Column != end.Column;
    }

    private void ClearSelection()
    {
        _selectionStart = null;
        _selectionEnd = null;
        _isDragging = false;
    }

    private string GetSelectedText()
    {
        if (!TryNormalizeSelection(out var selection))
            return string.Empty;

        EnsureDisplayLines();
        var lines = new List<string>();
        for (int i = selection.StartLine; i <= selection.EndLine; i++)
        {
            if (i < 0 || i >= _displayLines.Count)
                continue;

            string lineText = _displayLines[i].Text;
            int start = i == selection.StartLine ? Math.Clamp(selection.StartColumn, 0, lineText.Length) : 0;
            int end = i == selection.EndLine ? Math.Clamp(selection.EndColumn, 0, lineText.Length) : lineText.Length;

            if (end <= start)
                continue;

            lines.Add(lineText[start..end]);
        }

        return string.Join("\n", lines);
    }

    private bool TryNormalizeSelection(out NormalizedSelection selection)
    {
        if (_selectionStart is not { } start || _selectionEnd is not { } end)
        {
            selection = default;
            return false;
        }

        bool startFirst = start.Line < end.Line || (start.Line == end.Line && start.Column <= end.Column);
        selection = startFirst
            ? new NormalizedSelection(start.Line, start.Column, end.Line, end.Column)
            : new NormalizedSelection(end.Line, end.Column, start.Line, start.Column);
        return true;
    }

    private (int Start, int End)? GetLineSelectionRange(int lineIndex)
    {
        if (!TryNormalizeSelection(out var selection))
            return null;

        if (lineIndex < selection.StartLine || lineIndex > selection.EndLine || lineIndex >= _displayLines.Count)
            return null;

        string lineText = _displayLines[lineIndex].Text;
        int start = lineIndex == selection.StartLine ? Math.Clamp(selection.StartColumn, 0, lineText.Length) : 0;
        int end = lineIndex == selection.EndLine ? Math.Clamp(selection.EndColumn, 0, lineText.Length) : lineText.Length;
        return start < end ? (start, end) : null;
    }

    private void UpdateDimensions(int terminalWidth, int terminalHeight)
    {
        float sizeFraction = _sizePercent / 100f;

        switch (_position)
        {
            case ConsolePosition.Top:
                _consoleX = 0;
                _consoleY = 0;
                _consoleWidth = Math.Max(1, terminalWidth);
                _consoleHeight = Math.Max(1, (int)Math.Floor(terminalHeight * sizeFraction));
                break;
            case ConsolePosition.Bottom:
                _consoleWidth = Math.Max(1, terminalWidth);
                _consoleHeight = Math.Max(1, (int)Math.Floor(terminalHeight * sizeFraction));
                _consoleX = 0;
                _consoleY = Math.Max(0, terminalHeight - _consoleHeight);
                break;
            case ConsolePosition.Left:
                _consoleWidth = Math.Max(1, (int)Math.Floor(terminalWidth * sizeFraction));
                _consoleHeight = Math.Max(1, terminalHeight);
                _consoleX = 0;
                _consoleY = 0;
                break;
            case ConsolePosition.Right:
                _consoleWidth = Math.Max(1, (int)Math.Floor(terminalWidth * sizeFraction));
                _consoleHeight = Math.Max(1, terminalHeight);
                _consoleX = Math.Max(0, terminalWidth - _consoleWidth);
                _consoleY = 0;
                break;
        }

        _displayLinesDirty = true;

        if (_frameBuffer is { Width: var width, Height: var height }
            && width == (uint)_consoleWidth
            && height == (uint)_consoleHeight)
        {
            return;
        }

        _frameBuffer?.Dispose();
        _frameBuffer = OptimizedBuffer.Create(
            (uint)_consoleWidth,
            (uint)_consoleHeight,
            _renderer.WidthMethod,
            respectAlpha: true,
            id: "terminal-console");
    }

    private void ClampScrollState()
    {
        EnsureDisplayLines();

        int visibleHeight = Math.Max(1, _consoleHeight - 1);
        int maxScrollTop = Math.Max(0, _displayLines.Count - visibleHeight);
        _scrollTopIndex = Math.Clamp(_scrollTopIndex, 0, maxScrollTop);

        int visibleLineCount = Math.Min(visibleHeight, Math.Max(0, _displayLines.Count - _scrollTopIndex));
        _currentLineIndex = Math.Clamp(_currentLineIndex, 0, Math.Max(0, visibleLineCount - 1));
    }

    private void EnsureDisplayLines()
    {
        if (!_displayLinesDirty)
            return;

        _displayLines.Clear();
        int availableWidth = Math.Max(1, _consoleWidth - 1);

        ConsoleLogEntry[] entries;
        lock (_sync)
            entries = _entries.ToArray();

        foreach (var entry in entries)
        {
            string prefix = $"[{entry.Timestamp:HH:mm:ss}] [{entry.Level.ToString().ToUpperInvariant()}] ";
            string[] rawLines = entry.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            for (int i = 0; i < rawLines.Length; i++)
            {
                string fullLine = i == 0 ? prefix + rawLines[i] : new string(' ', IndentWidth) + rawLines[i];
                AppendWrappedLine(fullLine, entry.Level, availableWidth);
            }
        }

        _displayLinesDirty = false;

        if (_scrollToBottomPending)
            ScrollToBottom(forceCursorToLastLine: false);
        else
            ClampScrollState();
    }

    private void AppendWrappedLine(string text, ConsoleLogLevel level, int availableWidth)
    {
        if (text.Length == 0)
        {
            _displayLines.Add(new DisplayLine(string.Empty, level));
            return;
        }

        string remaining = text;
        while (remaining.Length > availableWidth)
        {
            _displayLines.Add(new DisplayLine(remaining[..availableWidth], level));
            remaining = new string(' ', IndentWidth) + remaining[availableWidth..];
        }

        _displayLines.Add(new DisplayLine(remaining, level));
    }

    private void RenderOverlay(OptimizedBuffer buffer, float deltaTime)
    {
        if (!_visible)
            return;

        UpdateDimensions(_renderer.TerminalWidth, _renderer.TerminalHeight);
        EnsureDisplayLines();

        if (_frameBuffer is null)
            return;

        _frameBuffer.Clear(_backgroundColor);
        _frameBuffer.FillRect(0, 0, (uint)_consoleWidth, 1, _titleBarColor);

        string title = _focused ? "Console (Focused)" : "Console";
        int titleX = Math.Max(0, (_consoleWidth - title.Length) / 2);
        _frameBuffer.DrawText(title, (uint)titleX, 0, _titleBarTextColor, _titleBarColor);

        string copyLabel = GetCopyButtonLabel();
        int copyX = Math.Max(0, _consoleWidth - copyLabel.Length - 1);
        _copyButtonBounds = new HitBounds(copyX, 0, copyLabel.Length, 1);
        _frameBuffer.DrawText(
            copyLabel,
            (uint)copyX,
            0,
            HasSelection() ? _copyButtonColor : _disabledColor,
            _titleBarColor);

        int visibleHeight = Math.Max(1, _consoleHeight - 1);
        int endIndex = Math.Min(_displayLines.Count, _scrollTopIndex + visibleHeight);
        int lineY = 1;
        for (int i = _scrollTopIndex; i < endIndex; i++, lineY++)
        {
            var line = _displayLines[i];
            bool showCursor = _focused && i == _scrollTopIndex + _currentLineIndex;
            _frameBuffer.DrawText(showCursor ? ">" : " ", 0, (uint)lineY, showCursor ? _cursorColor : _defaultColor, _backgroundColor);

            Rgba lineColor = GetLevelColor(line.Level);
            if (GetLineSelectionRange(i) is { } selection)
            {
                int start = selection.Start;
                int end = selection.End;
                if (start > 0)
                    _frameBuffer.DrawText(line.Text[..start], 1, (uint)lineY, lineColor, _backgroundColor);

                if (end > start)
                {
                    _frameBuffer.FillRect((uint)(1 + start), (uint)lineY, (uint)(end - start), 1, _selectionColor);
                    _frameBuffer.DrawText(line.Text[start..end], (uint)(1 + start), (uint)lineY, lineColor, _selectionColor);
                }

                if (end < line.Text.Length)
                    _frameBuffer.DrawText(line.Text[end..], (uint)(1 + end), (uint)lineY, lineColor, _backgroundColor);
            }
            else
            {
                _frameBuffer.DrawText(line.Text, 1, (uint)lineY, lineColor, _backgroundColor);
            }
        }

        buffer.DrawFrameBuffer(_consoleX, _consoleY, _frameBuffer, 0, 0, (uint)_consoleWidth, (uint)_consoleHeight);
    }

    private string GetCopyButtonLabel()
    {
        var bindings = GetEffectiveBindings();
        for (int i = bindings.Length - 1; i >= 0; i--)
        {
            if (bindings[i].Action == ConsoleAction.CopySelection)
                return $"[Copy ({FormatBinding(bindings[i])})]";
        }

        return "[Copy]";
    }

    private static string FormatBinding(ConsoleKeyBinding binding)
    {
        var parts = new List<string>(4);
        if (binding.Ctrl) parts.Add("Ctrl");
        if (binding.Shift) parts.Add("Shift");
        if (binding.Alt) parts.Add("Alt");
        if (binding.Meta) parts.Add("Meta");
        parts.Add(binding.Name.Length == 1 ? binding.Name.ToUpperInvariant() : binding.Name);
        return string.Join("+", parts);
    }

    private static string FormatArguments(object?[] args) =>
        string.Join(" ", args.Select(FormatArgument));

    private static string FormatArgument(object? value)
    {
        return value switch
        {
            null => "null",
            string text => text,
            Exception exception => $"{exception.GetType().Name}: {exception.Message}{Environment.NewLine}{exception.StackTrace}",
            IDictionary dictionary => FormatDictionary(dictionary),
            IEnumerable enumerable when value is not string => FormatEnumerable(enumerable),
            DateTime dateTime => dateTime.ToString("O"),
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static string FormatDictionary(IDictionary dictionary)
    {
        var parts = new List<string>();
        foreach (DictionaryEntry entry in dictionary)
            parts.Add($"{entry.Key}: {FormatArgument(entry.Value)}");
        return "{ " + string.Join(", ", parts) + " }";
    }

    private static string FormatEnumerable(IEnumerable enumerable)
    {
        var parts = new List<string>();
        foreach (var value in enumerable)
            parts.Add(FormatArgument(value));
        return "[ " + string.Join(", ", parts) + " ]";
    }

    private Rgba GetLevelColor(ConsoleLogLevel level) => level switch
    {
        ConsoleLogLevel.Info => _infoColor,
        ConsoleLogLevel.Warn => _warnColor,
        ConsoleLogLevel.Error => _errorColor,
        ConsoleLogLevel.Debug => _debugColor,
        _ => _defaultColor,
    };

    private readonly record struct DisplayLine(string Text, ConsoleLogLevel Level);

    private readonly record struct SelectionPoint(int Line, int Column);

    private readonly record struct NormalizedSelection(int StartLine, int StartColumn, int EndLine, int EndColumn);

    private readonly record struct HitBounds(int X, int Y, int Width, int Height)
    {
        public bool Contains(int x, int y) =>
            x >= X && y >= Y && x < X + Width && y < Y + Height;
    }
}
