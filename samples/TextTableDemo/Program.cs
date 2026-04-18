using OpenTui.Core;

using static OpenTui.Core.Style;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    BackgroundColor = Rgba.Black,
    EnableMouseMovement = true,
    ExitOnCtrlC = true,
    TargetFps = 30,
});

var palette = new
{
    Bg = Rgba.Black,
    Panel = Rgba.FromHex("#0d0d0d"),
    Text = Rgba.FromHex("#f0f0f0"),
    Muted = Rgba.FromHex("#666666"),
    Soft = Rgba.FromHex("#bbbbbb"),
    Rose = Rgba.FromHex("#e8c97a"),
    Ember = Rgba.FromHex("#b8a0ff"),
    Flame = Rgba.White,
    Eye = Rgba.FromHex("#00d4aa"),
    Border = Rgba.FromHex("#2a2a2a"),
};

byte[] wrapModes = [0, 2, 1];
string[] wrapLabels = ["none", "word", "char"];
BorderStyle[] borderStyles = [BorderStyle.Single, BorderStyle.Rounded, BorderStyle.Double, BorderStyle.Heavy];
string[] borderLabels = ["single", "rounded", "double", "heavy"];
string[] columnWidthModes = ["content", "full"];
string[] columnFitters = ["proportional", "balanced"];
int[] cellPaddingValues = [0, 1, 2];

int contentIndex = 0;
int wrapIndex = 1;
int borderIndex = 0;
int columnWidthModeIndex = 0;
int columnFitterIndex = 0;
int cellPaddingIndex = 0;
bool borderEnabled = true;
bool outerBorderEnabled = true;
bool showBordersEnabled = true;

TextChunk[] Cell(params TextChunk[] chunks) => chunks;
TextChunk[] Plain(string text) => [TextChunk.Plain(text)];
TextChunk[] BoldCell(string text) => [TextChunk.Styled(text, attributes: TextAttributes.Bold)];

var primaryContentSets = new List<TextChunk[][][]>
{
    new TextChunk[][][]
    {
        new[] { BoldCell("Service"), BoldCell("Status"), BoldCell("Notes") },
        new[] { Plain("api"), Cell(TextChunk.Styled("OK", fg: palette.Eye)), Cell(TextChunk.Styled("latency", fg: palette.Muted), TextChunk.Plain(" 28ms")) },
        new[] { Plain("worker"), Cell(TextChunk.Styled("DEGRADED", fg: palette.Ember)), Plain("queue depth: 124") },
        new[] { Plain("billing"), Cell(TextChunk.Styled("ERROR", fg: palette.Flame)), Plain("retrying payment provider") },
    },
    new TextChunk[][][]
    {
        new[] { BoldCell("Region"), BoldCell("Requests"), BoldCell("Trend") },
        new[] { Plain("us-east-1"), Plain("1.2M"), Cell(TextChunk.Styled("+12.4%", fg: palette.Eye)) },
        new[] { Plain("eu-west-1"), Plain("890K"), Cell(TextChunk.Styled("+5.1%", fg: palette.Soft)) },
        new[] { Plain("ap-south-1"), Plain("540K"), Cell(TextChunk.Styled("-2.0%", fg: palette.Flame)) },
    },
    new TextChunk[][][]
    {
        new[] { BoldCell("Task"), BoldCell("Owner"), BoldCell("ETA") },
        new[]
        {
            Plain("Wrap regression in operational status dashboard with dynamic row heights and constrained layout validation"),
            Plain("core platform and runtime reliability squad"),
            Cell(TextChunk.Styled("done after validating none, word, and char wrap modes across narrow, medium, wide, and ultra-wide terminal widths", fg: palette.Eye)),
        },
        new[]
        {
            Plain("Unicode layout stabilization for mixed Latin, punctuation, symbols, and long identifiers in adjacent columns"),
            Plain("render pipeline maintainers with fallback shaping support"),
            Plain("in review with follow-up checks for border style transitions, cell padding variants, and selection range consistency"),
        },
        new[]
        {
            Plain("Snapshot pass for table rendering in content mode and full mode with heavy and double border combinations"),
            Plain("qa automation and visual diff triage group"),
            Plain("today pending final baseline updates for oversized fixtures that intentionally stress wrapping behavior on high-resolution terminals"),
        },
        new[]
        {
            Plain("Document edge cases where long tokens without spaces force char wrapping and reveal per-cell clipping regressions"),
            Plain("developer experience and docs tooling"),
            Plain("planned for this sprint once final reproducible examples are captured and linked to regression tracking tickets"),
        },
        new[]
        {
            Plain("Performance sweep of wrapping algorithm under large datasets to confirm stable frame times during rapid key toggling"),
            Plain("runtime performance task force"),
            Plain("scheduled after review, with benchmark runs on laptop and desktop terminals at 200-plus column widths"),
        },
    },
};

