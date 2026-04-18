// Slider Demo — multiple horizontal and vertical sliders with animation
// Port of slider-demo.ts
using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 30,
});

// --- Header ---
var header = new BoxRenderable(renderer, new BoxOptions
{
    Id = "header",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#7c3aed"),
    BorderStyle = BorderStyle.Single,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    Border = true,
});
var headerText = new TextRenderable(renderer, new TextOptions
{
    Id = "header-text",
    Content = "SLIDER DEMO",
    Fg = Rgba.FromInts(255, 255, 255),
});
header.Add(headerText);

// --- Horizontal sliders row ---
var hRow = new BoxRenderable(renderer, new BoxOptions
{
    Id = "h-row",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    FlexDirection = FlexDirectionValue.Row,
    AlignItems = AlignValue.Center,
    Gap = 2,
    Padding = DimensionValue.Point(1),
});

var hLabel = new TextRenderable(renderer, new TextOptions
{
    Id = "h-label",
    Content = "Horizontal",
    Fg = Rgba.FromHex("#a78bfa"),
    Width = DimensionValue.Point(12),
});

var hSlider1 = new SliderRenderable(renderer, new SliderOptions
{
    Id = "h-slider-1",
    Orientation = SliderOrientation.Horizontal,
    Value = 25,
    Min = 0,
    Max = 100,
    Width = DimensionValue.Point(20),
    Height = DimensionValue.Point(1),
    Buffered = true,
    BackgroundColor = Rgba.FromHex("#374151"),
    ForegroundColor = Rgba.FromHex("#3b82f6"),
});

var hSlider2 = new SliderRenderable(renderer, new SliderOptions
{
    Id = "h-slider-2",
    Orientation = SliderOrientation.Horizontal,
    Value = 50,
    Min = 0,
    Max = 100,
    Width = DimensionValue.Point(30),
    Height = DimensionValue.Point(1),
    Buffered = true,
    BackgroundColor = Rgba.FromHex("#374151"),
    ForegroundColor = Rgba.FromHex("#10b981"),
});

var hSlider3 = new SliderRenderable(renderer, new SliderOptions
{
    Id = "h-slider-3",
    Orientation = SliderOrientation.Horizontal,
    Value = 75,
    Min = 0,
    Max = 100,
    Width = DimensionValue.Point(40),
    Height = DimensionValue.Point(1),
    Buffered = true,
    BackgroundColor = Rgba.FromHex("#374151"),
    ForegroundColor = Rgba.FromHex("#f59e0b"),
});

hRow.Add(hLabel);
hRow.Add(hSlider1);
hRow.Add(hSlider2);
hRow.Add(hSlider3);

// --- Vertical sliders row ---
var vRow = new BoxRenderable(renderer, new BoxOptions
{
    Id = "v-row",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Auto,
    FlexGrow = 1,
    FlexShrink = 1,
    FlexDirection = FlexDirectionValue.Row,
    AlignItems = AlignValue.FlexEnd,
    JustifyContent = JustifyValue.Center,
    Gap = 4,
    Padding = DimensionValue.Point(1),
});

var vLabel = new TextRenderable(renderer, new TextOptions
{
    Id = "v-label",
    Content = "Vertical",
    Fg = Rgba.FromHex("#a78bfa"),
    Width = DimensionValue.Point(10),
    AlignSelf = AlignValue.FlexStart,
});

var vSlider1 = new SliderRenderable(renderer, new SliderOptions
{
    Id = "v-slider-1",
    Orientation = SliderOrientation.Vertical,
    Value = 30,
    Min = 0,
    Max = 100,
    Width = DimensionValue.Point(2),
    Height = DimensionValue.Point(12),
    Buffered = true,
    BackgroundColor = Rgba.FromHex("#374151"),
    ForegroundColor = Rgba.FromHex("#ef4444"),
});

var vSlider2 = new SliderRenderable(renderer, new SliderOptions
{
    Id = "v-slider-2",
    Orientation = SliderOrientation.Vertical,
    Value = 60,
    Min = 0,
    Max = 100,
    Width = DimensionValue.Point(2),
    Height = DimensionValue.Point(16),
    Buffered = true,
    BackgroundColor = Rgba.FromHex("#374151"),
    ForegroundColor = Rgba.FromHex("#8b5cf6"),
});

