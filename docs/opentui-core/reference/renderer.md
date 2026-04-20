# Renderer reference

This page covers the renderer host, renderer configuration, and the built-in terminal console overlay.

## Types

| Type | Purpose |
| --- | --- |
| `CliRenderer` | Main application host and concrete `IRenderContext` |
| `CliRendererConfig` | Startup configuration for screen mode, input, fps, background, and terminal behavior |
| `ScreenMode` | Chooses alternate screen, main screen, or split-footer rendering |
| `ExternalOutputMode` | Controls whether stdout passes through or is captured |
| `KittyKeyboardOptions` | Enables richer keyboard reporting where supported |
| `TerminalConsole` | Built-in overlay log console |
| `ConsolePosition` | Console edge placement |
| `ConsoleAction` | Console key binding actions |
| `ConsoleKeyBinding` | Declarative binding for console commands |
| `ConsoleLogEntry` | Captured console log record |
| `ConsoleLogLevel` | Log severity levels |

## `CliRenderer`

Use `CliRenderer` to:

- create and own the terminal session with `Create(...)`
- access `Root`, `KeyInput`, `Console`, and the underlying `NativeRenderer`
- request redraws with `RequestRender()` or continuous rendering with `RequestLive()`
- manage focus and selection across the render tree
- control cursor, mouse pointer, and terminal palette queries

Important members:

- lifecycle: `Create(...)`, `Destroy()`, `Dispose()`, `Resize(...)`
- rendering: `RequestRender()`, `RequestLive()`, `DropLive()`, `SuspendRenderRequests()`
- focus: `FocusRenderable(...)`, `BlurRenderable(...)`
- selection: `StartSelection(...)`, `UpdateSelection(...)`, `ClearSelection()`, `GetSelection()`
- terminal: `SetBackgroundColor(...)`, `SetCursorPosition(...)`, `SetCursorStyle(...)`, `SetCursorColor(...)`
- diagnostics: `SetDebugMode(...)`, `SetDebugOverlay(...)`, `GetDebugInputs()`, `GetPalette(...)`

## `CliRendererConfig`

Start with:

- `ExitOnCtrlC = true`
- `TargetFps = 30`
- `UseMouse = true` if the UI is interactive

Use `Testing = true` in tests to avoid terminal setup.

## `TerminalConsole`

The console overlay is useful for in-app logging, captured stdout, and debugging while the renderer owns the terminal.

Important members:

- visibility and focus: `Show()`, `Hide()`, `Toggle()`, `Focus()`, `Blur()`
- logging: `Log(...)`, `Info(...)`, `Warn(...)`, `Error(...)`, `Debug(...)`, `Clear()`
- customization: `Position`, `SizePercent`, `KeyBindings`, `OnCopySelection`

## See also

- [Renderer lifecycle](../concepts/renderer-lifecycle.md)
- [Build a split-footer console](../how-to/build-a-split-footer-console.md)
- [Native and terminal interop](./native-and-terminal-interop.md)
