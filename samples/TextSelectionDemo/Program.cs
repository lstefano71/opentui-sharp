using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    BackgroundColor = Rgba.FromHex("#0d1117"),
    EnableMouseMovement = true,
    ExitOnCtrlC = true,
    TargetFps = 30,
});

var allTextRenderables = new List<TextRenderable>();

TextRenderable AddText(Renderable parent, string id, int zIndex, string? content = null, StyledText? styledContent = null, Rgba? fg = null, Rgba? selectionBg = null, Rgba? selectionFg = null, int? width = null, int? height = null)
{
    var text = new TextRenderable(renderer, new TextOptions
    {
        Id = id,
        Content = content,
        StyledContent = styledContent,
        Width = width is int textWidth ? DimensionValue.Point(textWidth) : null,
        Height = height is int textHeight ? DimensionValue.Point(textHeight) : null,
        ZIndex = zIndex,
        Fg = fg,
        SelectionBg = selectionBg,
        SelectionFg = selectionFg,
    });
    parent.Add(text);
    allTextRenderables.Add(text);
    return text;
}

var mainContainer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "mainContainer",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(1),
    Top = DimensionValue.Point(1),
    Width = DimensionValue.Point(88),
    Height = DimensionValue.Point(22),
    BackgroundColor = Rgba.FromHex("#161b22"),
    ZIndex = 1,
    BorderColor = Rgba.FromHex("#50565d"),
    Title = "Text Selection Demo",
    TitleAlignment = TitleAlignment.Center,
    Border = true,
});
renderer.Root.Add(mainContainer);

var leftGroup = new BoxRenderable(renderer, new BoxOptions
{
    Id = "leftGroup",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(2),
    Top = DimensionValue.Point(2),
    ZIndex = 10,
});
mainContainer.Add(leftGroup);

var box1 = new BoxRenderable(renderer, new BoxOptions
{
    Id = "box1",
    Width = DimensionValue.Point(45),
    Height = DimensionValue.Point(7),
    BackgroundColor = Rgba.FromHex("#1e2936"),
    ZIndex = 20,
    BorderColor = Rgba.FromHex("#58a6ff"),
    Title = "Document Section 1",
    FlexDirection = FlexDirectionValue.Column,
    Padding = DimensionValue.Point(1),
    Border = true,
});
leftGroup.Add(box1);

AddText(box1, "text1", 21, content: "This is a paragraph in the first box.", fg: Rgba.FromHex("#f0f6fc"));
AddText(box1, "text2", 21, content: "It contains multiple lines of text", fg: Rgba.FromHex("#f0f6fc"));
AddText(box1, "text3", 21, content: "that can be selected independently.", fg: Rgba.FromHex("#f0f6fc"));
AddText(box1, "text4", 21, content: "世界, 你好世界, 中文, 한글", fg: Rgba.FromHex("#f0f6fc"));

var nestedBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "nestedBox",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(2),
    Top = DimensionValue.Point(1),
    Width = DimensionValue.Point(31),
    Height = DimensionValue.Point(4),
    BackgroundColor = Rgba.FromHex("#2d1b69"),
    ZIndex = 25,
    BorderColor = Rgba.FromHex("#a371f7"),
    BorderStyle = BorderStyle.Double,
    Border = true,
});
leftGroup.Add(nestedBox);

AddText(
    nestedBox,
    "nestedText",
    26,
    styledContent: new StyledText(
        TextChunk.Styled("Important:", fg: Rgba.FromHex("#facc15")),
        TextChunk.Plain(" "),
        TextChunk.Styled("Nested content", fg: Rgba.FromHex("#22d3ee"), attributes: TextAttributes.Bold),
        TextChunk.Plain(" "),
        TextChunk.Styled("with styles", fg: Rgba.FromHex("#22c55e"), attributes: TextAttributes.Italic)),
    selectionBg: Rgba.FromHex("#4a5568"),
    selectionFg: Rgba.FromHex("#ffffff"),
    width: 27,
    height: 1);

var rightGroup = new BoxRenderable(renderer, new BoxOptions
{
    Id = "rightGroup",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(48),
    Top = DimensionValue.Point(2),
    ZIndex = 10,
});
mainContainer.Add(rightGroup);

var box2 = new BoxRenderable(renderer, new BoxOptions
{
    Id = "box2",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(2),
    Top = DimensionValue.Point(0),
    Width = DimensionValue.Point(35),
    Height = DimensionValue.Point(12),
    BackgroundColor = Rgba.FromHex("#1c2128"),
    ZIndex = 20,
    BorderColor = Rgba.FromHex("#f85149"),
    Title = "Code Example",
    BorderStyle = BorderStyle.Rounded,
    FlexDirection = FlexDirectionValue.Column,
    Padding = DimensionValue.Point(1),
    Border = true,
});
rightGroup.Add(box2);

AddText(
    box2,
    "codeText1",
    21,
    styledContent: new StyledText(
        TextChunk.Styled("function", fg: Rgba.FromHex("#d946ef")),
        TextChunk.Plain(" "),
        TextChunk.Styled("handleSelection", fg: Rgba.FromHex("#22d3ee")),
        TextChunk.Plain("() {")),
    selectionBg: Rgba.FromHex("#4a5568"));

