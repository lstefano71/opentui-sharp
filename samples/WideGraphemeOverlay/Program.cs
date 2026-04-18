using OpenTui.Core;

const int HeaderHeight = 2;

string[] graphemeLines =
[
    "東京都  北京市  서울시  大阪府  名古屋  横浜市  上海市",
    "👨‍👩‍👧‍👦  👩🏽‍💻  🏳️‍🌈  🇺🇸  🇩🇪  🇯🇵  🇮🇳  家族  絵文字  🎉🎊🎈",
    "こんにちは世界  你好世界  안녕하세요  สวัสดี  مرحبا",
    "漢字テスト  中文测试  한국어  日本語  繁體中文  简体中文",
    "🚀 Full-width: ＡＢＣＤＥＦ  Half: abcdef  ½ ⅞ ⅓",
    "混合テキスト mixed text with 漢字 and emoji 🎯",
];

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 30,
});

renderer.Native.SetBackgroundColor(Rgba.FromHex("#0A0E14"));

var root = new BoxRenderable(renderer, new BoxOptions
{
    Id = "wg-overlay-root",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Percent(100),
});
renderer.Root.Add(root);

bool scrimVisible = false;

var headerDisplay = new TextRenderable(renderer, new TextOptions
{
    Id = "wg-header",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(2),
    Top = DimensionValue.Point(0),
    Height = DimensionValue.Point(HeaderHeight),
    ZIndex = 200,
    StyledContent = GetHeaderContent(scrimVisible),
});
root.Add(headerDisplay);

var background = new GraphemeBackground(renderer, "wg-background", graphemeLines)
{
    PositionType = PositionValue.Absolute,
    Left = DimensionValue.Point(0),
    Top = DimensionValue.Point(HeaderHeight),
    WidthDimension = DimensionValue.Point(renderer.Width),
    HeightDimension = DimensionValue.Point(renderer.Height - HeaderHeight),
};
root.Add(background);

var scrim = new BoxRenderable(renderer, new BoxOptions
{
    Id = "wg-scrim",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(0),
    Top = DimensionValue.Point(HeaderHeight),
    Width = DimensionValue.Point(renderer.Width),
    Height = DimensionValue.Point(renderer.Height - HeaderHeight),
    BackgroundColor = Rgba.FromInts(0, 0, 0, 150),
    ZIndex = 50,
});
scrim.Visible = false;
root.Add(scrim);

root.Add(new DraggableBox(renderer, "wg-box-50", 4, HeaderHeight + 1, 25, 8, Rgba.FromValues(64f / 255f, 176f / 255f, 1f, 128f / 255f), 100));
root.Add(new DraggableBox(renderer, "wg-box-75", 20, HeaderHeight + 5, 25, 8, Rgba.FromValues(1f, 107f / 255f, 129f / 255f, 192f / 255f), 100));
root.Add(new DraggableBox(renderer, "wg-box-25", 40, HeaderHeight + 3, 25, 8, Rgba.FromValues(139f / 255f, 69f / 255f, 193f / 255f, 64f / 255f), 100));
root.Add(new DraggableBox(renderer, "wg-box-opaque", 60, HeaderHeight + 7, 25, 8, Rgba.FromValues(30f / 255f, 30f / 255f, 42f / 255f, 1f), 100));

renderer.KeyInput.On("keypress", (KeyEvent keyEvent) =>
{
    if (keyEvent.Name != "d")
        return;

    scrimVisible = !scrimVisible;
    scrim.Visible = scrimVisible;
    headerDisplay.Content = GetHeaderContent(scrimVisible);
    renderer.RequestRender();
});

renderer.On<(int Width, int Height)>(RendererEventNames.Resize, resize =>
{
    int backgroundHeight = resize.Height - HeaderHeight;
    background.WidthDimension = DimensionValue.Point(resize.Width);
    background.HeightDimension = DimensionValue.Point(backgroundHeight);
    scrim.WidthDimension = DimensionValue.Point(resize.Width);
    scrim.HeightDimension = DimensionValue.Point(backgroundHeight);
    renderer.RequestRender();
});

renderer.RequestRender();
await Task.Delay(Timeout.Infinite);

