using OpenTui.Core;

using static OpenTui.Core.Style;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    BackgroundColor = Rgba.FromHex("#0d1117"),
    ExitOnCtrlC = true,
    TargetFps = 30,
});

bool truncateEnabled = false;
WrapMode wrapMode = WrapMode.None;
int columnLayout = 0;

var allTextElements = new List<TextRenderable>();

var mainContainer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "mainContainer",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Auto,
    FlexGrow = 1,
    FlexDirection = FlexDirectionValue.Column,
    BackgroundColor = Rgba.FromHex("#0d1117"),
});
renderer.Root.Add(mainContainer);

var header = new BoxRenderable(renderer, new BoxOptions
{
    Id = "header",
    Width = DimensionValue.Auto,
    Height = 3,
    BackgroundColor = Rgba.FromHex("#161b22"),
    BorderStyle = BorderStyle.Single,
    BorderColor = Rgba.FromHex("#30363d"),
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    Border = true,
});
mainContainer.Add(header);

var headerText = new TextRenderable(renderer, new TextOptions
{
    Id = "headerText",
    Content = "Text Truncation Demo - Press 'T' to toggle truncation",
    Fg = Rgba.FromHex("#58a6ff"),
});
header.Add(headerText);

var contentArea = new BoxRenderable(renderer, new BoxOptions
{
    Id = "contentArea",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Auto,
    FlexGrow = 1,
    FlexDirection = FlexDirectionValue.Row,
    Gap = 1,
    Padding = 1,
});
mainContainer.Add(contentArea);

var leftColumn = new BoxRenderable(renderer, new BoxOptions
{
    Id = "leftColumn",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Auto,
    FlexGrow = 1,
    FlexDirection = FlexDirectionValue.Column,
    Gap = 1,
});
contentArea.Add(leftColumn);

var rightColumn = new BoxRenderable(renderer, new BoxOptions
{
    Id = "rightColumn",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Auto,
    FlexGrow = 1,
    FlexDirection = FlexDirectionValue.Column,
    Gap = 1,
});
contentArea.Add(rightColumn);

TextRenderable AddTextBox(
    BoxRenderable parent,
    string id,
    string title,
    Rgba borderColor,
    string? content = null,
    StyledText? styledContent = null,
    Rgba? foreground = null,
    int? minHeight = null,
    bool flexGrow = false)
{
    var box = new BoxRenderable(renderer, new BoxOptions
    {
        Id = $"{id}Box",
        Width = DimensionValue.Auto,
        Height = DimensionValue.Auto,
        MinHeight = minHeight,
        FlexGrow = flexGrow ? 1 : null,
        BackgroundColor = Rgba.FromHex("#161b22"),
        BorderStyle = BorderStyle.Rounded,
        BorderColor = borderColor,
        Title = title,
        Padding = 1,
        Border = true,
    });

    var text = new TextRenderable(renderer, new TextOptions
    {
        Id = id,
        Content = content,
        StyledContent = styledContent,
        Fg = foreground,
        WrapMode = wrapMode,
        Truncate = truncateEnabled,
    });

    box.Add(text);
    parent.Add(box);
    allTextElements.Add(text);
    return text;
}

AddTextBox(
    leftColumn,
    "singleLineText1",
    "Single Line Text 1",
    Rgba.FromHex("#58a6ff"),
    content: "This is a very long single line of text that will definitely exceed the width of most terminal windows and should be truncated when truncation is enabled",
    foreground: Rgba.FromHex("#c9d1d9"),
    minHeight: 5);

AddTextBox(
    leftColumn,
    "singleLineText2",
    "Single Line Text 2",
    Rgba.FromHex("#3fb950"),
    content: "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefghijklmnopqrstuvwxyz",
    foreground: Rgba.FromHex("#3fb950"),
    minHeight: 5);

AddTextBox(
    leftColumn,
    "singleLineText3",
    "Single Line Text 3 (Unicode)",
    Rgba.FromHex("#d29922"),
    content: "🌟 Unicode test: こんにちは世界 Hello World 你好世界 안녕하세요 🚀 More emoji: 🎨🎭🎪🎬🎮🎯",
    foreground: Rgba.FromHex("#d29922"),
    minHeight: 7);

