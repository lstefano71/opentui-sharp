// ASCII Font Selection — type characters and see them in large block font
using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 30,
});

renderer.Native.SetBackgroundColor(Rgba.FromHex("#0f172a"));

string currentText = "HELLO";
string currentFont = "tiny";
string[] fonts = ["tiny", "small"];
int fontIndex = 0;

// --- Header ---
var header = new BoxRenderable(renderer, new BoxOptions
{
    Id = "header",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#dc2626"),
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
});
var headerText = new TextRenderable(renderer, new TextOptions
{
    Id = "header-text",
    StyledContent = new StyledText(
        TextChunk.Styled("ASCII Font Selection", fg: Rgba.White, attributes: TextAttributes.Bold)),
    Fg = Rgba.White,
});
header.Add(headerText);

// --- Preview area ---
var previewBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "preview",
    Width = DimensionValue.Auto,
    FlexGrow = 1,
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Rgba.FromHex("#475569"),
    BackgroundColor = Rgba.FromHex("#1e293b"),
    FlexDirection = FlexDirectionValue.Column,
    Padding = DimensionValue.Point(1),
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
});

var asciiFont = new ASCIIFontRenderable(renderer, new ASCIIFontOptions
{
    Id = "ascii-display",
    Text = currentText,
    Font = currentFont,
    Color = Rgba.FromHex("#fbbf24"),
});
previewBox.Add(asciiFont);

// --- Info panel ---
var infoBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "info",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(5),
    FlexDirection = FlexDirectionValue.Column,
    Padding = DimensionValue.Point(1),
    BackgroundColor = Rgba.FromHex("#111827"),
});

var inputLabel = new TextRenderable(renderer, new TextOptions
{
    Id = "input-label",
    Content = "",
    Fg = Rgba.FromHex("#e2e8f0"),
});

var fontLabel = new TextRenderable(renderer, new TextOptions
{
    Id = "font-label",
    Content = "",
    Fg = Rgba.FromHex("#94a3b8"),
});

var sizeLabel = new TextRenderable(renderer, new TextOptions
{
    Id = "size-label",
    Content = "",
    Fg = Rgba.FromHex("#64748b"),
});

infoBox.Add(inputLabel);
infoBox.Add(fontLabel);
infoBox.Add(sizeLabel);

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
    Content = "Type A-Z/0-9 | Backspace: delete | F: toggle font | Ctrl+C: exit",
    Fg = Rgba.FromHex("#93c5fd"),
});
footer.Add(footerText);

// --- Build tree ---
renderer.Root.Add(header);
renderer.Root.Add(previewBox);
renderer.Root.Add(infoBox);
renderer.Root.Add(footer);

void UpdateDisplay()
{
    asciiFont.Text = currentText.Length > 0 ? currentText : " ";
    asciiFont.Font = currentFont;

    var (w, h) = ASCIIFontRenderable.MeasureText(currentText, currentFont);

    inputLabel.Content = new StyledText(
        TextChunk.Styled("Text: ", fg: Rgba.FromHex("#94a3b8")),
        TextChunk.Styled(currentText.Length > 0 ? currentText : "(empty)", fg: Rgba.FromHex("#fbbf24"),
            attributes: TextAttributes.Bold));

    fontLabel.Content = new StyledText(
        TextChunk.Styled("Font: ", fg: Rgba.FromHex("#94a3b8")),
        TextChunk.Styled(currentFont, fg: Rgba.FromHex("#38bdf8")));

    sizeLabel.SetContent($"Size: {w}×{h} cells");
    renderer.RequestRender();
}

// --- Key handling ---
renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    if (e.Name == "backspace")
    {
        if (currentText.Length > 0)
            currentText = currentText[..^1];
        UpdateDisplay();
        return;
    }

    if (e.Name == "f" && !e.Shift)
    {
        fontIndex = (fontIndex + 1) % fonts.Length;
        currentFont = fonts[fontIndex];
        UpdateDisplay();
        return;
    }

    // Accept printable characters that the font supports
    if (e.Name.Length == 1)
    {
        char ch = e.Shift ? char.ToUpper(e.Name[0]) : e.Name[0];
        if (char.IsLetterOrDigit(ch) || ch == ' ' || ch == '!' || ch == '?' || ch == '.' || ch == '-')
        {
            if (currentText.Length < 20)
            {
                currentText += char.ToUpper(ch);
                UpdateDisplay();
            }
        }
    }
});

renderer.On<(int Width, int Height)>(RendererEventNames.Resize, _ =>
{
    renderer.RequestRender();
});

UpdateDisplay();
renderer.RequestRender();
await Task.Delay(Timeout.Infinite);
