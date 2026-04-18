// Text Selection Demo — mouse text selection
// Port of text-selection-demo.ts
using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
});

// --- Header ---
var header = new BoxRenderable(renderer, new BoxOptions
{
    Id = "header",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#1e40af"),
    Border = true,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
});
var headerText = new TextRenderable(renderer, new TextOptions
{
    Id = "header-text",
    Content = "Text Selection Demo",
    Fg = Rgba.FromInts(255, 255, 255),
});
header.Add(headerText);

// --- Content with selectable text ---
var content = new BoxRenderable(renderer, new BoxOptions
{
    Id = "content",
    Width = DimensionValue.Auto,
    FlexGrow = 1,
    FlexDirection = FlexDirectionValue.Column,
    Padding = DimensionValue.Point(1),
    Gap = 1,
    BackgroundColor = Rgba.FromHex("#111827"),
});

var descText = new TextRenderable(renderer, new TextOptions
{
    Id = "desc",
    Content = "Click and drag to select text in the boxes below. Selection is handled by the native TextBufferView.",
    Fg = Rgba.FromHex("#9ca3af"),
    WrapMode = WrapMode.Word,
});
content.Add(descText);

// Sample texts in selectable boxes
string[] sampleTexts =
[
    "The quick brown fox jumps over the lazy dog. This is a classic pangram that contains every letter of the English alphabet at least once.",
    "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.",
    "🎉 Unicode text: 你好世界 (Hello World in Chinese). Emojis: 🚀🎨📦✨ — Wide characters and grapheme clusters.",
    "fn main() {\n    let greeting = \"Hello, Rust!\";\n    println!(\"{greeting}\");\n    let numbers: Vec<i32> = (1..=10).collect();\n}",
];

string[] labels = ["Pangram", "Lorem Ipsum", "Unicode & Emoji", "Code"];

for (int i = 0; i < sampleTexts.Length; i++)
{
    var textBox = new BoxRenderable(renderer, new BoxOptions
    {
        Id = $"textbox-{i}",
        Width = DimensionValue.Auto,
        FlexDirection = FlexDirectionValue.Column,
        Border = true,
        BorderColor = Rgba.FromHex("#374151"),
        BackgroundColor = Rgba.FromHex("#1f2937"),
        Padding = DimensionValue.Point(1),
    });

    var labelText = new TextRenderable(renderer, new TextOptions
    {
        Id = $"label-{i}",
        StyledContent = new StyledText(
            TextChunk.Styled(labels[i], fg: Rgba.FromHex("#60a5fa"), attributes: TextAttributes.Bold)),
    });
    textBox.Add(labelText);

    var bodyText = new TextRenderable(renderer, new TextOptions
    {
        Id = $"body-{i}",
        Content = sampleTexts[i],
        Fg = Rgba.FromHex("#d1d5db"),
        WrapMode = WrapMode.Word,
        Selectable = true,
        SelectionBg = Rgba.FromHex("#3b82f6"),
        SelectionFg = Rgba.FromInts(255, 255, 255),
    });
    textBox.Add(bodyText);
    content.Add(textBox);
}

// --- Status bar ---
var statusBar = new BoxRenderable(renderer, new BoxOptions
{
    Id = "status-bar",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(1),
    BackgroundColor = Rgba.FromHex("#374151"),
    Padding = DimensionValue.Point(1),
    AlignItems = AlignValue.Center,
});
var statusText = new TextRenderable(renderer, new TextOptions
{
    Id = "status-text",
    Content = "Click and drag on text to select",
    Fg = Rgba.FromHex("#9ca3af"),
});
statusBar.Add(statusText);

// --- Footer ---
var footer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "footer",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#1e40af"),
    Border = true,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
});
var footerText = new TextRenderable(renderer, new TextOptions
{
    Id = "footer-text",
    Content = "Mouse: select text | Ctrl+C: exit",
    Fg = Rgba.FromInts(255, 255, 255),
});
footer.Add(footerText);

renderer.Root.Add(header);
renderer.Root.Add(content);
renderer.Root.Add(statusBar);
renderer.Root.Add(footer);

renderer.RequestRender();
await Task.Delay(Timeout.Infinite);
