# OpenTUI-sharp — Reference & Project Notes

## Original Reference Implementation

- **GitHub**: <https://github.com/anomalyco/opentui>
- **Local clone**: `C:\Users\stf\Source\Repos\OpenTUI`
- **npm package** (native DLL source): `@opentui/core-win32-x64@0.1.100`

### Key source files in the reference repo

| File | What it contains |
|---|---|
| `packages/core/src/zig.ts` | Ground truth FFI definitions — all ~110 C ABI function signatures |
| `packages/core/src/zig/lib.zig` | Zig exported functions (the actual native implementation) |
| `packages/core/src/zig/renderer.zig` | Native renderer: setupTerminal, render loop, shutdown sequence |
| `packages/core/src/buffer.ts` | TypeScript buffer wrapper: `drawBox`, `drawText`, `packDrawOptions` |
| `packages/core/src/renderer.ts` | TS renderer: env var forwarding, render loop, resize handling |
| `packages/core/src/lib/border.ts` | Border character arrays and ordering conventions |
| `packages/core/src/renderable.ts` | Base widget class with `render()` / `renderSelf()` pattern |
| `packages/core/src/widgets/Box.ts` | Box widget — reference for `renderSelf()` with drawBox |

## Architecture

Three-layer design:

1. **OpenTui.Native** — Raw P/Invoke bindings (`LibraryImport`) to the native Zig DLL's C ABI. SafeHandle types for renderer, buffer, hit grid, etc.
2. **OpenTui** — High-level C# widget system. Full-screen TUI widgets (Box, Text, Progress, Spinner, etc.) using Yoga.Net for flexbox layout, plus inline CLI widgets (CliPanel, CliTable, CliSparkline, etc.) for non-fullscreen use.
3. **Samples** — WidgetShowcase and CliDemo.

## Important Technical Details

### Native DLL resolution
The managed assembly `OpenTui.dll` collides with the native `opentui.dll` on case-insensitive Windows. A `NativeLibrary.SetDllImportResolver` in `OpenTuiNative`'s static constructor resolves to `runtimes/win-x64/native/opentui.dll`.

### createRenderer parameters
```
createRenderer(u32 width, u32 height, bool testing, bool remote)
```
- `testing=true` suppresses ALL terminal output (test mode) — must be `false` for real rendering
- `remote=true` is for remote rendering — must be `false` for local terminal

### Console code page (Windows)
The native Zig renderer writes raw UTF-8 bytes to stdout. .NET defaults `Console.OutputEncoding` to the system OEM code page (typically 437). `App.Run()` calls `SetConsoleOutputCP(65001)` before rendering and restores the original code page on exit. This is the .NET equivalent of what Node.js/Bun do automatically.

### Packed bitfield for drawBox options
```
Bits 0-3:  Border sides (bit3=top, bit2=right, bit1=bottom, bit0=left). All sides = 0b1111 = 15.
Bit 4:     shouldFill flag
Bits 5-6:  title alignment (0=left, 1=center, 2=right)
Bits 7-8:  bottom title alignment
```

### Environment variable forwarding
The native renderer needs terminal capability env vars forwarded via `setTerminalEnvVar()`. The full list is in `NativeRenderer.ForwardEnvironment()` and matches the TypeScript `DEFAULT_FORWARDED_ENV_KEYS`.

### Border character ordering (for `drawBox` codepoints array)
TopLeft, TopRight, BottomLeft, BottomRight, Horizontal, Vertical, TopT, BottomT, LeftT, RightT, Cross (11 codepoints).

## Dependencies

- **.NET 10 / C# 14**
- **Yoga.Net 3.2.3** — Pure C# flexbox layout (namespace `Facebook.Yoga`)
- **Native DLL** — Installed via npm: `npm install @opentui/core-win32-x64@0.1.100`, copied to `runtimes/win-x64/native/`

## P/Invoke Audit

All ~110 exported functions were audited against `zig.ts` ground truth. Key fixes applied:
- `BufferDrawText`, `BufferDrawBox`, `BufferDrawChar` — parameter order/semantics corrected
- `LinkAlloc` — changed from `(linkStore, urlHash)` to `(urlPtr, urlLen)`
- `DumpBuffers`/`DumpStdoutBuffer` — param renamed `fd` → `timestamp`
- 9× HitGrid functions — first param renamed `hitGrid` → `renderer`
- `destroyHitGrid`/`destroyLinkStore` — removed (no native exports; renderer-owned)

## Build & Test

```powershell
dotnet build        # builds all projects
dotnet test         # 109 tests (108 OpenTui.Tests + 1 OpenTui.Native.Tests)
```

## Running

```powershell
# Full-screen TUI demo
.\samples\WidgetShowcase\bin\Debug\net10.0\WidgetShowcase.exe

# Inline CLI widgets demo
.\samples\CliDemo\bin\Debug\net10.0\CliDemo.exe
```
