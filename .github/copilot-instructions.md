# Copilot Instructions — OpenTUI-sharp

## Build & Test

```powershell
npm install          # downloads native opentui.dll from npm (first time / after clean)
dotnet build         # builds all projects; MSBuild copies DLL to runtimes/win-x64/native/
dotnet test          # run all tests (xUnit)

# Single test
dotnet test --filter "FullyQualifiedName~CliTableTests.Write_EmptyTable_DrawsBorders"

# Single project
dotnet test tests/OpenTui.Tests
dotnet test tests/OpenTui.Core.Tests
dotnet test tests/OpenTui.Native.Tests
```

Targets **.NET 10 / C# 14**. All projects use `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`. Targeting Windows x64 only for now. Targeting AOT. High-performance code preferred when possible in the library.

## Architecture

Three-layer stack — each layer depends only on the one below it:

| Layer | Project | Namespace | Purpose |
|-------|---------|-----------|---------|
| **Native** | `OpenTui.Native` | `OpenTui.Native` | ~110 `[LibraryImport]` P/Invoke declarations to `opentui.dll` (Zig). Raw `nint` handles. AOT-compatible. |
| **Core** | `OpenTui.Core` | `OpenTui.Core` | Low-level managed wrappers: `NativeRenderer`, `OptimizedBuffer`, `TextBuffer`, `EditBuffer`, `Renderable` tree, `EventEmitter`, Yoga layout, input parsing, animation. |
| **Widgets** | `OpenTui` | `OpenTui` | High-level API: `Widget` base class + full-screen TUI widgets (`Box`, `Text`, `Select`, `Dialog`, …) with Yoga flexbox, plus inline CLI widgets (`CliTable`, `CliProgress`, `CliSelect`, …) that work without the native DLL. |

**OpenTui.Core** is the newer, lower-level abstraction (renderable tree, render commands, event emitter). **OpenTui** is the higher-level widget API built on top. Both reference `OpenTui.Native` and `Yoga.Net`.

### Native DLL resolution

The managed assembly name (`OpenTui.dll`) collides with the native library (`opentui.dll`) on case-insensitive Windows. `OpenTuiNative`'s static constructor registers a `NativeLibrary.SetDllImportResolver` that loads from `runtimes/win-x64/native/opentui.dll`.

### Native DLL source

`opentui.dll` comes from the npm package `@opentui/core-win32-x64`. The `CopyNativeDll` MSBuild target in `OpenTui.Native.csproj` copies it from `node_modules/` to `runtimes/win-x64/native/` at build time. Currently **win-x64 only**.

## Key Conventions

### P/Invoke bindings (`OpenTuiNative.cs`)

- Use `[LibraryImport]` (source-generated, AOT-safe) — never `[DllImport]`.
- All native handles are `nint`. Safe wrappers live in `OpenTui.Native/Handles/` (e.g., `RendererHandle`, `BufferHandle`).
- Functions are organized into `#region` groups matching native subsystems: Renderer, Buffer, TextBuffer, EditorView, EditBuffer, HitGrid, Link, SyntaxStyle, Unicode, SpanFeed, Diagnostics.
- Strings are passed as `nint` pointer + `nuint` length (UTF-8). Use the `Utf8String` helper for conversions.
- Booleans use `[MarshalAs(UnmanagedType.U1)]`.

### Widget pattern (`OpenTui` layer)

- Widgets inherit from abstract `Widget` and override `Draw(NativeBuffer buffer, int offsetX, int offsetY)`.
- Layout is done via `Widget.Layout` (a `LayoutNode` wrapping Yoga.Net). Shortcut properties (`FlexGrow`, `Width`, `Padding`, …) are exposed directly on `Widget`.
- Child management uses `Add()` / `Remove()` / `Clear()`. `Widget` implements `IEnumerable<Widget>` for collection initializer syntax.
- Colors are `Rgba?` — null means "inherit from parent" (resolved via `ResolvedFg` / `ResolvedBg`).

### Renderable pattern (`OpenTui.Core` layer)

- `Renderable` extends `EventEmitter` and holds a Yoga `Node` directly.
- `IRenderContext` is the render-pass interface (hit grid, cursor, focus, selection, dimensions).
- Uses `#region` blocks to organize related fields and methods within large classes.

### CLI widgets (`OpenTui.Cli`)

- CLI widgets write to `ICliConsole` (not `System.Console` directly).
- Use `TestCliConsole` for unit testing — it captures all output in memory with ANSI codes stripped.
- CLI widgets have no dependency on the native DLL.

### Testing

- xUnit with `[Fact]` / `[Theory]`. Mocking via NSubstitute.
- Native-touching tests use `NativeRenderer.Create(cols, rows, testing: true)` to suppress terminal I/O.
- CLI widget tests inject `TestCliConsole` and assert against `console.Output` / `console.Lines`.

## Reference Implementations

OpenTUI-sharp is a C# port. **When in doubt about behaviour, always consult the Zig/TypeScript reference implementation — do not guess.**

| What | Location |
|------|----------|
| **openTUI (Zig + TypeScript)** | `C:\Users\stf\Source\Repos\opentui` — the canonical reference. Zig source is in `packages/core/src/zig/`, TypeScript wrappers in `packages/core/src/`. |
| **Yoga.Net** | `C:\Users\stf\Source\Repos\yoga.net` — source code for the Yoga.Net layout library used by this project. |
| **opencode** | `C:\Users\stf\Source\Repos\opencode` — a complex real-world application built on the reference TS/Zig implementation. Useful for understanding expected runtime behaviour and usage patterns. |

Key lookup patterns:
- Native FFI exports: `opentui/packages/core/src/zig/lib.zig`
- Text buffer internals: `opentui/packages/core/src/zig/text-buffer.zig`, `text-buffer-view.zig`
- TS renderable tree: `opentui/packages/core/src/renderables/`
- TS text buffer wrapper: `opentui/packages/core/src/text-buffer.ts`, `text-buffer-view.ts`

When a bug gets fixed, a non regression test should be added to the test suite.

for quick one-off experiments, remember file based apps in .NET 10: https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps

The examples directory contains a bunch of small demo apps which exercise different parts of the library. Most of the have equivalents in the reference implementation.