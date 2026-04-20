# Animation reference

This page covers the timeline and animation helpers used for live updates and visual effects.

## Types

| Type | Purpose |
| --- | --- |
| `Timeline` | Timeline container for scheduled tweens, callbacks, and child timelines |
| `TimelineOptions` | Top-level timeline configuration |
| `AnimationOptions` | Per-animation configuration |
| `TweenProperty` | Getter/setter-based tween target description |
| `AnimationState` | Per-update animation callback payload |
| `TimelineEngine` | Engine that coordinates renderer-attached timelines |
| `TimelineFactory` | Convenience creation helpers |
| `Easing` | Named easing functions |
| `VignetteEffect` | Post-process effect helper |

## `Timeline`

Key members:

- `Add(...)`
- `Once(...)`
- `Call(...)`
- `Sync(...)`
- `Play()`, `Pause()`, `Restart()`
- `Update(...)`

`Timeline` uses delegates instead of string-based property lookup, which keeps the API explicit and AOT-friendly.

## Typical use

1. Create a timeline.
2. Add one or more `AnimationOptions` objects.
3. Point each `TweenProperty` at a renderable property through `Get` and `Set`.
4. Call `Update(deltaTime)` from a live render path or use the timeline engine.

## See also

- `samples\TimelineExample`
- [Renderer lifecycle](../concepts/renderer-lifecycle.md)
