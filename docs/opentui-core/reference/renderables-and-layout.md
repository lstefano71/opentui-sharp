# Renderables and layout reference

This page covers the core tree and layout abstractions used by nearly every `OpenTui.Core` app.

## Types

| Type | Purpose |
| --- | --- |
| `IRenderContext` | Services exposed by the renderer to renderables |
| `Renderable` | Base class for all renderable nodes |
| `RootRenderable` | Root node owned by `CliRenderer` |
| `RenderableOptions` | Renderable-specific configuration |
| `LayoutOptions` | Yoga layout configuration |
| `LayoutConfig` | Shared Yoga configuration and layout helpers |
| `YogaEnumMapper` / `YogaNodeExtensions` | Internal-facing Yoga convenience helpers that remain public |
| `AlignValue`, `FlexDirectionValue`, `JustifyValue`, `OverflowValue`, `PositionValue`, `WrapValue` | Layout enums |
| `DimensionValue`, `DimensionUnit` | Point/percent/auto/undefined dimension model |
| `ComputedLayout` | Snapshot of Yoga-computed bounds |
| `Selection`, `SelectionHelpers`, `LocalSelectionBounds` | Tree-level selection helpers |
| `ILineInfoProvider` | Line-info surface used by text-aware renderables |
| `RenderCommand`, `RenderCommandAction` | Low-level render command representation |

## `Renderable`

Key capabilities:

- child management with `Add(...)`, `Remove(...)`, `GetChildren()`
- layout state through properties such as `FlexGrow`, `WidthDimension`, `Padding`, and `PositionType`
- focus and selection helpers (`Focus()`, `Blur()`, `HasSelection()`, `GetSelectedText()`)
- render scheduling with `RequestRender()`
- lifecycle helpers (`Destroy()`, `DestroyRecursively()`)

## `RenderableOptions`

Use this when you need:

- `Id` for lookup
- `Visible`, `Buffered`, `Live`, `Opacity`
- z-index
- render hooks (`RenderBefore`, `RenderAfter`)
- mouse, paste, key, and size-change delegates

## `LayoutOptions`

`LayoutOptions` is the reusable layout surface that most option objects inherit. Favor it over manual coordinate math whenever your UI is structurally nested.

## See also

- [Renderable tree and layout](../concepts/renderable-tree-and-layout.md)
- [Widgets reference](./widgets.md)
