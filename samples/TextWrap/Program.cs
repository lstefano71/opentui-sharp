// Text Wrap Demo — demonstrates WrapMode.None, WrapMode.Char, and WrapMode.Word
using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 30,
});

int sampleIndex = 0;
string[] sampleNames = ["Short", "Medium", "Long", "Unicode", "CJK"];
string[] samples =
[
    "Hello, world!",

    "The quick brown fox jumps over the lazy dog. Pack my box with five dozen liquor jugs.",

    "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. "
    + "Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. "
    + "Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. "
    + "Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.",

    "Héllo wörld! Ñoño señor. Ünïcödé tëxt with àccénts and spëcial characters: ß, ø, å, ñ, ü, ö, ä. "
    + "Emojis: 🎉🚀💻🌍✨ — Greek: αβγδε — Cyrillic: абвгд — Math: ∑∏∫∂√∞",

    "中文文本示例：天地玄黃，宇宙洪荒。日月盈昃，辰宿列張。寒來暑往，秋收冬藏。"
    + "日本語テスト：吾輩は猫である。名前はまだ無い。どこで生れたかとんと見当がつかぬ。"
    + "한국어 테스트: 모든 인간은 태어날 때부터 자유로우며 그 존엄과 권리에 있어 동등하다.",
];

// --- Header ---
var header = new BoxRenderable(renderer, new BoxOptions
{
    Id = "header",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#0f766e"),
    BorderStyle = BorderStyle.Rounded,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    Border = true,
});
var headerText = new TextRenderable(renderer, new TextOptions
{
    Id = "header-text",
    Content = "Text Wrap Demo",
    Fg = Rgba.FromInts(255, 255, 255),
});
header.Add(headerText);

// --- Row of three columns ---
var row = new BoxRenderable(renderer, new BoxOptions
{
    Id = "row",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Auto,
    FlexGrow = 1,
    FlexDirection = FlexDirectionValue.Row,
    Gap = 1,
    Padding = DimensionValue.Point(1),
});

// Helper: create a labeled column with a TextRenderable using a specific WrapMode
(BoxRenderable box, TextRenderable text) MakeColumn(string id, string title, WrapMode mode)
{
    var col = new BoxRenderable(renderer, new BoxOptions
    {
        Id = id,
        FlexGrow = 1,
        Border = true,
        BorderStyle = BorderStyle.Rounded,
        BorderColor = Rgba.FromHex("#6b7280"),
        BackgroundColor = Rgba.FromHex("#1f2937"),
        Title = title,
        Overflow = OverflowValue.Hidden,
    });
    var txt = new TextRenderable(renderer, new TextOptions
    {
        Id = $"{id}-text",
        Content = samples[sampleIndex],
        WrapMode = mode,
        Fg = Rgba.FromHex("#e5e7eb"),
    });
    col.Add(txt);
    return (col, txt);
}

var (noneBox, noneText) = MakeColumn("col-none", "WrapMode.None", WrapMode.None);
var (charBox, charText) = MakeColumn("col-char", "WrapMode.Char", WrapMode.Char);
var (wordBox, wordText) = MakeColumn("col-word", "WrapMode.Word", WrapMode.Word);

row.Add(noneBox);
row.Add(charBox);
row.Add(wordBox);

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
renderer.Root.Add(row);
renderer.Root.Add(footer);

void UpdateSample()
{
    string text = samples[sampleIndex];
    noneText.ContentText = text;
    charText.ContentText = text;
    wordText.ContentText = text;
    headerText.ContentText = $"Text Wrap Demo — Sample: {sampleNames[sampleIndex]}";
    footerText.ContentText = $"[n] Next sample ({sampleIndex + 1}/{samples.Length})  [Ctrl+C] Quit";
}

// --- Key handling ---
renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    if (e.Name is "n")
    {
        sampleIndex = (sampleIndex + 1) % samples.Length;
        UpdateSample();
        renderer.RequestRender();
    }
});

UpdateSample();
renderer.RequestRender();
await Task.Delay(Timeout.Infinite);
