// Console Demo — console overlay with clickable buttons
// Port of console-demo.ts
using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
});

var logLines = new List<string>();
int maxLines = 50;
bool autoScroll = true;

void Log(string message)
{
    logLines.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
    if (logLines.Count > maxLines)
        logLines.RemoveAt(0);
    UpdateLog();
}

// --- Header ---
var header = new BoxRenderable(renderer, new BoxOptions
{
    Id = "header",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#1e40af"),
    Border = true,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
});
var headerText = new TextRenderable(renderer, new TextOptions
{
    Id = "header-text",
    Content = "Console Demo — Interactive Log",
    Fg = Rgba.FromInts(255, 255, 255),
});
header.Add(headerText);

// --- Log area ---
var logArea = new ScrollBoxRenderable(renderer, new ScrollBoxOptions
{
    Id = "log-area",
    Width = DimensionValue.Auto,
    FlexGrow = 1,
    ScrollY = true,
    ScrollX = false,
    BackgroundColor = Rgba.FromHex("#0d1117"),
    Border = true,
    BorderColor = Rgba.FromHex("#30363d"),
    FlexDirection = FlexDirectionValue.Column,
});
var logText = new TextRenderable(renderer, new TextOptions
{
    Id = "log-text",
    Content = "",
    Fg = Rgba.FromHex("#c9d1d9"),
    WrapMode = WrapMode.Char,
    Width = DimensionValue.Auto,
});
logText.OnSizeChange = () =>
{
    if (autoScroll)
        logArea.ScrollTo(y: logArea.ScrollHeight);
};
logArea.Add(logText);

void UpdateLog()
{
    logText.ContentText = string.Join("\n", logLines);
    if (autoScroll)
        logArea.ScrollTo(y: logArea.ScrollHeight);
    renderer.RequestRender();
}

// --- Button row ---
var buttonRow = new BoxRenderable(renderer, new BoxOptions
{
    Id = "button-row",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    FlexDirection = FlexDirectionValue.Row,
    Gap = 2,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    Padding = DimensionValue.Point(1),
});

(string label, string color, string action)[] buttons =
[
    ("Info", "#3b82f6", "info"),
    ("Warn", "#f59e0b", "warn"),
    ("Error", "#ef4444", "error"),
    ("Clear", "#6b7280", "clear"),
    ("Debug", "#8b5cf6", "debug"),
];

foreach (var (label, color, action) in buttons)
{
    var btn = new BoxRenderable(renderer, new BoxOptions
    {
        Id = $"btn-{action}",
        Width = DimensionValue.Point(12),
        Height = DimensionValue.Point(1),
        BackgroundColor = Rgba.FromHex(color),
        AlignItems = AlignValue.Center,
        JustifyContent = JustifyValue.Center,
        ShouldFill = true,
    });
    var btnText = new TextRenderable(renderer, new TextOptions
    {
        Id = $"btn-text-{action}",
        Content = $" [{label}] ",
        Fg = Rgba.FromInts(255, 255, 255),
    });
    btn.Add(btnText);
    buttonRow.Add(btn);
}

// --- Footer ---
var footer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "footer",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#1e40af"),
    Border = true,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
});
var footerText = new TextRenderable(renderer, new TextOptions
{
    Id = "footer-text",
    Content = "I: info | W: warn | E: error | D: debug | C: clear | ↑/↓/🖱: scroll | Ctrl+C: exit",
    Fg = Rgba.FromInts(255, 255, 255),
});
footer.Add(footerText);

// --- Build tree ---
renderer.Root.Add(header);
renderer.Root.Add(logArea);
renderer.Root.Add(buttonRow);
renderer.Root.Add(footer);

int messageCount = 0;

renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    switch (e.Name)
    {
        case "i":
            messageCount++;
            Log($"ℹ️  INFO #{messageCount}: System operating normally");
            break;
        case "w":
            messageCount++;
            Log($"⚠️  WARN #{messageCount}: Memory usage at 85%");
            break;
        case "e":
            messageCount++;
            Log($"❌ ERROR #{messageCount}: Connection timeout after 30s");
            break;
        case "d":
            messageCount++;
            Log($"🔍 DEBUG #{messageCount}: Request processed in 42ms");
            break;
        case "c":
            logLines.Clear();
            messageCount = 0;
            autoScroll = true;
            UpdateLog();
            break;
        case "up":
            autoScroll = false;
            logArea.ScrollBy(0, -1);
            renderer.RequestRender();
            break;
        case "down":
            logArea.ScrollBy(0, 1);
            if (logArea.ScrollTop >= logArea.MaxScrollTop)
                autoScroll = true;
            renderer.RequestRender();
            break;
        case "pageup":
            autoScroll = false;
            logArea.ScrollBy(0, -10);
            renderer.RequestRender();
            break;
        case "pagedown":
            logArea.ScrollBy(0, 10);
            if (logArea.ScrollTop >= logArea.MaxScrollTop)
                autoScroll = true;
            renderer.RequestRender();
            break;
    }
});

Log("Console initialized. Press keys to add log entries.");
Log("Use I (info), W (warn), E (error), D (debug), C (clear)");
renderer.RequestRender();
await Task.Delay(Timeout.Infinite);
