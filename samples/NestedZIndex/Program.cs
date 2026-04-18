using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig { ExitOnCtrlC = true });

// Colors for each box
var colors = new[]
{
    Rgba.FromHex("#e74c3c"), Rgba.FromHex("#3498db"), Rgba.FromHex("#2ecc71"),
    Rgba.FromHex("#f39c12"), Rgba.FromHex("#9b59b6"), Rgba.FromHex("#1abc9c"),
};
string[] labels = ["Red", "Blue", "Green", "Orange", "Purple", "Teal"];

// Root column layout
var root = new BoxRenderable(renderer, new BoxOptions
{
    Id = "root",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Percent(100),
    FlexDirection = FlexDirectionValue.Column,
    BackgroundColor = Rgba.FromHex("#1a1a2e"),
    ShouldFill = true,
});

// Title
var titleBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "title-box",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#16213e"),
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Rgba.FromHex("#e94560"),
    JustifyContent = JustifyValue.Center,
    AlignItems = AlignValue.Center,
});
var titleText = new TextRenderable(renderer, new TextOptions
{
    Id = "title",
    Content = "Nested Z-Index Demo",
    Fg = Rgba.FromHex("#e94560"),
    Attributes = TextAttributes.Bold,
});
titleBox.Add(titleText);

// Container row for the 3 nested containers
var containerRow = new BoxRenderable(renderer, new BoxOptions
{
    Id = "container-row",
    Width = DimensionValue.Percent(100),
    FlexGrow = 1,
    FlexDirection = FlexDirectionValue.Row,
    Gap = 2,
    Padding = DimensionValue.Point(1),
});

// Track the 6 child boxes and their current z-indices
var childBoxes = new BoxRenderable[6];
var childZIndices = new int[] { 1, 2, 1, 2, 1, 2 };

for (int c = 0; c < 3; c++)
{
    var container = new BoxRenderable(renderer, new BoxOptions
    {
        Id = $"container-{c}",
        FlexGrow = 1,
        Height = DimensionValue.Percent(100),
        Border = true,
        BorderStyle = BorderStyle.Rounded,
        BorderColor = Rgba.FromHex("#333366"),
        BackgroundColor = Rgba.FromHex("#0f3460"),
        ShouldFill = true,
        Title = $" Container {c + 1} ",
    });

    for (int b = 0; b < 2; b++)
    {
        int idx = c * 2 + b;
        int zIdx = childZIndices[idx];
        var child = new BoxRenderable(renderer, new BoxOptions
        {
            Id = $"box-{idx}",
            Position = PositionValue.Absolute,
            Width = DimensionValue.Point(16),
            Height = DimensionValue.Point(5),
            Top = DimensionValue.Point(b == 0 ? 2 : 4),
            Left = DimensionValue.Point(b == 0 ? 1 : 5),
            ZIndex = zIdx,
            BackgroundColor = colors[idx],
            ShouldFill = true,
            Border = true,
            BorderStyle = BorderStyle.Single,
            BorderColor = Rgba.White,
            AlignItems = AlignValue.Center,
            JustifyContent = JustifyValue.Center,
        });
        var label = new TextRenderable(renderer, new TextOptions
        {
            Id = $"label-{idx}",
            Content = $"{idx + 1}:{labels[idx]} z={zIdx}",
            Fg = Rgba.White,
            Attributes = TextAttributes.Bold,
        });
        child.Add(label);
        childBoxes[idx] = child;
        container.Add(child);
    }

    containerRow.Add(container);
}

// Footer with instructions
var footer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "footer",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#16213e"),
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Rgba.FromHex("#533483"),
    JustifyContent = JustifyValue.Center,
    AlignItems = AlignValue.Center,
});
var footerText = new TextRenderable(renderer, new TextOptions
{
    Id = "footer-text",
    StyledContent = new StyledText(
        TextChunk.Styled("Press ", fg: Rgba.FromHex("#888888")),
        TextChunk.Styled("1-6", fg: Rgba.FromHex("#e94560"), attributes: TextAttributes.Bold),
        TextChunk.Styled(" to cycle z-index of each box  |  ", fg: Rgba.FromHex("#888888")),
        TextChunk.Styled("Ctrl+C", fg: Rgba.FromHex("#e94560"), attributes: TextAttributes.Bold),
        TextChunk.Styled(" to exit", fg: Rgba.FromHex("#888888"))
    ),
});
footer.Add(footerText);

root.Add(titleBox);
root.Add(containerRow);
root.Add(footer);
renderer.Root.Add(root);

// Key handler: press 1-6 to cycle z-index
renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    if (e.Name is { Length: 1 } n && n[0] >= '1' && n[0] <= '6')
    {
        int idx = n[0] - '1';
        childZIndices[idx] = (childZIndices[idx] % 5) + 1;
        childBoxes[idx].ZIndex = childZIndices[idx];

        // Update label
        var label = (TextRenderable)childBoxes[idx].GetChildren()[0];
        label.ContentText = $"{idx + 1}:{labels[idx]} z={childZIndices[idx]}";
        renderer.RequestRender();
    }
});

await Task.Delay(Timeout.Infinite);
