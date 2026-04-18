// Split Mode Demo — demonstrates ScreenMode.SplitFooter with fixed footer
using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 30,
    ScreenMode = ScreenMode.SplitFooter,
});

renderer.Native.SetBackgroundColor(Rgba.FromHex("#0f172a"));

int logCount = 0;

// --- Main content area ---
var mainArea = new BoxRenderable(renderer, new BoxOptions
{
    Id = "main",
    Width = DimensionValue.Auto,
    FlexGrow = 1,
    FlexDirection = FlexDirectionValue.Column,
    BackgroundColor = Rgba.FromHex("#1e293b"),
});

// --- Title bar ---
var titleBar = new BoxRenderable(renderer, new BoxOptions
{
    Id = "title-bar",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#0ea5e9"),
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
});
var titleText = new TextRenderable(renderer, new TextOptions
{
    Id = "title-text",
    StyledContent = new StyledText(
        TextChunk.Styled("Split Footer Demo", fg: Rgba.White, attributes: TextAttributes.Bold),
        TextChunk.Styled("  (ScreenMode.SplitFooter)", fg: Rgba.FromHex("#bae6fd"))),
    Fg = Rgba.White,
});
titleBar.Add(titleText);
mainArea.Add(titleBar);

// --- Description ---
var descBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "desc-box",
    Width = DimensionValue.Auto,
    Padding = DimensionValue.Point(1),
    FlexDirection = FlexDirectionValue.Column,
});

var descText = new TextRenderable(renderer, new TextOptions
{
    Id = "desc",
    StyledContent = new StyledText(
        TextChunk.Styled("SplitFooter mode", fg: Rgba.FromHex("#38bdf8"), attributes: TextAttributes.Bold),
        TextChunk.Plain(" renders the UI in a fixed footer region at the bottom of\n"),
        TextChunk.Plain("the terminal. Content above the footer scrolls normally. This is useful for\n"),
        TextChunk.Plain("tools that output to stdout while keeping a persistent status bar.")),
    Fg = Rgba.FromHex("#e2e8f0"),
});
descBox.Add(descText);
mainArea.Add(descBox);

// --- Content panels ---
var panelRow = new BoxRenderable(renderer, new BoxOptions
{
    Id = "panel-row",
    Width = DimensionValue.Auto,
    FlexGrow = 1,
    FlexDirection = FlexDirectionValue.Row,
    Padding = DimensionValue.Point(1),
    Gap = 1,
});

// Left panel: status
var leftPanel = new BoxRenderable(renderer, new BoxOptions
{
    Id = "left-panel",
    FlexGrow = 1,
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Rgba.FromHex("#22c55e"),
    BackgroundColor = Rgba.FromHex("#052e16"),
    FlexDirection = FlexDirectionValue.Column,
    Padding = DimensionValue.Point(1),
    Title = "Status",
});

var statusItems = new TextRenderable[4];
string[] labels = ["Mode", "Renderer", "FPS", "Events"];
string[] values = ["SplitFooter", "Active", "30", "0"];
Rgba[] labelColors = [Rgba.FromHex("#22c55e"), Rgba.FromHex("#3b82f6"), Rgba.FromHex("#eab308"), Rgba.FromHex("#ef4444")];

for (int i = 0; i < 4; i++)
{
    statusItems[i] = new TextRenderable(renderer, new TextOptions
    {
        Id = $"status-{i}",
        StyledContent = new StyledText(
            TextChunk.Styled($"{labels[i]}: ", fg: labelColors[i], attributes: TextAttributes.Bold),
            TextChunk.Styled(values[i], fg: Rgba.FromHex("#e2e8f0"))),
    });
    leftPanel.Add(statusItems[i]);
}

// Right panel: log
var rightPanel = new BoxRenderable(renderer, new BoxOptions
{
    Id = "right-panel",
    FlexGrow = 2,
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Rgba.FromHex("#6366f1"),
    BackgroundColor = Rgba.FromHex("#1e1b4b"),
    FlexDirection = FlexDirectionValue.Column,
    Padding = DimensionValue.Point(1),
    Title = "Event Log",
    Overflow = OverflowValue.Hidden,
});

var logLines = new TextRenderable[8];
for (int i = 0; i < logLines.Length; i++)
{
    logLines[i] = new TextRenderable(renderer, new TextOptions
    {
        Id = $"log-{i}",
        Content = "",
        Fg = Rgba.FromHex("#a5b4fc"),
    });
    rightPanel.Add(logLines[i]);
}

panelRow.Add(leftPanel);
panelRow.Add(rightPanel);
mainArea.Add(panelRow);

// --- Fixed footer bar ---
var footerBar = new BoxRenderable(renderer, new BoxOptions
{
    Id = "footer-bar",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(1),
    BackgroundColor = Rgba.FromHex("#334155"),
    FlexDirection = FlexDirectionValue.Row,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.SpaceBetween,
    PaddingLeft = DimensionValue.Point(1),
    PaddingRight = DimensionValue.Point(1),
});

var footerLeft = new TextRenderable(renderer, new TextOptions
{
    Id = "footer-left",
    StyledContent = new StyledText(
        TextChunk.Styled(" SPLIT ", fg: Rgba.White, bg: Rgba.FromHex("#0ea5e9"), attributes: TextAttributes.Bold),
        TextChunk.Plain(" "),
        TextChunk.Styled("SplitFooter Mode", fg: Rgba.FromHex("#94a3b8"))),
});

var footerRight = new TextRenderable(renderer, new TextOptions
{
    Id = "footer-right",
    Content = "L: log | R: resize info | Ctrl+C: exit",
    Fg = Rgba.FromHex("#64748b"),
});

footerBar.Add(footerLeft);
footerBar.Add(footerRight);

// --- Build tree ---
renderer.Root.Add(mainArea);
renderer.Root.Add(footerBar);

void AddLog(string message)
{
    logCount++;
    // Shift lines up
    for (int i = 0; i < logLines.Length - 1; i++)
        logLines[i].SetContent(logLines[i + 1].ContentText ?? "");

    var ts = DateTime.Now.ToString("HH:mm:ss");
    logLines[^1].Content = new StyledText(
        TextChunk.Styled($"[{ts}] ", fg: Rgba.FromHex("#6366f1")),
        TextChunk.Styled(message, fg: Rgba.FromHex("#c7d2fe")));

    statusItems[3].Content = new StyledText(
        TextChunk.Styled("Events: ", fg: labelColors[3], attributes: TextAttributes.Bold),
        TextChunk.Styled(logCount.ToString(), fg: Rgba.FromHex("#e2e8f0")));
}

// --- Key handling ---
renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    switch (e.Name)
    {
        case "l":
            AddLog($"Key event: '{e.Name}'");
            renderer.RequestRender();
            break;
        case "r":
            AddLog($"Terminal: {renderer.Width}×{renderer.Height}");
            renderer.RequestRender();
            break;
    }
});

renderer.On<(int Width, int Height)>(RendererEventNames.Resize, size =>
{
    AddLog($"Resized to {size.Width}×{size.Height}");
    renderer.RequestRender();
});

AddLog("SplitFooter demo started");
AddLog($"Terminal: {renderer.Width}×{renderer.Height}");
renderer.RequestRender();
await Task.Delay(Timeout.Infinite);
