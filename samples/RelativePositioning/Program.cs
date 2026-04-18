using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig { ExitOnCtrlC = true });

var root = new BoxRenderable(renderer, new BoxOptions
{
    Id = "root",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Percent(100),
    FlexDirection = FlexDirectionValue.Column,
    BackgroundColor = Rgba.FromHex("#0d1117"),
    ShouldFill = true,
});

// Title
var titleBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "title-box",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#161b22"),
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Rgba.FromHex("#58a6ff"),
    JustifyContent = JustifyValue.Center,
    AlignItems = AlignValue.Center,
});
titleBox.Add(new TextRenderable(renderer, new TextOptions
{
    Id = "title",
    Content = "Relative Positioning Demo",
    Fg = Rgba.FromHex("#58a6ff"),
    Attributes = TextAttributes.Bold,
}));

// Container for the 4 boxes
var container = new BoxRenderable(renderer, new BoxOptions
{
    Id = "container",
    FlexGrow = 1,
    Width = DimensionValue.Percent(100),
    FlexDirection = FlexDirectionValue.Row,
    JustifyContent = JustifyValue.Center,
    AlignItems = AlignValue.Center,
    Gap = 4,
});

var boxColors = new[]
{
    Rgba.FromHex("#ff7b72"), Rgba.FromHex("#7ee787"),
    Rgba.FromHex("#d2a8ff"), Rgba.FromHex("#79c0ff"),
};
string[] boxLabels = ["Static", "Offset(-2,1)", "Offset(2,-1)", "WASD Move"];

// Track offsets: [topOffset, leftOffset] per box
var offsets = new int[4, 2];
offsets[1, 0] = -2; offsets[1, 1] = 1;   // box 1: top=-2, left=1
offsets[2, 0] = 2;  offsets[2, 1] = -1;  // box 2: top=2, left=-1
offsets[3, 0] = 0;  offsets[3, 1] = 0;   // box 3: movable

var boxes = new BoxRenderable[4];
var labelTexts = new TextRenderable[4];

for (int i = 0; i < 4; i++)
{
    var box = new BoxRenderable(renderer, new BoxOptions
    {
        Id = $"box-{i}",
        Width = DimensionValue.Point(18),
        Height = DimensionValue.Point(7),
        Position = PositionValue.Relative,
        Top = DimensionValue.Point(offsets[i, 0]),
        Left = DimensionValue.Point(offsets[i, 1]),
        BackgroundColor = boxColors[i],
        ShouldFill = true,
        Border = true,
        BorderStyle = BorderStyle.Rounded,
        BorderColor = Rgba.White,
        FlexDirection = FlexDirectionValue.Column,
        AlignItems = AlignValue.Center,
        JustifyContent = JustifyValue.Center,
    });

    var nameLabel = new TextRenderable(renderer, new TextOptions
    {
        Id = $"name-{i}",
        Content = boxLabels[i],
        Fg = Rgba.FromHex("#0d1117"),
        Attributes = TextAttributes.Bold,
    });

    var offsetLabel = new TextRenderable(renderer, new TextOptions
    {
        Id = $"offset-{i}",
        Content = $"T:{offsets[i, 0]} L:{offsets[i, 1]}",
        Fg = Rgba.FromHex("#0d1117"),
    });

    box.Add(nameLabel);
    box.Add(offsetLabel);
    boxes[i] = box;
    labelTexts[i] = offsetLabel;
    container.Add(box);
}

// Footer
var footer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "footer",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#161b22"),
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Rgba.FromHex("#3fb950"),
    FlexDirection = FlexDirectionValue.Column,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
});
var footerText = new TextRenderable(renderer, new TextOptions
{
    Id = "footer-text",
    StyledContent = new StyledText(
        TextChunk.Styled("WASD", fg: Rgba.FromHex("#ffa657"), attributes: TextAttributes.Bold),
        TextChunk.Styled(" moves box 4  |  ", fg: Rgba.FromHex("#8b949e")),
        TextChunk.Styled("Box 4 offset: ", fg: Rgba.FromHex("#8b949e")),
        TextChunk.Styled($"T:{offsets[3, 0]} L:{offsets[3, 1]}", fg: Rgba.FromHex("#79c0ff"), attributes: TextAttributes.Bold)
    ),
});
footer.Add(footerText);

root.Add(titleBox);
root.Add(container);
root.Add(footer);
renderer.Root.Add(root);

void UpdateFooter()
{
    footerText.Content = new StyledText(
        TextChunk.Styled("WASD", fg: Rgba.FromHex("#ffa657"), attributes: TextAttributes.Bold),
        TextChunk.Styled(" moves box 4  |  ", fg: Rgba.FromHex("#8b949e")),
        TextChunk.Styled("Box 4 offset: ", fg: Rgba.FromHex("#8b949e")),
        TextChunk.Styled($"T:{offsets[3, 0]} L:{offsets[3, 1]}", fg: Rgba.FromHex("#79c0ff"), attributes: TextAttributes.Bold)
    );
}

renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    bool changed = false;
    switch (e.Name)
    {
        case "w": offsets[3, 0]--; changed = true; break;
        case "s": offsets[3, 0]++; changed = true; break;
        case "a": offsets[3, 1]--; changed = true; break;
        case "d": offsets[3, 1]++; changed = true; break;
    }

    if (changed)
    {
        boxes[3].Top = DimensionValue.Point(offsets[3, 0]);
        boxes[3].Left = DimensionValue.Point(offsets[3, 1]);
        labelTexts[3].ContentText = $"T:{offsets[3, 0]} L:{offsets[3, 1]}";
        UpdateFooter();
        renderer.RequestRender();
    }
});

await Task.Delay(Timeout.Infinite);
