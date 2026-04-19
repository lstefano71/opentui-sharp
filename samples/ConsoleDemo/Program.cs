using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    UseMouse = true,
});

renderer.SetBackgroundColor(Rgba.FromInts(18, 22, 35));
renderer.Console.KeyBindings =
[
    new ConsoleKeyBinding
    {
        Name = "y",
        Ctrl = true,
        Action = ConsoleAction.CopySelection,
    },
];
renderer.Console.OnCopySelection = text =>
{
    if (renderer.CopyToClipboardOSC52(text))
    {
        renderer.Console.Info($"Copied to clipboard: \"{Truncate(text, 50)}\"");
    }
    else
    {
        renderer.Console.Warn("Clipboard copy failed - OSC 52 not supported or stdout is not a TTY");
    }
};
renderer.Console.Show();

var buttonCounters = new Dictionary<ConsoleLogLevel, int>
{
    [ConsoleLogLevel.Log] = 0,
    [ConsoleLogLevel.Info] = 0,
    [ConsoleLogLevel.Warn] = 0,
    [ConsoleLogLevel.Error] = 0,
    [ConsoleLogLevel.Debug] = 0,
};

var titleText = new TextRenderable(renderer, new TextOptions
{
    Id = "console-demo-title",
    Content = "Console Logging Demo",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(2),
    Top = DimensionValue.Point(1),
    Fg = Rgba.FromInts(255, 215, 135),
    Attributes = TextAttributes.Bold,
    ZIndex = 1000,
});
renderer.Root.Add(titleText);

var instructionsText = new TextRenderable(renderer, new TextOptions
{
    Id = "console-demo-instructions",
    Content = "Click buttons to trigger different console log levels - Press ` to toggle console - Ctrl+Y to copy selection - Escape exits when the overlay is unfocused",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(2),
    Top = DimensionValue.Point(2),
    Fg = Rgba.FromInts(176, 196, 222),
    ZIndex = 1000,
});
renderer.Root.Add(instructionsText);

var statusText = new TextRenderable(renderer, new TextOptions
{
    Id = "console-demo-status",
    Content = "Click any button to start logging...",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(2),
    Top = DimensionValue.Point(4),
    Fg = Rgba.FromInts(144, 238, 144),
    Attributes = TextAttributes.Italic,
    ZIndex = 1000,
});
renderer.Root.Add(statusText);

int startY = 7;
int buttonWidth = 16;
int buttonHeight = 6;
int spacing = 18;

var buttons = new[]
{
    new ConsoleButton(renderer, "log-btn", 2, startY, buttonWidth, buttonHeight, Rgba.FromInts(160, 160, 170), "LOG", ConsoleLogLevel.Log, buttonCounters, statusText),
    new ConsoleButton(renderer, "info-btn", 2 + spacing, startY, buttonWidth, buttonHeight, Rgba.FromInts(100, 180, 200), "INFO", ConsoleLogLevel.Info, buttonCounters, statusText),
    new ConsoleButton(renderer, "warn-btn", 2 + (spacing * 2), startY, buttonWidth, buttonHeight, Rgba.FromInts(220, 180, 100), "WARN", ConsoleLogLevel.Warn, buttonCounters, statusText),
    new ConsoleButton(renderer, "error-btn", 2 + (spacing * 3), startY, buttonWidth, buttonHeight, Rgba.FromInts(200, 120, 120), "ERROR", ConsoleLogLevel.Error, buttonCounters, statusText),
    new ConsoleButton(renderer, "debug-btn", 2 + (spacing * 4), startY, buttonWidth, buttonHeight, Rgba.FromInts(140, 140, 150), "DEBUG", ConsoleLogLevel.Debug, buttonCounters, statusText),
};

foreach (var button in buttons)
    renderer.Root.Add(button);

renderer.Root.Add(new TextRenderable(renderer, new TextOptions
{
    Id = "console-demo-decor-1",
    Content = "✦ ✧ ✦ ✧ ✦ ✧ ✦ ✧ ✦ ✧ ✦ ✧ ✦ ✧ ✦ ✧ ✦",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(2),
    Top = DimensionValue.Point(startY + 12),
    Fg = Rgba.FromInts(100, 120, 150, 120),
    ZIndex = 50,
}));

renderer.Root.Add(new TextRenderable(renderer, new TextOptions
{
    Id = "console-demo-decor-2",
    Content = "Console appears at the bottom. Use Ctrl+P/Ctrl+O to change position, +/- to resize, and drag to select text.",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(2),
    Top = DimensionValue.Point(startY + 14),
    Fg = Rgba.FromInts(120, 140, 160, 200),
    Attributes = TextAttributes.Italic,
    ZIndex = 50,
}));

renderer.Console.Log("Console Demo initialized! Click the buttons above to test different log levels.");

renderer.KeyInput.On("keypress", (KeyEvent keyEvent) =>
{
    if (keyEvent.Raw == "`" || keyEvent.Name == "`")
    {
        renderer.Console.Toggle();
        keyEvent.PreventDefault();
        keyEvent.StopPropagation();
        return;
    }

    if (keyEvent.Name == "escape" && !renderer.Console.Focused)
        renderer.Destroy();
});

await Task.Delay(Timeout.Infinite);

static string Truncate(string text, int maxLength) =>
    text.Length <= maxLength ? text : text[..maxLength] + "...";