var unicodeContentSets = new List<TextChunk[][][]>
{
    new TextChunk[][][]
    {
        new[] { BoldCell("Locale"), BoldCell("Sample") },
        new[] { Plain("ja-JP"), Plain("東京の夜景と絵文字 🌃✨") },
        new[] { Plain("zh-CN"), Plain("你好世界，布局检查中 🚀") },
        new[] { Plain("ko-KR"), Plain("한글과 이모지 조합 테스트 😄") },
    },
    new TextChunk[][][]
    {
        new[] { BoldCell("Expression"), BoldCell("Meaning") },
        new[] { Plain("山川异域"), Plain("Different lands, shared sky 🌏") },
        new[] { Plain("꽃길만 걷자"), Plain("Walk only flower paths 🌸") },
        new[] { Plain("加油"), Plain("Keep pushing forward 💪") },
    },
    new TextChunk[][][]
    {
        new[] { BoldCell("Column"), BoldCell("Wrapped Text") },
        new[]
        {
            Plain("mixed-languages"),
            Plain("CJK and emoji wrapping stress case: こんにちは世界 and 안녕하세요 세계 and 你好，世界 followed by long English prose that keeps flowing to test whether each cell wraps naturally even when the terminal is extremely wide and the row still needs multiple visual lines for readability 🌍🚀"),
        },
        new[]
        {
            Plain("emoji-and-symbols"),
            Plain("Faces 😀😃😄😁😆 plus symbols 🧪📦🛰️🔧📊 mixed with version tags like release-candidate-build-2026-02-very-long-token-without-breaks to ensure char wrapping remains stable and no glyph alignment issues appear at column boundaries"),
        },
        new[]
        {
            Plain("long-cjk-phrase"),
            Plain("長文の日本語テキストと中文段落和한국어문장을連続して配置し、その後に additional English context describing renderer behavior, border intersection handling, and selection extraction so that this single cell remains a reliable wrapping torture test."),
        },
        new[]
        {
            Plain("mixed-punctuation"),
            Plain("Wrap behavior with punctuation-heavy content: [alpha]{beta}(gamma)<delta>|epsilon| then repeated fragments, commas, semicolons, and slashes to verify token boundaries do not break border drawing logic or spacing consistency in neighboring columns."),
        },
    },
};

var container = new BoxRenderable(renderer, new BoxOptions
{
    Id = "text-table-demo-container",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Percent(100),
    FlexDirection = FlexDirectionValue.Column,
    Padding = 1,
    Gap = 1,
    BackgroundColor = palette.Bg,
});
renderer.Root.Add(container);

var controlsText = new TextRenderable(renderer, new TextOptions
{
    Id = "text-table-demo-controls",
    Content = "",
    Fg = palette.Text,
    WrapMode = WrapMode.Word,
    Selectable = false,
});

var tableAreaScrollBox = new ScrollBoxRenderable(renderer, new ScrollBoxOptions
{
    Id = "text-table-demo-table-area-scroll",
    Width = DimensionValue.Percent(100),
    FlexGrow = 1,
    FlexShrink = 1,
    ScrollY = true,
    ScrollX = false,
    Border = false,
    BackgroundColor = palette.Bg,
    ContentOptions = new BoxOptions
    {
        FlexDirection = FlexDirectionValue.Column,
        Gap = 1,
    },
});

var primaryLabel = new TextRenderable(renderer, new TextOptions
{
    Id = "text-table-demo-primary-label",
    StyledContent = new StyledText(Bold("Operational Table")),
    Fg = palette.Ember,
    Selectable = false,
});

