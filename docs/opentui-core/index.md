# OpenTui.Core developer guide

`OpenTui.Core` is the low-level C# application API for building terminal UIs directly on top of the OpenTUI renderer. It sits below the higher-level `OpenTui` widget layer and above `OpenTui.Native`.

Use this layer when you want to:

- compose a render tree yourself with `CliRenderer` and `Renderable`
- control layout with Yoga-based options instead of higher-level widgets
- work directly with text buffers, editor views, selection, and input events
- build custom terminal components without dropping all the way to raw P/Invoke

## Mental model

1. `CliRenderer` owns the terminal session, the native renderer, the input loop, and the root render tree.
2. `Renderable` is the base building block for layout, focus, selection, and drawing.
3. Widget renderables such as `BoxRenderable`, `TextRenderable`, `ScrollBoxRenderable`, and `InputRenderable` are reusable concrete nodes built on top of `Renderable`.
4. Text and editor-backed components use `StyledText`, `TextBuffer`, `EditBuffer`, and `EditorView`.
5. Advanced integrations can drop to `NativeRenderer`, `SyntaxStyle`, `NativeSpanFeed`, and other interop helpers.

## Documentation map

| Area | What it covers |
| --- | --- |
| [Getting started](./getting-started.md) | Prerequisites, setup, and the smallest useful renderer-backed app |
| [Tutorial: build your first CLI screen](./tutorials/build-your-first-cli-screen.md) | End-to-end example using layout, text, and keyboard input |
| [Renderer lifecycle](./concepts/renderer-lifecycle.md) | Renderer ownership, screen modes, render requests, shutdown |
| [Renderable tree and layout](./concepts/renderable-tree-and-layout.md) | Tree composition, Yoga layout options, z-index, visibility, buffering |
| [Text, styling, and selection](./concepts/text-styling-and-selection.md) | `StyledText`, text renderables, selection behavior, text buffers |
| [Input and events](./concepts/input-and-events.md) | Keyboard, mouse, paste, focus, and event propagation |
| [Plugins and slots](./concepts/plugins-and-slots.md) | Plugin slot system for dynamic, composable UI extension points |
| [How-to guides](./how-to/build-a-split-footer-console.md) | Task-oriented recipes for console overlays, forms, editing, and selection |
| [Reference](./reference/renderer.md) | Subsystem reference pages covering the full public API surface |

## Reference coverage

The reference set is organized by subsystem instead of by raw source file:

- [Renderer](./reference/renderer.md)
- [Renderables and layout](./reference/renderables-and-layout.md)
- [Text and styling](./reference/text-and-styling.md)
- [Input and events](./reference/input-and-events.md)
- [Widgets](./reference/widgets.md)
- [Editor and buffers](./reference/editor-and-buffers.md)
- [Animation](./reference/animation.md)
- [Native and terminal interop](./reference/native-and-terminal-interop.md)
- [Plugins](./reference/plugins.md)
- [Shared types and enums](./reference/shared-types-and-enums.md)

## Recommended reading order

1. Start with [getting started](./getting-started.md).
2. Build the sample in [the tutorial](./tutorials/build-your-first-cli-screen.md).
3. Read the concept pages for the parts you will customize most.
4. Use the reference pages as the API map once you begin building your own renderables or editor-backed components.
