// Link Demo — OSC 8 hyperlinks in text
// Port of link-demo.ts
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
    Content = "Link Demo — OSC 8 Hyperlinks",
    Fg = Rgba.FromInts(255, 255, 255),
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
    Gap = 1,
    BackgroundColor = Rgba.FromHex("#111827"),
});

// Description
var descText = new TextRenderable(renderer, new TextOptions
{
    Id = "desc",
    Content = "Modern terminals support clickable hyperlinks via OSC 8 escape sequences.\nHover over the styled text below — in supported terminals, they are clickable links.",
    Fg = Rgba.FromHex("#9ca3af"),
    WrapMode = WrapMode.Word,
});
content.Add(descText);

// Links section
var linksBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "links-box",
    FlexDirection = FlexDirectionValue.Column,
    Gap = 1,
    Padding = DimensionValue.Point(1),
    Border = true,
    BorderColor = Rgba.FromHex("#374151"),
    BackgroundColor = Rgba.FromHex("#1f2937"),
});

var linksTitle = new TextRenderable(renderer, new TextOptions
{
    Id = "links-title",
    StyledContent = new StyledText(
        TextChunk.Styled("Hyperlinks", fg: Rgba.FromHex("#60a5fa"), attributes: TextAttributes.Bold | TextAttributes.Underline)),
});
linksBox.Add(linksTitle);

// Various links as styled text with link attribute
var link1 = new TextRenderable(renderer, new TextOptions
{
    Id = "link-1",
    StyledContent = new StyledText(
        TextChunk.Plain("• "),
        TextChunk.Styled("OpenTUI GitHub Repository",
            fg: Rgba.FromHex("#3b82f6"),
            attributes: TextAttributes.Underline,
            link: "https://github.com/nicksrandall/opentui")),
});
linksBox.Add(link1);

var link2 = new TextRenderable(renderer, new TextOptions
{
    Id = "link-2",
    StyledContent = new StyledText(
        TextChunk.Plain("• "),
        TextChunk.Styled("Zig Programming Language",
            fg: Rgba.FromHex("#f59e0b"),
            attributes: TextAttributes.Underline,
            link: "https://ziglang.org")),
});
linksBox.Add(link2);

var link3 = new TextRenderable(renderer, new TextOptions
{
    Id = "link-3",
    StyledContent = new StyledText(
        TextChunk.Plain("• "),
        TextChunk.Styled(".NET 10 Documentation",
            fg: Rgba.FromHex("#a78bfa"),
            attributes: TextAttributes.Underline,
            link: "https://learn.microsoft.com/en-us/dotnet/")),
});
linksBox.Add(link3);

var link4 = new TextRenderable(renderer, new TextOptions
{
    Id = "link-4",
    StyledContent = new StyledText(
        TextChunk.Plain("• "),
        TextChunk.Styled("xterm.js — Terminal for the web",
            fg: Rgba.FromHex("#34d399"),
            attributes: TextAttributes.Underline,
            link: "https://xtermjs.org")),
});
linksBox.Add(link4);

content.Add(linksBox);

// Mixed text with inline links
var mixedBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "mixed-box",
    FlexDirection = FlexDirectionValue.Column,
    Padding = DimensionValue.Point(1),
    Border = true,
    BorderColor = Rgba.FromHex("#374151"),
    BackgroundColor = Rgba.FromHex("#1f2937"),
});

var mixedTitle = new TextRenderable(renderer, new TextOptions
{
    Id = "mixed-title",
    StyledContent = new StyledText(
        TextChunk.Styled("Inline Links in Text", fg: Rgba.FromHex("#60a5fa"), attributes: TextAttributes.Bold)),
});
mixedBox.Add(mixedTitle);

var mixedText = new TextRenderable(renderer, new TextOptions
{
    Id = "mixed-text",
    StyledContent = new StyledText(
        TextChunk.Plain("Check out "),
        TextChunk.Styled("OpenTUI", fg: Rgba.FromHex("#3b82f6"), attributes: TextAttributes.Underline,
            link: "https://github.com/nicksrandall/opentui"),
        TextChunk.Plain(" for building "),
        TextChunk.Styled("beautiful TUI apps", fg: Rgba.FromHex("#34d399"), attributes: TextAttributes.Bold),
        TextChunk.Plain(" with the power of "),
        TextChunk.Styled("Zig", fg: Rgba.FromHex("#f59e0b"), attributes: TextAttributes.Underline,
            link: "https://ziglang.org"),
        TextChunk.Plain(".")),
    WrapMode = WrapMode.Word,
});
mixedBox.Add(mixedText);
content.Add(mixedBox);

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
    Content = "Hover links in supported terminals | Ctrl+C: exit",
    Fg = Rgba.FromInts(255, 255, 255),
});
footer.Add(footerText);

renderer.Root.Add(header);
renderer.Root.Add(content);
renderer.Root.Add(footer);

renderer.RequestRender();
await Task.Delay(Timeout.Infinite);
