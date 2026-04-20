# OpenTUI-sharp

C# 14 / .NET 10 bindings for [OpenTUI](https://github.com/anomalyco/opentui) — a high-performance terminal UI engine written in Zig.

## Architecture

OpenTUI-sharp is organized as a three-layer stack. Each layer builds on the one below it:

```
┌──────────────────────────────────────────────────┐
│  OpenTui                                         │
│  High-level API: Yoga flexbox layout, widget     │
│  tree, TUI widgets, CLI widgets                  │
├──────────────────────────────────────────────────┤
│  OpenTui.Native                                  │
│  P/Invoke bindings to opentui.dll (~110 funcs),  │
│  SafeHandle types for all opaque pointers        │
├──────────────────────────────────────────────────┤
│  opentui.dll  (native Zig library)               │
└──────────────────────────────────────────────────┘
```

| Layer | Package | Description |
|-------|---------|-------------|
| **Native** | `OpenTui.Native` | 1:1 P/Invoke bindings for ~110 C ABI functions across 11 regions (Renderer, Buffer, TextBuffer, EditorView, EditBuffer, HitGrid, Link, SyntaxStyle, Unicode, SpanFeed, Diagnostics). SafeHandle types for all opaque pointers. AOT-compatible via `[LibraryImport]`. |
| **Widgets** | `OpenTui` | Full-screen TUI widget system — Box, Text, TextInput, TextArea, Select, ScrollBox, SplitPane, Dialog, Table, Accordion, and more — with Yoga flexbox layout. |
| **CLI** | `OpenTui.Cli` | Inline CLI widgets for regular console apps — tables, progress bars, spinners, prompts, figlet text (Spectre.Console-style). These work without the native DLL. |

### Native Layer (`OpenTui.Native`)

The native layer provides direct access to every opentui C function. Methods are organized into `#region` groups corresponding to native subsystems. Each `nint` handle is wrapped in a `SafeHandle` subclass for deterministic cleanup:

| SafeHandle | Wraps |
|------------|-------|
| `RendererHandle` | Terminal renderer |
| `BufferHandle` | Optimized cell buffer |
| `TextBufferHandle` | Read-only text buffer |
| `TextBufferViewHandle` | Scrollable text view |
| `EditorViewHandle` | Editable text view |
| `EditBufferHandle` | Editable text buffer (rope) |
| `SyntaxStyleHandle` | Syntax highlight registry |
| `HitGridHandle` | Mouse hit-test grid |
| `LinkHandle` | Hyperlink store |
| `SpanFeedHandle` | Streaming span processor |

### Widget Layer (`OpenTui`)

Widgets inherit from the abstract `Widget` base class and participate in Yoga flexbox layout. The `App` class hosts the render loop and input dispatch.

### CLI Layer (`OpenTui.Cli`)

CLI widgets write directly to the console using ANSI escape codes. They are self-contained, require no native DLL, and are ideal for CLI tools that want rich output (similar to Spectre.Console).

## Widget Catalog

### TUI Widgets (full-screen, require native DLL)

| Widget | Description |
|--------|-------------|
| `Box` | Container with optional borders — the primary building block for layouts |
| `Text` | Styled text display |
| `TextInput` | Single-line text input with cursor and placeholder support |
| `TextArea` | Multi-line text editor with cursor tracking |
| `Select<T>` | Vertical list picker with keyboard navigation |
| `TabSelect<T>` | Horizontal tab bar selector |
| `Table` | Data table with column headers and row selection |
| `ScrollBox` | Scrollable container that clips children to a viewport |
| `ScrollBar` | Standalone scrollbar for position indication |
| `SplitPane` | Resizable two-panel layout (horizontal or vertical) |
| `Dialog` | Modal overlay with title, content, and action buttons |
| `Accordion` | Vertically stacked expandable/collapsible sections |
| `ContextMenu` | Right-click popup menu with nested sub-menus |
| `CommandPalette` | Fuzzy-search command picker (VS Code Ctrl+Shift+P style) |
| `Progress` | Progress bar with percentage label |
| `Spinner` | Animated Unicode spinner for ongoing operations |
| `StatusBar` | Bottom bar with left/right-aligned sections |
| `Toast` | Auto-dismissing notification popup (info/success/warning/error) |
| `FrameBuffer` | Raw pixel drawing surface for custom rendering |
| `App` | Application host — render loop, input dispatch, focus management |
| `FocusManager` | Tab/Shift+Tab keyboard focus navigation |

### CLI Widgets (inline, no native DLL needed)

