// Live State Demo — renderable lifecycle
// Port of live-state-demo.ts
using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 30,
});

int tickCount = 0;
bool liveMode = false;

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
    Content = "Live State Demo",
    Fg = Rgba.FromInts(255, 255, 255),
});
header.Add(headerText);

// --- State display ---
var stateArea = new BoxRenderable(renderer, new BoxOptions
{
    Id = "state-area",
    Width = DimensionValue.Auto,
    FlexGrow = 1,
    FlexDirection = FlexDirectionValue.Column,
    Padding = DimensionValue.Point(1),
    BackgroundColor = Rgba.FromHex("#111827"),
});

var modeLabel = new TextRenderable(renderer, new TextOptions
{
    Id = "mode-label",
    Content = "Mode: IDLE (press L to toggle live)",
    Fg = Rgba.FromHex("#60a5fa"),
    Attributes = TextAttributes.Bold,
});

var counterLabel = new TextRenderable(renderer, new TextOptions
{
    Id = "counter-label",
    Content = "Ticks: 0",
    Fg = Rgba.FromHex("#34d399"),
});

var fpsLabel = new TextRenderable(renderer, new TextOptions
{
    Id = "fps-label",
    Content = "FPS: --",
    Fg = Rgba.FromHex("#fbbf24"),
});

// Progress bar
var progressBar = new BoxRenderable(renderer, new BoxOptions
{
    Id = "progress-bar-bg",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(1),
    BackgroundColor = Rgba.FromHex("#374151"),
    FlexDirection = FlexDirectionValue.Row,
    ShouldFill = true,
});
var progressFill = new BoxRenderable(renderer, new BoxOptions
{
    Id = "progress-fill",
    Width = DimensionValue.Point(0),
    Height = DimensionValue.Point(1),
    BackgroundColor = Rgba.FromHex("#3b82f6"),
    ShouldFill = true,
});
progressBar.Add(progressFill);

// Animated boxes
var animRow = new BoxRenderable(renderer, new BoxOptions
{
    Id = "anim-row",
    FlexDirection = FlexDirectionValue.Row,
    Gap = 1,
    Height = DimensionValue.Point(3),
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
});

var animBoxes = new BoxRenderable[8];
for (int i = 0; i < 8; i++)
{
    animBoxes[i] = new BoxRenderable(renderer, new BoxOptions
    {
        Id = $"anim-{i}",
        Width = DimensionValue.Point(4),
        Height = DimensionValue.Point(2),
        BackgroundColor = Rgba.FromHex("#3b82f6"),
        ShouldFill = true,
    });
    animRow.Add(animBoxes[i]);
}

stateArea.Add(modeLabel);
stateArea.Add(counterLabel);
stateArea.Add(fpsLabel);
stateArea.Add(progressBar);
stateArea.Add(animRow);

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
    Content = "L: toggle live mode | R: reset counter | Ctrl+C: exit",
    Fg = Rgba.FromInts(255, 255, 255),
});
footer.Add(footerText);

renderer.Root.Add(header);
renderer.Root.Add(stateArea);
renderer.Root.Add(footer);

// Frame callback for live mode
float elapsed = 0;
renderer.AddFrameCallback(dt =>
{
    if (!liveMode) return Task.CompletedTask;

    tickCount++;
    elapsed += dt;

    counterLabel.ContentText = $"Ticks: {tickCount}";
    fpsLabel.ContentText = $"FPS: {(tickCount / (elapsed / 1000f)):F1}";

    // Progress bar cycles every 3 seconds
    float progress = (elapsed % 3000f) / 3000f;
    int barWidth = Math.Max(0, (int)(renderer.Width * progress));
    progressFill.WidthDimension = DimensionValue.Point(barWidth);

    // Color wave on animated boxes
    for (int i = 0; i < animBoxes.Length; i++)
    {
        float phase = (elapsed / 500f) + i * 0.5f;
        float r = (MathF.Sin(phase) + 1) / 2;
        float g = (MathF.Sin(phase + 2.094f) + 1) / 2;
        float b = (MathF.Sin(phase + 4.189f) + 1) / 2;
        animBoxes[i].BackgroundColor = Rgba.FromValues(r, g, b, 1f);
    }

    renderer.RequestRender();
    return Task.CompletedTask;
});

renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    switch (e.Name)
    {
        case "l":
            liveMode = !liveMode;
            modeLabel.ContentText = liveMode
                ? "Mode: LIVE (continuous rendering)"
                : "Mode: IDLE (press L to toggle live)";
            if (liveMode)
                renderer.RequestLive();
            else
                renderer.DropLive();
            renderer.RequestRender();
            break;
        case "r":
            tickCount = 0;
            elapsed = 0;
            counterLabel.ContentText = "Ticks: 0";
            fpsLabel.ContentText = "FPS: --";
            renderer.RequestRender();
            break;
    }
});

renderer.RequestRender();
await Task.Delay(Timeout.Infinite);
