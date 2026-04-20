# Renderable tree and layout

Every visible element in `OpenTui.Core` is a `Renderable` or a type derived from it. The tree defines layout, drawing order, hit testing, focus, and selection.

## Renderable basics

`Renderable` combines:

- a Yoga node for layout
- event subscription via `EventEmitter`
- child management through `Add(...)`, `Remove(...)`, and `GetChildren()`
- focus and selection participation
- render hooks and optional buffering

The root of the tree is always `renderer.Root`.

## Layout options

`LayoutOptions` models the Yoga/CSS-style layout surface:

- dimensions: `Width`, `Height`, `MinWidth`, `MaxHeight`
- flex behavior: `FlexGrow`, `FlexShrink`, `FlexBasis`, `FlexDirection`, `FlexWrap`
- alignment: `AlignItems`, `AlignSelf`, `JustifyContent`
- positioning: `Position`, `Top`, `Right`, `Bottom`, `Left`
- box model: `Margin*`, `Padding*`, `Gap`, `RowGap`, `ColumnGap`

`RenderableOptions` adds renderable-specific behavior such as `Id`, `Visible`, `Buffered`, `Live`, `Opacity`, z-index, and event delegates.

## Flex and absolute positioning

The common pattern is:

- use flex layout for structural containers
- use absolute positioning for overlays, badges, floating status panes, and drag surfaces

Because renderables expose live properties, you can change layout at runtime without rebuilding the entire tree.

## Render order

Render order depends on:

1. parent-child structure
2. layout order
3. z-index

Use z-index for overlays and floating content, but keep structural ordering obvious in the tree whenever possible.

## Buffering and live rendering

Two `RenderableOptions` switches matter for advanced components:

| Option | Meaning |
| --- | --- |
| `Buffered` | Render into an off-screen `OptimizedBuffer` first |
| `Live` | Keep the renderer in continuous rendering mode while visible |

Buffering is useful for effects and overlays. Live mode is useful for animation or frequently changing content.

## Recommended pattern

1. Start with `BoxRenderable` containers.
2. Add content renderables below them.
3. Keep layout declarative with options and property updates.
4. Reach for custom `Renderable` subclasses only when existing renderables do not fit.

## Related APIs

- [Renderables and layout reference](../reference/renderables-and-layout.md)
- [Widgets reference](../reference/widgets.md)
