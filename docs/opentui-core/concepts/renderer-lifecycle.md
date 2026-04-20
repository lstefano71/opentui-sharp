# Renderer lifecycle

`CliRenderer` is the center of an `OpenTui.Core` app. It owns the native renderer, double buffers, the render tree root, terminal mode setup, keyboard and mouse input, selection state, and optional console overlay support.

## Create the renderer

Most apps start with:

```csharp
using var renderer = CliRenderer.Create(new CliRendererConfig { ExitOnCtrlC = true });
```

Important `CliRendererConfig` knobs:

| Setting | Use it for |
| --- | --- |
| `Testing` | Disable terminal setup and interactive I/O in tests |
| `TargetFps` / `MaxFps` | Tune live rendering and rerender throttling |
| `UseMouse` / `EnableMouseMovement` | Turn mouse support on or off |
| `ScreenMode` | Choose alternate screen, main screen, or split-footer mode |
| `ExternalOutputMode` | Capture or pass through stdout while rendering |
| `UseKittyKeyboard` | Enable richer keyboard sequences when supported |
| `BackgroundColor` | Fill the render surface before drawing |

## Root and render requests

`renderer.Root` is a `RootRenderable`. Add your application tree below it and call `RequestRender()` when state changes outside built-in widget logic.

Use `RequestLive()` and `DropLive()` when a component needs continuous rendering, such as animation or timers.

## Screen modes

| Mode | What it does |
| --- | --- |
| `AlternateScreen` | Full-screen app without mixing with normal shell output |
| `MainScreen` | Draw directly on the main terminal buffer |
| `SplitFooter` | Reserve a footer area for renderer output and optionally capture stdout above it |

When `ExternalOutputMode` is `CaptureStdout`, `ScreenMode` must be `SplitFooter`.

## Terminal services

`CliRenderer` exposes several services beyond plain drawing:

- `Console` for the built-in overlay log viewer
- `KeyInput` for renderer-level keyboard input
- selection helpers such as `StartSelection(...)`, `UpdateSelection(...)`, and `ClearSelection()`
- cursor helpers such as `SetCursorPosition(...)`, `SetCursorStyle(...)`, and `SetCursorColor(...)`
- `Native` for advanced access to `NativeRenderer`

## Shutdown

Destroy the renderer when your app exits:

```csharp
renderer.Destroy();
```

The renderer also implements `IDisposable`, so `using` is the normal ownership pattern.

## Related APIs

- [Renderer reference](../reference/renderer.md)
- [Build a split-footer console](../how-to/build-a-split-footer-console.md)
- [Native and terminal interop](../reference/native-and-terminal-interop.md)
