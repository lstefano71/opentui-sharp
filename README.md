# OpenTUI-sharp

C# 14 / .NET 10 bindings for [OpenTUI](https://github.com/anomalyco/opentui) — a high-performance terminal UI engine written in Zig.

## Architecture

| Layer | Package | Description |
|-------|---------|-------------|
| **Native** | `OpenTui.Native` | 1:1 P/Invoke bindings for ~110 C ABI functions. SafeHandle types for all opaque pointers. AOT-compatible. |
| **Widgets** | `OpenTui` | Full-screen TUI widget system (Box, Text, Input, Select, ScrollView, SplitPane, Dialog, etc.) with Yoga flexbox layout. |
| **CLI** | `OpenTui` | Inline CLI widgets for regular console apps — tables, progress bars, spinners, prompts, figlet text (Spectre.Console-style). |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js / npm](https://nodejs.org/) (to fetch the native DLL)
- Windows x64 (native binary currently win-x64 only)

## Quick Start

```bash
# Clone and restore
git clone <repo-url>
cd openTUI-sharp
npm install          # downloads opentui.dll from npm
dotnet build         # MSBuild copies DLL to runtimes/win-x64/native/
dotnet test          # run all tests
```

## Project Structure

```
openTUI-sharp/
├── src/
│   ├── OpenTui.Native/         # P/Invoke bindings + SafeHandles
│   │   └── Handles/            # SafeHandle types (RendererHandle, BufferHandle, etc.)
│   └── OpenTui/                # Widget library
│       ├── Enums/              # CursorStyle, TextAttribute, BorderStyle, etc.
│       ├── Types/              # Rgba, StyledText, BorderCharacters, etc.
│       ├── Layout/             # Yoga flexbox integration
│       ├── Widgets/            # TUI widgets (Box, Text, Input, Select, ...)
│       └── Cli/                # Inline CLI widgets (CliTable, CliProgress, ...)
├── tests/
│   ├── OpenTui.Native.Tests/   # Native binding tests
│   └── OpenTui.Tests/          # Widget + type tests
├── samples/
│   ├── HelloOpenTui/           # Minimal full-screen app
│   ├── WidgetShowcase/         # All widgets demo
│   └── CliDemo/                # CLI widget examples
├── docs/                       # Architecture & API documentation
├── package.json                # npm dependency for native DLL
└── openTUI-sharp.slnx          # Solution file
```

## Native DLL

The native `opentui.dll` is sourced from the [`@opentui/core-win32-x64`](https://www.npmjs.com/package/@opentui/core-win32-x64) npm package (v0.1.100). The MSBuild target in `OpenTui.Native.csproj` automatically copies it from `node_modules/` to `runtimes/win-x64/native/` at build time.

## API Examples

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

```csharp
using OpenTui;

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

## AOT Compatibility

Both `OpenTui.Native` and `OpenTui` are marked `IsAotCompatible=true` and use source-generated `[LibraryImport]` for P/Invoke — no runtime marshalling code generation required.

## License

MIT
