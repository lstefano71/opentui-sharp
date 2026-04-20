# Get started with OpenTui.Core

This guide shows the minimum setup needed to build a renderer-backed terminal UI with `OpenTui.Core`.

## Prerequisites

| Requirement | Notes |
| --- | --- |
| .NET 10 SDK | `OpenTui.Core` targets `net10.0` |
| Node.js / npm | Needed to restore `opentui.dll` from the npm package |
| Windows x64 | Current native support target |

## Repository setup

From the repository root:

```powershell
npm install
dotnet build
```

`npm install` restores the native `opentui.dll`, and the build copies it into `runtimes\win-x64\native\`.

## Minimal application

```csharp
using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 30,
    UseMouse = true,
});

var rootBox = new BoxRenderable(renderer, new BoxOptions
{
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Percent(100),
    Padding = 1,
    Border = true,
    Title = "OpenTui.Core",
    BorderStyle = BorderStyle.Rounded,
});

var text = new TextRenderable(renderer, new TextOptions
{
    Content = "Hello from OpenTui.Core. Press q to quit.",
    Fg = Rgba.FromHex("#7dd3fc"),
});

rootBox.Add(text);
renderer.Root.Add(rootBox);

renderer.KeyInput.On("keypress", (KeyEvent key) =>
{
    if (key.Name == "q")
    {
        key.StopPropagation();
        renderer.Destroy();
    }
});

renderer.RequestRender();

while (!renderer.IsDestroyed)
    await Task.Delay(50);
```

## What this code does

- `CliRenderer.Create(...)` creates the terminal session and the concrete `IRenderContext`.
- `renderer.Root` is the root of the render tree.
- `BoxRenderable` provides a simple container with layout and border support.
- `TextRenderable` draws plain or styled text.
- `renderer.KeyInput` exposes renderer-level keyboard events.

## Choose the right abstraction

| If you want... | Start with... |
| --- | --- |
| A full-screen CLI/TUI app | `CliRenderer`, `Renderable`, and the widget renderables in `OpenTui.Core` |
| A simpler high-level app model | `OpenTui` |
| Raw native access | `OpenTui.Native` or advanced `OpenTui.Core` interop helpers |

## Next steps

1. Build the walkthrough in [Build your first CLI screen](./tutorials/build-your-first-cli-screen.md).
2. Read [Renderer lifecycle](./concepts/renderer-lifecycle.md) before adding custom render loops or split-screen behavior.
3. Use [Renderables and layout](./concepts/renderable-tree-and-layout.md) when you start composing more than a few nodes.
