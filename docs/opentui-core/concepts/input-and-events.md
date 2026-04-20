# Input and events

`OpenTui.Core` uses a DOM-like event model: events can be observed, stopped, and handled at renderer, widget, or custom-renderable level.

## Event emitters

`IEventEmitter` and `EventEmitter` provide the base pub/sub surface used across the render tree. Renderer and renderables both participate in the same event model.

Common event name constants live in:

- `RenderableEventNames`
- `RendererEventNames`
- `LayoutEvents`
- widget-specific nested `Events` classes such as `InputRenderable.Events`

## Keyboard input

Renderer-level keyboard input comes from `KeyHandler`:

```csharp
renderer.KeyInput.On("keypress", (KeyEvent key) => { /* ... */ });
```

`KeyEvent` wraps `ParsedKey` and exposes:

- `Name`
- modifier flags such as `Ctrl`, `Shift`, `Meta`, `Super`
- raw sequence data
- `StopPropagation()` and `PreventDefault()`

## Mouse input

Mouse events are surfaced as `UiMouseEvent` and include:

- `Type`
- `Button`
- `X` / `Y`
- `Modifiers`
- `Scroll`
- drag state and target information

Mouse handling depends on renderer configuration (`UseMouse`, `EnableMouseMovement`) and whether the renderable participates in the hit grid.

## Paste input

Paste sequences arrive as `PasteEvent`, which carries raw bytes plus optional metadata. Use `Text` when you want the decoded UTF-8 content.

## Widget-specific input

Several renderables layer their own event model on top of the base input system:

- `InputRenderable.Events` exposes `Input`, `Change`, and `Enter`
- `TextareaRenderable.Events` exposes `Submit`
- select, slider, tab, and scroll widgets expose nested `Events` constants for higher-level interaction

## Low-level parsing surface

For advanced integrations, `OpenTui.Core` also exposes the parser types used internally:

- `KeypressParser`
- `MouseParser`
- `StdinParser`
- `ParsedKey`, `RawMouseEvent`, `Response`, `StdinEvent`

These are mainly useful when you are building custom input pipelines, diagnostics tooling, or tests.

## Related APIs

- [Input and events reference](../reference/input-and-events.md)
- [Handle focus and form input](../how-to/handle-focus-and-form-input.md)
