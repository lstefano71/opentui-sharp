# How to: work with editing and selection

Use the editor stack when you need real text editing instead of static display text.

## Choose the right level

| If you need... | Start with... |
| --- | --- |
| Display-only text with style | `TextRenderable` |
| A single-line input | `InputRenderable` |
| A multi-line editor | `TextareaRenderable` |
| A custom editor widget | `EditBufferRenderable`, `EditBuffer`, and `EditorView` |

## Work directly with `EditBuffer`

```csharp
using var buffer = EditBuffer.Create();
buffer.SetText("hello");
buffer.MoveCursorRight();
buffer.InsertText(" world");
```

## Use `EditorView` for viewport-aware behavior

```csharp
using var view = EditorView.Create(buffer, viewportWidth: 80, viewportHeight: 24);
view.SetWrapMode((byte)WrapMode.Word);
view.SetSelection(0, 5);
var selected = view.GetSelectedText();
```

## Selection-related APIs

- `SetSelection(...)`
- `ResetSelection()`
- `GetSelectionRange()`
- `SetLocalSelection(...)`
- `UpdateLocalSelection(...)`
- `DeleteSelectedText()`

## Extmarks and advanced editing

`ExtmarksController`, `Extmark`, and `ExtmarkOptions` are the marker/annotation layer for editor-backed renderables. Use them when you need gutter marks, highlights, or stable positions that move with edits.

## Samples

- `samples\EditorDemo`
- `samples\ExtmarksDemo`
- `samples\TextSelectionDemo`