static StyledText GetHeaderContent(bool scrimVisible) => new(
    TextChunk.Styled("Wide Grapheme Overlay", fg: Rgba.FromHex("#00D4AA"), attributes: TextAttributes.Bold),
    TextChunk.Styled(
        $" | {(scrimVisible ? "D: hide scrim" : "D: show scrim")} | Drag boxes over CJK/emoji | Ctrl+C: quit",
        fg: Rgba.FromHex("#A8A8B2")));

internal static class WideGraphemeOverlayState
{
    public static int NextZIndex { get; set; } = 101;
}

internal sealed class DraggableBox : BoxRenderable
{
    private bool _isDragging;
    private int _dragOffsetX;
    private int _dragOffsetY;
    private readonly int _alphaPercentage;

    public DraggableBox(IRenderContext ctx, string id, int x, int y, int width, int height, Rgba backgroundColor, int zIndex)
        : base(ctx, new BoxOptions
        {
            Id = id,
            Width = DimensionValue.Point(width),
            Height = DimensionValue.Point(height),
            ZIndex = zIndex,
            BackgroundColor = backgroundColor,
            Position = PositionValue.Absolute,
            Left = DimensionValue.Point(x),
            Top = DimensionValue.Point(y),
        })
    {
        _alphaPercentage = (int)MathF.Round(backgroundColor.A * 100f);
    }

    protected override void RenderSelf(OptimizedBuffer buffer, float deltaTime)
    {
        base.RenderSelf(buffer, deltaTime);

        string alphaText = $"{_alphaPercentage}%";
        int centerX = X + Math.Max(0, (Width - alphaText.Length) / 2);
        int centerY = Y + Math.Max(0, Height / 2);
        buffer.DrawText(alphaText, (uint)centerX, (uint)centerY, Rgba.FromInts(255, 255, 255, 220));
    }

    protected override void OnMouseEvent(UiMouseEvent evt)
    {
        switch (evt.Type)
        {
            case MouseEventType.Down:
                _isDragging = true;
                _dragOffsetX = evt.X - X;
                _dragOffsetY = evt.Y - Y;
                ZIndex = WideGraphemeOverlayState.NextZIndex++;
                evt.StopPropagation();
                break;

            case MouseEventType.DragEnd:
                if (_isDragging)
                {
                    _isDragging = false;
                    evt.StopPropagation();
                }
                break;

            case MouseEventType.Drag:
                if (_isDragging)
                {
                    int newX = evt.X - _dragOffsetX;
                    int newY = evt.Y - _dragOffsetY;

                    X = Math.Max(0, Math.Min(newX, _ctx.Width - Width));
                    Y = Math.Max(0, Math.Min(newY, _ctx.Height - Height));

                    evt.StopPropagation();
                }
                break;
        }
    }
}

internal sealed class GraphemeBackground : FrameBufferRenderable
{
    private readonly string[] _graphemeLines;
    private int _lastFilledWidth = -1;
    private int _lastFilledHeight = -1;

    public GraphemeBackground(IRenderContext ctx, string id, string[] graphemeLines)
        : base(ctx, new FrameBufferOptions
        {
            Id = id,
            BackgroundColor = Rgba.FromInts(10, 14, 20),
        })
    {
        _graphemeLines = graphemeLines;
    }

    protected override void RenderSelf(OptimizedBuffer buffer, float deltaTime)
    {
        EnsureBackground();
        base.RenderSelf(buffer, deltaTime);
    }

    private void EnsureBackground()
    {
        if (Width <= 0 || Height <= 0 || Buffer is null)
            return;

        if (_lastFilledWidth == Width && _lastFilledHeight == Height)
            return;

        Buffer.RespectAlpha = false;
        Buffer.Clear(Rgba.FromInts(10, 14, 20));

        var fgColor = Rgba.FromInts(220, 220, 220);
        var bgColor = Rgba.FromInts(10, 14, 20);
        for (int y = 0; y < Height; y++)
        {
            string line = _graphemeLines[y % _graphemeLines.Length];
            Buffer.DrawText(line, 2, (uint)y, fgColor, bgColor);
        }

        _lastFilledWidth = Width;
        _lastFilledHeight = Height;
    }
}
