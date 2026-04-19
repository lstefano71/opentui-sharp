using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    EnableMouseMovement = true,
    BackgroundColor = Rgba.FromInts(0, 17, 34, 255),
});

var exitTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
renderer.On(RendererEventNames.Destroy, () => exitTcs.TrySetResult());

string[] graphemeLines =
[
    "✅ 👩🏽‍💻  👨‍👩‍👧‍👦  🏳️‍🌈  🇺🇸  🇩🇪  🇯🇵  🇮🇳",
    "a̐éö̲  Z͑͗͛̒͘a̴͈͚̐̓l̷͓̱͉g̶̙̗̓͘o̵͍͈  क्‍ष",
    "مرحبا  こんにちは  สวัสดี  Здравствуйте",
    "𝔘𝔫𝔦𝔠𝔬𝔡𝔢  𝒻𝓊𝓁𝓁 𝓌𝒾𝒹𝓉𝒽：ＡＢＣ  ½ ⅞ ⅓",
];

var vignetteEffect = new VignetteEffect(0.55f);
bool vignetteEnabled = false;
bool needsRedraw = true;
var redrawActions = new List<Action>();

var rootGroup = new BoxRenderable(renderer, new BoxOptions
{
    Id = "full-unicode-root",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Percent(100),
    ZIndex = 1,
});
renderer.Root.Add(rootGroup);

// Use concrete terminal dimensions (Point), matching the upstream reference.
// This ensures the private buffer is allocated immediately at construction,
// so the first frame callback can draw into it without waiting for layout.
var background = new FrameBufferRenderable(renderer, new FrameBufferOptions
{
    Id = "grapheme-bg",
    Position = PositionValue.Absolute,
    Left = 0,
    Top = 0,
    Width = renderer.TerminalWidth,
    Height = renderer.TerminalHeight,
    RespectAlpha = false,
});
rootGroup.Add(background);

void MarkDirty()
{
    needsRedraw = true;
    renderer.RequestRender();
}

void DrawBackground()
{
    var buffer = background.Buffer;
    if (buffer is null)
        return;

    var fg = Rgba.FromInts(220, 220, 220, 255);
    var bg = Rgba.FromInts(0, 17, 34, 255);

    buffer.Clear(bg);
    for (uint y = 0; y < buffer.Height; y++)
    {
        string line = graphemeLines[y % (uint)graphemeLines.Length];
        buffer.DrawText(line, 2, y, fg, bg);
    }
}

FrameBufferRenderable CreateDraggableGraphemeBox(string id, int x, int y, int width, int height, Rgba bg, bool respectAlpha)
{
    var box = new FrameBufferRenderable(renderer, new FrameBufferOptions
    {
        Id = id,
        Position = PositionValue.Absolute,
        Left = x,
        Top = y,
        Width = width,
        Height = height,
        RespectAlpha = respectAlpha,
    });

    bool dragging = false;
    int dragOffsetX = 0;
    int dragOffsetY = 0;

    box.OnMouseDown = mouseEvent =>
    {
        dragging = true;
        dragOffsetX = mouseEvent.X - box.X;
        dragOffsetY = mouseEvent.Y - box.Y;
        mouseEvent.StopPropagation();
        MarkDirty();
    };
    box.OnMouseDrag = mouseEvent =>
    {
        if (!dragging)
            return;

        box.X = mouseEvent.X - dragOffsetX;
        box.Y = mouseEvent.Y - dragOffsetY;
        mouseEvent.StopPropagation();
        MarkDirty();
    };
    box.OnMouseDragEnd = mouseEvent =>
    {
        if (!dragging)
            return;

        dragging = false;
        mouseEvent.StopPropagation();
        MarkDirty();
    };

    void Draw()
    {
        var buffer = box.Buffer;
        if (buffer is null)
            return;

        buffer.Clear(bg);
        for (uint row = 0; row < buffer.Height; row++)
        {
            string line = graphemeLines[row % (uint)graphemeLines.Length];
            buffer.DrawText(line, 1, row, Rgba.White, bg);
        }
    }

    redrawActions.Add(Draw);
    return box;
}