var primaryTable = new TextTableRenderable(renderer, new TextTableOptions
{
    Id = "text-table-demo-primary",
    Width = DimensionValue.Percent(100),
    WrapMode = wrapModes[wrapIndex],
    ColumnFitter = columnFitters[columnFitterIndex],
    ColumnWidthMode = columnWidthModes[columnWidthModeIndex],
    BorderStyle = borderStyles[borderIndex],
    BorderColor = palette.Ember,
    Fg = palette.Text,
    Bg = palette.Bg,
    BackgroundColor = palette.Bg,
    Content = primaryContentSets[contentIndex],
});

var unicodeLabel = new TextRenderable(renderer, new TextOptions
{
    Id = "text-table-demo-unicode-label",
    StyledContent = new StyledText(Bold("Unicode/CJK/Emoji Table")),
    Fg = palette.Rose,
    Selectable = false,
});

var unicodeTable = new TextTableRenderable(renderer, new TextTableOptions
{
    Id = "text-table-demo-unicode",
    Width = DimensionValue.Percent(100),
    WrapMode = wrapModes[wrapIndex],
    ColumnFitter = columnFitters[columnFitterIndex],
    ColumnWidthMode = columnWidthModes[columnWidthModeIndex],
    BorderStyle = borderStyles[borderIndex],
    BorderColor = palette.Rose,
    Fg = palette.Text,
    Bg = palette.Bg,
    BackgroundColor = palette.Bg,
    Content = unicodeContentSets[contentIndex],
});

var selectionBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "text-table-demo-selection-box",
    Width = DimensionValue.Percent(100),
    Height = 10,
    Border = true,
    BorderStyle = BorderStyle.Double,
    BorderColor = palette.Border,
    Title = "Selected Text",
    TitleAlignment = TitleAlignment.Left,
    Padding = 1,
    BackgroundColor = palette.Panel,
});

var selectionMetaText = new TextRenderable(renderer, new TextOptions
{
    Id = "text-table-demo-selection-meta",
    Content = "No selection yet",
    Fg = palette.Eye,
    Selectable = false,
});

var selectionScrollBox = new ScrollBoxRenderable(renderer, new ScrollBoxOptions
{
    Id = "text-table-demo-selection-scroll",
    Width = DimensionValue.Percent(100),
    FlexGrow = 1,
    FlexShrink = 1,
    ScrollY = true,
    ScrollX = false,
    Border = false,
    BackgroundColor = palette.Panel,
});

var selectionStatusText = new TextRenderable(renderer, new TextOptions
{
    Id = "text-table-demo-selection-text",
    Content = "",
    Fg = palette.Text,
    WrapMode = WrapMode.Word,
    Width = DimensionValue.Percent(100),
    Selectable = false,
});

selectionBox.Add(selectionMetaText);
selectionBox.Add(selectionScrollBox);
selectionScrollBox.Add(selectionStatusText);

tableAreaScrollBox.Add(controlsText);
tableAreaScrollBox.Add(primaryLabel);
tableAreaScrollBox.Add(primaryTable);
tableAreaScrollBox.Add(unicodeLabel);
tableAreaScrollBox.Add(unicodeTable);

container.Add(tableAreaScrollBox);
container.Add(selectionBox);

StyledText BuildControlsText()
{
    var builder = new StyledTextBuilder();
    builder.Bold("TextTable Demo");
    builder.Add("  ");
    builder.Styled("1/2/3 dataset • W wrap • B style • M width • F fitter • P padding • N inner • O outer • H draw • drag to select • C clear", fg: palette.Muted);
    builder.Add("\nCurrent: dataset ");
    builder.Styled($"{contentIndex + 1}", fg: palette.Soft);
    builder.Add(" | wrap ");
    builder.Styled(wrapLabels[wrapIndex], fg: palette.Rose);
    builder.Add(" | style ");
    builder.Styled(borderLabels[borderIndex], fg: palette.Ember);
    builder.Add(" | width ");
    builder.Styled(columnWidthModes[columnWidthModeIndex], fg: palette.Eye);
    builder.Add(" | fitter ");
    builder.Styled(columnFitters[columnFitterIndex], fg: palette.Rose);
    builder.Add(" | padding ");
    builder.Styled($"{cellPaddingValues[cellPaddingIndex]}", fg: palette.Soft);
    builder.Add(" | inner ");
    builder.Styled(borderEnabled ? "on" : "off", fg: palette.Rose);
    builder.Add(" | outer ");
    builder.Styled(outerBorderEnabled ? "on" : "off", fg: palette.Ember);
    builder.Add(" | draw ");
    builder.Styled(showBordersEnabled ? "on" : "off", fg: palette.Eye);
    return builder.Build();
}

