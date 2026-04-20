# How to: build a split-footer console

Use split-footer mode when you want a full-screen UI plus a log console or captured stdout area.

## Configure the renderer

```csharp
using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ScreenMode = ScreenMode.SplitFooter,
    ExternalOutputMode = ExternalOutputMode.CaptureStdout,
    FooterHeight = 12,
    ExitOnCtrlC = true,
});
```

## Show the built-in console overlay

```csharp
renderer.Console.Show();
renderer.Console.Info("Console started");
renderer.Console.Warn("Captured stdout will appear above the footer");
```

## Useful console APIs

| API | Purpose |
| --- | --- |
| `Show()` / `Hide()` / `Toggle()` | Control visibility |
| `Focus()` / `Blur()` | Move keyboard focus into or out of the console |
| `Log()` / `Info()` / `Warn()` / `Error()` / `Debug()` | Append structured log entries |
| `KeyBindings` | Override the default console keyboard actions |
| `OnCopySelection` | Handle copy-selection behavior |

## When to use `CaptureStdout`

Use `ExternalOutputMode.CaptureStdout` when your app or libraries still write to stdout and you want that output preserved instead of corrupting the full-screen renderer.

## Sample

For a larger example, see `samples\ConsoleDemo`.
