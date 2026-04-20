# Text, styling, and selection

`OpenTui.Core` offers three increasingly advanced text layers:

| Layer | Use it when |
| --- | --- |
| `TextRenderable` + `TextOptions` | You want display-only text with simple wrapping and styling |
| `TextBuffer` / `TextBufferView` | You need direct control over native text storage, highlights, and extracted ranges |
| `EditBuffer` / `EditorView` / editor-backed renderables | You need editing, cursor movement, undo/redo, or viewport-aware selection |

## Styled text

Use `StyledText`, `TextChunk`, and `Style` helpers when plain strings are not enough:

```csharp
var content = new StyledText(
    Style.Bold("Build: "),
    TextChunk.Styled("ok", fg: Rgba.FromHex("#22c55e")));
```

You can also build content fluently with `StyledTextBuilder`.

## Selection model

Selection can happen at multiple levels:

- renderer-level selection across renderables
- text renderable selection for display content
- editor view selection for editable content

Important selection-related APIs:

- `CliRenderer.StartSelection(...)`
- `CliRenderer.UpdateSelection(...)`
- `CliRenderer.ClearSelection()`
- `Renderable.HasSelection()` and `Renderable.GetSelectedText()`
- `EditorView.SetSelection(...)`, `GetSelectionRange()`, and `GetSelectedText()`

## Text colors and attributes

Use:

- `Rgba` for color
- `TextAttributes` for bold, underline, italic, dim, reverse, and similar styling
- `SyntaxStyle` when you want a reusable named style registry for highlighting

## When to choose editor-backed renderables

Choose `TextareaRenderable`, `InputRenderable`, `CodeRenderable`, or `EditBufferRenderable` when you need:

- keyboard editing
- cursor placement
- selection updates that track the view
- word and line navigation
- undo/redo

## Related APIs

- [Text and styling reference](../reference/text-and-styling.md)
- [Editor and buffers reference](../reference/editor-and-buffers.md)
- [Work with editing and selection](../how-to/work-with-editing-and-selection.md)
