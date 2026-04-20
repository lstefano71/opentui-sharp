# Input and events reference

This page covers the event model, input events, parser types, and renderer-level input services.

## Types

| Type | Purpose |
| --- | --- |
| `IEventEmitter`, `EventEmitter` | Base pub/sub surface |
| `KeyHandler`, `KeyHandlerEvents` | Renderer-level keyboard input source |
| `RenderableEventNames`, `RendererEventNames`, `LayoutEvents` | Shared event name constants |
| `UiEvent`, `KeyEvent`, `UiMouseEvent`, `PasteEvent` | High-level event objects |
| `ParsedKey`, `RawMouseEvent`, `PasteMetadata`, `DebugInputRecord` | Parsed input data |
| `Key`, `Mouse`, `Paste`, `Response`, `StdinEvent` | Lower-level stdin protocol payloads |
| `KeypressParser`, `MouseParser`, `StdinParser`, `StdinParserOptions`, `ProtocolContext` | Input parser pipeline |
| `KeyModifiers`, `ScrollInfo` | Supporting input value types |
| `KeyEventType`, `MouseEventType`, `MouseButton`, `MouseEncoding`, `PasteKind`, `StdinResponseProtocol` | Input enums |

## Key patterns

- Use `renderer.KeyInput.On("keypress", ...)` for app-level shortcuts.
- Use renderable event delegates or widget-specific events for local handling.
- Call `StopPropagation()` when a widget should fully consume a key or mouse action.
- Use parser types only when you are building diagnostics, custom input infrastructure, or tests.

## Widget-specific nested event types

Several widgets expose nested `Events` classes containing string constants, including:

- `TextareaRenderable.Events`
- `InputRenderable.Events`
- `SelectRenderable.Events`
- `SliderRenderable.Events`
- `TabControllerRenderable.Events`
- `TabSelectRenderable.Events`
- `ScrollBarRenderable.Events`
- `DiffRenderable.Events`

## See also

- [Input and events](../concepts/input-and-events.md)
- [Handle focus and form input](../how-to/handle-focus-and-form-input.md)
