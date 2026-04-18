// Editor Demo — reference-aligned TextareaRenderable showcase
using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 60,
    BackgroundColor = Rgba.FromHex("#0D1117"),
});

const string InitialContent = """
    Welcome to the TextareaRenderable Demo!

    This is an interactive text editor powered by EditBuffer and EditorView.

    	This is a tab
    			Multiple tabs

    Emojis:
    👩🏽‍💻  👨‍👩‍👧‍👦  🏳️‍🌈  🇺🇸  🇩🇪  🇯🇵  🇮🇳

    NAVIGATION:
      • Arrow keys to move cursor
      • Ctrl+A/Ctrl+E for line start/end
      • Home/End for buffer start/end
      • Ctrl+F/Ctrl+B to move right/left (Emacs-style)
      • Alt+F/Alt+B for word forward/backward
      • Alt+Left/Alt+Right for word forward/backward
      • Ctrl+Left/Ctrl+Right for word forward/backward
      • Alt+A/Alt+E for visual line start/end

    SELECTION:
      • Shift+Arrow keys to select
      • Ctrl+Shift+A/E to select to line start/end
      • Shift+Home/End to select to buffer start/end
      • Alt+Shift+F/B to select word forward/backward
      • Alt+Shift+Left/Right to select word forward/backward
      • Alt+Shift+A/E to select to visual line start/end

    EDITING:
      • Type any text to insert
      • Backspace/Delete to remove text
      • Enter to create new lines
      • Ctrl+Shift+D to delete current line
      • Ctrl+D to delete character forward
      • Ctrl+K to delete to line end
      • Ctrl+U to delete to line start
      • Alt+D to delete word forward
      • Alt+Backspace or Ctrl+W to delete word backward
      • Ctrl+Delete or Alt+Delete to delete word forward

    UNDO/REDO:
      • Ctrl+- to undo
      • Ctrl+. to redo

    VIEW:
      • Shift+W to toggle wrap mode (word/char/none)
      • Shift+L to toggle line numbers
      • Shift+H to toggle diff highlights (colors + +/- signs)
      • Shift+D to toggle diagnostics (error/warning/info emojis)
      • Ctrl+] to increase scroll speed
      • Ctrl+[ to decrease scroll speed

    FEATURES:
      ✓ Grapheme-aware cursor movement
      ✓ Unicode (emoji 🌟 and CJK 世界, 你好世界, 中文, 한글)
      ✓ Incremental editing
      ✓ Text wrapping and viewport management
      ✓ Undo/redo support
      ✓ Word-based navigation and deletion
      ✓ Text selection with shift keys
    """;

byte wrapMode = 2; // 0=none, 1=char, 2=word
bool highlightsEnabled = false;
bool diagnosticsEnabled = false;

var diffLines = new (int Line, bool Added)[]
{
    (2, true), (5, false), (8, true), (11, false), (14, true), (17, false),
    (20, true), (23, false), (27, true), (30, false), (34, true), (38, false),
    (42, true), (46, false), (50, true), (54, false), (58, true),
};

var diagnosticLines = new (int Line, string Before, Rgba Color)[]
{
    (0, "❌", Rgba.FromHex("#ef4444")),
    (4, "⚠️", Rgba.FromHex("#f59e0b")),
    (10, "💡", Rgba.FromHex("#3b82f6")),
    (25, "❌", Rgba.FromHex("#ef4444")),
    (40, "⚠️", Rgba.FromHex("#f59e0b")),
    (52, "💡", Rgba.FromHex("#3b82f6")),
};

var parentContainer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "parent-container",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Percent(100),
    Padding = DimensionValue.Point(1),
    FlexGrow = 1,
});
renderer.Root.Add(parentContainer);

var editorBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "editor-box",
    Border = true,
    BorderStyle = BorderStyle.Single,
    BorderColor = Rgba.FromHex("#6BCF7F"),
    BackgroundColor = Rgba.FromHex("#0D1117"),
    Title = "Interactive Editor (TextareaRenderable)",
    TitleAlignment = TitleAlignment.Left,
    Width = DimensionValue.Percent(100),
    FlexGrow = 1,
});
parentContainer.Add(editorBox);

var editor = new TextareaRenderable(renderer, new TextareaOptions
{
    Id = "editor",
    InitialValue = InitialContent,
    TextColor = Rgba.FromHex("#F0F6FC"),
    BackgroundColor = Rgba.FromHex("#0D1117"),
    FocusedBackgroundColor = Rgba.FromHex("#0D1117"),
    FocusedTextColor = Rgba.FromHex("#F0F6FC"),
    SelectionBg = Rgba.FromHex("#264F78"),
    SelectionFg = Rgba.FromHex("#FFFFFF"),
    WrapMode = wrapMode,
    ShowCursor = true,
    CursorColor = Rgba.FromHex("#4ECDC4"),
    Placeholder = "Enter text here...",
    PlaceholderColor = Rgba.FromHex("#333333"),
    TabIndicator = "→",
    TabIndicatorColor = Rgba.FromHex("#30363D"),
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Percent(100),
    FlexGrow = 1,
    Buffered = true,
});

