// Terminal Palette — shows terminal color capabilities
// Port of terminal.ts
using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
});

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
    Content = "Terminal Palette — Color Capabilities",
    Fg = Rgba.FromInts(255, 255, 255),
});
header.Add(headerText);

// --- 16 ANSI colors ---
var ansiSection = new BoxRenderable(renderer, new BoxOptions
{
    Id = "ansi-section",
    Width = DimensionValue.Auto,
    FlexDirection = FlexDirectionValue.Column,
    Padding = DimensionValue.Point(1),
});
var ansiLabel = new TextRenderable(renderer, new TextOptions
{
    Id = "ansi-label",
    Content = "Standard ANSI Colors (16)",
    Fg = Rgba.FromInts(255, 255, 255),
    Attributes = TextAttributes.Bold,
});
ansiSection.Add(ansiLabel);

var ansiRow = new BoxRenderable(renderer, new BoxOptions
{
    Id = "ansi-row",
    FlexDirection = FlexDirectionValue.Row,
    FlexWrap = WrapValue.Wrap,
    Gap = 1,
});

string[] ansiColors =
[
    "#000000", "#aa0000", "#00aa00", "#aa5500",
    "#0000aa", "#aa00aa", "#00aaaa", "#aaaaaa",
    "#555555", "#ff5555", "#55ff55", "#ffff55",
    "#5555ff", "#ff55ff", "#55ffff", "#ffffff",
];

for (int i = 0; i < ansiColors.Length; i++)
{
    var colorBox = new BoxRenderable(renderer, new BoxOptions
    {
        Id = $"ansi-{i}",
        Width = DimensionValue.Point(6),
        Height = DimensionValue.Point(2),
        BackgroundColor = Rgba.FromHex(ansiColors[i]),
        ShouldFill = true,
        AlignItems = AlignValue.Center,
        JustifyContent = JustifyValue.Center,
    });
    var numText = new TextRenderable(renderer, new TextOptions
    {
        Id = $"ansi-text-{i}",
        Content = i.ToString(),
        Fg = i < 8 ? Rgba.FromInts(255, 255, 255) : Rgba.FromInts(0, 0, 0),
    });
    colorBox.Add(numText);
    ansiRow.Add(colorBox);
}
ansiSection.Add(ansiRow);

// --- 256-color palette (6x6x6 cube) ---
var cubeSection = new BoxRenderable(renderer, new BoxOptions
{
    Id = "cube-section",
    Width = DimensionValue.Auto,
    FlexDirection = FlexDirectionValue.Column,
    Padding = DimensionValue.Point(1),
    FlexGrow = 1,
});
var cubeLabel = new TextRenderable(renderer, new TextOptions
{
    Id = "cube-label",
    Content = "216 Color Cube (6×6×6)",
    Fg = Rgba.FromInts(255, 255, 255),
    Attributes = TextAttributes.Bold,
});
cubeSection.Add(cubeLabel);

var cubeGrid = new BoxRenderable(renderer, new BoxOptions
{
    Id = "cube-grid",
    FlexDirection = FlexDirectionValue.Row,
    FlexWrap = WrapValue.Wrap,
});

for (int r = 0; r < 6; r++)
{
    for (int g = 0; g < 6; g++)
    {
        for (int b = 0; b < 6; b++)
        {
            int rv = r == 0 ? 0 : 55 + r * 40;
            int gv = g == 0 ? 0 : 55 + g * 40;
            int bv = b == 0 ? 0 : 55 + b * 40;

            var cell = new BoxRenderable(renderer, new BoxOptions
            {
                Id = $"cube-{r}-{g}-{b}",
                Width = DimensionValue.Point(2),
                Height = DimensionValue.Point(1),
                BackgroundColor = Rgba.FromInts(rv, gv, bv),
                ShouldFill = true,
            });
            cubeGrid.Add(cell);
        }
    }
}
cubeSection.Add(cubeGrid);

// --- Grayscale ramp ---
var graySection = new BoxRenderable(renderer, new BoxOptions
{
    Id = "gray-section",
    Width = DimensionValue.Auto,
    FlexDirection = FlexDirectionValue.Column,
    Padding = DimensionValue.Point(1),
});
var grayLabel = new TextRenderable(renderer, new TextOptions
{
    Id = "gray-label",
    Content = "Grayscale Ramp (24 shades)",
    Fg = Rgba.FromInts(255, 255, 255),
    Attributes = TextAttributes.Bold,
});
graySection.Add(grayLabel);

var grayRow = new BoxRenderable(renderer, new BoxOptions
{
    Id = "gray-row",
    FlexDirection = FlexDirectionValue.Row,
});
for (int i = 0; i < 24; i++)
{
    int v = 8 + i * 10;
    var cell = new BoxRenderable(renderer, new BoxOptions
    {
        Id = $"gray-{i}",
        Width = DimensionValue.Point(3),
        Height = DimensionValue.Point(1),
        BackgroundColor = Rgba.FromInts(v, v, v),
        ShouldFill = true,
    });
    grayRow.Add(cell);
}
graySection.Add(grayRow);

// --- Text attributes demo ---
var attrSection = new BoxRenderable(renderer, new BoxOptions
{
    Id = "attr-section",
    Width = DimensionValue.Auto,
    FlexDirection = FlexDirectionValue.Column,
    Padding = DimensionValue.Point(1),
});
var attrLabel = new TextRenderable(renderer, new TextOptions
{
    Id = "attr-label",
    Content = "Text Attributes",
    Fg = Rgba.FromInts(255, 255, 255),
    Attributes = TextAttributes.Bold,
});
attrSection.Add(attrLabel);

var attrRow = new BoxRenderable(renderer, new BoxOptions
{
    Id = "attr-row",
    FlexDirection = FlexDirectionValue.Row,
    Gap = 2,
    FlexWrap = WrapValue.Wrap,
});

(string name, TextAttributes attr)[] Attributes =
[
    ("Bold", TextAttributes.Bold),
    ("Dim", TextAttributes.Dim),
    ("Italic", TextAttributes.Italic),
    ("Underline", TextAttributes.Underline),
    ("Blink", TextAttributes.Blink),
    ("Inverse", TextAttributes.Inverse),
    ("Strikethrough", TextAttributes.Strikethrough),
];
foreach (var (name, attr) in Attributes)
{
    var attrText = new TextRenderable(renderer, new TextOptions
    {
        Id = $"attr-{name.ToLowerInvariant()}",
        StyledContent = new StyledText(TextChunk.Styled(name, fg: Rgba.FromInts(255, 255, 255), attributes: attr)),
    });
    attrRow.Add(attrText);
}
attrSection.Add(attrRow);

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
    Content = "Ctrl+C: exit",
    Fg = Rgba.FromInts(255, 255, 255),
});
footer.Add(footerText);

// --- Build tree ---
renderer.Root.Add(header);
renderer.Root.Add(ansiSection);
renderer.Root.Add(cubeSection);
renderer.Root.Add(graySection);
renderer.Root.Add(attrSection);
renderer.Root.Add(footer);

renderer.RequestRender();
await Task.Delay(Timeout.Infinite);
