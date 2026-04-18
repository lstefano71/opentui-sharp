using System.Text;
using OpenTui.Core;

const string MarkdownContent = """
# OpenTUI Markdown Demo

Welcome to the **MarkdownRenderable** showcase! This demonstrates automatic table alignment and syntax highlighting.

## Features

- Automatic **table column alignment** based on content width
- Proper handling of `inline code`, **bold**, and *italic* in tables
- Multiple syntax themes to choose from
- Conceal mode hides formatting markers

## Comparison Table

| Feature | Status | Priority | Notes |
|---|---|---|---|
| Table alignment | **Done** | High | Uses `Markdig` parser |
| Conceal mode | *Working* | Medium | Hides `**`, ```, etc. |
| Theme switching | **Done** | Low | Multiple themes available |
| Unicode support | 日本語 | High | CJK characters |

## Code Examples

Here's how to use it:

```typescript
import { MarkdownRenderable } from "@opentui/core"

const md = new MarkdownRenderable(renderer, {
  content: "# Hello World",
  syntaxStyle: mySyntaxStyle,
  fg: "#24292F",
  bg: "#FFFFFF",
  conceal: true, // Hide formatting markers
})
```

And a JSON configuration example:

```json
{
  "name": "opentui-markdown-demo",
  "theme": "github",
  "features": ["table-alignment", "syntax-highlighting", "conceal-mode"],
  "streaming": {
    "enabled": true,
    "speed": "slowest"
  }
}
```

Here's a TSX component example:

```tsx
import React from "react"
import { useState } from "react"

interface Props {
  title: string
  count: number
}

export const Counter: React.FC<Props> = ({ title, count: initialCount }) => {
  const [count, setCount] = useState(initialCount)

  return (
    <div className="counter">
      <h1>{title}</h1>
      <p>Count: {count}</p>
      <button onClick={() => setCount(c => c + 1)}>
        Increment
      </button>
    </div>
  )
}
```

## Light Theme Fallback Checks

Press `T` until **GitHub Light**. These fences intentionally skip syntax
highlighting and should still inherit the theme text color.

Unlabeled fenced block:

```
this fence has no language tag
it should stay readable in GitHub Light
```

Unsupported parser fallback:

```toml
title = "GitHub Light"
status = "fallback text should stay readable"
```

### API Reference

| Method | Parameters | Returns | Description |
|---|---|---|---|
| `constructor` | `ctx, options` | `MarkdownRenderable` | Create new instance |
| `clearCache` | none | `void` | Force re-render content |

## Inline Formatting Examples

| Style | Syntax | Rendered |
|---|---|---|
| Bold | `**text**` | **bold text** |
| Italic | `*text*` | *italic text* |
| Code | `code` | `inline code` |
| Link | `[text](url)` | [OpenTUI](https://github.com) |

## Mixed Content

> **Note**: This blockquote contains **bold** and `code` formatting.
> It should render correctly with proper styling.

### Emoji Support

| Emoji | Name | Category |
|---|---|---|
| 🚀 | Rocket | Transport |
| 🎨 | Palette | Art |
| ⚡ | Lightning | Nature |
| 🔥 | Fire | Nature |

---

## Alignment Examples

| Left | Center | Right |
|:---|:---:|---:|
| L1 | C1 | R1 |
| Left aligned | Centered text | Right aligned |
| Short | Medium length | Longer content here |

## Performance

The table alignment uses:
1. AST-based parsing with `Markdig`
2. Caching for repeated content
3. Smart width calculation accounting for concealed chars

---

*Press `?` for keybindings*
""";

var themes = CreateThemes();
using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 60,
});

var streamSpeeds = new[]
{
    new StreamSpeed("Slowest", 200, 500),
    new StreamSpeed("Slower", 150, 350),
    new StreamSpeed("Slow", 100, 250),
    new StreamSpeed("Medium", 70, 150),
    new StreamSpeed("Fast", 40, 100),
    new StreamSpeed("Faster", 20, 60),
    new StreamSpeed("Fastest", 10, 50),
};

