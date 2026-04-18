// Text Truncation Demo — shows truncation and wrap toggling side by side
using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 30,
});

bool truncateEnabled = true;
int wrapIndex = 0;
WrapMode[] wrapModes = [WrapMode.None, WrapMode.Char, WrapMode.Word];
string[] wrapLabels = ["None", "Char", "Word"];

string[] labels =
[
    "Single-line",
    "Multi-line",
    "Unicode",
    "Long path",
    "Styled text",
];

string[] texts =
[
    "The quick brown fox jumps over the lazy dog and keeps running far into the distance beyond the horizon.",

    "First line of a multi-line paragraph.\n"
    + "Second line continues with more detail about the topic.\n"
    + "Third line wraps up the explanation with a final thought that extends beyond the visible area.",

    "Héllo wörld! 🎉🚀💻🌍✨ Ünïcödé tëxt with spëcial characters: ß, ø, å, ñ — "
    + "Greek: αβγδε — Cyrillic: абвгд — Math: ∑∏∫∂√∞ — Arrows: ←→↑↓",

    "C:\\Users\\developer\\Projects\\openTUI-sharp\\src\\OpenTui.Core\\Widgets\\TextBufferRenderable.cs",

    "Bold and italic text demonstration with various styled segments that should be truncated properly when enabled.",
];

// --- Header ---
var header = new BoxRenderable(renderer, new BoxOptions
{
    Id = "header",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#b45309"),
    BorderStyle = BorderStyle.Rounded,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    Border = true,
});
var headerText = new TextRenderable(renderer, new TextOptions
{
    Id = "header-text",
    Content = "Text Truncation Demo",
    Fg = Rgba.FromInts(255, 255, 255),
});
header.Add(headerText);

// --- Two-column layout ---
var columns = new BoxRenderable(renderer, new BoxOptions
{
    Id = "columns",
    Width = DimensionValue.Auto,
    FlexGrow = 1,
    FlexDirection = FlexDirectionValue.Row,
    Gap = 1,
    Padding = DimensionValue.Point(1),
});

// Helper: create a column with text items
(BoxRenderable col, TextRenderable[] items) MakeColumn(string id, string title, bool truncate)
{
    var col = new BoxRenderable(renderer, new BoxOptions
    {
        Id = id,
        FlexGrow = 1,
        Border = true,
        BorderStyle = BorderStyle.Rounded,
        BorderColor = truncate ? Rgba.FromHex("#f59e0b") : Rgba.FromHex("#6b7280"),
        BackgroundColor = Rgba.FromHex("#1c1917"),
        Title = title,
    });

    var itemList = new TextRenderable[labels.Length];
    for (int i = 0; i < labels.Length; i++)
    {
        // Label
        var lbl = new TextRenderable(renderer, new TextOptions
        {
            Id = $"{id}-label-{i}",
            Content = $"— {labels[i]} —",
            Fg = Rgba.FromHex("#a3a3a3"),
            WrapMode = WrapMode.None,
        });
        col.Add(lbl);

        // Text item
        var txt = new TextRenderable(renderer, new TextOptions
        {
            Id = $"{id}-text-{i}",
            Content = texts[i],
            Fg = Rgba.FromHex("#e5e7eb"),
            WrapMode = wrapModes[wrapIndex],
            Truncate = truncate,
        });
        col.Add(txt);

        // Spacer
        var spacer = new BoxRenderable(renderer, new BoxOptions
        {
            Id = $"{id}-spacer-{i}",
            Width = DimensionValue.Auto,
            Height = DimensionValue.Point(1),
        });
        col.Add(spacer);

        itemList[i] = txt;
    }

    return (col, itemList);
}

var (leftCol, leftItems) = MakeColumn("left", "Truncated", true);
var (rightCol, rightItems) = MakeColumn("right", "Full Text", false);
columns.Add(leftCol);
columns.Add(rightCol);

// --- Footer ---
var footer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "footer",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#1e3a5f"),
    BorderStyle = BorderStyle.Rounded,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    Border = true,
});
var footerText = new TextRenderable(renderer, new TextOptions
{
    Id = "footer-text",
    Content = "",
    Fg = Rgba.FromHex("#94a3b8"),
});
footer.Add(footerText);

// --- Build tree ---
renderer.Root.Add(header);
renderer.Root.Add(columns);
renderer.Root.Add(footer);

void UpdateDisplay()
{
    string truncStr = truncateEnabled ? "ON" : "OFF";
    string wrapStr = wrapLabels[wrapIndex];
    headerText.ContentText = $"Text Truncation Demo — Truncate: {truncStr} — Wrap: {wrapStr}";
    footerText.ContentText = $"[t] Toggle truncation ({truncStr})  [w] Cycle wrap ({wrapStr})  [Ctrl+C] Quit";

    for (int i = 0; i < leftItems.Length; i++)
    {
        leftItems[i].Truncate = truncateEnabled;
        leftItems[i].WrapMode = wrapModes[wrapIndex];
        rightItems[i].WrapMode = wrapModes[wrapIndex];
    }
}

// --- Key handling ---
renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    switch (e.Name)
    {
        case "t":
            truncateEnabled = !truncateEnabled;
            UpdateDisplay();
            renderer.RequestRender();
            break;
        case "w":
            wrapIndex = (wrapIndex + 1) % wrapModes.Length;
            UpdateDisplay();
            renderer.RequestRender();
            break;
    }
});

UpdateDisplay();
renderer.RequestRender();
await Task.Delay(Timeout.Infinite);