AddTextBox(
    rightColumn,
    "multilineText1",
    "Multiline Text (Word Wrap)",
    Rgba.FromHex("#f778ba"),
    content: "This is a multiline text block that demonstrates how truncation works with word wrapping enabled. Each line that exceeds the viewport width will be truncated independently. Try resizing the terminal to see how it behaves!",
    foreground: Rgba.FromHex("#f778ba"),
    flexGrow: true);

AddTextBox(
    rightColumn,
    "multilineText2",
    "Multiline Text",
    Rgba.FromHex("#bc8cff"),
    content: "Line 1: This is a long line without wrapping\nLine 2: Another very long line that will be truncated when enabled\nLine 3: Short line\nLine 4: Yet another extremely long line with lots of text to demonstrate middle truncation behavior",
    foreground: Rgba.FromHex("#bc8cff"),
    flexGrow: true);

AddTextBox(
    rightColumn,
    "styledText",
    "Styled Text with Truncation",
    Rgba.FromHex("#ff7b72"),
    styledContent: new StyledText(
        Bold("Bold Cyan: ").WithFg(Rgba.FromHex("#7dd3fc")),
        Yellow("Yellow text "),
        Magenta("and magenta "),
        Green("with green parts "),
        TextChunk.Plain("and more styled text that goes on and on")),
    foreground: Rgba.FromHex("#c9d1d9"),
    flexGrow: true);

var footer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "footer",
    Width = DimensionValue.Auto,
    Height = 3,
    BackgroundColor = Rgba.FromHex("#161b22"),
    BorderStyle = BorderStyle.Single,
    BorderColor = Rgba.FromHex("#30363d"),
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    Border = true,
});
mainContainer.Add(footer);

var footerText = new TextRenderable(renderer, new TextOptions
{
    Id = "footerText",
    Content = "",
    Fg = Rgba.FromHex("#8b949e"),
});
footer.Add(footerText);

var selectionBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "selectionBox",
    Width = DimensionValue.Auto,
    Height = 7,
    BackgroundColor = Rgba.FromHex("#0d1117"),
    BorderStyle = BorderStyle.Single,
    BorderColor = Rgba.FromHex("#30363d"),
    Title = "Selection",
    TitleAlignment = TitleAlignment.Left,
    FlexDirection = FlexDirectionValue.Column,
    Gap = 1,
    Padding = 1,
    Border = true,
});
mainContainer.Add(selectionBox);

var selectionStatusText = new TextRenderable(renderer, new TextOptions
{
    Id = "selectionStatusText",
    Content = "Select text to see details here",
    Fg = Rgba.FromHex("#8b949e"),
});
selectionBox.Add(selectionStatusText);

var selectionStartText = new TextRenderable(renderer, new TextOptions
{
    Id = "selectionStartText",
    Content = "",
    Fg = Rgba.FromHex("#7dd3fc"),
});
selectionBox.Add(selectionStartText);

var selectionMiddleText = new TextRenderable(renderer, new TextOptions
{
    Id = "selectionMiddleText",
    Content = "",
    Fg = Rgba.FromHex("#94a3b8"),
});
selectionBox.Add(selectionMiddleText);

var selectionEndText = new TextRenderable(renderer, new TextOptions
{
    Id = "selectionEndText",
    Content = "",
    Fg = Rgba.FromHex("#7dd3fc"),
});
selectionBox.Add(selectionEndText);