int currentThemeIndex = 0;
int currentSpeedIndex = 0;
int streamPosition = 0;
int streamingGeneration = 0;
bool concealEnabled = true;
bool showingHelp = false;
bool streamingMode = false;
bool endlessMode = false;

var currentTheme = themes[currentThemeIndex];

var parentContainer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "parent-container",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Auto,
    FlexGrow = 1,
    Padding = DimensionValue.Point(1),
    BackgroundColor = currentTheme.Background,
});
renderer.Root.Add(parentContainer);

var titleBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "title-box",
    Height = DimensionValue.Point(3),
    Border = true,
    BorderStyle = BorderStyle.Double,
    BorderColor = Rgba.FromHex("#4ECDC4"),
    BackgroundColor = currentTheme.Background,
    Title = "Markdown Demo - Table Alignment + Syntax Highlighting",
    TitleAlignment = TitleAlignment.Center,
});
parentContainer.Add(titleBox);

var instructionsText = new TextRenderable(renderer, new TextOptions
{
    Id = "instructions",
    Content = "Ctrl+C to exit | Press ? for keybindings",
    Fg = currentTheme.MutedText,
});
titleBox.Add(instructionsText);

var helpModal = new BoxRenderable(renderer, new BoxOptions
{
    Id = "help-modal",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Percent(50),
    Top = DimensionValue.Percent(50),
    Width = DimensionValue.Point(60),
    Height = DimensionValue.Point(20),
    MarginLeft = DimensionValue.Point(-30),
    MarginTop = DimensionValue.Point(-10),
    Padding = DimensionValue.Point(2),
    Border = true,
    BorderStyle = BorderStyle.Double,
    BorderColor = Rgba.FromHex("#4ECDC4"),
    BackgroundColor = currentTheme.Background,
    Title = "Keybindings",
    TitleAlignment = TitleAlignment.Center,
    ZIndex = 100,
    Visible = false,
});

var helpContent = new TextRenderable(renderer, new TextOptions
{
    Id = "help-content",
    Content = """
Theme:
  T : Cycle through themes

View Controls:
  C : Toggle concealment (hide **, `, etc.)

Streaming:
  S : Start/restart streaming simulation
  E : Toggle endless mode (repeats content forever)
  X : Stop streaming
  [ : Decrease speed (slower)
  ] : Increase speed (faster)

Navigation:
  ↑/↓ : Scroll
  PgUp/PgDn : Jump scroll

Other:
  ? : Toggle this help screen
  Ctrl+C : Exit
""",
    Fg = currentTheme.DefaultText,
});
helpModal.Add(helpContent);
renderer.Root.Add(helpModal);

var markdownScrollBox = new ScrollBoxRenderable(renderer, new ScrollBoxOptions
{
    Id = "markdown-scroll-box",
    Border = true,
    BorderStyle = BorderStyle.Single,
    BorderColor = Rgba.FromHex("#6BCF7F"),
    BackgroundColor = currentTheme.Background,
    Title = $"MarkdownRenderable - {currentTheme.Name}",
    TitleAlignment = TitleAlignment.Left,
    ScrollY = true,
    ScrollX = false,
    FlexGrow = 1,
    FlexShrink = 1,
    Padding = DimensionValue.Point(2),
});
parentContainer.Add(markdownScrollBox);
markdownScrollBox.Focus();

var markdownDisplay = new MarkdownRenderable(renderer, new MarkdownOptions
{
    Id = "markdown-display",
    Content = MarkdownContent,
    SyntaxStyle = currentTheme.Style,
    Fg = currentTheme.DefaultText,
    Bg = currentTheme.Background,
    Conceal = concealEnabled,
    Width = DimensionValue.Percent(100),
});
markdownScrollBox.Add(markdownDisplay);

var statusText = new TextRenderable(renderer, new TextOptions
{
    Id = "status-display",
    Content = "",
    Fg = currentTheme.DefaultText,
    WrapMode = WrapMode.Word,
    FlexShrink = 0,
});
parentContainer.Add(statusText);

