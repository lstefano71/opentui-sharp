using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    BackgroundColor = Rgba.FromHex("#1A1B26"),
});

int nextIndex = 1000;

static TextChunk Chunk(string text, string fg, TextAttributes attributes = TextAttributes.None) =>
    TextChunk.Styled(text, fg: Rgba.FromHex(fg), attributes: attributes);

StyledText MakeMultilineContent(int i)
{
    string[] palette = ["#7aa2f7", "#9ece6a", "#f7768e", "#7dcfff", "#bb9af7", "#e0af68"];
    string color = palette[i % palette.Length];
    string id = (i + 1).ToString("0000");
    string tagText = i % 3 == 0 ? "INFO" : i % 3 == 1 ? "WARN" : "ERROR";
    var tagColor = i % 3 == 2 ? "#f7768e" : color;
    var tagAttributes = i % 3 == 0 ? TextAttributes.Underline : TextAttributes.Bold;

    int barUnits = 10 + (i % 30);
    string bar = new string('█', (int)Math.Floor(barUnits * 0.6)).PadRight(barUnits, '░');
    string details = string.Join(" ", Enumerable.Repeat("data", (i % 4) + 2));

    return new StyledText(
        Chunk($"[{id}]", "#565f89"),
        TextChunk.Plain(" "),
        Chunk($"Box {i + 1}", color, TextAttributes.Bold),
        TextChunk.Plain(" "),
        Chunk("|", "#565f89"),
        TextChunk.Plain(" "),
        Chunk(tagText, tagColor, tagAttributes),
        TextChunk.Plain("\n"),
        Chunk("Multiline content with mixed styles for stress testing.", "#9aa5ce"),
        TextChunk.Plain("\n"),
        Chunk("• Title:", color),
        TextChunk.Plain(" "),
        Chunk($"Lorem ipsum {i}", "#c0caf5", TextAttributes.Bold | TextAttributes.Italic),
        TextChunk.Plain("\n"),
        Chunk("• Detail A:", "#9ece6a"),
        TextChunk.Plain(" "),
        Chunk(details, "#c0caf5"),
        TextChunk.Plain("\n"),
        Chunk("• Detail B:", "#bb9af7"),
        TextChunk.Plain(" "),
        Chunk("The quick brown fox jumps over the lazy dog.", "#a9b1d6"),
        TextChunk.Plain("\n"),
        Chunk("• Progress:", "#7dcfff"),
        TextChunk.Plain(" "),
        Chunk(bar, "#73daca"),
        TextChunk.Plain(" "),
        Chunk(barUnits.ToString(), "#565f89"),
        TextChunk.Plain("\n"),
        Chunk("— end of box —", "#565f89"));
}

var mainContainer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "main-container",
    FlexGrow = 1,
    MaxHeight = DimensionValue.Percent(100),
    MaxWidth = DimensionValue.Percent(100),
    FlexDirection = FlexDirectionValue.Column,
    BackgroundColor = Rgba.FromHex("#1A1B26"),
});

var scrollBox = new ScrollBoxRenderable(renderer, new ScrollBoxOptions
{
    Id = "scroll-box",
    FlexGrow = 1,
    ScrollX = true,
    ScrollY = true,
    BackgroundColor = Rgba.FromHex("#24283B"),
    Border = true,
    RootOptions = new BoxOptions
    {
        BackgroundColor = Rgba.FromHex("#24283B"),
        Border = true,
    },
    WrapperOptions = new BoxOptions
    {
        BackgroundColor = Rgba.FromHex("#1F2335"),
    },
    ViewportOptions = new BoxOptions
    {
        BackgroundColor = Rgba.FromHex("#1A1B26"),
    },
    ContentOptions = new BoxOptions
    {
        BackgroundColor = Rgba.FromHex("#16161E"),
    },
    ScrollbarOptions = new ScrollBarOptions
    {
        Orientation = SliderOrientation.Vertical,
        TrackOptions = new SliderOptions
        {
            Orientation = SliderOrientation.Vertical,
            ForegroundColor = Rgba.FromHex("#7AA2F7"),
            BackgroundColor = Rgba.FromHex("#414868"),
        },
    },
});

var instructionsBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "instructions",
    Width = DimensionValue.Percent(100),
    FlexDirection = FlexDirectionValue.Column,
    BackgroundColor = Rgba.FromHex("#2A2B3A"),
    PaddingLeft = DimensionValue.Point(1),
    FlexShrink = 0,
});

var instructionsText1 = new TextRenderable(renderer, new TextOptions
{
    StyledContent = new StyledText(
        Chunk("Controls:", "#7aa2f7", TextAttributes.Bold),
        TextChunk.Plain(" "),
        Chunk("↑/↓/PgUp/PgDn/Home/End", "#c0caf5"),
        TextChunk.Plain(" "),
        Chunk("|", "#565f89"),
        TextChunk.Plain(" "),
        Chunk("A", "#9ece6a", TextAttributes.Bold),
        TextChunk.Plain(" "),
        Chunk("Toggle arrows", "#c0caf5"),
        TextChunk.Plain(" "),
        Chunk("|", "#565f89"),
        TextChunk.Plain(" "),
        Chunk("Tab", "#bb9af7", TextAttributes.Bold),
        TextChunk.Plain(" "),
        Chunk("Focus scrollbox", "#c0caf5"),
        TextChunk.Plain(" "),
        Chunk("|", "#565f89"),
        TextChunk.Plain(" "),
        Chunk("N", "#f7768e", TextAttributes.Bold),
        TextChunk.Plain(" "),
        Chunk("Add child", "#c0caf5")),
});

