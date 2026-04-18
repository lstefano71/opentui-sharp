// Sticky Scroll — auto-scrolling list with StickyScroll = true
using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 30,
});

renderer.Native.SetBackgroundColor(Rgba.FromHex("#0f172a"));

int itemCount = 0;
Rgba colorA = Rgba.FromHex("#1f2937");
Rgba colorB = Rgba.FromHex("#111827");

// --- Header ---
var header = new BoxRenderable(renderer, new BoxOptions
{
    Id = "header",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#7c3aed"),
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
});
var headerText = new TextRenderable(renderer, new TextOptions
{
    Id = "header-text",
    StyledContent = new StyledText(
        TextChunk.Styled("Sticky Scroll Demo", fg: Rgba.White, attributes: TextAttributes.Bold)),
    Fg = Rgba.White,
});
header.Add(headerText);

// --- ScrollBox with sticky scroll ---
var scrollBox = new ScrollBoxRenderable(renderer, new ScrollBoxOptions
{
    Id = "scroll",
    ScrollY = true,
    StickyScroll = true,
    Width = DimensionValue.Auto,
    FlexGrow = 1,
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Rgba.FromHex("#6d28d9"),
    BackgroundColor = Rgba.FromHex("#111827"),
});

void AddItem()
{
    int i = itemCount++;
    var row = new BoxRenderable(renderer, new BoxOptions
    {
        Id = $"item-{i}",
        Width = DimensionValue.Auto,
        Height = DimensionValue.Point(1),
        BackgroundColor = i % 2 == 0 ? colorA : colorB,
        FlexDirection = FlexDirectionValue.Row,
        AlignItems = AlignValue.Center,
        PaddingLeft = DimensionValue.Point(1),
    });

    var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
    var label = new TextRenderable(renderer, new TextOptions
    {
        Id = $"item-text-{i}",
        StyledContent = new StyledText(
            TextChunk.Styled($"#{i,4} ", fg: Rgba.FromHex("#6366f1"), attributes: TextAttributes.Bold),
            TextChunk.Styled($"[{timestamp}] ", fg: Rgba.FromHex("#64748b")),
            TextChunk.Styled($"Log entry {i}", fg: Rgba.FromHex("#e2e8f0"))),
    });
    row.Add(label);
    scrollBox.Add(row);
}

// Seed initial 50 items
for (int i = 0; i < 50; i++) AddItem();

// --- Footer ---
var footer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "footer",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#1e293b"),
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    FlexDirection = FlexDirectionValue.Column,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
});

var footerStatus = new TextRenderable(renderer, new TextOptions
{
    Id = "footer-status",
    Content = "",
    Fg = Rgba.FromHex("#94a3b8"),
});

var footerHelp = new TextRenderable(renderer, new TextOptions
{
    Id = "footer-help",
    Content = "A: add item | A+5: hold Shift+A for 5 | J/K: scroll | T/B: top/bottom | Ctrl+C: exit",
    Fg = Rgba.FromHex("#64748b"),
});

footer.Add(footerStatus);
footer.Add(footerHelp);

// --- Build tree ---
renderer.Root.Add(header);
renderer.Root.Add(scrollBox);
renderer.Root.Add(footer);

void UpdateFooter()
{
    footerStatus.Content = new StyledText(
        TextChunk.Styled($"Items: {itemCount}", fg: Rgba.FromHex("#a78bfa")),
        TextChunk.Styled($"  Scroll: {scrollBox.ScrollTop:F0}/{scrollBox.ScrollHeight:F0}", fg: Rgba.FromHex("#64748b")),
        TextChunk.Styled("  Sticky: ON", fg: Rgba.FromHex("#22c55e"), attributes: TextAttributes.Bold));
}

// --- Key handling ---
renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    switch (e.Name)
    {
        case "a" when !e.Shift:
            AddItem();
            UpdateFooter();
            renderer.RequestRender();
            break;
        case "a" when e.Shift:
            for (int n = 0; n < 5; n++) AddItem();
            UpdateFooter();
            renderer.RequestRender();
            break;
        case "j":
            scrollBox.ScrollBy(0, 3);
            UpdateFooter();
            renderer.RequestRender();
            break;
        case "k":
            scrollBox.ScrollBy(0, -3);
            UpdateFooter();
            renderer.RequestRender();
            break;
        case "t":
            scrollBox.ScrollTo(y: 0);
            UpdateFooter();
            renderer.RequestRender();
            break;
        case "b":
            scrollBox.ScrollTo(y: scrollBox.ScrollHeight);
            UpdateFooter();
            renderer.RequestRender();
            break;
    }
});

renderer.On<(int Width, int Height)>(RendererEventNames.Resize, _ =>
{
    UpdateFooter();
    renderer.RequestRender();
});

UpdateFooter();
renderer.RequestRender();
await Task.Delay(Timeout.Infinite);
