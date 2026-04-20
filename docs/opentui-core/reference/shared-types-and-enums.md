# Shared types and enums reference

This page collects the public value types and supporting models that are shared across multiple `OpenTui.Core` subsystems.

## Cursor, viewport, and measurement

| Type | Purpose |
| --- | --- |
| `LogicalCursor` | Logical row/column/offset cursor location |
| `VisualCursor` | Visual cursor location after wrapping/layout |
| `ViewportBounds` | Viewport rectangle |
| `MeasureResult` | Measured text dimensions |
| `CursorState` | Native cursor state snapshot |
| `CursorStyleOptions` | Cursor appearance update payload |

## Text, selection, and capture data

| Type | Purpose |
| --- | --- |
| `LineInfo` | Wrapped-line layout information |
| `Highlight` | Text highlight range |
| `CapturedSpan`, `CapturedLine`, `CapturedFrame` | Captured output models used heavily in testing |
| `EncodedChar` | Unicode encoding result |
| `SelectOption<T>` | Generic select option model for typed value selection |

## Terminal and diagnostics data

| Type | Purpose |
| --- | --- |
| `TerminalCapabilities` | Terminal feature detection result |
| `GetPaletteOptions` | Palette-query options |
| `TerminalColors` | Palette and special terminal colors |
| `BuildOptions` | Native build flags |
| `AllocatorStats` | Native allocator statistics |
| `SpanFeedOptions`, `SpanFeedStats`, `GrowthPolicy` | Native span-feed configuration and statistics |

## Notes

- Many of these types are returned by the advanced renderer, editor, or diagnostics surfaces.
- You usually consume them rather than create them directly.
- For the APIs that produce these types, start from the relevant subsystem reference page.

## See also

- [Renderer reference](./renderer.md)
- [Editor and buffers reference](./editor-and-buffers.md)
- [Native and terminal interop reference](./native-and-terminal-interop.md)