void ApplyTheme(DemoTheme theme)
{
    parentContainer.BackgroundColor = theme.Background;
    titleBox.BackgroundColor = theme.Background;
    instructionsText.Fg = theme.MutedText;
    markdownScrollBox.Title = $"MarkdownRenderable - {theme.Name}";
    markdownScrollBox.BackgroundColor = theme.Background;
    helpModal.BackgroundColor = theme.Background;
    helpContent.ContentText = helpContent.ContentText;
    helpContent.Fg = theme.DefaultText;
    statusText.Fg = theme.DefaultText;
    markdownDisplay.SyntaxStyle = theme.Style;
    markdownDisplay.Fg = theme.DefaultText;
    markdownDisplay.Bg = theme.Background;
}

void UpdateStatusText()
{
    var theme = themes[currentThemeIndex];
    var speed = streamSpeeds[currentSpeedIndex];
    string streamStatus = streamingMode ? "STREAMING" : "NORMAL";
    string endlessStatus = endlessMode ? " [ENDLESS]" : string.Empty;
    statusText.ContentText =
        $"Theme: {theme.Name} | Conceal: {(concealEnabled ? "ON" : "OFF")} | Mode: {streamStatus}{endlessStatus} | Speed: {speed.Name} | Press T/C/S/E/[/]";
}

void SetStreamingInProgressStatus()
{
    var theme = themes[currentThemeIndex];
    var speed = streamSpeeds[currentSpeedIndex];
    string mode = endlessMode ? "ENDLESS" : "NORMAL";
    statusText.ContentText =
        $"Theme: {theme.Name} | Conceal: {(concealEnabled ? "ON" : "OFF")} | Streaming: IN PROGRESS ({speed.Name}, {mode}) | Press X to stop";
}

void SetStreamingCompleteStatus()
{
    var theme = themes[currentThemeIndex];
    var speed = streamSpeeds[currentSpeedIndex];
    statusText.ContentText =
        $"Theme: {theme.Name} | Conceal: {(concealEnabled ? "ON" : "OFF")} | Streaming: COMPLETE ({speed.Name}) | Press S to restart";
}

void StopStreaming()
{
    streamingMode = false;
    streamPosition = 0;
    streamingGeneration++;
}

static string BuildStreamingContent(string content, int streamPosition, int chunkSize)
{
    int positionInCurrentIteration = streamPosition % content.Length;
    int nextPositionInIteration = Math.Min(positionInCurrentIteration + chunkSize, content.Length);
    int fullIterations = streamPosition / content.Length;

    if (fullIterations == 0)
        return content[..nextPositionInIteration];

    var builder = new StringBuilder(content.Length * (fullIterations + 1));
    for (int i = 0; i < fullIterations; i++)
        builder.Append(content);
    builder.Append(content, 0, nextPositionInIteration);
    return builder.ToString();
}

async Task StreamAsync(int generation)
{
    while (streamingMode && generation == streamingGeneration)
    {
        int chunkSize = Random.Shared.Next(1, 51);
        markdownDisplay.Content = BuildStreamingContent(MarkdownContent, streamPosition, chunkSize);
        streamPosition += chunkSize;

        bool shouldContinue = endlessMode || streamPosition < MarkdownContent.Length;
        if (!shouldContinue)
        {
            streamingMode = false;
            markdownDisplay.Streaming = false;
            SetStreamingCompleteStatus();
            return;
        }

        var speed = streamSpeeds[currentSpeedIndex];
        int delay = Random.Shared.Next(speed.MinDelayMs, speed.MaxDelayMs + 1);
        await Task.Delay(delay);
    }
}

void StartStreaming()
{
    StopStreaming();
    streamingMode = true;
    markdownDisplay.Streaming = true;
    markdownDisplay.Content = string.Empty;
    markdownScrollBox.StickyScroll = true;
    markdownScrollBox.StickyStart = "bottom";

    int generation = streamingGeneration;
    SetStreamingInProgressStatus();
    _ = StreamAsync(generation);
}

