using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 30,
    BackgroundColor = Rgba.FromHex("#001122"),
});

float animationSpeed = 4000f;
float animationTime = 0f;

var rootContainer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "root-container",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Percent(100),
    Position = PositionValue.Relative,
    Left = DimensionValue.Point(0),
    Top = DimensionValue.Point(0),
    ZIndex = 10,
});
renderer.Root.Add(rootContainer);

TextRenderable CreateAbsoluteText(
    string id,
    string content,
    int left,
    int top,
    string fgHex,
    TextAttributes attributes = TextAttributes.None,
    int zIndex = 0)
{
    return new TextRenderable(renderer, new TextOptions
    {
        Id = id,
        Content = content,
        Position = PositionValue.Absolute,
        Left = DimensionValue.Point(left),
        Top = DimensionValue.Point(top),
        Fg = Rgba.FromHex(fgHex),
        Attributes = attributes,
        ZIndex = zIndex,
    });
}

TextRenderable CreateText(
    string id,
    string content,
    string fgHex,
    TextAttributes attributes = TextAttributes.None,
    int zIndex = 0)
{
    return new TextRenderable(renderer, new TextOptions
    {
        Id = id,
        Content = content,
        Fg = Rgba.FromHex(fgHex),
        Attributes = attributes,
        ZIndex = zIndex,
    });
}

BoxRenderable CreateFlexChildBox(string id, string title, string backgroundHex)
{
    return new BoxRenderable(renderer, new BoxOptions
    {
        Id = id,
        Width = DimensionValue.Auto,
        Height = DimensionValue.Auto,
        BackgroundColor = Rgba.FromHex(backgroundHex),
        ZIndex = 2,
        BorderStyle = BorderStyle.Single,
        BorderColor = Rgba.FromHex("#FF88FF"),
        Title = title,
        TitleAlignment = TitleAlignment.Center,
        FlexGrow = 1,
        FlexShrink = 1,
        MinWidth = DimensionValue.Point(8),
        Border = true,
    });
}

var title = CreateAbsoluteText(
    "main-title",
    "Relative Positioning Demo - Child positions are relative to parent",
    left: 5,
    top: 1,
    fgHex: "#FFFF00",
    attributes: TextAttributes.Bold | TextAttributes.Underline,
    zIndex: 1000);
rootContainer.Add(title);

var parentContainerA = new BoxRenderable(renderer, new BoxOptions
{
    Id = "parent-container-a",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(10),
    Top = DimensionValue.Point(5),
    ZIndex = 50,
});
rootContainer.Add(parentContainerA);

var parentBoxA = new BoxRenderable(renderer, new BoxOptions
{
    Id = "parent-box-a",
    Width = DimensionValue.Point(40),
    Height = DimensionValue.Point(12),
    BackgroundColor = Rgba.FromHex("#220044"),
    ZIndex = 1,
    BorderStyle = BorderStyle.Double,
    BorderColor = Rgba.FromHex("#FF44FF"),
    Title = "Parent A (moves in circle)",
    TitleAlignment = TitleAlignment.Center,
    FlexDirection = FlexDirectionValue.Row,
    AlignItems = AlignValue.Stretch,
    JustifyContent = JustifyValue.SpaceBetween,
    Border = true,
});
parentContainerA.Add(parentBoxA);
parentBoxA.Add(CreateFlexChildBox("child-a1", "Child 1", "#440066"));
parentBoxA.Add(CreateFlexChildBox("child-a2", "Child 2", "#660044"));
parentBoxA.Add(CreateFlexChildBox("child-a3", "Child 3", "#440044"));

var parentContainerB = new BoxRenderable(renderer, new BoxOptions
{
    Id = "parent-container-b",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(50),
    Top = DimensionValue.Point(8),
    ZIndex = 50,
});
rootContainer.Add(parentContainerB);

var parentBoxB = new BoxRenderable(renderer, new BoxOptions
{
    Id = "parent-box-b",
    Width = DimensionValue.Point(40),
    Height = DimensionValue.Point(10),
    BackgroundColor = Rgba.FromHex("#004422"),
    ZIndex = 1,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Rgba.FromHex("#44FF44"),
    Title = "Parent B (moves vertically)",
    TitleAlignment = TitleAlignment.Center,
    Padding = DimensionValue.Point(1),
    FlexDirection = FlexDirectionValue.Column,
    JustifyContent = JustifyValue.SpaceBetween,
    Border = true,
});
parentContainerB.Add(parentBoxB);

