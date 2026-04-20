# Tutorial: build your first OpenTui.Core CLI screen

This tutorial builds a small full-screen CLI with a header, a status panel, a command log, and keyboard interaction. It uses the same building blocks you will reuse in real applications: `CliRenderer`, `BoxRenderable`, `TextRenderable`, Yoga layout options, and renderer-level key input.

## What you will build

- a header row
- a main content area with two panels
- a live status line that updates from key input
- a quit command and a simple counter

## The complete example

```csharp
using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 30,
    UseMouse = true,
});

int counter = 0;

var root = new BoxRenderable(renderer, new BoxOptions
{
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Percent(100),
    FlexDirection = FlexDirectionValue.Column,
    BackgroundColor = Rgba.FromHex("#0f172a"),
});

var header = new BoxRenderable(renderer, new BoxOptions
{
    Height = 3,
    PaddingX = 1,
    AlignItems = AlignValue.Center,
    Border = true,
    BorderStyle = BorderStyle.Single,
    BorderColor = Rgba.FromHex("#38bdf8"),
    Title = "OpenTui.Core tutorial",
});

var body = new BoxRenderable(renderer, new BoxOptions
{
    FlexGrow = 1,
    FlexDirection = FlexDirectionValue.Row,
    Gap = 1,
    Padding = 1,
});

var leftPanel = new BoxRenderable(renderer, new BoxOptions
{
    FlexGrow = 2,
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    Title = "Commands",
    Padding = 1,
});

var rightPanel = new BoxRenderable(renderer, new BoxOptions
{
    FlexGrow = 1,
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    Title = "Status",
    Padding = 1,
});

var title = new TextRenderable(renderer, new TextOptions
{
    Content = "Press + to increment, c to clear, q to quit",
    Fg = Rgba.FromHex("#e2e8f0"),
    Attributes = TextAttributes.Bold,
});

var commandText = new TextRenderable(renderer, new TextOptions
{
    Content = "Recent commands:\n- app started",
    Fg = Rgba.FromHex("#cbd5e1"),
});

var statusText = new TextRenderable(renderer, new TextOptions
{
    Content = "Counter: 0",
    StyledContent = new StyledText(
        TextChunk.Styled("Counter:", fg: Rgba.FromHex("#7dd3fc"), attributes: TextAttributes.Bold),
        TextChunk.Plain(" 0")),
});

header.Add(title);
leftPanel.Add(commandText);
rightPanel.Add(statusText);
body.Add(leftPanel);
body.Add(rightPanel);
root.Add(header);
root.Add(body);
renderer.Root.Add(root);

void UpdateStatus(string message)
{
    commandText.SetContent($"Recent commands:\n- {message}");
    statusText.Content = new StyledText(
        TextChunk.Styled("Counter:", fg: Rgba.FromHex("#7dd3fc"), attributes: TextAttributes.Bold),
        TextChunk.Plain($" {counter}"));
    renderer.RequestRender();
}

renderer.KeyInput.On("keypress", (KeyEvent key) =>
{
    switch (key.Name)
    {
        case "+":
            counter++;
            UpdateStatus("incremented");
            key.StopPropagation();
            break;

        case "c":
            counter = 0;
            UpdateStatus("cleared");
            key.StopPropagation();
            break;

        case "q":
            key.StopPropagation();
            renderer.Destroy();
            break;
    }
});

renderer.RequestRender();

while (!renderer.IsDestroyed)
    await Task.Delay(50);
```

## Why it works

### 1. The renderer owns the terminal session

`CliRenderer` is both the render loop host and the concrete `IRenderContext` passed to renderables. Most application code starts there.

### 2. Layout is data, not imperative drawing code

`BoxOptions` and `TextOptions` inherit from `RenderableOptions` and `LayoutOptions`, so width, height, flex behavior, padding, positioning, and event hooks are all configured at construction time and then updated through properties.

### 3. The tree is explicit

You add children with `Add(...)`, starting from `renderer.Root`. Parent-child relationships drive layout, render order, focus handling, and selection behavior.

### 4. Input is event driven

`renderer.KeyInput.On("keypress", ...)` gives you `KeyEvent` objects that support `StopPropagation()` and `PreventDefault()`. Widget-specific components also raise their own events.

## Where to go next

- Add a console overlay with [Build a split-footer console](../how-to/build-a-split-footer-console.md).
- Add forms with [Handle focus and form input](../how-to/handle-focus-and-form-input.md).
- Read [Renderables and layout](../concepts/renderable-tree-and-layout.md) when you start mixing flex and absolute positioning.