ApplyTheme(currentTheme);
UpdateStatusText();
renderer.RequestRender();

renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    if (e.Key.Raw == "?")
    {
        showingHelp = !showingHelp;
        helpModal.Visible = showingHelp;
        return;
    }

    if (showingHelp)
        return;

    if (e.Name == "s" && !e.Ctrl && !e.Meta)
    {
        StartStreaming();
        return;
    }

    if (e.Name == "e" && !e.Ctrl && !e.Meta)
    {
        endlessMode = !endlessMode;
        UpdateStatusText();
        return;
    }

    if (e.Name == "x" && !e.Ctrl && !e.Meta)
    {
        StopStreaming();
        markdownDisplay.Streaming = false;
        UpdateStatusText();
        return;
    }

    if (e.Key.Raw == "[" && !e.Ctrl && !e.Meta)
    {
        if (currentSpeedIndex > 0)
            currentSpeedIndex--;

        UpdateStatusText();
        return;
    }

    if (e.Key.Raw == "]" && !e.Ctrl && !e.Meta)
    {
        if (currentSpeedIndex < streamSpeeds.Length - 1)
            currentSpeedIndex++;

        UpdateStatusText();
        return;
    }

    if (e.Name == "t" && !e.Ctrl && !e.Meta)
    {
        currentThemeIndex = (currentThemeIndex + 1) % themes.Length;
        ApplyTheme(themes[currentThemeIndex]);
        UpdateStatusText();
        return;
    }

    if (e.Name == "c" && !e.Ctrl && !e.Meta)
    {
        StopStreaming();
        concealEnabled = !concealEnabled;
        markdownDisplay.Conceal = concealEnabled;
        markdownDisplay.Streaming = false;
        markdownDisplay.Content = MarkdownContent;
        UpdateStatusText();
        return;
    }

    switch (e.Name)
    {
        case "up":
            markdownScrollBox.ScrollBy(0, -1);
            break;
        case "down":
            markdownScrollBox.ScrollBy(0, 1);
            break;
        case "pageup":
            markdownScrollBox.ScrollBy(0, -10);
            break;
        case "pagedown":
            markdownScrollBox.ScrollBy(0, 10);
            break;
        case "home":
            markdownScrollBox.ScrollTo(y: 0);
            break;
        case "end":
            markdownScrollBox.ScrollTo(y: (int)markdownScrollBox.ScrollHeight);
            break;
    }
});

try
{
    await Task.Delay(Timeout.Infinite);
}
finally
{
    foreach (var theme in themes)
        theme.Style.Dispose();
}

