// Markdown Demo — demonstrates MarkdownRenderable with scrolling, conceal toggle, and document cycling
using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig { ExitOnCtrlC = true, TargetFps = 30 });

int docIndex = 0;
bool conceal = true;

// --- Markdown documents ---
string[] docs =
[
    // Document 0: Feature overview
    """
    # OpenTUI-sharp Feature Overview

    ## Introduction

    OpenTUI-sharp is a **terminal UI framework** for building rich, interactive
    applications in *C#*. It brings modern UI patterns to the console.

    ## Key Features

    ### Layout System
    - **Flexbox-based** layout using Yoga
    - Nested containers with `FlexDirection`, `AlignItems`, `JustifyContent`
    - Absolute and relative positioning

    ### Widgets

    1. **TextRenderable** — styled text with colors and attributes
    2. **BoxRenderable** — container with borders and backgrounds
    3. **ScrollBoxRenderable** — scrollable viewport
    4. **CodeRenderable** — syntax-highlighted code viewer
    5. **DiffRenderable** — unified and split diff viewer
    6. **TextTableRenderable** — formatted data tables
    7. **MarkdownRenderable** — rich markdown rendering

    ### Code Example

    ```csharp
    using OpenTui.Core;

    using var renderer = CliRenderer.Create(new CliRendererConfig
    {
        ExitOnCtrlC = true,
    });

    var box = new BoxRenderable(renderer, new BoxOptions
    {
        Id = "main",
        Border = true,
        BorderStyle = BorderStyle.Rounded,
    });

    renderer.Root.Add(box);
    renderer.RequestRender();
    ```

    ### Performance

    | Metric       | Value     | Notes                |
    |--------------|-----------|----------------------|
    | Target FPS   | 30-60     | Configurable         |
    | Render time  | < 2ms     | Typical frame        |
    | Memory       | ~15 MB    | Base footprint       |
    | Startup      | < 100ms   | Cold start           |

    ---

    > **Note**: OpenTUI-sharp uses native rendering via interop for maximum performance.

    For more information, visit [GitHub](https://github.com/example/opentui-sharp).
    """,

    // Document 1: API Guide
    """
    # API Quick Reference

    ## Creating a Renderer

    The `CliRenderer` is your entry point. Always wrap in a `using` statement:

    ```csharp
    using var renderer = CliRenderer.Create(new CliRendererConfig
    {
        ExitOnCtrlC = true,
        TargetFps = 30,
    });
    ```

    ## Working with Widgets

    ### Text

    Simple styled text:

    ```csharp
    var text = new TextRenderable(renderer, new TextOptions
    {
        Id = "greeting",
        Content = "Hello, World!",
        Fg = Rgba.FromHex("#00ff88"),
    });
    ```

    ### Styled Text Chunks

    For rich inline styling, use `TextChunk`:

    - `TextChunk.Plain("text")` — unstyled text
    - `TextChunk.Styled("text", fg, bg, attrs)` — fully styled
    - Chainable: `.WithFg()`, `.WithBg()`, `.WithAttributes()`

    ### Boxes and Layouts

    Boxes are the **primary container**. They support:

    1. Borders (`BorderStyle.Single`, `Double`, `Rounded`, `Heavy`)
    2. Background colors with *alpha blending*
    3. Flexbox properties for layout control

    ```python
    # Even non-C# code blocks render nicely
    def fibonacci(n: int) -> list[int]:
        fib = [0, 1]
        for i in range(2, n):
            fib.append(fib[i-1] + fib[i-2])
        return fib[:n]
    ```

    ## Event Handling

    Subscribe to keyboard events:

    ```csharp
    renderer.KeyInput.On("keypress", (KeyEvent e) =>
    {
        if (e.Name == "q") Environment.Exit(0);
    });
    ```

    ---

    *That's the basics! Explore the samples for more.*
    """,

    // Document 2: Changelog
    """
    # Changelog

    ## v0.3.0 — Latest

    ### Added
    - **MarkdownRenderable** with full block support
    - **DiffRenderable** with split and unified views
    - **TextTableRenderable** with proportional column fitting
    - ScrollBox mouse wheel support

    ### Changed
    - Improved `FlexDirection` handling in nested layouts
    - Better Unicode width calculation for CJK characters
    - `Rgba.FromHex()` now accepts 3, 4, 6, and 8-digit formats

    ### Fixed
    - Border rendering artifacts at small terminal sizes
    - Memory leak in `TextBuffer.Dispose()` path
    - Scroll position reset when content changes

    ## v0.2.0

    ### Added
    - `CodeRenderable` with filetype detection
    - `ScrollBoxRenderable` with keyboard and mouse scrolling
    - `StatusBar` widget for editor-style status lines

    ### Changed
    - Migrated from custom layout to **Yoga** flexbox engine
    - Unified event system across all renderables

    ```js
    // The TypeScript version that inspired the C# port
    const renderer = CliRenderer.create({
      exitOnCtrlC: true,
      targetFps: 30,
    });
    ```

    ## v0.1.0

    ### Added
    - Initial release with core rendering pipeline
    - `BoxRenderable`, `TextRenderable`, `ProgressRenderable`
    - Native interop layer for high-performance terminal I/O
    - Basic keyboard input handling

    ---

    *Full history available in the Git log.*
    """,
];
string[] docNames = ["Features", "API Guide", "Changelog"];

