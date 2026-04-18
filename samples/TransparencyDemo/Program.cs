// Transparency Demo — draggable RGBA boxes with alpha blending
// Port of transparency-demo.ts
using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 30,
});

int themeIndex = 0;
Rgba[] themes =
[
    Rgba.FromHex("#001122"), // dark
    Rgba.FromHex("#f0f0f0"), // light
    Rgba.Transparent,        // transparent
];

renderer.Native.SetBackgroundColor(themes[0]);

// --- Header ---
var header = new BoxRenderable(renderer, new BoxOptions
{
    Id = "header",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#1e40af"),
    Border = true,
    BorderStyle = BorderStyle.Single,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
});
var headerText = new TextRenderable(renderer, new TextOptions
{
    Id = "header-text",
    Content = "Transparency Demo",
    Fg = Rgba.FromInts(255, 255, 255),
});
header.Add(headerText);

// --- Content area ---
var content = new BoxRenderable(renderer, new BoxOptions
{
    Id = "content",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Auto,
    FlexGrow = 1,
    BackgroundColor = Rgba.FromHex("#111122"),
});

// --- Colored transparent boxes ---
(string name, string color, float alpha, int x, int y)[] boxDefs =
[
    ("Red",    "#ff0000", 0.6f, 2,  2),
    ("Green",  "#00ff00", 0.5f, 12, 4),
    ("Blue",   "#0000ff", 0.7f, 22, 2),
    ("Yellow", "#ffff00", 0.4f, 8,  6),
    ("Cyan",   "#00ffff", 0.5f, 18, 5),
    ("Purple", "#ff00ff", 0.3f, 5,  3),
];

var boxes = new List<BoxRenderable>();
var labels = new List<TextRenderable>();

foreach (var (name, color, alpha, x, y) in boxDefs)
{
    var rgba = Rgba.FromHex(color);
    var box = new BoxRenderable(renderer, new BoxOptions
    {
        Id = $"box-{name.ToLowerInvariant()}",
        Width = DimensionValue.Point(16),
        Height = DimensionValue.Point(5),
        BackgroundColor = rgba,
        Opacity = alpha,
        Position = PositionValue.Absolute,
        Left = DimensionValue.Point(x),
        Top = DimensionValue.Point(y),
        Border = true,
        BorderStyle = BorderStyle.Rounded,
        BorderColor = rgba,
        AlignItems = AlignValue.Center,
        JustifyContent = JustifyValue.Center,
        ZIndex = 10 + boxes.Count,
        ShouldFill = true,
    });

    var label = new TextRenderable(renderer, new TextOptions
    {
        Id = $"label-{name.ToLowerInvariant()}",
        Content = $"{name} ({alpha:P0})",
        Fg = Rgba.FromInts(255, 255, 255),
    });
    box.Add(label);
    content.Add(box);
    boxes.Add(box);
    labels.Add(label);
}

// --- Nested opacity demo ---
var parentBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "nested-parent",
    Width = DimensionValue.Point(24),
    Height = DimensionValue.Point(7),
    BackgroundColor = Rgba.FromHex("#ff8800"),
    Opacity = 0.7f,
    Position = PositionValue.Absolute,
    Right = DimensionValue.Point(2),
    Top = DimensionValue.Point(2),
    Border = true,
    BorderStyle = BorderStyle.Double,
    BorderColor = Rgba.FromHex("#ff8800"),
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    FlexDirection = FlexDirectionValue.Column,
    ZIndex = 50,
    ShouldFill = true,
});

var parentLabel = new TextRenderable(renderer, new TextOptions
{
    Id = "nested-parent-label",
    Content = "Parent (0.7)",
    Fg = Rgba.FromInts(255, 255, 255),
});

var childBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "nested-child",
    Width = DimensionValue.Point(18),
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#0088ff"),
    Opacity = 0.5f,
    Border = true,
    BorderStyle = BorderStyle.Single,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    ShouldFill = true,
});

var childLabel = new TextRenderable(renderer, new TextOptions
{
    Id = "nested-child-label",
    Content = "Child (0.5)",
    Fg = Rgba.FromInts(255, 255, 255),
});

childBox.Add(childLabel);
parentBox.Add(parentLabel);
parentBox.Add(childBox);
content.Add(parentBox);

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
    Content = "T: cycle theme | +/-: adjust opacity | 1-6: select box | WASD: move",
    Fg = Rgba.FromInts(255, 255, 255),
});
footer.Add(footerText);

renderer.Root.Add(header);
renderer.Root.Add(content);
renderer.Root.Add(footer);

int[] posX = boxDefs.Select(b => b.x).ToArray();
int[] posY = boxDefs.Select(b => b.y).ToArray();

int selectedBox = 0;

void UpdateSelection()
{
    for (int i = 0; i < boxes.Count; i++)
    {
        boxes[i].BorderStyle = i == selectedBox ? BorderStyle.Double : BorderStyle.Rounded;
    }
}

UpdateSelection();

// --- Key handling ---
renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    switch (e.Name)
    {
        case "t":
            themeIndex = (themeIndex + 1) % themes.Length;
            renderer.Native.SetBackgroundColor(themes[themeIndex]);
            content.BackgroundColor = themeIndex == 1
                ? Rgba.FromHex("#e0e0e0")
                : Rgba.FromHex("#111122");
            renderer.RequestRender();
            break;

        case "1": case "2": case "3": case "4": case "5": case "6":
            selectedBox = int.Parse(e.Name) - 1;
            if (selectedBox < boxes.Count)
                UpdateSelection();
            break;

        case "=": case "+":
        {
            float op = Math.Min(1f, boxes[selectedBox].Opacity + 0.1f);
            boxes[selectedBox].Opacity = op;
            labels[selectedBox].ContentText = $"{boxDefs[selectedBox].name} ({op:P0})";
            break;
        }

        case "-":
        {
            float op = Math.Max(0f, boxes[selectedBox].Opacity - 0.1f);
            boxes[selectedBox].Opacity = op;
            labels[selectedBox].ContentText = $"{boxDefs[selectedBox].name} ({op:P0})";
            break;
        }

        case "w":
            posY[selectedBox] = Math.Max(0, posY[selectedBox] - 1);
            boxes[selectedBox].Top = DimensionValue.Point(posY[selectedBox]);
            break;
        case "a":
            posX[selectedBox] = Math.Max(0, posX[selectedBox] - 1);
            boxes[selectedBox].Left = DimensionValue.Point(posX[selectedBox]);
            break;
        case "s":
            posY[selectedBox]++;
            boxes[selectedBox].Top = DimensionValue.Point(posY[selectedBox]);
            break;
        case "d":
            posX[selectedBox]++;
            boxes[selectedBox].Left = DimensionValue.Point(posX[selectedBox]);
            break;
    }
});

renderer.RequestRender();
await Task.Delay(Timeout.Infinite);
