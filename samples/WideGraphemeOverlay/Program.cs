// Wide Grapheme Overlay — shows CJK, emoji, and double-width character handling
using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 30,
});

renderer.Native.SetBackgroundColor(Rgba.FromHex("#0f172a"));

// --- Example sets ---
string[][] examples =
[
    [
        "CJK Characters",
        "你好世界",
        "日本語テスト",
        "한국어 테스트",
        "ＡＢＣ全角英字",
    ],
    [
        "Emoji",
        "🎉🎊🎈🎁🎀",
        "🚀🌍🌙⭐🔥",
        "👨‍👩‍👧‍👦 Family",
        "🏳️‍🌈 Flag",
    ],
    [
        "Mixed Width",
        "Hello你好World",
        "ABC🎉DEF🚀GHI",
        "1️⃣2️⃣3️⃣ Numbers",
        "café naïve résumé",
    ],
    [
        "Box Drawing + CJK",
        "┌──────┐",
        "│ 漢字 │",
        "│ カナ │",
        "└──────┘",
    ],
    [
        "Overlay Test",
        "▓▓漢▓▓字▓▓",
        "██🎉██🚀██",
        "░░你░░好░░",
        "──全──角──",
    ],
];

int currentExample = 0;

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
        TextChunk.Styled("Wide Grapheme Overlay", fg: Rgba.White, attributes: TextAttributes.Bold)),
    Fg = Rgba.White,
});
header.Add(headerText);

// --- Content ---
var content = new BoxRenderable(renderer, new BoxOptions
{
    Id = "content",
    Width = DimensionValue.Auto,
    FlexGrow = 1,
    FlexDirection = FlexDirectionValue.Column,
    Padding = DimensionValue.Point(1),
    BackgroundColor = Rgba.FromHex("#1e293b"),
});

var titleText = new TextRenderable(renderer, new TextOptions
{
    Id = "title",
    Content = "",
    Fg = Rgba.FromHex("#38bdf8"),
    Attributes = TextAttributes.Bold,
});
content.Add(titleText);

// Create 4 text lines for the example content
var lines = new TextRenderable[4];
for (int i = 0; i < 4; i++)
{
    lines[i] = new TextRenderable(renderer, new TextOptions
    {
        Id = $"line-{i}",
        Content = "",
        Fg = Rgba.FromHex("#e2e8f0"),
    });
    content.Add(lines[i]);
}

var indexText = new TextRenderable(renderer, new TextOptions
{
    Id = "index",
    Content = "",
    Fg = Rgba.FromHex("#64748b"),
});
content.Add(indexText);

// --- Footer ---
var footer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "footer",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#1e40af"),
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
});
var footerText = new TextRenderable(renderer, new TextOptions
{
    Id = "footer-text",
    Content = "N: next example | P: previous | Ctrl+C: exit",
    Fg = Rgba.FromHex("#93c5fd"),
});
footer.Add(footerText);

// --- Build tree ---
renderer.Root.Add(header);
renderer.Root.Add(content);
renderer.Root.Add(footer);

void ShowExample(int idx)
{
    var ex = examples[idx];
    titleText.SetContent(ex[0]);
    for (int i = 0; i < 4; i++)
        lines[i].SetContent(ex[i + 1]);
    indexText.SetContent($"\nExample {idx + 1}/{examples.Length}");
}

// --- Key handling ---
renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    switch (e.Name)
    {
        case "n":
            currentExample = (currentExample + 1) % examples.Length;
            ShowExample(currentExample);
            renderer.RequestRender();
            break;
        case "p":
            currentExample = (currentExample - 1 + examples.Length) % examples.Length;
            ShowExample(currentExample);
            renderer.RequestRender();
            break;
    }
});

renderer.On<(int Width, int Height)>(RendererEventNames.Resize, _ =>
{
    renderer.RequestRender();
});

ShowExample(0);
renderer.RequestRender();
await Task.Delay(Timeout.Infinite);
