# Native and terminal interop reference

This page covers the public APIs that sit closest to the native renderer and terminal capability layer.

## Types

| Type | Purpose |
| --- | --- |
| `NativeRenderer` | Managed wrapper around the native renderer, hit grid, and terminal services |
| `Diagnostics` | Native diagnostics surface |
| `Link` | Hyperlink-related interop helper |
| `SyntaxStyle`, `SyntaxStyleEntry` | Native syntax-style registry |
| `NativeSpanFeed` | Streaming span-feed wrapper |
| `Unicode` | Unicode helper surface |
| `WidthMethod`, `CursorStyle`, `DebugOverlayCorner`, `MousePointerStyle`, `TargetChannel`, `ThemeMode` | Native-facing enums used by renderer and terminal APIs |

## `NativeRenderer`

Reach for `NativeRenderer` only when `CliRenderer` does not expose the control you need directly.

Capabilities include:

- terminal setup and restore
- raw render calls
- clipboard access
- palette and capability queries
- hit-grid access
- debug dumps and overlays
- mouse and Kitty keyboard protocol control

## `SyntaxStyle`

Use `SyntaxStyle` when you want a reusable registry of named text styles that can be applied to text buffers or editor-backed content.

## `NativeSpanFeed`

`NativeSpanFeed` is the streaming surface for structured span processing. It is advanced and most application authors can ignore it until they are building custom streaming/highlighting integrations.

## See also

- [Renderer reference](./renderer.md)
- [Shared types and enums](./shared-types-and-enums.md)