sealed class ConsoleButton : BoxRenderable
{
    private readonly CliRenderer _renderer;
    private readonly ConsoleLogLevel _level;
    private readonly Dictionary<ConsoleLogLevel, int> _counters;
    private readonly TextRenderable _statusText;
    private readonly Rgba _baseColor;
    private readonly Rgba _hoverColor;
    private readonly Rgba _pressedColor;
    private bool _hovered;
    private bool _pressed;
    private long _lastClickTicks;

    public ConsoleButton(
        CliRenderer renderer,
        string id,
        int x,
        int y,
        int width,
        int height,
        Rgba color,
        string label,
        ConsoleLogLevel level,
        Dictionary<ConsoleLogLevel, int> counters,
        TextRenderable statusText)
        : base(renderer, new BoxOptions
        {
            Id = id,
            Position = PositionValue.Absolute,
            Left = DimensionValue.Point(x),
            Top = DimensionValue.Point(y),
            Width = DimensionValue.Point(width),
            Height = DimensionValue.Point(height),
            ZIndex = 100,
            BackgroundColor = color,
            BorderColor = Brighten(color, 1.3f),
            BorderStyle = BorderStyle.Rounded,
            Title = label,
            TitleAlignment = TitleAlignment.Center,
            Border = true,
        })
    {
        _renderer = renderer;
        _level = level;
        _counters = counters;
        _statusText = statusText;
        _baseColor = color;
        _hoverColor = Brighten(color, 1.2f);
        _pressedColor = Brighten(color, 0.8f);
    }

    protected override void RenderSelf(OptimizedBuffer buffer, float deltaTime)
    {
        var targetColor = _pressed ? _pressedColor : _hovered ? _hoverColor : _baseColor;
        if (BackgroundColor != targetColor)
            BackgroundColor = targetColor;
        base.RenderSelf(buffer, deltaTime);

        var elapsed = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - _lastClickTicks).TotalMilliseconds;
        if (elapsed is >= 0 and < 300)
        {
            float alpha = 1f - (float)(elapsed / 300d);
            var sparkle = new Rgba(1f, 1f, 1f, alpha);
            int centerX = (int)ScreenX + (Width / 2);
            int centerY = (int)ScreenY + (Height / 2);
            buffer.SetCell((uint)Math.Max(0, centerX - 1), (uint)Math.Max(0, centerY), (uint)'✦', sparkle, BackgroundColor);
            buffer.SetCell((uint)Math.Max(0, centerX + 1), (uint)Math.Max(0, centerY), (uint)'✦', sparkle, BackgroundColor);
        }
    }

    protected override void OnMouseEvent(UiMouseEvent evt)
    {
        switch (evt.Type)
        {
            case MouseEventType.Down when evt.Button == (int)MouseButton.Left:
                _pressed = true;
                _lastClickTicks = DateTime.UtcNow.Ticks;
                TriggerConsoleLog();
                evt.StopPropagation();
                evt.PreventDefault();
                break;
            case MouseEventType.Up:
                _pressed = false;
                evt.StopPropagation();
                evt.PreventDefault();
                break;
            case MouseEventType.Over:
                _hovered = true;
                break;
            case MouseEventType.Out:
                _hovered = false;
                _pressed = false;
                break;
        }
    }

    private void TriggerConsoleLog()
    {
        _counters[_level]++;
        int count = _counters[_level];
        string timestamp = DateTime.Now.ToString("HH:mm:ss");

        switch (_level)
        {
            case ConsoleLogLevel.Log:
                _renderer.Console.Log(
                    $"Console Log #{count} triggered at {timestamp}",
                    "\n{ data: \"This is a regular log message\", count: ",
                    count,
                    ", timestamp: \"",
                    DateTime.Now.ToString("O"),
                    "\", metadata: { source: \"console-demo\", type: \"log\" } }");
                break;
            case ConsoleLogLevel.Info:
                _renderer.Console.Info(
                    $"Info Log #{count} triggered at {timestamp}",
                    "\n{ message: \"This is an informational message\", details: \"Info messages are used for general information\", level: \"INFO\", count: ",
                    count,
                    " }");
                break;
            case ConsoleLogLevel.Warn:
                _renderer.Console.Warn(
                    $"Warning Log #{count} triggered at {timestamp}",
                    "\n{ warning: \"This is a warning message\", reason: \"Something might need attention\", severity: \"WARNING\", count: ",
                    count,
                    " }");
                break;
            case ConsoleLogLevel.Error:
                _renderer.Console.Error(
                    $"Error Log #{count} triggered at {timestamp}",
                    "\n{ error: \"This is an error message\", details: \"Something went wrong (simulated)\", errorCode: \"ERR_",
                    count,
                    "\" }");
                break;
            case ConsoleLogLevel.Debug:
                _renderer.Console.Debug(
                    $"Debug Log #{count} triggered at {timestamp}",
                    "\n{ debug: \"This is a debug message\", variables: { x: ",
                    $"{Random.Shared.NextDouble():0.000}",
                    ", y: ",
                    $"{Random.Shared.NextDouble():0.000}",
                    " }, state: \"debugging\" }");
                break;
        }

        _statusText.ContentText = $"Last triggered: {_level.ToString().ToUpperInvariant()} #{count} at {timestamp}";
    }

    private static Rgba Brighten(Rgba color, float factor) => new(
        Math.Clamp(color.R * factor, 0f, 1f),
        Math.Clamp(color.G * factor, 0f, 1f),
        Math.Clamp(color.B * factor, 0f, 1f),
        color.A);
}
