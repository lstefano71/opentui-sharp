using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig { ExitOnCtrlC = true });

var root = new BoxRenderable(renderer, new BoxOptions
{
    Id = "root",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Percent(100),
    FlexDirection = FlexDirectionValue.Column,
    BackgroundColor = Rgba.FromHex("#0c0c1d"),
    ShouldFill = true,
});

// Title
var titleBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "title-box",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#1a1a3e"),
    Border = true,
    BorderStyle = BorderStyle.Double,
    BorderColor = Rgba.FromHex("#ffd700"),
    JustifyContent = JustifyValue.Center,
    AlignItems = AlignValue.Center,
});
titleBox.Add(new TextRenderable(renderer, new TextOptions
{
    Id = "title",
    Content = "🌍 Full Unicode Demo 🌏",
    Fg = Rgba.FromHex("#ffd700"),
    Attributes = TextAttributes.Bold,
}));

// Content area
var content = new BoxRenderable(renderer, new BoxOptions
{
    Id = "content",
    FlexGrow = 1,
    Width = DimensionValue.Percent(100),
    FlexDirection = FlexDirectionValue.Column,
    Padding = DimensionValue.Point(1),
    Gap = 1,
});

// Panel helper
BoxRenderable MakePanel(string id, string title, Rgba borderColor, StyledText body)
{
    var panel = new BoxRenderable(renderer, new BoxOptions
    {
        Id = id,
        Width = DimensionValue.Percent(100),
        Border = true,
        BorderStyle = BorderStyle.Rounded,
        BorderColor = borderColor,
        BackgroundColor = Rgba.FromHex("#12122a"),
        ShouldFill = true,
        FlexDirection = FlexDirectionValue.Column,
        Padding = DimensionValue.Point(1),
        Title = $" {title} ",
    });
    var text = new TextRenderable(renderer, new TextOptions
    {
        Id = $"{id}-text",
        StyledContent = body,
    });
    panel.Add(text);
    return panel;
}

// Panel 1: Emoji
content.Add(MakePanel("emoji", "Emoji", Rgba.FromHex("#ff6b6b"), new StyledText(
    TextChunk.Styled("Party: ", fg: Rgba.FromHex("#aaaaaa")),
    TextChunk.Styled("🎉 🎊 🎈 🎁 🎆 🎇 🧨 ✨ 🪅", fg: Rgba.White),
    TextChunk.Styled("  Faces: ", fg: Rgba.FromHex("#aaaaaa")),
    TextChunk.Styled("😀 😎 🤩 😈 🥳 🤯 🫠 🫡", fg: Rgba.White)
)));

// Panel 2: CJK Characters
content.Add(MakePanel("cjk", "CJK Characters (Wide)", Rgba.FromHex("#4ecdc4"), new StyledText(
    TextChunk.Styled("Chinese: ", fg: Rgba.FromHex("#aaaaaa")),
    TextChunk.Styled("你好世界 ", fg: Rgba.FromHex("#ff6b6b")),
    TextChunk.Styled("Japanese: ", fg: Rgba.FromHex("#aaaaaa")),
    TextChunk.Styled("こんにちは ", fg: Rgba.FromHex("#4ecdc4")),
    TextChunk.Styled("Korean: ", fg: Rgba.FromHex("#aaaaaa")),
    TextChunk.Styled("안녕하세요", fg: Rgba.FromHex("#ffe66d"))
)));

// Panel 3: Combining marks
content.Add(MakePanel("combining", "Combining Marks & Diacritics", Rgba.FromHex("#a8e6cf"), new StyledText(
    TextChunk.Styled("Composed: ", fg: Rgba.FromHex("#aaaaaa")),
    TextChunk.Styled("é ñ ü ö ā ǎ ", fg: Rgba.FromHex("#a8e6cf")),
    TextChunk.Styled("Decomposed: ", fg: Rgba.FromHex("#aaaaaa")),
    TextChunk.Styled("é ñ ü ö ", fg: Rgba.FromHex("#ffd3b6")),
    TextChunk.Styled("Stacked: ", fg: Rgba.FromHex("#aaaaaa")),
    TextChunk.Styled("ḁ̴̡̢̛̗̣̙̤̦̩̫̬̮̰̲̈́̃̂̄̅̆̇̈̉̊̋̌̍̎̏", fg: Rgba.FromHex("#ff8b94"))
)));

// Panel 4: ZWJ sequences and family
content.Add(MakePanel("zwj", "ZWJ Sequences & Skin Tones", Rgba.FromHex("#ffd3b6"), new StyledText(
    TextChunk.Styled("Family: ", fg: Rgba.FromHex("#aaaaaa")),
    TextChunk.Styled("👨‍👩‍👧‍👦 👩‍👧‍👦 ", fg: Rgba.White),
    TextChunk.Styled("Professions: ", fg: Rgba.FromHex("#aaaaaa")),
    TextChunk.Styled("👨‍💻 👩‍🔬 👨‍🎨 👩‍🚀 ", fg: Rgba.White),
    TextChunk.Styled("Tones: ", fg: Rgba.FromHex("#aaaaaa")),
    TextChunk.Styled("👋🏻 👋🏼 👋🏽 👋🏾 👋🏿", fg: Rgba.White)
)));

// Panel 5: Box drawing and symbols
content.Add(MakePanel("symbols", "Box Drawing & Mathematical", Rgba.FromHex("#dcedc1"), new StyledText(
    TextChunk.Styled("Box: ", fg: Rgba.FromHex("#aaaaaa")),
    TextChunk.Styled("┌─┬─┐ │ ├─┼─┤ └─┴─┘ ", fg: Rgba.FromHex("#dcedc1")),
    TextChunk.Styled("Math: ", fg: Rgba.FromHex("#aaaaaa")),
    TextChunk.Styled("∑ ∏ ∫ ∂ √ ∞ ≈ ≠ ≤ ≥ ∈ ∉ ⊂ ⊃", fg: Rgba.FromHex("#ffd93d"))
)));

// Panel 6: Flags
content.Add(MakePanel("flags", "Regional Flags", Rgba.FromHex("#ff9a9e"), new StyledText(
    TextChunk.Styled("🇺🇸 🇬🇧 🇫🇷 🇩🇪 🇯🇵 🇰🇷 🇨🇳 🇧🇷 🇮🇳 🇦🇺 🇨🇦 🇪🇸 🇮🇹 🇷🇺 🇲🇽", fg: Rgba.White)
)));

// Footer
var footer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "footer",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#1a1a3e"),
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Rgba.FromHex("#888888"),
    JustifyContent = JustifyValue.Center,
    AlignItems = AlignValue.Center,
});
footer.Add(new TextRenderable(renderer, new TextOptions
{
    Id = "footer-text",
    StyledContent = new StyledText(
        TextChunk.Styled("Displaying: ", fg: Rgba.FromHex("#888888")),
        TextChunk.Styled("Emoji • CJK • Combining Marks • ZWJ • Box Drawing • Flags", fg: Rgba.FromHex("#ffd700")),
        TextChunk.Styled("  |  ", fg: Rgba.FromHex("#444444")),
        TextChunk.Styled("Ctrl+C", fg: Rgba.FromHex("#ff6b6b"), attributes: TextAttributes.Bold),
        TextChunk.Styled(" exit", fg: Rgba.FromHex("#888888"))
    ),
}));

root.Add(titleBox);
root.Add(content);
root.Add(footer);
renderer.Root.Add(root);

await Task.Delay(Timeout.Infinite);
