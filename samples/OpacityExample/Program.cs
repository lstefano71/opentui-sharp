using OpenTui.Core;

static Rgba Hex(string value) => Rgba.FromHex(value);

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 30,
});

renderer.Native.SetBackgroundColor(Hex("#1a1a2e"));

bool animating = false;
float animationPhase = 0f;
float[] opacityValues = [1.0f, 0.8f, 0.5f, 0.3f];

var header = new BoxRenderable(renderer, new BoxOptions
{
    Id = "opacity-demo-header",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Hex("#16213e"),
    Border = true,
    BorderStyle = BorderStyle.Single,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
});

var infoText = new TextRenderable(renderer, new TextOptions
{
    Id = "info",
    Content = "OPACITY DEMO | 1-4: Toggle opacity | A: Animate | Ctrl+C: Exit",
    Fg = Hex("#e94560"),
    Bg = Rgba.Transparent,
});
header.Add(infoText);

var container = new BoxRenderable(renderer, new BoxOptions
{
    Id = "opacity-demo-container",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Auto,
    FlexGrow = 1,
    FlexDirection = FlexDirectionValue.Row,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    Padding = DimensionValue.Point(2),
});

string[] colors = ["#e94560", "#0f3460", "#533483", "#16a085"];
string[] labels = ["Box 1", "Box 2", "Box 3", "Box 4"];
var boxes = new List<BoxRenderable>(4);
var opacityLabels = new List<TextRenderable>(4);

for (int i = 0; i < 4; i++)
{
    var box = new BoxRenderable(renderer, new BoxOptions
    {
        Id = $"box-{i}",
        Width = DimensionValue.Point(20),
        Height = DimensionValue.Point(8),
        BackgroundColor = Hex(colors[i]),
        Border = true,
        BorderStyle = BorderStyle.Double,
        BorderColor = Rgba.White,
        Position = PositionValue.Absolute,
        Left = DimensionValue.Point(10 + i * 8),
        Top = DimensionValue.Point(5 + i * 2),
        Opacity = opacityValues[i],
        AlignItems = AlignValue.Center,
        JustifyContent = JustifyValue.Center,
        FlexDirection = FlexDirectionValue.Column,
    });

    var label = new TextRenderable(renderer, new TextOptions
    {
        Id = $"label-{i}",
        Content = labels[i],
        Fg = Rgba.White,
        Bg = Rgba.Transparent,
    });

    var opacityLabel = new TextRenderable(renderer, new TextOptions
    {
        Id = $"opacity-{i}",
        Content = $"Opacity: {opacityValues[i]:F1}",
        Fg = Rgba.White,
        Bg = Rgba.Transparent,
    });

    box.Add(label);
    box.Add(opacityLabel);
    boxes.Add(box);
    opacityLabels.Add(opacityLabel);
    container.Add(box);
}

var nestedContainer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "nested-container",
    Width = DimensionValue.Point(35),
    Height = DimensionValue.Point(10),
    BackgroundColor = Hex("#e94560"),
    Border = true,
    BorderStyle = BorderStyle.Single,
    Position = PositionValue.Absolute,
    Right = DimensionValue.Point(5),
    Top = DimensionValue.Point(5),
    Opacity = 0.7f,
    Padding = DimensionValue.Point(1),
    FlexDirection = FlexDirectionValue.Column,
});

var nestedLabel = new TextRenderable(renderer, new TextOptions
{
    Id = "nested-label",
    Content = "Parent: 0.7 opacity",
    Fg = Rgba.White,
    Bg = Rgba.Transparent,
});

var nestedChild = new BoxRenderable(renderer, new BoxOptions
{
    Id = "nested-child",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(5),
    BackgroundColor = Hex("#0f3460"),
    Border = true,
    Opacity = 0.5f,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    FlexDirection = FlexDirectionValue.Column,
});

var childLabel = new TextRenderable(renderer, new TextOptions
{
    Id = "child-label",
    Content = "Child: 0.5 opacity",
    Fg = Rgba.White,
    Bg = Rgba.Transparent,
});

var effectiveLabel = new TextRenderable(renderer, new TextOptions
{
    Id = "effective-label",
    Content = "Effective: 0.35",
    Fg = Hex("#ffcc00"),
    Bg = Rgba.Transparent,
});

nestedChild.Add(childLabel);
nestedChild.Add(effectiveLabel);
nestedContainer.Add(nestedLabel);
nestedContainer.Add(nestedChild);
container.Add(nestedContainer);

renderer.Root.Add(header);
renderer.Root.Add(container);

void UpdateOpacityLabels()
{
    for (int i = 0; i < boxes.Count; i++)
    {
        opacityLabels[i].ContentText = $"Opacity: {boxes[i].Opacity:F1}";
    }
}

void ToggleAnimation()
{
    animating = !animating;
    if (animating)
    {
        animationPhase = 0f;
        infoText.ContentText = "OPACITY DEMO | Animating... | A: Stop | Ctrl+C: Exit";
        renderer.RequestLive();
    }
    else
    {
        infoText.ContentText = "OPACITY DEMO | 1-4: Toggle opacity | A: Animate | Ctrl+C: Exit";
        renderer.DropLive();
    }
}

renderer.AddFrameCallback(dt =>
{
    if (!animating)
    {
        return Task.CompletedTask;
    }

    animationPhase += dt * 0.001f;
    for (int i = 0; i < boxes.Count; i++)
    {
        boxes[i].Opacity = 0.3f + 0.7f * MathF.Abs(MathF.Sin(animationPhase + i * 0.5f));
    }

    UpdateOpacityLabels();
    renderer.RequestRender();
    return Task.CompletedTask;
});

renderer.KeyInput.On("keypress", (KeyEvent key) =>
{
    switch (key.Name)
    {
        case "1":
        case "2":
        case "3":
        case "4":
        {
            int index = key.Name[0] - '1';
            boxes[index].Opacity = Math.Abs(boxes[index].Opacity - 1.0f) < 0.001f ? 0.3f : 1.0f;
            opacityValues[index] = boxes[index].Opacity;
            UpdateOpacityLabels();
            renderer.RequestRender();
            break;
        }
        case "a":
            ToggleAnimation();
            renderer.RequestRender();
            break;
    }
});

renderer.RequestRender();
await Task.Delay(Timeout.Infinite);