var editorWithLines = new LineNumberRenderable(renderer, new LineNumberOptions
{
    Id = "editor-lines",
    Target = editor,
    MinWidth = 3,
    PaddingRight = 1,
    Fg = Rgba.FromHex("#6b7280"),
    Bg = Rgba.FromHex("#161b22"),
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Percent(100),
    FlexGrow = 1,
});
editorBox.Add(editorWithLines);

var statusText = new TextRenderable(renderer, new TextOptions
{
    Id = "status",
    Content = "",
    Fg = Rgba.FromHex("#A5D6FF"),
    Height = DimensionValue.Point(1),
    Width = DimensionValue.Percent(100),
});
parentContainer.Add(statusText);

editor.Focus();

renderer.AddFrameCallback(async _ =>
{
    var cursor = editor.LogicalCursor;
    string wrap = editor.WrapMode == 0 ? "OFF" : "ON";
    string diff = highlightsEnabled ? "ON" : "OFF";
    string diagnostics = diagnosticsEnabled ? "ON" : "OFF";
    statusText.ContentText =
        $"Line {cursor.Row + 1}, Col {cursor.Col + 1} | Wrap: {wrap} | Diff: {diff} | Diag: {diagnostics} | Scroll: {editor.ScrollSpeed:0} lines/s";
    await Task.CompletedTask;
});

renderer.KeyInput.On("keypress", (KeyEvent key) =>
{
    if (key.Shift && string.Equals(key.Name, "l", StringComparison.OrdinalIgnoreCase))
    {
        key.PreventDefault();
        editorWithLines.ShowLineNumbers = !editorWithLines.ShowLineNumbers;
        return;
    }

    if (key.Shift && string.Equals(key.Name, "w", StringComparison.OrdinalIgnoreCase))
    {
        key.PreventDefault();
        wrapMode = wrapMode switch
        {
            2 => (byte)1,
            1 => (byte)0,
            _ => (byte)2,
        };
        editor.WrapMode = wrapMode;
        return;
    }

    if (key.Shift && string.Equals(key.Name, "h", StringComparison.OrdinalIgnoreCase))
    {
        key.PreventDefault();
        highlightsEnabled = !highlightsEnabled;
        ApplyLineDecorations();
        return;
    }

    if (key.Shift && string.Equals(key.Name, "d", StringComparison.OrdinalIgnoreCase))
    {
        key.PreventDefault();
        diagnosticsEnabled = !diagnosticsEnabled;
        ApplyLineDecorations();
        return;
    }

    if (key.Ctrl && (key.Name == "pageup" || key.Name == "pagedown"))
    {
        key.PreventDefault();
        if (key.Name == "pageup")
            editor.GotoBufferHome();
        else
            editor.GotoBufferEnd();
        return;
    }

    if (key.Ctrl && key.Name == "]")
    {
        key.PreventDefault();
        editor.ScrollSpeed = Math.Min(100, editor.ScrollSpeed + 4);
        return;
    }

    if (key.Ctrl && key.Name == "[")
    {
        key.PreventDefault();
        editor.ScrollSpeed = Math.Max(4, editor.ScrollSpeed - 4);
    }
});

void ApplyLineDecorations()
{
    editorWithLines.ClearAllLineColors();
    editorWithLines.SetLineSigns([]);

    if (highlightsEnabled)
    {
        foreach (var (line, added) in diffLines)
        {
            var background = Rgba.FromHex(added ? "#1a4d1a" : "#4d1a1a");
            var signColor = Rgba.FromHex(added ? "#22c55e" : "#ef4444");
            editorWithLines.SetLineColor(line, new LineColorConfig(background, background, null));
            MergeLineSign(line, added ? " +" : " -", afterColor: signColor);
        }
    }

    if (diagnosticsEnabled)
    {
        foreach (var (line, before, color) in diagnosticLines)
            MergeLineSign(line, before: before, beforeColor: color);
    }
}

void MergeLineSign(int line, string? before = null, Rgba? beforeColor = null, string? after = null, Rgba? afterColor = null)
{
    editorWithLines.GetLineSigns().TryGetValue(line, out var existing);
    editorWithLines.SetLineSign(line, new LineSign(
        Before: before ?? existing.Before,
        After: after ?? existing.After,
        BeforeColor: beforeColor ?? existing.BeforeColor,
        AfterColor: afterColor ?? existing.AfterColor,
        Bg: existing.Bg,
        Fg: existing.Fg));
}

renderer.RequestRender();
await Task.Delay(Timeout.Infinite);