static DemoTheme[] CreateThemes() =>
[
    CreateTheme("GitHub Light", "#FFFFFF", new Dictionary<string, SyntaxStyleEntry>(StringComparer.Ordinal)
    {
        ["keyword"] = Style("#CF222E", bold: true),
        ["string"] = Style("#0A3069"),
        ["comment"] = Style("#6E7781", italic: true),
        ["number"] = Style("#0550AE"),
        ["function"] = Style("#8250DF"),
        ["type"] = Style("#953800"),
        ["operator"] = Style("#CF222E"),
        ["variable"] = Style("#24292F"),
        ["property"] = Style("#0550AE"),
        ["punctuation.bracket"] = Style("#24292F"),
        ["punctuation.delimiter"] = Style("#57606A"),
        ["markup.heading"] = Style("#0550AE", bold: true),
        ["markup.heading.1"] = Style("#1A7F37", bold: true, underline: true),
        ["markup.heading.2"] = Style("#0550AE", bold: true),
        ["markup.heading.3"] = Style("#8250DF"),
        ["markup.bold"] = Style("#24292F", bold: true),
        ["markup.strong"] = Style("#24292F", bold: true),
        ["markup.italic"] = Style("#24292F", italic: true),
        ["markup.list"] = Style("#CF222E"),
        ["markup.quote"] = Style("#6E7781", italic: true),
        ["markup.raw"] = Style("#24292F", "#F6F8FA"),
        ["markup.raw.block"] = Style("#24292F", "#F6F8FA"),
        ["markup.raw.inline"] = Style("#24292F", "#F6F8FA"),
        ["markup.link"] = Style("#0969DA", underline: true),
        ["markup.link.label"] = Style("#0A3069", underline: true),
        ["markup.link.url"] = Style("#0969DA", underline: true),
        ["label"] = Style("#1A7F37"),
        ["conceal"] = Style("#6E7781"),
        ["punctuation.special"] = Style("#57606A"),
        ["default"] = Style("#24292F"),
    }),
    CreateTheme("GitHub Dark", "#0D1117", new Dictionary<string, SyntaxStyleEntry>(StringComparer.Ordinal)
    {
        ["keyword"] = Style("#FF7B72", bold: true),
        ["string"] = Style("#A5D6FF"),
        ["comment"] = Style("#8B949E", italic: true),
        ["number"] = Style("#79C0FF"),
        ["function"] = Style("#D2A8FF"),
        ["type"] = Style("#FFA657"),
        ["operator"] = Style("#FF7B72"),
        ["variable"] = Style("#E6EDF3"),
        ["property"] = Style("#79C0FF"),
        ["punctuation.bracket"] = Style("#F0F6FC"),
        ["punctuation.delimiter"] = Style("#C9D1D9"),
        ["markup.heading"] = Style("#58A6FF", bold: true),
        ["markup.heading.1"] = Style("#00FF88", bold: true, underline: true),
        ["markup.heading.2"] = Style("#00D7FF", bold: true),
        ["markup.heading.3"] = Style("#FF69B4"),
        ["markup.bold"] = Style("#F0F6FC", bold: true),
        ["markup.strong"] = Style("#F0F6FC", bold: true),
        ["markup.italic"] = Style("#F0F6FC", italic: true),
        ["markup.list"] = Style("#FF7B72"),
        ["markup.quote"] = Style("#8B949E", italic: true),
        ["markup.raw"] = Style("#A5D6FF", "#161B22"),
        ["markup.raw.block"] = Style("#A5D6FF", "#161B22"),
        ["markup.raw.inline"] = Style("#A5D6FF", "#161B22"),
        ["markup.link"] = Style("#58A6FF", underline: true),
        ["markup.link.label"] = Style("#A5D6FF", underline: true),
        ["markup.link.url"] = Style("#58A6FF", underline: true),
        ["label"] = Style("#7EE787"),
        ["conceal"] = Style("#6E7681"),
        ["punctuation.special"] = Style("#8B949E"),
        ["default"] = Style("#E6EDF3"),
    }),
    CreateTheme("Monokai", "#272822", new Dictionary<string, SyntaxStyleEntry>(StringComparer.Ordinal)
    {
        ["keyword"] = Style("#F92672", bold: true),
        ["string"] = Style("#E6DB74"),
        ["comment"] = Style("#75715E", italic: true),
        ["number"] = Style("#AE81FF"),
        ["function"] = Style("#A6E22E"),
        ["type"] = Style("#66D9EF", italic: true),
        ["operator"] = Style("#F92672"),
        ["variable"] = Style("#F8F8F2"),
        ["property"] = Style("#A6E22E"),
        ["punctuation.bracket"] = Style("#F8F8F2"),
        ["punctuation.delimiter"] = Style("#F8F8F2"),
        ["markup.heading"] = Style("#A6E22E", bold: true),
        ["markup.heading.1"] = Style("#F92672", bold: true, underline: true),
        ["markup.heading.2"] = Style("#66D9EF", bold: true),
        ["markup.heading.3"] = Style("#E6DB74"),
        ["markup.bold"] = Style("#F8F8F2", bold: true),
        ["markup.strong"] = Style("#F8F8F2", bold: true),
        ["markup.italic"] = Style("#F8F8F2", italic: true),
        ["markup.list"] = Style("#F92672"),
        ["markup.quote"] = Style("#75715E", italic: true),
        ["markup.raw"] = Style("#E6DB74", "#3E3D32"),
        ["markup.raw.block"] = Style("#E6DB74", "#3E3D32"),
        ["markup.raw.inline"] = Style("#E6DB74", "#3E3D32"),
        ["markup.link"] = Style("#66D9EF", underline: true),
        ["markup.link.label"] = Style("#E6DB74", underline: true),
        ["markup.link.url"] = Style("#66D9EF", underline: true),
        ["label"] = Style("#A6E22E"),
        ["conceal"] = Style("#75715E"),
        ["punctuation.special"] = Style("#75715E"),
        ["default"] = Style("#F8F8F2"),
    }),
    CreateTheme("Nord", "#2E3440", new Dictionary<string, SyntaxStyleEntry>(StringComparer.Ordinal)
    {
        ["keyword"] = Style("#81A1C1", bold: true),
        ["string"] = Style("#A3BE8C"),
        ["comment"] = Style("#616E88", italic: true),
        ["number"] = Style("#B48EAD"),
        ["function"] = Style("#88C0D0"),
        ["type"] = Style("#8FBCBB"),
        ["operator"] = Style("#81A1C1"),
        ["variable"] = Style("#D8DEE9"),
        ["property"] = Style("#88C0D0"),
        ["punctuation.bracket"] = Style("#ECEFF4"),
        ["punctuation.delimiter"] = Style("#D8DEE9"),
        ["markup.heading"] = Style("#88C0D0", bold: true),
        ["markup.heading.1"] = Style("#8FBCBB", bold: true, underline: true),
        ["markup.heading.2"] = Style("#81A1C1", bold: true),
        ["markup.heading.3"] = Style("#B48EAD"),
        ["markup.bold"] = Style("#ECEFF4", bold: true),
        ["markup.strong"] = Style("#ECEFF4", bold: true),
        ["markup.italic"] = Style("#ECEFF4", italic: true),
        ["markup.list"] = Style("#81A1C1"),
        ["markup.quote"] = Style("#616E88", italic: true),
        ["markup.raw"] = Style("#A3BE8C", "#3B4252"),
        ["markup.raw.block"] = Style("#A3BE8C", "#3B4252"),
        ["markup.raw.inline"] = Style("#A3BE8C", "#3B4252"),
        ["markup.link"] = Style("#88C0D0", underline: true),
        ["markup.link.label"] = Style("#A3BE8C", underline: true),
        ["markup.link.url"] = Style("#88C0D0", underline: true),
        ["label"] = Style("#A3BE8C"),
        ["conceal"] = Style("#4C566A"),
        ["punctuation.special"] = Style("#616E88"),
        ["default"] = Style("#D8DEE9"),
    }),
];

