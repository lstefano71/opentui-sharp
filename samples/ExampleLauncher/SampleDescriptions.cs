namespace ExampleLauncher;

/// <summary>
/// Human-readable descriptions for known sample projects.
/// Sourced from the TS reference implementation's example registry.
/// </summary>
internal static class SampleDescriptions
{
    private static readonly Dictionary<string, string> Descriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ASCIIFontDemo"] = "ASCII font rendering with various colors and text",
        ["ASCIIFontSelection"] = "Text selection with ASCII fonts — precise character-level selection",
        ["CliDemo"] = "Inline CLI widgets: tables, panels, figlet text, rules, progress bars",
        ["CodeDemo"] = "Code viewer with line numbers, diff highlights, and diagnostics",
        ["ConsoleDemo"] = "Interactive console logging with clickable buttons for log levels",
        ["DiffDemo"] = "Unified and split diff views with syntax highlighting and themes",
        ["EditorDemo"] = "Interactive text editor with full editing capabilities",
        ["ExtmarksDemo"] = "Virtual extmarks — text ranges the cursor jumps over, with deletion handling",
        ["FocusRestore"] = "Focus restore — alt-tab away and back to verify mouse tracking resumes",
        ["FullUnicode"] = "Draggable boxes and background filled with complex graphemes",
        ["HelloOpenTui"] = "Minimal full-screen TUI app with a bordered box",
        ["InputDemo"] = "Interactive input demo with validation and multiple fields",
        ["InputSelectLayout"] = "Interactive layout with input and select elements",
        ["KeypressDebug"] = "Debug tool to inspect keypress events, raw input, and terminal capabilities",
        ["LinkDemo"] = "Hyperlink support with OSC 8 — clickable links and link inheritance",
        ["LiveStateDemo"] = "Automatic renderer lifecycle management with live renderables",
        ["MarkdownDemo"] = "Markdown rendering with table alignment, syntax highlighting, and themes",
        ["MouseInteraction"] = "Interactive mouse trails and clickable cells demonstration",
        ["NestedZIndex"] = "Z-index behavior with nested render objects",
        ["OpacityExample"] = "Box opacity and transparency effects with animated transitions",
        ["RelativePositioning"] = "Child positions relative to their parent containers",
        ["ScrollExample"] = "Scrollable container with customization",
        ["SelectDemo"] = "Interactive select demo with customizable options",
        ["SimpleLayout"] = "Flex layout system with multiple configurations",
        ["SliderDemo"] = "Interactive slider components with various orientations",
        ["SplitModeDemo"] = "Renderer confined to bottom area with normal terminal output above",
        ["StickyScroll"] = "ScrollBox with sticky scroll — maintains position at borders when content changes",
        ["StyledTextDemo"] = "Template literals with styled text, colors, and formatting",
        ["TabSelectDemo"] = "Tab selection demo with horizontal tab bar",
        ["TerminalPalette"] = "Terminal color palette detection and visualization — all 256 colors",
        ["TextNodeDemo"] = "TextNode API for building complex styled text structures",
        ["TextSelectionDemo"] = "Text selection across multiple renderables with mouse drag",
        ["TextTableDemo"] = "TextTable renderable with styled chunks, Unicode content, and wrap/border toggles",
        ["TextTruncation"] = "Middle truncation with ellipsis — toggle with 'T' key, resize to test",
        ["TextWrap"] = "Text wrapping example",
        ["TimelineExample"] = "Animation timeline system",
        ["TransparencyDemo"] = "Alpha blending and transparency effects demonstration",
        ["VNodeComposition"] = "Declarative Box(Box(Box(children))) composition",
        ["WideGraphemeOverlay"] = "Drag transparent boxes over CJK/emoji, toggle dimming with D key",
        ["WidgetShowcase"] = "All-in-one widget demo with multiple TUI widgets",
    };

    public static string Get(string directoryName) =>
        Descriptions.TryGetValue(directoryName, out var desc) ? desc : "";
}