void ClearSelectionStatus(string message)
{
    selectionMetaText.ContentText = message;
    selectionStatusText.ContentText = "";
    selectionScrollBox.ScrollTop = 0;
}

void ApplyTableState()
{
    primaryTable.Content = primaryContentSets[contentIndex];
    unicodeTable.Content = unicodeContentSets[contentIndex];

    primaryTable.WrapMode = wrapModes[wrapIndex];
    unicodeTable.WrapMode = wrapModes[wrapIndex];

    primaryTable.TableBorderStyle = borderStyles[borderIndex];
    unicodeTable.TableBorderStyle = borderStyles[borderIndex];

    primaryTable.ColumnWidthMode = columnWidthModes[columnWidthModeIndex];
    unicodeTable.ColumnWidthMode = columnWidthModes[columnWidthModeIndex];

    primaryTable.ColumnFitter = columnFitters[columnFitterIndex];
    unicodeTable.ColumnFitter = columnFitters[columnFitterIndex];

    primaryTable.CellPadding = cellPaddingValues[cellPaddingIndex];
    unicodeTable.CellPadding = cellPaddingValues[cellPaddingIndex];

    primaryTable.Border = borderEnabled;
    unicodeTable.Border = borderEnabled;

    primaryTable.OuterBorder = outerBorderEnabled;
    unicodeTable.OuterBorder = outerBorderEnabled;

    primaryTable.ShowBorders = showBordersEnabled;
    unicodeTable.ShowBorders = showBordersEnabled;

    controlsText.Content = BuildControlsText();
    renderer.RequestRender();
}

renderer.On<Selection>(RendererEventNames.Selection, selection =>
{
    string selectedText = selection.GetSelectedText();
    if (string.IsNullOrEmpty(selectedText))
    {
        ClearSelectionStatus("Empty selection");
        return;
    }

    int lineCount = selectedText.Split('\n').Length;
    int charCount = selectedText.Length;
    selectionMetaText.ContentText = $"Selected {lineCount} line{(lineCount == 1 ? "" : "s")} ({charCount} chars)";
    selectionStatusText.ContentText = selectedText;
    selectionScrollBox.ScrollTop = 0;
});

renderer.KeyInput.On("keypress", (KeyEvent key) =>
{
    if (key.Ctrl || key.Meta)
        return;

    switch (key.Name)
    {
        case "1":
        case "2":
        case "3":
            contentIndex = int.Parse(key.Name) - 1;
            ApplyTableState();
            break;
        case "w":
            wrapIndex = (wrapIndex + 1) % wrapModes.Length;
            ApplyTableState();
            break;
        case "b":
            borderIndex = (borderIndex + 1) % borderStyles.Length;
            ApplyTableState();
            break;
        case "m":
            columnWidthModeIndex = (columnWidthModeIndex + 1) % columnWidthModes.Length;
            ApplyTableState();
            break;
        case "f":
            columnFitterIndex = (columnFitterIndex + 1) % columnFitters.Length;
            ApplyTableState();
            break;
        case "p":
            cellPaddingIndex = (cellPaddingIndex + 1) % cellPaddingValues.Length;
            ApplyTableState();
            break;
        case "n":
            borderEnabled = !borderEnabled;
            ApplyTableState();
            break;
        case "o":
            outerBorderEnabled = !outerBorderEnabled;
            ApplyTableState();
            break;
        case "h":
            showBordersEnabled = !showBordersEnabled;
            ApplyTableState();
            break;
        case "c":
            renderer.ClearSelection();
            ClearSelectionStatus("Selection cleared");
            renderer.RequestRender();
            break;
    }
});

ApplyTableState();
await Task.Delay(Timeout.Infinite);
