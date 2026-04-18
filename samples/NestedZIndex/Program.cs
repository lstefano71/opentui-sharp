using OpenTui.Core;

static Rgba Hex(string value) => Rgba.FromHex(value);

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 30,
});

renderer.Native.SetBackgroundColor(Hex("#001122"));

int zIndexPhase = 0;
float animationSpeed = 2000f;
float elapsedMs = 0f;

var parentContainer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "parent-container",
    ZIndex = 10,
});
renderer.Root.Add(parentContainer);

var title = new TextRenderable(renderer, new TextOptions
{
    Id = "main-title",
    Content = "Nested Render Objects & Z-Index Demo",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(10),
    Top = DimensionValue.Point(2),
    Fg = Hex("#FFFF00"),
    Attributes = TextAttributes.Bold | TextAttributes.Underline,
    ZIndex = 1000,
});
parentContainer.Add(title);

var parentGroupA = new BoxRenderable(renderer, new BoxOptions
{
    Id = "parent-group-a",
    Position = PositionValue.Absolute,
    ZIndex = 100,
});
parentContainer.Add(parentGroupA);

var parentGroupB = new BoxRenderable(renderer, new BoxOptions
{
    Id = "parent-group-b",
    Position = PositionValue.Absolute,
    ZIndex = 50,
});
parentContainer.Add(parentGroupB);

var parentGroupC = new BoxRenderable(renderer, new BoxOptions
{
    Id = "parent-group-c",
    Position = PositionValue.Absolute,
    ZIndex = 20,
});
parentContainer.Add(parentGroupC);

var boxA1 = new BoxRenderable(renderer, new BoxOptions
{
    Id = "box-a1",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(15),
    Top = DimensionValue.Point(8),
    Width = DimensionValue.Point(25),
    Height = DimensionValue.Point(6),
    BackgroundColor = Hex("#220044"),
    ZIndex = 10,
    Border = true,
    BorderStyle = BorderStyle.Single,
    BorderColor = Hex("#FF44FF"),
    Title = "Parent A (z=100)",
    TitleAlignment = TitleAlignment.Center,
});
parentGroupA.Add(boxA1);

var textA1 = new TextRenderable(renderer, new TextOptions
{
    Id = "text-a1",
    Content = "Child A1 (z=10)",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(17),
    Top = DimensionValue.Point(10),
    Fg = Hex("#FF44FF"),
    Attributes = TextAttributes.Bold,
    ZIndex = 10,
});
parentGroupA.Add(textA1);

var boxA2 = new BoxRenderable(renderer, new BoxOptions
{
    Id = "box-a2",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(20),
    Top = DimensionValue.Point(11),
    Width = DimensionValue.Point(15),
    Height = DimensionValue.Point(4),
    BackgroundColor = Hex("#440044"),
    ZIndex = 5,
    Border = true,
    BorderStyle = BorderStyle.Single,
    BorderColor = Hex("#FF88FF"),
});
parentGroupA.Add(boxA2);

var textA2 = new TextRenderable(renderer, new TextOptions
{
    Id = "text-a2",
    Content = "Child A2 (z=5)",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(22),
    Top = DimensionValue.Point(12),
    Fg = Hex("#FF88FF"),
    ZIndex = 5,
});
parentGroupA.Add(textA2);

var boxB1 = new BoxRenderable(renderer, new BoxOptions
{
    Id = "box-b1",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(30),
    Top = DimensionValue.Point(12),
    Width = DimensionValue.Point(25),
    Height = DimensionValue.Point(6),
    BackgroundColor = Hex("#004422"),
    ZIndex = 20,
    Border = true,
    BorderStyle = BorderStyle.Double,
    BorderColor = Hex("#44FF44"),
    Title = "Parent B (z=50)",
    TitleAlignment = TitleAlignment.Center,
});
parentGroupB.Add(boxB1);

var textB1 = new TextRenderable(renderer, new TextOptions
{
    Id = "text-b1",
    Content = "Child B1 (z=20)",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(32),
    Top = DimensionValue.Point(14),
    Fg = Hex("#44FF44"),
    Attributes = TextAttributes.Bold,
    ZIndex = 20,
});
parentGroupB.Add(textB1);

var boxB2 = new BoxRenderable(renderer, new BoxOptions
{
    Id = "box-b2",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(35),
    Top = DimensionValue.Point(15),
    Width = DimensionValue.Point(15),
    Height = DimensionValue.Point(4),
    BackgroundColor = Hex("#004400"),
    ZIndex = 15,
    Border = true,
    BorderStyle = BorderStyle.Single,
    BorderColor = Hex("#88FF88"),
});
parentGroupB.Add(boxB2);

var textB2 = new TextRenderable(renderer, new TextOptions
{
    Id = "text-b2",
    Content = "Child B2 (z=15)",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(37),
    Top = DimensionValue.Point(16),
    Fg = Hex("#88FF88"),
    ZIndex = 15,
});
parentGroupB.Add(textB2);

var boxC1 = new BoxRenderable(renderer, new BoxOptions
{
    Id = "box-c1",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(45),
    Top = DimensionValue.Point(16),
    Width = DimensionValue.Point(25),
    Height = DimensionValue.Point(6),
    BackgroundColor = Hex("#442200"),
    ZIndex = 30,
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Hex("#FFFF44"),
    Title = "Parent C (z=20)",
    TitleAlignment = TitleAlignment.Center,
});
parentGroupC.Add(boxC1);

