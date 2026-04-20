# Editor and buffers reference

This page covers the editable-text stack and the lower-level text buffer APIs.

## Types

| Type | Purpose |
| --- | --- |
| `TextBuffer` | Native text buffer wrapper for loading, extracting, styling, and highlighting text |
| `TextBufferView` | View over a native text buffer |
| `EditBuffer` | Editable text buffer with cursor movement and undo/redo |
| `EditorView` | Viewport-aware editor view for wrapping, scrolling, and selection |
| `Extmark`, `ExtmarkOptions`, `ExtmarksController` | Marker/annotation support for editor-backed content |
| `EditBufferOptions`, `EditBufferRenderable` | Base options and base class for editor-backed renderables |
| `TextareaOptions`, `TextareaRenderable`, `TextareaRenderable.Events` | Multi-line editor widget |
| `InputOptions`, `InputRenderable`, `InputRenderable.Events` | Single-line input widget |

## `TextBuffer`

Use `TextBuffer` when you need direct control over:

- plain text content
- text ranges by offset or coordinates
- file loading
- highlights
- default foreground, background, and attributes
- syntax style application

## `EditBuffer`

Use `EditBuffer` for editing operations:

- insert, replace, delete, newline
- cursor movement
- word boundary lookup
- undo/redo
- offset and position conversion

## `EditorView`

Use `EditorView` when editing behavior depends on the visible viewport:

- wrapping and virtual line counts
- visual cursor tracking
- viewport sizing and scroll margin
- logical and local selection

## `EditBufferRenderable`

This abstract base glues together:

- an `EditBuffer`
- an `EditorView`
- selection colors
- cursor appearance
- tab indicators
- syntax styling
- extmarks

Concrete widgets such as `TextareaRenderable`, `InputRenderable`, and `CodeRenderable` build on this layer.

## See also

- [Text, styling, and selection](../concepts/text-styling-and-selection.md)
- [Work with editing and selection](../how-to/work-with-editing-and-selection.md)