// --- Header ---
var header = new BoxRenderable(renderer, new BoxOptions
{
    Id = "header",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#059669"),
    BorderStyle = BorderStyle.Single,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    Border = true,
});
var headerText = new TextRenderable(renderer, new TextOptions
{
    Id = "header-text",
    Content = "MARKDOWN DEMO",
    Fg = Rgba.FromInts(255, 255, 255),
});
header.Add(headerText);

// --- ScrollBox with Markdown ---
var scrollBox = new ScrollBoxRenderable(renderer, new ScrollBoxOptions
{
    Id = "scroll",
    ScrollY = true,
    ScrollX = false,
    Width = DimensionValue.Auto,
    Height = DimensionValue.Auto,
    FlexGrow = 1,
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Rgba.FromHex("#444444"),
});

var md = new MarkdownRenderable(renderer, new MarkdownOptions
{
    Id = "md",
    Content = docs[docIndex],
    Fg = Rgba.FromHex("#d4d4d4"),
    Conceal = conceal,
    Width = DimensionValue.Auto,
    FlexGrow = 1,
});
scrollBox.Add(md);

// --- Footer ---
var footer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "footer",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#1e293b"),
    BorderStyle = BorderStyle.Single,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    Border = true,
});
var footerText = new TextRenderable(renderer, new TextOptions
{
    Id = "footer-text",
    Content = "",
    Fg = Rgba.FromHex("#94a3b8"),
});
footer.Add(footerText);

// --- Build tree ---
renderer.Root.Add(header);
renderer.Root.Add(scrollBox);
renderer.Root.Add(footer);

void UpdateDisplay()
{
    string concealLabel = conceal ? "on" : "off";
    headerText.ContentText = $"MARKDOWN DEMO — {docNames[docIndex]} ({docIndex + 1}/{docNames.Length})";
    footerText.ContentText = $"[N] Document ({docNames[docIndex]})  [C] Conceal ({concealLabel})  [↑/↓] Scroll";
}

// --- Key handling ---
renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    switch (e.Name)
    {
        case "n":
            docIndex = (docIndex + 1) % docs.Length;
            md.Content = docs[docIndex];
            scrollBox.ScrollTo(y: 0);
            break;
        case "c":
            conceal = !conceal;
            md.Conceal = conceal;
            break;
        case "up":
            scrollBox.ScrollBy(0, -1);
            break;
        case "down":
            scrollBox.ScrollBy(0, 1);
            break;
        case "pageup":
            scrollBox.ScrollBy(0, -10);
            break;
        case "pagedown":
            scrollBox.ScrollBy(0, 10);
            break;
    }
    UpdateDisplay();
});

UpdateDisplay();
renderer.RequestRender();
await Task.Delay(Timeout.Infinite);
