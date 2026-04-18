// Opacity Example — overlapping boxes with varying opacity and animation
// Port of opacity-example.ts
using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 30,
});

renderer.Native.SetBackgroundColor(Rgba.FromHex("#0a0a1a"));

// --- State ---
bool animating = false;
bool lowOpacity = false;
float animTime = 0f;
float[] baseOpacities = [1.0f, 0.8f, 0.5f, 0.3f];
float[] currentOpacities = [1.0f, 0.8f, 0.5f, 0.3f];

// --- Header ---
var header = new BoxRenderable(renderer, new BoxOptions
{
    Id = "header",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#6366f1"),
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
});
var headerText = new TextRenderable(renderer, new TextOptions
{
    Id = "header-text",
    Content = "Opacity Example",
    Fg = Rgba.FromInts(255, 255, 255),
});
header.Add(headerText);

// --- Container for the overlapping boxes ---
var container = new BoxRenderable(renderer, new BoxOptions
{
    Id = "container",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Auto,
    FlexGrow = 1,
    BackgroundColor = Rgba.FromHex("#111827"),
});

// --- Four overlapping absolute-positioned boxes ---
(string name, string color, float opacity, int xOff, int yOff)[] boxDefs =
[
    ("box1", "#ef4444", 1.0f, 2,  1),   // Red    — full opacity
    ("box2", "#22c55e", 0.8f, 10, 2),   // Green  — 80%
    ("box3", "#3b82f6", 0.5f, 18, 3),   // Blue   — 50%
    ("box4", "#eab308", 0.3f, 26, 1),   // Yellow — 30%
];

var boxes = new List<BoxRenderable>(4);
var boxLabels = new List<TextRenderable>(4);

for (int i = 0; i < boxDefs.Length; i++)
{
    var (name, color, opacity, xOff, yOff) = boxDefs[i];
    var rgba = Rgba.FromHex(color);

    var box = new BoxRenderable(renderer, new BoxOptions
    {
        Id = name,
        Width = DimensionValue.Point(18),
        Height = DimensionValue.Point(7),
        BackgroundColor = rgba,
        Opacity = opacity,
        Position = PositionValue.Absolute,
        Left = DimensionValue.Point(xOff),
        Top = DimensionValue.Point(yOff),
        Border = true,
        BorderStyle = BorderStyle.Rounded,
        BorderColor = rgba,
        AlignItems = AlignValue.Center,
        JustifyContent = JustifyValue.Center,
        FlexDirection = FlexDirectionValue.Column,
        ZIndex = 10 + i,
        ShouldFill = true,
    });

    var label = new TextRenderable(renderer, new TextOptions
    {
        Id = $"{name}-label",
        Content = $"{opacity:P0} opacity",
        Fg = Rgba.FromInts(255, 255, 255),
    });

    box.Add(label);
    container.Add(box);
    boxes.Add(box);
    boxLabels.Add(label);
}

// --- Nested opacity demo: parent at 0.7, child at 0.5 ---
var nestedParent = new BoxRenderable(renderer, new BoxOptions
{
    Id = "nested-parent",
    Width = DimensionValue.Point(28),
    Height = DimensionValue.Point(9),
    BackgroundColor = Rgba.FromHex("#a855f7"),
    Opacity = 0.7f,
    Position = PositionValue.Absolute,
    Right = DimensionValue.Point(2),
    Top = DimensionValue.Point(1),
    Border = true,
    BorderStyle = BorderStyle.Double,
    BorderColor = Rgba.FromHex("#a855f7"),
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    FlexDirection = FlexDirectionValue.Column,
    ZIndex = 50,
    ShouldFill = true,
});

var nestedParentLabel = new TextRenderable(renderer, new TextOptions
{
    Id = "nested-parent-label",
    Content = "Parent (opacity 0.7)",
    Fg = Rgba.FromInts(255, 255, 255),
});

var nestedChild = new BoxRenderable(renderer, new BoxOptions
{
    Id = "nested-child",
    Width = DimensionValue.Point(22),
    Height = DimensionValue.Point(4),
    BackgroundColor = Rgba.FromHex("#06b6d4"),
    Opacity = 0.5f,
    Border = true,
    BorderStyle = BorderStyle.Single,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    ShouldFill = true,
});

var nestedChildLabel = new TextRenderable(renderer, new TextOptions
{
    Id = "nested-child-label",
    Content = "Child (opacity 0.5)",
    Fg = Rgba.FromInts(255, 255, 255),
});

nestedChild.Add(nestedChildLabel);
nestedParent.Add(nestedParentLabel);
nestedParent.Add(nestedChild);
container.Add(nestedParent);

// --- Footer ---
var footer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "footer",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#1e40af"),
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
});
var footerText = new TextRenderable(renderer, new TextOptions
{
    Id = "footer-text",
    Content = "A: toggle animation | SPACE: toggle opacity | Ctrl+C: exit",
    Fg = Rgba.FromInts(255, 255, 255),
});
footer.Add(footerText);

// --- Build tree ---
renderer.Root.Add(header);
renderer.Root.Add(container);
renderer.Root.Add(footer);

// --- Helpers ---
void UpdateLabels()
{
    for (int i = 0; i < boxes.Count; i++)
    {
        boxLabels[i].ContentText = $"{boxes[i].Opacity:P0} opacity";
    }
}

void SetOpacities(float[] values)
{
    for (int i = 0; i < boxes.Count; i++)
    {
        boxes[i].Opacity = values[i];
        currentOpacities[i] = values[i];
    }
    UpdateLabels();
}

// --- Frame callback for animation ---
renderer.AddFrameCallback(dt =>
{
    if (!animating) return Task.CompletedTask;

    animTime += dt;

    // Cycle each box's opacity with a sine wave, offset per box
    for (int i = 0; i < boxes.Count; i++)
    {
        float phase = animTime * 0.002f + i * MathF.PI * 0.5f;
        float opacity = 0.15f + 0.85f * (0.5f + 0.5f * MathF.Sin(phase));
        boxes[i].Opacity = opacity;
        currentOpacities[i] = opacity;
    }

    UpdateLabels();
    renderer.RequestRender();
    return Task.CompletedTask;
});

// --- Key handling ---
renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    switch (e.Name)
    {
        case "a":
            animating = !animating;
            if (animating)
            {
                animTime = 0f;
                headerText.ContentText = "Opacity Example  [animating]";
            }
            else
            {
                headerText.ContentText = "Opacity Example";
            }
            break;

        case "space":
            if (animating) break; // don't toggle while animating
            lowOpacity = !lowOpacity;
            if (lowOpacity)
            {
                SetOpacities([0.15f, 0.15f, 0.15f, 0.15f]);
            }
            else
            {
                SetOpacities([.. baseOpacities]);
            }
            break;
    }

    renderer.RequestRender();
});

renderer.On<(int Width, int Height)>(RendererEventNames.Resize, _ =>
{
    renderer.RequestRender();
});

renderer.RequestRender();
await Task.Delay(Timeout.Infinite);
