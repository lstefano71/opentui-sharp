# Widgets reference

This page covers the concrete renderables you use to build screens and reusable UI components.

## Container and structural widgets

| Type | Purpose |
| --- | --- |
| `BoxOptions`, `BoxRenderable` | Core container with border, background, title, and gap support |
| `ScrollBoxOptions`, `ScrollBoxRenderable` | Clipped scrollable container with optional scrollbars |
| `ScrollBarOptions`, `ScrollBarRenderable`, `ScrollUnit` | Scroll indicators and interaction |
| `ArrowDirection`, `ArrowOptions`, `ArrowRenderable` | Arrow primitives used by scrollbar-style widgets |
| `FrameBufferOptions`, `FrameBufferRenderable` | Raw framebuffer-style drawing surface |
| `GenericOptions`, `GenericRenderable` | Minimal generic renderable wrapper |
| `DelegatingOptions`, `DelegatingRenderable` | Renderable that delegates its drawing logic |

## Text and document widgets

| Type | Purpose |
| --- | --- |
| `TextNodeOptions`, `TextNodeRenderable`, `RootTextNodeRenderable` | Tree-structured text composition |
| `MarkdownOptions`, `MarkdownRenderable`, `MarkdownTableData`, `MarkdownTableOptions`, `MarkdownToken` | Markdown rendering |
| `CodeOptions`, `CodeRenderable` | Code display |
| `DiffOptions`, `DiffRenderable`, `DiffHunk`, `DiffLine`, `DiffLineType`, `DiffRenderable.Events` | Diff rendering |
| `LineNumberOptions`, `LineNumberRenderable`, `LineSign`, `LineColorConfig` | Gutter and line-number rendering |
| `TextTableOptions`, `TextTableRenderable` | Text-table presentation |

## Input and selection widgets

| Type | Purpose |
| --- | --- |
| `SelectOptions`, `SelectOption`, `SelectRenderable`, `SelectRenderable.Events` | Vertical option picker |
| `TabSelectOption`, `TabSelectOptions`, `TabSelectRenderable`, `TabSelectRenderable.Events` | Tab-style selection |
| `TabControllerOptions`, `TabControllerRenderable`, `TabControllerTab`, `TabControllerRenderable.Events` | Tab hosting and switching |
| `SliderOptions`, `SliderRenderable`, `SliderOrientation`, `SliderRenderable.Events` | Slider interaction |

## Text-entry widgets

These are documented in detail in [Editor and buffers](./editor-and-buffers.md), but they are also concrete widgets:

- `EditBufferOptions`
- `EditBufferRenderable`
- `TextareaOptions`
- `TextareaRenderable`
- `InputOptions`
- `InputRenderable`

## Decorative and specialty widgets

| Type | Purpose |
| --- | --- |
| `AsciiFont`, `AsciiFontRenderOptions`, `ASCIIFontOptions`, `ASCIIFontRenderable` | ASCII-art font rendering |
| `BorderCharacters`, `BorderSides`, `BorderStyle`, `TitleAlignment` | Border glyph and border-style types used across box-like widgets |

## See also

- [Renderables and layout reference](./renderables-and-layout.md)
- [Editor and buffers reference](./editor-and-buffers.md)