static DemoTheme CreateTheme(string name, string backgroundHex, Dictionary<string, SyntaxStyleEntry> styles)
{
    var syntaxStyle = SyntaxStyle.Create();
    foreach (var style in styles)
        syntaxStyle.Register(style.Key, style.Value.Fg, style.Value.Bg, style.Value.Attributes);

    var background = Rgba.FromHex(backgroundHex);
    var defaultText = styles["default"].Fg ?? Rgba.FromHex("#FFFFFF");
    var mutedText = styles.TryGetValue("conceal", out var concealStyle)
        ? concealStyle.Fg ?? defaultText
        : defaultText;

    return new DemoTheme(name, background, syntaxStyle, defaultText, mutedText);
}

static SyntaxStyleEntry Style(
    string? fg = null,
    string? bg = null,
    bool bold = false,
    bool italic = false,
    bool underline = false,
    bool dim = false) =>
    new(
        fg is null ? null : Rgba.FromHex(fg),
        bg is null ? null : Rgba.FromHex(bg),
        TextAttributeUtils.Create(bold: bold, italic: italic, underline: underline, dim: dim));

sealed record DemoTheme(string Name, Rgba Background, SyntaxStyle Style, Rgba DefaultText, Rgba MutedText);
sealed record StreamSpeed(string Name, int MinDelayMs, int MaxDelayMs);
