using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig { ExitOnCtrlC = true });

var root = new BoxRenderable(renderer, new BoxOptions
{
    Id = "root",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Percent(100),
    FlexDirection = FlexDirectionValue.Column,
    BackgroundColor = Rgba.FromHex("#0a0a23"),
    ShouldFill = true,
});

// Title
var titleBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "title-box",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#1b1b4b"),
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Rgba.FromHex("#f77f00"),
    JustifyContent = JustifyValue.Center,
    AlignItems = AlignValue.Center,
});
titleBox.Add(new TextRenderable(renderer, new TextOptions
{
    Id = "title",
    Content = "ASCII Font Demo",
    Fg = Rgba.FromHex("#f77f00"),
    Attributes = TextAttributes.Bold,
}));

// Display container
var displayContainer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "display-container",
    FlexGrow = 1,
    Width = DimensionValue.Percent(100),
    FlexDirection = FlexDirectionValue.Column,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    Gap = 2,
    Padding = DimensionValue.Point(1),
});

string[] texts = ["HELLO", "WORLD", "OPENTUI", "42!", "ABCDEF"];


int currentText = 0;

var fontColors = new[]
{
    Rgba.FromHex("#00f5d4"), Rgba.FromHex("#f15bb5"),
    Rgba.FromHex("#fee440"), Rgba.FromHex("#00bbf9"),
    Rgba.FromHex("#9b5de5"),
};

// Tiny font display
var tinyBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "tiny-box",
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Rgba.FromHex("#333366"),
    BackgroundColor = Rgba.FromHex("#0a0a23"),
    ShouldFill = true,
    Padding = DimensionValue.Point(1),
    FlexDirection = FlexDirectionValue.Column,
    AlignItems = AlignValue.Center,
    Title = " tiny font ",
});
var tinyAscii = new ASCIIFontRenderable(renderer, new ASCIIFontOptions
{
    Id = "tiny-ascii",
    Text = texts[0],
    Font = "tiny",
    Color = fontColors[0],
    BackgroundColor = Rgba.Transparent,
});
tinyBox.Add(tinyAscii);

// Small font display
var smallBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "small-box",
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Rgba.FromHex("#333366"),
    BackgroundColor = Rgba.FromHex("#0a0a23"),
    ShouldFill = true,
    Padding = DimensionValue.Point(1),
    FlexDirection = FlexDirectionValue.Column,
    AlignItems = AlignValue.Center,
    Title = " small font ",
});
var smallAscii = new ASCIIFontRenderable(renderer, new ASCIIFontOptions
{
    Id = "small-ascii",
    Text = texts[0],
    Font = "small",
    Color = fontColors[0],
    BackgroundColor = Rgba.Transparent,
});
smallBox.Add(smallAscii);

// Info label
var infoText = new TextRenderable(renderer, new TextOptions
{
    Id = "info",
    Fg = Rgba.FromHex("#888888"),
});

displayContainer.Add(tinyBox);
displayContainer.Add(smallBox);
displayContainer.Add(infoText);

// Footer
var footer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "footer",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#1b1b4b"),
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Rgba.FromHex("#888888"),
    JustifyContent = JustifyValue.Center,
    AlignItems = AlignValue.Center,
});
footer.Add(new TextRenderable(renderer, new TextOptions
{
    Id = "footer-text",
    StyledContent = new StyledText(
        TextChunk.Styled("n", fg: Rgba.FromHex("#f77f00"), attributes: TextAttributes.Bold),
        TextChunk.Styled(" next text  |  ", fg: Rgba.FromHex("#888888")),
        TextChunk.Styled("Ctrl+C", fg: Rgba.FromHex("#f77f00"), attributes: TextAttributes.Bold),
        TextChunk.Styled(" exit", fg: Rgba.FromHex("#888888"))
    ),
}));

root.Add(titleBox);
root.Add(displayContainer);
root.Add(footer);
renderer.Root.Add(root);

UpdateDisplay();

renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    if (e.Name == "n")
    {
        currentText = (currentText + 1) % texts.Length;
        UpdateDisplay();
        renderer.RequestRender();
    }
});

void UpdateDisplay()
{
    var color = fontColors[currentText % fontColors.Length];
    tinyAscii.Text = texts[currentText];
    tinyAscii.Color = color;
    smallAscii.Text = texts[currentText];
    smallAscii.Color = color;

    var (tw, th) = ASCIIFontRenderable.MeasureText(texts[currentText], "tiny");
    var (sw, sh) = ASCIIFontRenderable.MeasureText(texts[currentText], "small");
    infoText.Content = new StyledText(
        TextChunk.Styled($"Text: \"{texts[currentText]}\"", fg: Rgba.FromHex("#cccccc")),
        TextChunk.Styled($"  |  tiny: {tw}x{th}  small: {sw}x{sh}", fg: Rgba.FromHex("#666666"))
    );
}

await Task.Delay(Timeout.Infinite);
