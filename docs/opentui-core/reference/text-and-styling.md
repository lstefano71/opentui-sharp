# Text and styling reference

This page covers the display-text surface: colors, attributes, styled text composition, and text-focused renderables.

## Types

| Type | Purpose |
| --- | --- |
| `Rgba` | RGBA color value used throughout the library |
| `TextAttributes` / `TextAttributeUtils` | Text styling flags and helpers |
| `StyledText`, `StyledTextBuilder`, `Style`, `TextChunk` | Styled text composition APIs |
| `TextBufferOptions` | Shared options for text display renderables |
| `TextOptions` | Adds content fields for `TextRenderable` |
| `TextRenderable` | Display-only text renderable |
| `TextBufferRenderable` | Base class for renderables backed by a native `TextBufferView` |
| `WrapMode` | Text wrapping behavior |

## `StyledText` family

Use:

- `TextChunk` for individual styled segments
- `StyledText` for immutable composed content
- `StyledTextBuilder` for fluent construction
- `Style` for terse helpers such as `Bold(...)`, `Italic(...)`, `Underline(...)`, and color helpers

## `TextRenderable`

`TextRenderable` is the usual display-text control.

Important members:

- `ContentText`
- `Content`
- `SetContent(string)`
- `SetContent(StyledText)`
- `Clear()`

## `TextBufferRenderable`

Use `TextBufferRenderable` when you need text display that still benefits from the native text buffer pipeline, selection support, or advanced wrapping behavior.

## See also

- [Text, styling, and selection](../concepts/text-styling-and-selection.md)
- [Editor and buffers reference](./editor-and-buffers.md)