TextRenderable CreateDraggableStyledText(string id, int x, int y, StyledText content)
{
    var text = new TextRenderable(renderer, new TextOptions
    {
        Id = id,
        Position = PositionValue.Absolute,
        Left = x,
        Top = y,
        ZIndex = 2,
        Selectable = false,
        StyledContent = content,
        Fg = Rgba.White,
        Bg = Rgba.Transparent,
    });

    bool dragging = false;
    int dragOffsetX = 0;
    int dragOffsetY = 0;

    text.OnMouseDown = mouseEvent =>
    {
        dragging = true;
        dragOffsetX = mouseEvent.X - text.X;
        dragOffsetY = mouseEvent.Y - text.Y;
        mouseEvent.StopPropagation();
        renderer.RequestRender();
    };
    text.OnMouseDrag = mouseEvent =>
    {
        if (!dragging)
            return;

        text.X = mouseEvent.X - dragOffsetX;
        text.Y = mouseEvent.Y - dragOffsetY;
        mouseEvent.StopPropagation();
        renderer.RequestRender();
    };
    text.OnMouseDragEnd = mouseEvent =>
    {
        if (!dragging)
            return;

        dragging = false;
        mouseEvent.StopPropagation();
        renderer.RequestRender();
    };

    return text;
}

redrawActions.Add(DrawBackground);

var box1 = CreateDraggableGraphemeBox("grapheme-box-1", 6, 4, 30, 6, Rgba.FromInts(32, 96, 192, 160), true);
var box2 = CreateDraggableGraphemeBox("grapheme-box-2", 24, 10, 28, 6, Rgba.FromInts(192, 96, 128, 180), true);
var box3 = CreateDraggableGraphemeBox("grapheme-box-3", 42, 7, 26, 6, Rgba.FromInts(64, 176, 96, 128), true);
rootGroup.Add(box1);
rootGroup.Add(box2);
rootGroup.Add(box3);

var styledText = CreateDraggableStyledText(
    "draggable-styled-text",
    8,
    12,
    new StyledText(
        TextChunk.Styled("Graphemes:", fg: Rgba.FromHex("#77aaff"), attributes: TextAttributes.Bold),
        TextChunk.Plain(" ✅ 👩🏽‍💻  👨‍👩‍👧‍👦  🏳️‍🌈  🇺🇸  🇩🇪  🇯🇵  🇮🇳\n"),
        TextChunk.Styled("Complex:", fg: Rgba.FromHex("#ffffff"), attributes: TextAttributes.Underline),
        TextChunk.Plain(" a̐éö̲  Z͑͗͛̒͘a̴͈͚̐̓l̷͓̱͉g̶̙̗̓͘o̵͍͈  क्‍ष")));
rootGroup.Add(styledText);

var styledText2 = CreateDraggableStyledText(
    "draggable-styled-text-2",
    18,
    16,
    new StyledText(
        TextChunk.Styled("Emoji Check:", fg: Rgba.FromHex("#55FF55"), attributes: TextAttributes.Bold),
        TextChunk.Plain(" ✅ 👩🏽‍💻  👨‍👩‍👧‍👦  🏳️‍🌈\n"),
        TextChunk.Styled("Drag me too:", fg: Rgba.White, attributes: TextAttributes.Underline),
        TextChunk.Plain(" 🇺🇸  🇩🇪  🇯🇵  🇮🇳  a̐éö̲")));
rootGroup.Add(styledText2);

var hintText = new TextRenderable(renderer, new TextOptions
{
    Id = "full-unicode-hint",
    Position = PositionValue.Absolute,
    Left = 2,
    Top = 1,
    ZIndex = 3,
    Content = "V: Toggle vignette",
    Fg = Rgba.FromHex("#AAFFAA"),
    Selectable = false,
});
rootGroup.Add(hintText);

renderer.AddFrameCallback(_ =>
{
    if (!needsRedraw)
        return Task.CompletedTask;

    foreach (var redraw in redrawActions)
        redraw();

    needsRedraw = false;
    return Task.CompletedTask;
});

renderer.On<(int Width, int Height)>(RendererEventNames.Resize, e =>
{
    background.WidthDimension = e.Width;
    background.HeightDimension = e.Height;
    MarkDirty();
});

renderer.KeyInput.On("keypress", (KeyEvent keyEvent) =>
{
    string? name = keyEvent.Name?.ToLowerInvariant();
    if (name == "v")
    {
        vignetteEnabled = !vignetteEnabled;
        hintText.Content = $"V: Toggle vignette ({(vignetteEnabled ? "ON" : "OFF")})";
        renderer.ClearPostProcessFns();
        if (vignetteEnabled)
            renderer.AddPostProcessFn(vignetteEffect.Apply);
        MarkDirty();
        keyEvent.StopPropagation();
        return;
    }

    if (name == "escape")
    {
        renderer.Destroy();
        keyEvent.StopPropagation();
    }
});

MarkDirty();

await exitTcs.Task;