var textC1 = new TextRenderable(renderer, new TextOptions
{
    Id = "text-c1",
    Content = "Child C1 (z=30)",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(47),
    Top = DimensionValue.Point(18),
    Fg = Hex("#FFFF44"),
    Attributes = TextAttributes.Bold,
    ZIndex = 30,
});
parentGroupC.Add(textC1);

var boxC2 = new BoxRenderable(renderer, new BoxOptions
{
    Id = "box-c2",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(50),
    Top = DimensionValue.Point(19),
    Width = DimensionValue.Point(15),
    Height = DimensionValue.Point(4),
    BackgroundColor = Hex("#444400"),
    ZIndex = 25,
    Border = true,
    BorderStyle = BorderStyle.Single,
    BorderColor = Hex("#FFFF88"),
});
parentGroupC.Add(boxC2);

var textC2 = new TextRenderable(renderer, new TextOptions
{
    Id = "text-c2",
    Content = "Child C2 (z=25)",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(52),
    Top = DimensionValue.Point(20),
    Fg = Hex("#FFFF88"),
    ZIndex = 25,
});
parentGroupC.Add(textC2);

var explanation1 = new TextRenderable(renderer, new TextOptions
{
    Id = "explanation-1",
    Content = "Key Concept: Parent z-index determines group layering, child z-index determines order within group",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(10),
    Top = DimensionValue.Point(25),
    Fg = Hex("#AAAAAA"),
    ZIndex = 1000,
});
parentContainer.Add(explanation1);

var explanation2 = new TextRenderable(renderer, new TextOptions
{
    Id = "explanation-2",
    Content = "Even if Child C1 has z=30, it renders behind Parent A & B because Parent C has z=20",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(10),
    Top = DimensionValue.Point(26),
    Fg = Hex("#AAAAAA"),
    ZIndex = 1000,
});
parentContainer.Add(explanation2);

var phaseIndicator = new TextRenderable(renderer, new TextOptions
{
    Id = "phase-indicator",
    Content = "Animation Phase: 1/4",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(10),
    Top = DimensionValue.Point(28),
    Fg = Rgba.White,
    Attributes = TextAttributes.Bold,
    ZIndex = 1000,
});
parentContainer.Add(phaseIndicator);

var zIndexDisplay = new TextRenderable(renderer, new TextOptions
{
    Id = "zindex-display",
    Content = "Current Z-Indices - A:100, B:50, C:20",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(10),
    Top = DimensionValue.Point(29),
    Fg = Rgba.White,
    ZIndex = 1000,
});
parentContainer.Add(zIndexDisplay);

string[] phases =
[
    "Original Hierarchy",
    "C Group on Top",
    "B Group on Top",
    "Equal Parents (Child z-index matters)",
];

void ApplyPhase(int phase)
{
    parentGroupA.ZIndex = 100;
    parentGroupB.ZIndex = 50;
    parentGroupC.ZIndex = 20;

    switch (phase)
    {
        case 0:
            parentGroupA.ZIndex = 100;
            parentGroupB.ZIndex = 50;
            parentGroupC.ZIndex = 20;
            boxA1.Title = "Parent A (z=100)";
            boxB1.Title = "Parent B (z=50)";
            boxC1.Title = "Parent C (z=20)";
            break;
        case 1:
            parentGroupA.ZIndex = 50;
            parentGroupB.ZIndex = 20;
            parentGroupC.ZIndex = 100;
            boxA1.Title = "Parent A (z=50)";
            boxB1.Title = "Parent B (z=20)";
            boxC1.Title = "Parent C (z=100)";
            break;
        case 2:
            parentGroupA.ZIndex = 20;
            parentGroupB.ZIndex = 100;
            parentGroupC.ZIndex = 50;
            boxA1.Title = "Parent A (z=20)";
            boxB1.Title = "Parent B (z=100)";
            boxC1.Title = "Parent C (z=50)";
            break;
        case 3:
            parentGroupA.ZIndex = 60;
            parentGroupB.ZIndex = 60;
            parentGroupC.ZIndex = 60;
            boxA1.Title = "Parent A (z=60)";
            boxB1.Title = "Parent B (z=60)";
            boxC1.Title = "Parent C (z=60)";
            break;
    }

    phaseIndicator.ContentText = $"Animation Phase: {phase + 1}/4 - {phases[phase]}";
    zIndexDisplay.ContentText = $"Current Z-Indices - A:{parentGroupA.ZIndex}, B:{parentGroupB.ZIndex}, C:{parentGroupC.ZIndex}";
}

ApplyPhase(0);

Func<float, Task> frameCallback = deltaTime =>
{
    elapsedMs += deltaTime;
    int newPhase = (int)(elapsedMs % (animationSpeed * 4)) / (int)animationSpeed;
    if (newPhase != zIndexPhase)
    {
        zIndexPhase = newPhase;
        ApplyPhase(zIndexPhase);
    }

    return Task.CompletedTask;
};

renderer.AddFrameCallback(frameCallback);
renderer.RequestLive();

renderer.KeyInput.On("keypress", (KeyEvent key) =>
{
    if (key.Name is "+" or "=")
    {
        animationSpeed = Math.Max(500f, animationSpeed - 200f);
    }
    else if (key.Name is "-" or "_")
    {
        animationSpeed = Math.Min(5000f, animationSpeed + 200f);
    }
});

renderer.RequestRender();
await Task.Delay(Timeout.Infinite);