var parentLabelB = CreateText(
    "parent-label-b",
    "Parent B Position: (50, 8)",
    "#44FF44",
    attributes: TextAttributes.Bold,
    zIndex: 2);
parentBoxB.Add(parentLabelB);
parentBoxB.Add(CreateText("child-b1", "Child at (1,3) - relative to parent", "#88FF88", zIndex: 2));
parentBoxB.Add(CreateText("child-b2", "Child at (1,5) - relative to parent", "#88FF88", zIndex: 2));

var staticContainer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "static-container",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(5),
    Top = DimensionValue.Point(20),
    ZIndex = 50,
});
rootContainer.Add(staticContainer);

var staticBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "static-box",
    Width = DimensionValue.Point(40),
    Height = DimensionValue.Point(8),
    BackgroundColor = Rgba.FromHex("#442200"),
    ZIndex = 1,
    BorderStyle = BorderStyle.Single,
    BorderColor = Rgba.FromHex("#FFFF44"),
    Title = "Static Parent (doesn't move)",
    TitleAlignment = TitleAlignment.Center,
    Padding = DimensionValue.Point(1),
    FlexDirection = FlexDirectionValue.Column,
    Border = true,
    Overflow = OverflowValue.Hidden,
});
staticContainer.Add(staticBox);
staticBox.Add(CreateText("static-child1", "Static child at (2,2) - never moves", "#FFFF88", zIndex: 2));
staticBox.Add(CreateText("static-child2", "Static child at (2,4) - never moves", "#FFFF88", zIndex: 2));

rootContainer.Add(CreateAbsoluteText(
    "explanation1",
    "Key Concept: Parent A uses flex layout - children are arranged in a row",
    left: 5,
    top: 30,
    fgHex: "#AAAAAA",
    attributes: TextAttributes.Bold,
    zIndex: 1000));
rootContainer.Add(CreateAbsoluteText(
    "explanation2",
    "When parent moves, children move with it while maintaining flex layout",
    left: 5,
    top: 31,
    fgHex: "#AAAAAA",
    zIndex: 1000));
rootContainer.Add(CreateAbsoluteText(
    "explanation3",
    "Flex children automatically fit parent width and grow/shrink as needed",
    left: 5,
    top: 32,
    fgHex: "#AAAAAA",
    zIndex: 1000));
rootContainer.Add(CreateAbsoluteText(
    "controls",
    "Controls: +/- to change animation speed",
    left: 5,
    top: 34,
    fgHex: "#FFFFFF",
    attributes: TextAttributes.Bold,
    zIndex: 1000));

var speedDisplay = CreateAbsoluteText(
    "speed-display",
    $"Animation Speed: {(int)animationSpeed}ms (min: 500, max: 8000)",
    left: 5,
    top: 35,
    fgHex: "#CCCCCC",
    zIndex: 1000);
rootContainer.Add(speedDisplay);

void UpdateSpeedDisplay()
{
    speedDisplay.ContentText = $"Animation Speed: {(int)animationSpeed}ms (min: 500, max: 8000)";
}

renderer.AddFrameCallback(dt =>
{
    animationTime += dt;

    const float circleRadius = 15f;
    float circleSpeed = (animationTime / animationSpeed) * MathF.PI * 2f;
    float parentAX = 20f + MathF.Cos(circleSpeed) * circleRadius;
    float parentAY = 8f + (MathF.Sin(circleSpeed) * circleRadius) / 2f;

    parentContainerA.Left = DimensionValue.Point((int)MathF.Round(parentAX));
    parentContainerA.Top = DimensionValue.Point((int)MathF.Round(parentAY));

    float verticalSpeed = (animationTime / (animationSpeed * 1.5f)) * MathF.PI * 2f;
    float parentBY = 8f + MathF.Sin(verticalSpeed) * 8f;

    parentContainerB.Left = DimensionValue.Point(50);
    parentContainerB.Top = DimensionValue.Point((int)MathF.Round(parentBY));
    parentLabelB.ContentText = $"Parent B Position: (50, {(int)MathF.Round(parentBY)})";

    return Task.CompletedTask;
});

renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    bool changed = false;

    if (e.Name is "+" or "=")
    {
        animationSpeed = Math.Max(500f, animationSpeed - 300f);
        changed = true;
    }
    else if (e.Name is "-" or "_")
    {
        animationSpeed = Math.Min(8000f, animationSpeed + 300f);
        changed = true;
    }

    if (changed)
    {
        UpdateSpeedDisplay();
        renderer.RequestRender();
    }
});

renderer.RequestLive();
await Task.Delay(Timeout.Infinite);
