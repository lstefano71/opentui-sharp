using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    EnableMouseMovement = true,
    BackgroundColor = Rgba.FromHex("#0D1117"),
});

var exitTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
renderer.On(RendererEventNames.Destroy, () => exitTcs.TrySetResult());

int mouseX = 0;
int mouseY = 0;
int mouseEvents = 0;
int focusCount = 0;
int blurCount = 0;
int restoreCount = 0;
string lastFocusTime = "";
string lastBlurTime = "";
string lastMouseTime = "";
bool focused = true;

var logEntries = new List<(string Text, Rgba Color)>();
var logRenderables = new List<TextRenderable>();
const int MaxLogEntries = 20;

TextRenderable? focusStatus = null;
TextRenderable? mouseStatus = null;
TextRenderable? countersStatus = null;
TextRenderable? timestampStatus = null;
BoxRenderable? logBox = null;

string Timestamp() => DateTime.Now.ToString("HH:mm:ss");

void UpdateDisplay()
{
    if (focusStatus is not null)
    {
        focusStatus.Content = focused
            ? "Focus: YES  (terminal modes active)"
            : "Focus: NO   (modes may be stripped by terminal)";
        focusStatus.Fg = focused ? Rgba.FromInts(126, 231, 135) : Rgba.FromInts(255, 100, 100);
    }

    if (mouseStatus is not null)
        mouseStatus.Content = $"Mouse: ({mouseX}, {mouseY}) | Events: {mouseEvents}";

    if (countersStatus is not null)
        countersStatus.Content = $"Focus-in: {focusCount} | Focus-out: {blurCount} | Mode restores: {restoreCount}";

    if (timestampStatus is not null)
        timestampStatus.Content = $"Last focus: {lastFocusTime.Or("--")} | Last blur: {lastBlurTime.Or("--")} | Last mouse: {lastMouseTime.Or("--")}";

    renderer.RequestRender();
}

void AddLogLine(string text, Rgba color)
{
    if (logBox is null)
        return;

    logEntries.Add((text, color));
    while (logEntries.Count > MaxLogEntries)
        logEntries.RemoveAt(0);

    foreach (var renderable in logRenderables)
    {
        logBox.Remove(renderable.Id);
        renderable.Destroy();
    }
    logRenderables.Clear();

    for (int i = 0; i < logEntries.Count; i++)
    {
        var entry = logEntries[i];
        var line = new TextRenderable(renderer, new TextOptions
        {
            Id = $"focus-demo-log-{i}",
            Content = entry.Text,
            Fg = entry.Color,
            Height = 1,
        });
        logBox.Add(line);
        logRenderables.Add(line);
    }
}

var container = new BoxRenderable(renderer, new BoxOptions
{
    Id = "focus-demo-main",
    FlexDirection = FlexDirectionValue.Column,
    Padding = 1,
});
renderer.Root.Add(container);

var title = new TextRenderable(renderer, new TextOptions
{
    Id = "focus-demo-title",
    Content = "Focus Restore Demo - Mouse Tracking + Terminal Mode Restore",
    Fg = Rgba.FromInts(72, 209, 204),
    Attributes = TextAttributes.Bold,
    Height = 2,
});
container.Add(title);

var instructions = new TextRenderable(renderer, new TextOptions
{
    Id = "focus-demo-instructions",
    Content = "Move mouse to see tracking. Alt-tab away and back. Mouse should resume.\n" +
              "Minimize and restore. Try clicking after returning. Escape to exit.",
    Fg = Rgba.FromInts(160, 160, 180),
    Height = 3,
});
container.Add(instructions);

var statusBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "focus-demo-status-box",
    Border = true,
    BorderColor = Rgba.FromHex("#4ECDC4"),
    BorderStyle = BorderStyle.Rounded,
    Title = "Terminal State",
    TitleAlignment = TitleAlignment.Center,
    Padding = 1,
    FlexDirection = FlexDirectionValue.Column,
    MarginTop = 1,
});
container.Add(statusBox);

focusStatus = new TextRenderable(renderer, new TextOptions
{
    Id = "focus-demo-focus-status",
    Content = "Focus: YES  (terminal modes active)",
    Fg = Rgba.FromInts(126, 231, 135),
    Height = 1,
});
statusBox.Add(focusStatus);

mouseStatus = new TextRenderable(renderer, new TextOptions
{
    Id = "focus-demo-mouse-status",
    Content = "Mouse: (0, 0) | Events: 0",
    Fg = Rgba.FromInts(165, 214, 255),
    Height = 1,
});
statusBox.Add(mouseStatus);

countersStatus = new TextRenderable(renderer, new TextOptions
{
    Id = "focus-demo-counters",
    Content = "Focus-in: 0 | Focus-out: 0 | Mode restores: 0",
    Fg = Rgba.FromInts(210, 168, 255),
    Height = 1,
});
statusBox.Add(countersStatus);

timestampStatus = new TextRenderable(renderer, new TextOptions
{
    Id = "focus-demo-timestamps",
    Content = "Last focus: -- | Last blur: -- | Last mouse: --",
    Fg = Rgba.FromInts(139, 148, 158),
    Height = 1,
});
statusBox.Add(timestampStatus);

logBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "focus-demo-log-box",
    Border = true,
    BorderColor = Rgba.FromHex("#6BCF7F"),
    BorderStyle = BorderStyle.Rounded,
    Title = "Event Log (latest 20)",
    TitleAlignment = TitleAlignment.Center,
    Padding = 1,
    FlexDirection = FlexDirectionValue.Column,
    MarginTop = 1,
    FlexGrow = 1,
});
container.Add(logBox);

var mouseArea = new BoxRenderable(renderer, new BoxOptions
{
    Id = "focus-demo-mouse-area",
    Position = PositionValue.Absolute,
    Left = 0,
    Top = 0,
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Percent(100),
    ZIndex = -1,
    OnMouse = mouseEvent =>
    {
        mouseX = mouseEvent.X;
        mouseY = mouseEvent.Y;
        mouseEvents++;
        lastMouseTime = Timestamp();
        UpdateDisplay();
    },
});
renderer.Root.Add(mouseArea);

renderer.On(RendererEventNames.Focus, () =>
{
    focused = true;
    focusCount++;
    restoreCount++;
    lastFocusTime = Timestamp();
    AddLogLine($"[{Timestamp()}] FOCUS IN  - terminal modes restored (restore #{restoreCount})", Rgba.FromInts(126, 231, 135));
    UpdateDisplay();
});

renderer.On(RendererEventNames.Blur, () =>
{
    focused = false;
    blurCount++;
    lastBlurTime = Timestamp();
    AddLogLine($"[{Timestamp()}] FOCUS OUT - terminal may strip escape codes", Rgba.FromInts(255, 165, 0));
    UpdateDisplay();
});

renderer.KeyInput.On("keypress", (KeyEvent keyEvent) =>
{
    if (keyEvent.Name == "escape")
    {
        renderer.Destroy();
        keyEvent.StopPropagation();
    }
});

AddLogLine($"[{Timestamp()}] Demo started. Move mouse, then alt-tab away and back.", Rgba.FromInts(165, 214, 255));
UpdateDisplay();

await exitTcs.Task;

static class StringExtensions
{
    public static string Or(this string value, string fallback) =>
        string.IsNullOrEmpty(value) ? fallback : value;
}
