using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 60,
});

renderer.Native.SetBackgroundColor(Rgba.FromHex("#0f172a"));

var container = new BoxRenderable(renderer, new BoxOptions
{
    Id = "main-container",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Percent(100),
});
renderer.Root.Add(container);

var header = new TextRenderable(renderer, new TextOptions
{
    Id = "header",
    StyledContent = GetHeaderContent(),
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(2),
    Top = DimensionValue.Point(1),
    Width = DimensionValue.Point(80),
    Height = DimensionValue.Point(4),
    ZIndex = 10,
});
container.Add(header);

renderer.KeyInput.On("keypress", (KeyEvent keyEvent) =>
{
    if (keyEvent.Name != "d")
        return;

    LinkDemoState.DragModeEnabled = !LinkDemoState.DragModeEnabled;
    header.Content = GetHeaderContent();
});

CreateCard(
    "project-card",
    5,
    6,
    40,
    8,
    Rgba.FromHex("#1e293be6"),
    BuildProjectCardContent());

CreateCard(
    "docs-card",
    50,
    8,
    35,
    9,
    Rgba.FromHex("#334155e6"),
    BuildDocsCardContent());

CreateCard(
    "social-card",
    20,
    16,
    30,
    7,
    Rgba.FromHex("#0f766ecc"),
    BuildSocialCardContent());

renderer.RequestRender();
await Task.Delay(Timeout.Infinite);

void CreateCard(string id, int x, int y, int width, int height, Rgba backgroundColor, StyledText content)
{
    var card = new DraggableBox(renderer, id, x, y, width, height, backgroundColor);
    var text = new TextRenderable(renderer, new TextOptions
    {
        Id = $"{id}-text",
        StyledContent = content,
        Width = DimensionValue.Point(width - 2),
        Height = DimensionValue.Point(height - 2),
    });

    card.Add(text);
    container.Add(card);
}

static StyledText GetHeaderContent()
{
    var dragStatusColor = LinkDemoState.DragModeEnabled ? "#34d399" : "#f87171";

    return new StyledText(
        TextChunk.Styled("OpenTUI Interactive Link Demo", fg: Rgba.FromHex("#38bdf8"), attributes: TextAttributes.Bold),
        TextChunk.Plain("\n"),
        TextChunk.Styled("Click the links to open them.", fg: Rgba.FromHex("#94a3b8")),
        TextChunk.Plain(" "),
        TextChunk.Styled("Press", fg: Rgba.FromHex("#64748b")),
        TextChunk.Plain(" "),
        TextChunk.Styled("d", fg: Rgba.FromHex("#fbbf24"), attributes: TextAttributes.Bold),
        TextChunk.Plain(" "),
        TextChunk.Styled("to toggle drag mode:", fg: Rgba.FromHex("#64748b")),
        TextChunk.Plain(" "),
        TextChunk.Styled(LinkDemoState.DragModeEnabled ? "ON" : "OFF", fg: Rgba.FromHex(dragStatusColor)),
        TextChunk.Plain("\n"),
        TextChunk.Styled("(Terminal must support OSC 8 hyperlinks)", fg: Rgba.FromHex("#64748b"), attributes: TextAttributes.Italic));
}

static StyledText BuildProjectCardContent() => new(
    TextChunk.Styled("\u2665 Project Info", fg: Rgba.FromHex("#f472b6"), attributes: TextAttributes.Bold),
    TextChunk.Plain("\n\n"),
    TextChunk.Styled("Source: ", fg: Rgba.FromHex("#e2e8f0")),
    TextChunk.Styled("GitHub Repository", fg: Rgba.FromHex("#38bdf8"), attributes: TextAttributes.Underline, link: "https://github.com/anomalyco/opentui"),
    TextChunk.Plain("\n"),
    TextChunk.Styled("Web:    ", fg: Rgba.FromHex("#e2e8f0")),
    TextChunk.Styled("Official Website", fg: Rgba.FromHex("#34d399"), attributes: TextAttributes.Underline, link: "https://opentui.com"),
    TextChunk.Plain("\n"),
    TextChunk.Styled("License: ", fg: Rgba.FromHex("#e2e8f0")),
    TextChunk.Styled("MIT", fg: Rgba.FromHex("#fbbf24"), attributes: TextAttributes.Underline, link: "https://github.com/anomalyco/opentui/blob/main/LICENSE"));

static StyledText BuildDocsCardContent() => new(
    TextChunk.Styled("\U0001F4DA Documentation", fg: Rgba.FromHex("#a78bfa"), attributes: TextAttributes.Bold),
    TextChunk.Plain("\n\n"),
    TextChunk.Styled("Get started with:", fg: Rgba.FromHex("#cbd5e1")),
    TextChunk.Plain("\n"),
    TextChunk.Plain("\u2022 "),
    TextChunk.Styled("Quick Start", fg: Rgba.White, attributes: TextAttributes.Bold, link: "https://github.com/anomalyco/opentui#readme"),
    TextChunk.Plain("\n"),
    TextChunk.Plain("\u2022 "),
    TextChunk.Styled("Examples", fg: Rgba.White, link: "https://github.com/anomalyco/opentui/tree/main/packages/core/src/examples"),
    TextChunk.Plain("\n"),
    TextChunk.Plain("\u2022 "),
    TextChunk.Styled("Known Issues", fg: Rgba.White, link: "https://github.com/anomalyco/opentui/issues"));

static StyledText BuildSocialCardContent() => new(
    TextChunk.Styled("\U0001F44B Connect", fg: Rgba.FromHex("#2dd4bf"), attributes: TextAttributes.Bold),
    TextChunk.Plain("\n\n"),
    TextChunk.Styled("Twitter / X", fg: Rgba.FromHex("#60a5fa"), link: "https://x.com/anomalyco"),
    TextChunk.Plain("\n"),
    TextChunk.Styled("Discord Community", fg: Rgba.FromHex("#818cf8"), link: "https://discord.gg/Fc8UPAeV"));

internal static class LinkDemoState
{
    public static int NextZIndex { get; set; } = 100;

    public static bool DragModeEnabled { get; set; }
}

internal sealed class DraggableBox : BoxRenderable
{
    private bool _isDragging;
    private int _dragOffsetX;
    private int _dragOffsetY;

    public DraggableBox(IRenderContext ctx, string id, int x, int y, int width, int height, Rgba backgroundColor)
        : base(ctx, new BoxOptions
        {
            Id = id,
            Width = DimensionValue.Point(width),
            Height = DimensionValue.Point(height),
            ZIndex = LinkDemoState.NextZIndex++,
            BackgroundColor = backgroundColor,
            Position = PositionValue.Absolute,
            Left = DimensionValue.Point(x),
            Top = DimensionValue.Point(y),
            Border = true,
            BorderStyle = BorderStyle.Rounded,
            BorderColor = Rgba.White,
            Padding = DimensionValue.Point(1),
            FlexDirection = FlexDirectionValue.Column,
        })
    {
    }

    protected override void OnMouseEvent(UiMouseEvent evt)
    {
        if (!LinkDemoState.DragModeEnabled)
            return;

        switch (evt.Type)
        {
            case MouseEventType.Down:
                _isDragging = true;
                _dragOffsetX = evt.X - X;
                _dragOffsetY = evt.Y - Y;
                ZIndex = LinkDemoState.NextZIndex++;
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