StyledText BuildFooterText()
{
    var truncateStatus = truncateEnabled ? "ENABLED" : "DISABLED";
    Func<string, TextChunk> truncateColor = truncateEnabled ? Green : Yellow;
    var wrapLabel = wrapMode switch
    {
        WrapMode.Char => "CHAR",
        WrapMode.Word => "WORD",
        _ => "NONE",
    };
    Func<string, TextChunk> wrapColor = wrapMode == WrapMode.None ? Yellow : Cyan;

    return new StyledText(
        TextChunk.Plain("Truncate: "),
        truncateColor(truncateStatus).WithAttributes(TextAttributes.Bold),
        TextChunk.Plain(" | Wrap: "),
        wrapColor(wrapLabel).WithAttributes(TextAttributes.Bold),
        TextChunk.Plain(" | "),
        Cyan("T").WithAttributes(TextAttributes.Bold),
        TextChunk.Plain(": toggle truncate | "),
        Cyan("W").WithAttributes(TextAttributes.Bold),
        TextChunk.Plain(": cycle wrap | "),
        Cyan("R").WithAttributes(TextAttributes.Bold),
        TextChunk.Plain(": resize | "),
        Cyan("C").WithAttributes(TextAttributes.Bold),
        TextChunk.Plain(": clear selection | "),
        Cyan("Ctrl+C").WithAttributes(TextAttributes.Bold),
        TextChunk.Plain(": exit"));
}

void UpdateFooterText() => footerText.Content = BuildFooterText();

void ApplyTextSettings()
{
    foreach (var text in allTextElements)
    {
        text.Truncate = truncateEnabled;
        text.WrapMode = wrapMode;
    }
}

void ToggleColumnSizes()
{
    columnLayout = (columnLayout + 1) % 3;
    switch (columnLayout)
    {
        case 1:
            leftColumn.FlexGrow = 2;
            rightColumn.FlexGrow = 1;
            break;
        case 2:
            leftColumn.FlexGrow = 1;
            rightColumn.FlexGrow = 2;
            break;
        default:
            leftColumn.FlexGrow = 1;
            rightColumn.FlexGrow = 1;
            break;
    }
}

void ClearSelectionDisplay(string message)
{
    selectionStatusText.ContentText = message;
    selectionStartText.ContentText = "";
    selectionMiddleText.ContentText = "";
    selectionEndText.ContentText = "";
}

renderer.On<Selection>(RendererEventNames.Selection, selection =>
{
    string selectedText = selection.GetSelectedText();
    if (string.IsNullOrEmpty(selectedText))
    {
        ClearSelectionDisplay("Empty selection");
        return;
    }

    string[] lines = selectedText.Split('\n');
    int totalLength = selectedText.Length;

    if (lines.Length > 1)
    {
        selectionStatusText.ContentText = $"Selected {lines.Length} lines ({totalLength} chars):";
        selectionStartText.ContentText = lines[0];
        selectionMiddleText.ContentText = "...";
        selectionEndText.ContentText = lines[^1];
    }
    else if (selectedText.Length > 60)
    {
        selectionStatusText.ContentText = $"Selected {totalLength} chars:";
        selectionStartText.ContentText = selectedText[..30];
        selectionMiddleText.ContentText = "...";
        selectionEndText.ContentText = selectedText[^30..];
    }
    else
    {
        selectionStatusText.ContentText = $"Selected {totalLength} chars:";
        selectionStartText.ContentText = $"\"{selectedText}\"";
        selectionMiddleText.ContentText = "";
        selectionEndText.ContentText = "";
    }
});

renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    switch (e.Sequence?.ToLowerInvariant())
    {
        case "t":
            truncateEnabled = !truncateEnabled;
            ApplyTextSettings();
            UpdateFooterText();
            renderer.RequestRender();
            break;
        case "w":
            wrapMode = wrapMode switch
            {
                WrapMode.None => WrapMode.Char,
                WrapMode.Char => WrapMode.Word,
                _ => WrapMode.None,
            };
            ApplyTextSettings();
            UpdateFooterText();
            renderer.RequestRender();
            break;
        case "r":
            ToggleColumnSizes();
            renderer.RequestRender();
            break;
        case "c":
            renderer.ClearSelection();
            ClearSelectionDisplay("Selection cleared");
            renderer.RequestRender();
            break;
    }
});

ApplyTextSettings();
UpdateFooterText();
renderer.RequestRender();

await Task.Delay(Timeout.Infinite);
