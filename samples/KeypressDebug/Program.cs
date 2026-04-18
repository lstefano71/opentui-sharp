using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig { ExitOnCtrlC = true });

const int MaxEntries = 20;
var keyLog = new List<string>();

var root = new BoxRenderable(renderer, new BoxOptions
{
    Id = "root",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Percent(100),
    FlexDirection = FlexDirectionValue.Column,
    BackgroundColor = Rgba.FromHex("#000000"),
    ShouldFill = true,
});

// Title
var titleBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "title-box",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#1a1a1a"),
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Rgba.FromHex("#00ff88"),
    JustifyContent = JustifyValue.Center,
    AlignItems = AlignValue.Center,
});
titleBox.Add(new TextRenderable(renderer, new TextOptions
{
    Id = "title",
    Content = "⌨ Keypress Debug Monitor",
    Fg = Rgba.FromHex("#00ff88"),
    Attributes = TextAttributes.Bold,
}));

// Header row
var headerBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "header-row",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Point(1),
    BackgroundColor = Rgba.FromHex("#222222"),
    ShouldFill = true,
    Padding = DimensionValue.Point(1),
});
headerBox.Add(new TextRenderable(renderer, new TextOptions
{
    Id = "header-text",
    StyledContent = new StyledText(
        TextChunk.Styled("Name".PadRight(12), fg: Rgba.FromHex("#ffaa00"), attributes: TextAttributes.Bold),
        TextChunk.Styled("Ctrl ".PadRight(6), fg: Rgba.FromHex("#ffaa00"), attributes: TextAttributes.Bold),
        TextChunk.Styled("Shift".PadRight(6), fg: Rgba.FromHex("#ffaa00"), attributes: TextAttributes.Bold),
        TextChunk.Styled("Meta ".PadRight(6), fg: Rgba.FromHex("#ffaa00"), attributes: TextAttributes.Bold),
        TextChunk.Styled("Opt  ".PadRight(6), fg: Rgba.FromHex("#ffaa00"), attributes: TextAttributes.Bold),
        TextChunk.Styled("Type".PadRight(10), fg: Rgba.FromHex("#ffaa00"), attributes: TextAttributes.Bold),
        TextChunk.Styled("Sequence", fg: Rgba.FromHex("#ffaa00"), attributes: TextAttributes.Bold)
    ),
}));

// Log area
var logBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "log-box",
    FlexGrow = 1,
    Width = DimensionValue.Percent(100),
    FlexDirection = FlexDirectionValue.Column,
    BackgroundColor = Rgba.FromHex("#0a0a0a"),
    ShouldFill = true,
    Padding = DimensionValue.Point(1),
    Overflow = OverflowValue.Hidden,
});
var logText = new TextRenderable(renderer, new TextOptions
{
    Id = "log-text",
    Content = "Press any key to see it logged here...",
    Fg = Rgba.FromHex("#555555"),
});
logBox.Add(logText);

// Stats bar
var statsBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "stats",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Point(1),
    BackgroundColor = Rgba.FromHex("#1a1a1a"),
    ShouldFill = true,
    Padding = DimensionValue.Point(1),
});
var statsText = new TextRenderable(renderer, new TextOptions
{
    Id = "stats-text",
    Content = "Total: 0 keypresses",
    Fg = Rgba.FromHex("#888888"),
});
statsBox.Add(statsText);

// Footer
var footer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "footer",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#1a1a1a"),
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Rgba.FromHex("#555555"),
    JustifyContent = JustifyValue.Center,
    AlignItems = AlignValue.Center,
});
footer.Add(new TextRenderable(renderer, new TextOptions
{
    Id = "footer-text",
    StyledContent = new StyledText(
        TextChunk.Styled("c", fg: Rgba.FromHex("#00ff88"), attributes: TextAttributes.Bold),
        TextChunk.Styled(" clear log  |  ", fg: Rgba.FromHex("#888888")),
        TextChunk.Styled("Ctrl+C", fg: Rgba.FromHex("#00ff88"), attributes: TextAttributes.Bold),
        TextChunk.Styled(" exit", fg: Rgba.FromHex("#888888"))
    ),
}));

root.Add(titleBox);
root.Add(headerBox);
root.Add(logBox);
root.Add(statsBox);
root.Add(footer);
renderer.Root.Add(root);

int totalKeys = 0;

renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    // Clear on 'c' (without modifiers)
    if (e.Name == "c" && !e.Ctrl && !e.Meta)
    {
        keyLog.Clear();
        totalKeys = 0;
        UpdateLog();
        renderer.RequestRender();
        return;
    }

    totalKeys++;

    // Escape non-printable sequences for display
    string seqDisplay = string.Join("", e.Sequence.Select(c =>
        c < 0x20 || c == 0x7f ? $"^{(char)(c + 0x40)}" : c.ToString()));
    if (string.IsNullOrEmpty(seqDisplay)) seqDisplay = "(empty)";

    string flag(bool v) => v ? "✓" : "·";

    string entry = $"{e.Name.PadRight(12)}{flag(e.Ctrl).PadRight(6)}{flag(e.Shift).PadRight(6)}" +
                   $"{flag(e.Meta).PadRight(6)}{flag(e.Option).PadRight(6)}" +
                   $"{e.EventType.ToString().PadRight(10)}{seqDisplay}";

    keyLog.Add(entry);
    if (keyLog.Count > MaxEntries)
        keyLog.RemoveAt(0);

    UpdateLog();
    renderer.RequestRender();
});

void UpdateLog()
{
    if (keyLog.Count == 0)
    {
        logText.Content = new StyledText(TextChunk.Styled(
            "Press any key to see it logged here...", fg: Rgba.FromHex("#555555")));
        statsText.ContentText = "Total: 0 keypresses";
        return;
    }

    var chunks = new List<TextChunk>();
    for (int i = 0; i < keyLog.Count; i++)
    {
        var color = i % 2 == 0 ? Rgba.FromHex("#cccccc") : Rgba.FromHex("#999999");
        if (i > 0) chunks.Add(TextChunk.Plain("\n"));
        chunks.Add(TextChunk.Styled(keyLog[i], fg: color));
    }

    logText.Content = new StyledText(chunks.ToArray());
    statsText.Content = new StyledText(
        TextChunk.Styled($"Total: ", fg: Rgba.FromHex("#888888")),
        TextChunk.Styled($"{totalKeys}", fg: Rgba.FromHex("#00ff88"), attributes: TextAttributes.Bold),
        TextChunk.Styled($" keypresses  |  Showing last {keyLog.Count}", fg: Rgba.FromHex("#888888"))
    );
}

await Task.Delay(Timeout.Infinite);