var instructionsText2 = new TextRenderable(renderer, new TextOptions
{
    StyledContent = new StyledText(
        Chunk("Scrollbars:", "#7aa2f7", TextAttributes.Bold),
        TextChunk.Plain(" "),
        Chunk("V", "#e0af68", TextAttributes.Bold),
        TextChunk.Plain(" "),
        Chunk("Toggle vertical", "#c0caf5"),
        TextChunk.Plain(" "),
        Chunk("|", "#565f89"),
        TextChunk.Plain(" "),
        Chunk("H", "#f7768e", TextAttributes.Bold),
        TextChunk.Plain(" "),
        Chunk("Toggle horizontal", "#c0caf5")),
});

instructionsBox.Add(instructionsText1);
instructionsBox.Add(instructionsText2);

mainContainer.Add(scrollBox);
mainContainer.Add(instructionsBox);

void AddBox(int i)
{
    var box = new BoxRenderable(renderer, new BoxOptions
    {
        Id = $"box-{i + 1}",
        Width = DimensionValue.Auto,
        Padding = DimensionValue.Point(1),
        MarginBottom = DimensionValue.Point(1),
        BackgroundColor = i % 2 == 0 ? Rgba.FromHex("#292E42") : Rgba.FromHex("#2F3449"),
    });

    box.Add(new TextRenderable(renderer, new TextOptions
    {
        StyledContent = MakeMultilineContent(i),
    }));

    scrollBox.Add(box);
}

void AddAsciiRenderable(int i)
{
    string[] fonts = ["tiny", "block", "shade", "slick"];
    string font = fonts[i % fonts.Length];
    Rgba[][] palette =
    [
        [Rgba.FromInts(166, 227, 161, 255), Rgba.FromInts(122, 162, 247, 255)],
        [Rgba.FromInts(247, 118, 142, 255), Rgba.FromInts(245, 194, 231, 255)],
        [Rgba.FromInts(125, 196, 228, 255), Rgba.FromInts(199, 146, 234, 255)],
        [Rgba.FromInts(244, 191, 117, 255), Rgba.FromInts(249, 226, 175, 255)],
    ];

    string longText =
        string.Concat(Enumerable.Repeat($"ASCII FONT RENDERABLE #{i + 1} - {font.ToUpperInvariant()} STYLE - This is an extremely long piece of text that will definitely exceed the width of the scrollbox and trigger horizontal scrolling functionality. ", 15)) +
        string.Concat(Enumerable.Repeat("Additional content includes: Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum. ", 12)) +
        string.Concat(Enumerable.Repeat("The quick brown fox jumps over the lazy dog while the sly red panda silently observes from the treetops, contemplating the mysteries of the universe and wondering about the meaning of life. Meanwhile, technology continues to advance at an unprecedented rate, bringing both amazing opportunities and challenging ethical dilemmas to humanity's doorstep. From artificial intelligence to quantum computing, the future holds limitless possibilities that our ancestors could only dream of in their wildest imaginations.", 8));

    scrollBox.Add(new ASCIIFontRenderable(renderer, new ASCIIFontOptions
    {
        Id = $"ascii-{i + 1}",
        Text = longText,
        Font = font,
        Colors = palette[i % palette.Length],
        BackgroundColor = Rgba.FromInts(26, 27, 38, 255),
        SelectionBg = Rgba.FromHex("#F7768E"),
        SelectionFg = Rgba.FromHex("#C0CAF5"),
        ZIndex = 10,
    }));
}

AddAsciiRenderable(0);
for (int index = 1; index < nextIndex; index++)
{
    if ((index + 1) % 100 == 0)
        AddAsciiRenderable(index);
    else
        AddBox(index);
}

renderer.Root.Add(mainContainer);
scrollBox.Focus();

renderer.KeyInput.On("keypress", (KeyEvent key) =>
{
    switch (key.Name)
    {
        case "a":
            bool showArrows = !(scrollBox.VerticalScrollBar?.ShowArrows ?? false);
            if (scrollBox.VerticalScrollBar is not null)
                scrollBox.VerticalScrollBar.ShowArrows = showArrows;
            if (scrollBox.HorizontalScrollBar is not null)
                scrollBox.HorizontalScrollBar.ShowArrows = showArrows;
            break;
        case "v":
            if (scrollBox.VerticalScrollBar is not null)
                scrollBox.VerticalScrollBar.Visible = !scrollBox.VerticalScrollBar.Visible;
            break;
        case "h":
            if (scrollBox.HorizontalScrollBar is not null)
                scrollBox.HorizontalScrollBar.Visible = !scrollBox.HorizontalScrollBar.Visible;
            break;
        case "n":
            AddBox(nextIndex);
            nextIndex++;
            break;
    }
});

renderer.RequestRender();
await Task.Delay(Timeout.Infinite);