// Animated sliders
var vSlider3 = new SliderRenderable(renderer, new SliderOptions
{
    Id = "v-slider-3",
    Orientation = SliderOrientation.Vertical,
    Value = 50,
    Min = 0,
    Max = 100,
    Width = DimensionValue.Point(2),
    Height = DimensionValue.Point(14),
    Buffered = true,
    BackgroundColor = Rgba.FromHex("#374151"),
    ForegroundColor = Rgba.FromHex("#06b6d4"),
});

var vSlider4 = new SliderRenderable(renderer, new SliderOptions
{
    Id = "v-slider-4",
    Orientation = SliderOrientation.Vertical,
    Value = 50,
    Min = 0,
    Max = 100,
    Width = DimensionValue.Point(2),
    Height = DimensionValue.Point(10),
    Buffered = true,
    BackgroundColor = Rgba.FromHex("#374151"),
    ForegroundColor = Rgba.FromHex("#f43f5e"),
});

vRow.Add(vLabel);
vRow.Add(vSlider1);
vRow.Add(vSlider2);
vRow.Add(vSlider3);
vRow.Add(vSlider4);

// --- Status panel ---
var statusBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "status",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(5),
    BackgroundColor = Rgba.FromHex("#1e293b"),
    BorderStyle = BorderStyle.Rounded,
    Border = true,
    BorderColor = Rgba.FromHex("#475569"),
    Padding = DimensionValue.Point(1),
});

var statusTitle = new TextRenderable(renderer, new TextOptions
{
    Id = "status-title",
    Content = "Slider Values",
    Fg = Rgba.FromHex("#c084fc"),
});

var statusText = new TextRenderable(renderer, new TextOptions
{
    Id = "status-text",
    Content = "",
    Fg = Rgba.FromHex("#94a3b8"),
});

statusBox.Add(statusTitle);
statusBox.Add(statusText);

// --- Footer ---
var footer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "footer",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#1e1b4b"),
    BorderStyle = BorderStyle.Single,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    Border = true,
    BorderColor = Rgba.FromHex("#4338ca"),
});
var footerText = new TextRenderable(renderer, new TextOptions
{
    Id = "footer-text",
    Content = "Sliders 3 & 4 (vertical) are animated with sin/cos • Ctrl+C to quit",
    Fg = Rgba.FromHex("#818cf8"),
});
footer.Add(footerText);

// --- Build tree ---
renderer.Root.Add(header);
renderer.Root.Add(hRow);
renderer.Root.Add(vRow);
renderer.Root.Add(statusBox);
renderer.Root.Add(footer);

// --- Update status display ---
void UpdateStatus()
{
    statusText.ContentText =
        $"H1: {hSlider1.Value,6:F1}  H2: {hSlider2.Value,6:F1}  H3: {hSlider3.Value,6:F1}   |   " +
        $"V1: {vSlider1.Value,6:F1}  V2: {vSlider2.Value,6:F1}  V3: {vSlider3.Value,6:F1}  V4: {vSlider4.Value,6:F1}";
}

// --- Slider change events ---
hSlider1.On<float>(SliderRenderable.Events.Change, _ => UpdateStatus());
hSlider2.On<float>(SliderRenderable.Events.Change, _ => UpdateStatus());
hSlider3.On<float>(SliderRenderable.Events.Change, _ => UpdateStatus());
vSlider1.On<float>(SliderRenderable.Events.Change, _ => UpdateStatus());
vSlider2.On<float>(SliderRenderable.Events.Change, _ => UpdateStatus());
vSlider3.On<float>(SliderRenderable.Events.Change, _ => UpdateStatus());
vSlider4.On<float>(SliderRenderable.Events.Change, _ => UpdateStatus());

// --- Animation: sin/cos drive vertical sliders 3 & 4 ---
float elapsed = 0f;
renderer.AddFrameCallback(dt =>
{
    elapsed += dt / 1000f;

    float sin = (MathF.Sin(elapsed * 1.5f) + 1f) * 0.5f; // 0..1
    float cos = (MathF.Cos(elapsed * 2.0f) + 1f) * 0.5f;  // 0..1

    vSlider3.Value = sin * 100f;
    vSlider4.Value = cos * 100f;

    UpdateStatus();
    return Task.CompletedTask;
});

// --- Start ---
renderer.Native.SetBackgroundColor(Rgba.FromHex("#0f0d23"));
UpdateStatus();
renderer.RequestRender();

await Task.Delay(Timeout.Infinite);
