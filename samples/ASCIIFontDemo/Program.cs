using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 30,
    BackgroundColor = Rgba.FromHex("#000028"),
});

int scrollY = 0;
const int ContentHeight = 56;
bool needsRedraw = true;

var overlay = new BoxRenderable(renderer, new BoxOptions
{
    Id = "fonts-container",
    ZIndex = 15,
    Visible = true,
});
renderer.Root.Add(overlay);

var buffer = new FrameBufferRenderable(renderer, new FrameBufferOptions
{
    Id = "ascii-demo",
    Position = PositionValue.Absolute,
    Left = 0,
    Top = 0,
    Width = DimensionValue.Percent(100),
    Height = ContentHeight,
    ZIndex = 10,
    BackgroundColor = Rgba.FromHex("#000028"),
});
renderer.Root.Add(buffer);

var scrollInstructions = new TextRenderable(renderer, new TextOptions
{
    Id = "scroll-instructions",
    Position = PositionValue.Absolute,
    Left = Math.Max(0, renderer.Width - 32),
    Top = 1,
    Fg = Rgba.FromInts(255, 255, 0, 255),
    Content = "USE J/K OR ARROW KEYS TO SCROLL",
    ZIndex = 25,
});
overlay.Add(scrollInstructions);

void RedrawBuffer()
{
    var surface = buffer.Buffer;
    if (surface is null)
        return;

    var bg = Rgba.FromInts(0, 0, 40, 255);
    surface.Clear(bg);

    AsciiFont.RenderToBuffer(surface, new AsciiFontRenderOptions
    {
        Text = "FONTS",
        X = 5,
        Y = 1,
        Font = "block",
        Colors = [Rgba.FromInts(255, 100, 100, 255), Rgba.FromInts(100, 100, 255, 255)],
        BackgroundColor = bg,
    });

    AsciiFont.RenderToBuffer(surface, new AsciiFontRenderOptions
    {
        Text = "TINY FONT DEMO",
        X = 5,
        Y = 8,
        Font = "tiny",
        Color = Rgba.White,
        BackgroundColor = bg,
    });

    AsciiFont.RenderToBuffer(surface, new AsciiFontRenderOptions
    {
        Text = "HELLO WORLD",
        X = 5,
        Y = 11,
        Font = "tiny",
        Color = Rgba.FromInts(255, 255, 0, 255),
        BackgroundColor = bg,
    });

    AsciiFont.RenderToBuffer(surface, new AsciiFontRenderOptions
    {
        Text = "1234567890",
        X = 5,
        Y = 14,
        Font = "tiny",
        Color = Rgba.FromInts(0, 255, 0, 255),
        BackgroundColor = bg,
    });

    AsciiFont.RenderToBuffer(surface, new AsciiFontRenderOptions
    {
        Text = "!@#$%&*()+-=",
        X = 5,
        Y = 17,
        Font = "tiny",
        Color = Rgba.FromInts(255, 0, 255, 255),
        BackgroundColor = bg,
    });

    AsciiFont.RenderToBuffer(surface, new AsciiFontRenderOptions
    {
        Text = "BLOCK FONT DEMO",
        X = 5,
        Y = 20,
        Font = "tiny",
        Color = Rgba.White,
        BackgroundColor = bg,
    });

    AsciiFont.RenderToBuffer(surface, new AsciiFontRenderOptions
    {
        Text = "HI",
        X = 5,
        Y = 23,
        Font = "block",
        Colors = [Rgba.FromInts(255, 255, 0, 255), Rgba.FromInts(0, 255, 255, 255)],
        BackgroundColor = bg,
    });

    AsciiFont.RenderToBuffer(surface, new AsciiFontRenderOptions
    {
        Text = "2025",
        X = 25,
        Y = 23,
        Font = "block",
        Colors = [Rgba.FromInts(255, 128, 0, 255), Rgba.FromInts(128, 255, 128, 255)],
        BackgroundColor = bg,
    });

    AsciiFont.RenderToBuffer(surface, new AsciiFontRenderOptions
    {
        Text = "SHADE FONT DEMO",
        X = 5,
        Y = 30,
        Font = "tiny",
        Color = Rgba.White,
        BackgroundColor = bg,
    });

    AsciiFont.RenderToBuffer(surface, new AsciiFontRenderOptions
    {
        Text = "COOL",
        X = 5,
        Y = 33,
        Font = "shade",
        Colors = [Rgba.FromInts(255, 200, 100, 255), Rgba.FromInts(100, 150, 200, 255)],
        BackgroundColor = bg,
    });

    AsciiFont.RenderToBuffer(surface, new AsciiFontRenderOptions
    {
        Text = "SLICK FONT DEMO",
        X = 5,
        Y = 42,
        Font = "tiny",
        Color = Rgba.White,
        BackgroundColor = bg,
    });

    AsciiFont.RenderToBuffer(surface, new AsciiFontRenderOptions
    {
        Text = "STYLE",
        X = 5,
        Y = 45,
        Font = "slick",
        Colors = [Rgba.FromInts(100, 255, 100, 255), Rgba.FromInts(255, 100, 255, 255)],
        BackgroundColor = bg,
    });

    AsciiFont.RenderToBuffer(surface, new AsciiFontRenderOptions
    {
        Text = "ESC TO RETURN",
        X = 5,
        Y = 53,
        Font = "tiny",
        Color = Rgba.FromInts(128, 128, 128, 255),
        BackgroundColor = bg,
    });

    needsRedraw = false;
}

void UpdateScrollPosition()
{
    int maxScroll = Math.Max(0, ContentHeight - renderer.Height);
    scrollY = Math.Clamp(scrollY, 0, maxScroll);
    buffer.Y = -scrollY;
    renderer.RequestRender();
}

renderer.AddFrameCallback(_ =>
{
    if (needsRedraw)
        RedrawBuffer();

    return Task.CompletedTask;
});

renderer.KeyInput.On("keypress", (KeyEvent key) =>
{
    const int scrollAmount = 3;

    switch (key.Name)
    {
        case "up":
        case "k":
            scrollY -= scrollAmount;
            UpdateScrollPosition();
            break;
        case "down":
        case "j":
            scrollY += scrollAmount;
            UpdateScrollPosition();
            break;
    }
});

renderer.On<(int Width, int Height)>(RendererEventNames.Resize, _ =>
{
    scrollInstructions.Left = Math.Max(0, renderer.Width - 32);
    needsRedraw = true;
    UpdateScrollPosition();
});

UpdateScrollPosition();
renderer.RequestRender();
await Task.Delay(Timeout.Infinite);