| Widget | Description |
|--------|-------------|
| `CliTable` | Bordered table with columns, rows, and optional title |
| `CliSelect<T>` | Interactive arrow-key select prompt |
| `CliPrompt<T>` | Text prompt with default value and validation |
| `CliConfirm` | Yes/no confirmation prompt |
| `CliProgress` | Multi-task progress bar with async support |
| `CliStatus` | Spinner with status text for long operations |
| `CliPanel` | Bordered panel with content text |
| `CliRule` | Horizontal rule with optional centered title |
| `CliFiglet` | Large ASCII art text (block font) |
| `CliLive` | Live-updating console region for dynamic content |
| `AnsiConsole` | Default `ICliConsole` implementation using ANSI codes |
| `TestCliConsole` | In-memory console for unit testing CLI widgets |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js / npm](https://nodejs.org/) (to fetch the native DLL)
- Windows x64 (native binary currently win-x64 only)

## Quick Start

### Setup

```bash
git clone <repo-url>
cd openTUI-sharp
npm install          # downloads opentui.dll from npm
dotnet build         # MSBuild copies DLL to runtimes/win-x64/native/
dotnet test          # run all tests
```

### Full-screen TUI App

```csharp
using OpenTui;

using var app = new App(new AppOptions { TargetFps = 30 });

app.Root.Add(new Box {
    BorderStyle = BorderStyle.Rounded,
    Padding = 1,
    Children = {
        new Text("Hello, OpenTUI!") { Fg = Rgba.FromHex("#FFFF00") },
    },
});

app.Run();
```

### CLI Widgets (Spectre-style)

CLI widgets work in any console app — no native DLL or `App` host required:

```csharp
using OpenTui.Cli;

// Rich table
new CliTable()
    .AddColumn("Name", "Age", "City")
    .AddRow("Alice", "30", "NYC")
    .AddRow("Bob", "25", "London")
    .Write();

// Interactive select
var color = new CliSelect<string>("Pick a color")
    .AddChoices("Red", "Blue", "Green")
    .Prompt();

// Progress bar
await new CliProgress()
    .AddTask("Downloading", maxValue: 100)
    .RunAsync(async ctx => {
        while (!ctx.IsFinished) {
            ctx.Tasks[0].Increment(10);
            await Task.Delay(200);
        }
    });
```

## Project Structure

```
openTUI-sharp/
├── src/
│   ├── OpenTui.Native/             # P/Invoke bindings + SafeHandles
│   │   ├── OpenTuiNative.cs        # ~110 [LibraryImport] declarations (11 regions)
│   │   └── Handles/                # SafeHandle types (RendererHandle, BufferHandle, etc.)
│   └── OpenTui/                    # Widget library
│       ├── Enums/                  # CursorStyle, TextAttribute, BorderStyle, etc.
│       ├── Types/                  # Rgba, StyledText, BorderCharacters, etc.
│       ├── Layout/                 # Yoga flexbox integration (LayoutNode, FlexDirection, etc.)
│       ├── Widgets/                # TUI widgets (Box, Text, TextInput, Select, Dialog, ...)
│       ├── Cli/                    # Inline CLI widgets (CliTable, CliProgress, CliSelect, ...)
│       ├── Events/                 # Event types
│       └── Input/                  # Input handling
├── tests/
│   ├── OpenTui.Native.Tests/       # Native binding tests
│   └── OpenTui.Tests/              # Widget + type tests
├── samples/
│   ├── HelloOpenTui/               # Minimal full-screen app
│   ├── WidgetShowcase/             # All widgets demo
│   └── CliDemo/                    # CLI widget examples
├── docs/                           # Architecture & API documentation
├── package.json                    # npm dependency for native DLL
└── openTUI-sharp.slnx              # Solution file
```

## OpenTui.Core documentation

The developer-oriented `OpenTui.Core` guide now lives under [`docs/opentui-core`](docs/opentui-core/index.md), with:

- getting-started guidance
- an end-to-end tutorial
- concept and how-to pages
- subsystem reference pages covering the full public `OpenTui.Core` surface

## Native DLL

The native `opentui.dll` is sourced from the [`@opentui/core-win32-x64`](https://www.npmjs.com/package/@opentui/core-win32-x64) npm package (v0.1.100). The MSBuild target in `OpenTui.Native.csproj` automatically copies it from `node_modules/` to `runtimes/win-x64/native/` at build time.

## AOT Compatibility

Both `OpenTui.Native` and `OpenTui` are marked `IsAotCompatible=true` and use source-generated `[LibraryImport]` for P/Invoke — no runtime marshalling code generation required.

## License

MIT
