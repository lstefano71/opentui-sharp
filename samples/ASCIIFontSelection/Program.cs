using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    EnableMouseMovement = true,
    BackgroundColor = Rgba.FromHex("#0D1117"),
});

var mainContainer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "mainContainer",
    Position = PositionValue.Absolute,
    Left = 1,
    Top = 1,
    Width = 95,
    Height = 30,
    BackgroundColor = Rgba.FromHex("#161B22"),
    ZIndex = 1,
    Border = true,
    BorderColor = Rgba.FromHex("#50565D"),
    Title = "ASCII Font Selection Demo",
    TitleAlignment = TitleAlignment.Center,
});
renderer.Root.Add(mainContainer);

var fontGroup = new BoxRenderable(renderer, new BoxOptions
{
    Id = "fontGroup",
    Position = PositionValue.Absolute,
    Left = 2,
    Top = 2,
    ZIndex = 10,
    FlexDirection = FlexDirectionValue.Column,
    Gap = 1,
});
mainContainer.Add(fontGroup);

var allFontRenderables = new List<ASCIIFontRenderable>();

ASCIIFontRenderable AddAsciiFont(string id, string text, string font, Rgba[] colors)
{
    var renderable = new ASCIIFontRenderable(renderer, new ASCIIFontOptions
    {
        Id = id,
        Text = text,
        Font = font,
        Colors = colors,
        BackgroundColor = Rgba.FromInts(0, 0, 40, 255),
        SelectionBg = Rgba.FromHex("#4A5568"),
        SelectionFg = Rgba.FromHex("#FFFFFF"),
        ZIndex = 20,
    });

    fontGroup.Add(renderable);
    allFontRenderables.Add(renderable);
    return renderable;
}

AddAsciiFont(
    "tinyFont",
    "TINY FONT DEMO",
    "tiny",
    [Rgba.FromInts(255, 255, 0, 255)]);

AddAsciiFont(
    "blockFont",
    "opentui",
    "block",
    [Rgba.FromInts(255, 100, 100, 255), Rgba.FromInts(100, 255, 100, 255)]);

AddAsciiFont(
    "shadeFont",
    "SHADE",
    "shade",
    [Rgba.FromInts(255, 200, 100, 255), Rgba.FromInts(100, 150, 200, 255)]);

AddAsciiFont(
    "slickFont",
    "SLICK",
    "slick",
    [Rgba.FromInts(100, 255, 100, 255), Rgba.FromInts(255, 100, 255, 255)]);

var instructions = new TextRenderable(renderer, new TextOptions
{
    Id = "ascii-font-instructions",
    Content = "Click and drag to select text across any ASCII font elements. Press 'C' to clear selection.",
    Position = PositionValue.Absolute,
    Left = 2,
    Top = 26,
    ZIndex = 2,
    Fg = Rgba.FromHex("#F0F6FC"),
});
mainContainer.Add(instructions);

var statusBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "statusBox",
    Position = PositionValue.Absolute,
    Left = 1,
    Top = 32,
    Width = 95,
    Height = 10,
    BackgroundColor = Rgba.FromHex("#0D1117"),
    Border = true,
    BorderColor = Rgba.FromHex("#50565D"),
    Title = "Selection Status",
    TitleAlignment = TitleAlignment.Left,
});
renderer.Root.Add(statusBox);

var statusText = new TextRenderable(renderer, new TextOptions
{
    Id = "statusText",
    Content = "No selection - try selecting across different ASCII fonts",
    Fg = Rgba.FromHex("#F0F6FC"),
});
statusBox.Add(statusText);

var selectionStartText = new TextRenderable(renderer, new TextOptions
{
    Id = "selectionStartText",
    Position = PositionValue.Absolute,
    Left = 3,
    Top = 2,
    ZIndex = 2,
    Fg = Rgba.FromHex("#7DD3FC"),
});
statusBox.Add(selectionStartText);

var selectionMiddleText = new TextRenderable(renderer, new TextOptions
{
    Id = "selectionMiddleText",
    Position = PositionValue.Absolute,
    Left = 3,
    Top = 4,
    ZIndex = 2,
    Fg = Rgba.FromHex("#94A3B8"),
});
statusBox.Add(selectionMiddleText);

var selectionEndText = new TextRenderable(renderer, new TextOptions
{
    Id = "selectionEndText",
    Position = PositionValue.Absolute,
    Left = 3,
    Top = 6,
    ZIndex = 2,
    Fg = Rgba.FromHex("#7DD3FC"),
});
statusBox.Add(selectionEndText);

var debugText = new TextRenderable(renderer, new TextOptions
{
    Id = "debugText",
    Position = PositionValue.Absolute,
    Left = 3,
    Top = 8,
    ZIndex = 2,
    Fg = Rgba.FromHex("#E6EDF3"),
});
statusBox.Add(debugText);

renderer.On<Selection>(RendererEventNames.Selection, selection =>
{
    string selectedText = selection.GetSelectedText();
    int selectedCount = allFontRenderables.Count(renderable => renderable.HasSelection());
    var container = renderer.GetSelectionContainer();
    string containerInfo = container is not null ? $"Container: {container.Id}" : "Container: none";
    debugText.ContentText = $"Selected fonts: {selectedCount}/{allFontRenderables.Count} | {containerInfo}";

    if (string.IsNullOrEmpty(selectedText))
    {
        statusText.ContentText = "Empty selection";
        selectionStartText.ContentText = "";
        selectionMiddleText.ContentText = "";
        selectionEndText.ContentText = "";
        return;
    }

    string[] lines = selectedText.Split('\n');
    int totalLength = selectedText.Length;

    if (lines.Length > 1)
    {
        statusText.ContentText = $"Selected {lines.Length} lines ({totalLength} chars):";
        selectionStartText.ContentText = lines[0];
        selectionMiddleText.ContentText = "...";
        selectionEndText.ContentText = lines[^1];
    }
    else if (selectedText.Length > 60)
    {
        statusText.ContentText = $"Selected {totalLength} chars:";
        selectionStartText.ContentText = selectedText[..30];
        selectionMiddleText.ContentText = "...";
        selectionEndText.ContentText = selectedText[^30..];
    }
    else
    {
        statusText.ContentText = $"Selected {totalLength} chars:";
        selectionStartText.ContentText = $"\"{selectedText}\"";
        selectionMiddleText.ContentText = "";
        selectionEndText.ContentText = "";
    }
});

renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    if (e.Sequence is not ("c" or "C"))
        return;

    renderer.ClearSelection();
    statusText.ContentText = "Selection cleared";
    selectionStartText.ContentText = "";
    selectionMiddleText.ContentText = "";
    selectionEndText.ContentText = "";
    debugText.ContentText = "";
    renderer.RequestRender();
});

renderer.RequestRender();
await Task.Delay(Timeout.Infinite);