AddText(
    box2,
    "codeText2",
    21,
    styledContent: new StyledText(
        TextChunk.Plain("  "),
        TextChunk.Styled("const", fg: Rgba.FromHex("#d946ef")),
        TextChunk.Plain(" selected = "),
        TextChunk.Styled("getSelectedText", fg: Rgba.FromHex("#22d3ee")),
        TextChunk.Plain("()")),
    selectionBg: Rgba.FromHex("#4a5568"));

AddText(
    box2,
    "codeText3",
    21,
    styledContent: new StyledText(
        TextChunk.Plain("  "),
        TextChunk.Styled("console", fg: Rgba.FromHex("#facc15")),
        TextChunk.Plain("."),
        TextChunk.Styled("log", fg: Rgba.FromHex("#22c55e")),
        TextChunk.Plain("(selected)")),
    selectionBg: Rgba.FromHex("#4a5568"));

AddText(box2, "codeText4", 21, content: "}", fg: Rgba.FromHex("#e6edf3"));

var floatingBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "floatingBox",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(90),
    Top = DimensionValue.Point(11),
    Width = DimensionValue.Point(31),
    Height = DimensionValue.Point(6),
    BackgroundColor = Rgba.FromHex("#1b2f23"),
    ZIndex = 30,
    BorderColor = Rgba.FromHex("#2ea043"),
    Title = "README",
    BorderStyle = BorderStyle.Single,
    Border = true,
});
renderer.Root.Add(floatingBox);

AddText(
    floatingBox,
    "multilineText",
    31,
    styledContent: new StyledText(
        TextChunk.Styled("Selection Demo", fg: Rgba.FromHex("#22d3ee"), attributes: TextAttributes.Bold),
        TextChunk.Plain("\n"),
        TextChunk.Styled("✓", fg: Rgba.FromHex("#22c55e")),
        TextChunk.Plain(" Cross-renderable selection\n"),
        TextChunk.Styled("✓", fg: Rgba.FromHex("#22c55e")),
        TextChunk.Plain(" Nested groups and boxes\n"),
        TextChunk.Styled("✓", fg: Rgba.FromHex("#22c55e")),
        TextChunk.Plain(" Styled text support")),
    selectionBg: Rgba.FromHex("#4a5568"),
    selectionFg: Rgba.FromHex("#ffffff"));

var instructions = AddText(
    mainContainer,
    "instructions",
    2,
    content: "Click and drag to select text across any elements. Press 'C' to clear selection.",
    fg: Rgba.FromHex("#f0f6fc"));
instructions.Left = DimensionValue.Point(2);
instructions.Top = DimensionValue.Point(17);

var statusBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "statusBox",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(1),
    Top = DimensionValue.Point(24),
    Width = DimensionValue.Point(88),
    Height = DimensionValue.Point(9),
    BackgroundColor = Rgba.FromHex("#0d1117"),
    ZIndex = 1,
    BorderColor = Rgba.FromHex("#50565d"),
    Title = "Selection Status",
    TitleAlignment = TitleAlignment.Left,
    Padding = DimensionValue.Point(1),
    Border = true,
});
renderer.Root.Add(statusBox);

var statusText = new TextRenderable(renderer, new TextOptions
{
    Id = "statusText",
    Content = "No selection - try selecting across different nested elements",
    ZIndex = 2,
    Fg = Rgba.FromHex("#f0f6fc"),
});
statusBox.Add(statusText);

var selectionStartText = new TextRenderable(renderer, new TextOptions
{
    Id = "selectionStartText",
    Content = "",
    ZIndex = 2,
    Fg = Rgba.FromHex("#7dd3fc"),
});
statusBox.Add(selectionStartText);

var selectionMiddleText = new TextRenderable(renderer, new TextOptions
{
    Id = "selectionMiddleText",
    Content = "",
    ZIndex = 2,
    Fg = Rgba.FromHex("#94a3b8"),
});
statusBox.Add(selectionMiddleText);

var selectionEndText = new TextRenderable(renderer, new TextOptions
{
    Id = "selectionEndText",
    Content = "",
    ZIndex = 2,
    Fg = Rgba.FromHex("#7dd3fc"),
});
statusBox.Add(selectionEndText);

var debugText = new TextRenderable(renderer, new TextOptions
{
    Id = "debugText",
    Content = "",
    ZIndex = 2,
    Fg = Rgba.FromHex("#e6edf3"),
});
statusBox.Add(debugText);

renderer.On<Selection>(RendererEventNames.Selection, selection =>
{
    string selectedText = selection.GetSelectedText();
    int selectedCount = allTextRenderables.Count(renderable => renderable.HasSelection());
    var container = renderer.GetSelectionContainer();
    string containerInfo = container is not null ? $"Container: {container.Id}" : "Container: none";

    debugText.ContentText = $"Selected renderables: {selectedCount}/{allTextRenderables.Count} | {containerInfo}";

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
    if (e.Sequence is "c" or "C")
    {
        renderer.ClearSelection();
        statusText.ContentText = "Selection cleared";
        selectionStartText.ContentText = "";
        selectionMiddleText.ContentText = "";
        selectionEndText.ContentText = "";
        debugText.ContentText = "";
        renderer.RequestRender();
    }
});

renderer.RequestRender();
await Task.Delay(Timeout.Infinite);
