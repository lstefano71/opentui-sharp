// VNode Composition — functional composition pattern
// Port of vnode-composition-demo.ts
using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
});

// --- Helper: Create a labeled card ---
static BoxRenderable CreateCard(IRenderContext ctx, string id, string title, string body, string borderColor, string bgColor)
{
    var card = new BoxRenderable(ctx, new BoxOptions
    {
        Id = id,
        FlexDirection = FlexDirectionValue.Column,
        Border = true,
        BorderStyle = BorderStyle.Rounded,
        BorderColor = Rgba.FromHex(borderColor),
        BackgroundColor = Rgba.FromHex(bgColor),
        FlexGrow = 1,
        Padding = DimensionValue.Point(1),
        ShouldFill = true,
    });

    var titleText = new TextRenderable(ctx, new TextOptions
    {
        Id = $"{id}-title",
        StyledContent = new StyledText(
            TextChunk.Styled(title, fg: Rgba.FromHex("#ffffff"), attributes: TextAttributes.Bold)),
    });
    card.Add(titleText);

    var bodyText = new TextRenderable(ctx, new TextOptions
    {
        Id = $"{id}-body",
        Content = body,
        Fg = Rgba.FromHex("#d1d5db"),
        WrapMode = WrapMode.Word,
    });
    card.Add(bodyText);

    return card;
}

// --- Helper: Create a stat box ---
static BoxRenderable CreateStatBox(IRenderContext ctx, string id, string label, string value, string color)
{
    var statBox = new BoxRenderable(ctx, new BoxOptions
    {
        Id = id,
        FlexDirection = FlexDirectionValue.Column,
        AlignItems = AlignValue.Center,
        JustifyContent = JustifyValue.Center,
        Width = DimensionValue.Point(16),
        Height = DimensionValue.Point(4),
        Border = true,
        BorderColor = Rgba.FromHex(color),
        BackgroundColor = Rgba.FromHex("#111827"),
        ShouldFill = true,
    });

    var valueText = new TextRenderable(ctx, new TextOptions
    {
        Id = $"{id}-value",
        StyledContent = new StyledText(
            TextChunk.Styled(value, fg: Rgba.FromHex(color), attributes: TextAttributes.Bold)),
    });
    statBox.Add(valueText);

    var labelText = new TextRenderable(ctx, new TextOptions
    {
        Id = $"{id}-label",
        Content = label,
        Fg = Rgba.FromHex("#9ca3af"),
    });
    statBox.Add(labelText);

    return statBox;
}

// --- Header ---
var header = new BoxRenderable(renderer, new BoxOptions
{
    Id = "header",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#7c3aed"),
    Border = true,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
});
var headerText = new TextRenderable(renderer, new TextOptions
{
    Id = "header-text",
    Content = "VNode Composition — Functional Components",
    Fg = Rgba.FromInts(255, 255, 255),
});
header.Add(headerText);

// --- Stats row ---
var statsRow = new BoxRenderable(renderer, new BoxOptions
{
    Id = "stats-row",
    FlexDirection = FlexDirectionValue.Row,
    Gap = 1,
    Padding = DimensionValue.Point(1),
    JustifyContent = JustifyValue.Center,
});
statsRow.Add(CreateStatBox(renderer, "stat-1", "Users", "1,234", "#3b82f6"));
statsRow.Add(CreateStatBox(renderer, "stat-2", "Revenue", "$45.6K", "#10b981"));
statsRow.Add(CreateStatBox(renderer, "stat-3", "Growth", "+12.5%", "#f59e0b"));
statsRow.Add(CreateStatBox(renderer, "stat-4", "Active", "89%", "#ef4444"));

// --- Cards row ---
var cardsRow = new BoxRenderable(renderer, new BoxOptions
{
    Id = "cards-row",
    FlexDirection = FlexDirectionValue.Row,
    Gap = 1,
    Padding = DimensionValue.Point(1),
    FlexGrow = 1,
});
cardsRow.Add(CreateCard(renderer, "card-1", "🚀 Performance",
    "Optimized rendering pipeline with double-buffering and diff-based updates for minimal terminal writes.",
    "#3b82f6", "#1e293b"));
cardsRow.Add(CreateCard(renderer, "card-2", "🎨 Styling",
    "Rich text with bold, italic, underline, colors, backgrounds, and hyperlinks. Full Unicode support.",
    "#10b981", "#1e293b"));
cardsRow.Add(CreateCard(renderer, "card-3", "📦 Composable",
    "Build complex UIs by composing simple functional components. Each component is a pure function.",
    "#f59e0b", "#1e293b"));

// --- Description ---
var descBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "desc-box",
    Padding = DimensionValue.Point(1),
});
var descText = new TextRenderable(renderer, new TextOptions
{
    Id = "desc-text",
    StyledContent = new StyledText(
        TextChunk.Plain("This demo shows "),
        TextChunk.Styled("functional composition", fg: Rgba.FromHex("#a78bfa"), attributes: TextAttributes.Bold),
        TextChunk.Plain(" — building UI from reusable helper functions ("),
        TextChunk.Styled("CreateCard", fg: Rgba.FromHex("#34d399"), attributes: TextAttributes.Italic),
        TextChunk.Plain(", "),
        TextChunk.Styled("CreateStatBox", fg: Rgba.FromHex("#34d399"), attributes: TextAttributes.Italic),
        TextChunk.Plain(") instead of inheritance.")),
    Fg = Rgba.FromHex("#d1d5db"),
    WrapMode = WrapMode.Word,
});
descBox.Add(descText);

// --- Footer ---
var footer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "footer",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#7c3aed"),
    Border = true,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
});
var footerText = new TextRenderable(renderer, new TextOptions
{
    Id = "footer-text",
    Content = "Ctrl+C: exit",
    Fg = Rgba.FromInts(255, 255, 255),
});
footer.Add(footerText);

renderer.Root.Add(header);
renderer.Root.Add(statsRow);
renderer.Root.Add(cardsRow);
renderer.Root.Add(descBox);
renderer.Root.Add(footer);

renderer.RequestRender();
await Task.Delay(Timeout.Infinite);
