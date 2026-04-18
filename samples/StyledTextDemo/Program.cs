// Styled Text Demo — showcases StyledText, TextChunk, and TextAttributes
// Port of styled-text-demo.ts
using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 30,
});

renderer.Native.SetBackgroundColor(Rgba.FromHex("#0f172a"));

// --- State ---
int currentPage = 0;
int tickCount = 0;
string[] pageNames = ["Basic Styles", "Colors & Backgrounds", "Combinations", "Dashboard"];

// --- Header ---
var header = new BoxRenderable(renderer, new BoxOptions
{
    Id = "header",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#7c3aed"),
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
});
var headerText = new TextRenderable(renderer, new TextOptions
{
    Id = "header-text",
    StyledContent = new StyledText(
        TextChunk.Styled("Styled Text Demo", fg: Rgba.FromHex("#f8fafc"), attributes: TextAttributes.Bold)),
    Fg = Rgba.FromInts(255, 255, 255),
});
header.Add(headerText);

// --- Content area ---
var content = new BoxRenderable(renderer, new BoxOptions
{
    Id = "content",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Auto,
    FlexGrow = 1,
    FlexDirection = FlexDirectionValue.Column,
    Padding = DimensionValue.Point(1),
    BackgroundColor = Rgba.FromHex("#1e293b"),
});

// --- Page indicator ---
var pageIndicator = new TextRenderable(renderer, new TextOptions
{
    Id = "page-indicator",
    Content = "",
    Fg = Rgba.FromHex("#94a3b8"),
});
content.Add(pageIndicator);

// --- Page containers (one per page, toggle visibility) ---
BoxRenderable MakePage(string id) => new(renderer, new BoxOptions
{
    Id = id,
    Width = DimensionValue.Auto,
    Height = DimensionValue.Auto,
    FlexGrow = 1,
    FlexDirection = FlexDirectionValue.Column,
    Padding = DimensionValue.Point(1),
});

// =====================
//  PAGE 0: Basic Styles
// =====================
var page0 = MakePage("page-basic");

var boldText = new TextRenderable(renderer, new TextOptions
{
    Id = "bold-text",
    StyledContent = new StyledText(
        TextChunk.Plain("Normal text, then "),
        TextChunk.Styled("this is bold", attributes: TextAttributes.Bold)),
    Fg = Rgba.FromHex("#e2e8f0"),
});

var italicText = new TextRenderable(renderer, new TextOptions
{
    Id = "italic-text",
    StyledContent = new StyledText(
        TextChunk.Plain("Normal text, then "),
        TextChunk.Styled("this is italic", attributes: TextAttributes.Italic)),
    Fg = Rgba.FromHex("#e2e8f0"),
});

var underlineText = new TextRenderable(renderer, new TextOptions
{
    Id = "underline-text",
    StyledContent = new StyledText(
        TextChunk.Plain("Normal text, then "),
        TextChunk.Styled("this is underlined", attributes: TextAttributes.Underline)),
    Fg = Rgba.FromHex("#e2e8f0"),
});

var strikeText = new TextRenderable(renderer, new TextOptions
{
    Id = "strike-text",
    StyledContent = new StyledText(
        TextChunk.Plain("Normal text, then "),
        TextChunk.Styled("this is strikethrough", attributes: TextAttributes.Strikethrough)),
    Fg = Rgba.FromHex("#e2e8f0"),
});

var dimText = new TextRenderable(renderer, new TextOptions
{
    Id = "dim-text",
    StyledContent = new StyledText(
        TextChunk.Plain("Normal text, then "),
        TextChunk.Styled("this is dim", attributes: TextAttributes.Dim)),
    Fg = Rgba.FromHex("#e2e8f0"),
});

page0.Add(boldText);
page0.Add(italicText);
page0.Add(underlineText);
page0.Add(strikeText);
page0.Add(dimText);

// ==============================
//  PAGE 1: Colors & Backgrounds
// ==============================
var page1 = MakePage("page-colors");

var redText = new TextRenderable(renderer, new TextOptions
{
    Id = "red-text",
    StyledContent = new StyledText(
        TextChunk.Styled("Red foreground", fg: Rgba.FromHex("#ef4444"))),
});

var greenText = new TextRenderable(renderer, new TextOptions
{
    Id = "green-text",
    StyledContent = new StyledText(
        TextChunk.Styled("Green foreground", fg: Rgba.FromHex("#22c55e"))),
});

var blueText = new TextRenderable(renderer, new TextOptions
{
    Id = "blue-text",
    StyledContent = new StyledText(
        TextChunk.Styled("Blue foreground", fg: Rgba.FromHex("#3b82f6"))),
});

var bgText = new TextRenderable(renderer, new TextOptions
{
    Id = "bg-text",
    StyledContent = new StyledText(
        TextChunk.Styled(" Dark on yellow bg ",
            fg: Rgba.FromHex("#1e293b"),
            bg: Rgba.FromHex("#facc15"))),
});

var multiBgText = new TextRenderable(renderer, new TextOptions
{
    Id = "multi-bg-text",
    StyledContent = new StyledText(
        TextChunk.Styled(" Red bg ",   fg: Rgba.White, bg: Rgba.FromHex("#dc2626")),
        TextChunk.Styled(" Green bg ", fg: Rgba.White, bg: Rgba.FromHex("#16a34a")),
        TextChunk.Styled(" Blue bg ",  fg: Rgba.White, bg: Rgba.FromHex("#2563eb"))),
});

var rainbowText = new TextRenderable(renderer, new TextOptions
{
    Id = "rainbow-text",
    StyledContent = new StyledText(
        TextChunk.Styled("R", fg: Rgba.FromHex("#ef4444")),
        TextChunk.Styled("A", fg: Rgba.FromHex("#f97316")),
        TextChunk.Styled("I", fg: Rgba.FromHex("#eab308")),
        TextChunk.Styled("N", fg: Rgba.FromHex("#22c55e")),
        TextChunk.Styled("B", fg: Rgba.FromHex("#3b82f6")),
        TextChunk.Styled("O", fg: Rgba.FromHex("#8b5cf6")),
        TextChunk.Styled("W", fg: Rgba.FromHex("#ec4899"))),
});

page1.Add(redText);
page1.Add(greenText);
page1.Add(blueText);
page1.Add(bgText);
page1.Add(multiBgText);
page1.Add(rainbowText);

// ========================
//  PAGE 2: Combinations
// ========================
var page2 = MakePage("page-combos");

var boldUnderline = new TextRenderable(renderer, new TextOptions
{
    Id = "bold-underline",
    StyledContent = new StyledText(
        TextChunk.Styled("Bold + Underline",
            fg: Rgba.FromHex("#f472b6"),
            attributes: TextAttributes.Bold | TextAttributes.Underline)),
});

var boldItalicColor = new TextRenderable(renderer, new TextOptions
{
    Id = "bold-italic-color",
    StyledContent = new StyledText(
        TextChunk.Styled("Bold + Italic + Cyan",
            fg: Rgba.FromHex("#22d3ee"),
            attributes: TextAttributes.Bold | TextAttributes.Italic)),
});

var multiChunk = new TextRenderable(renderer, new TextOptions
{
    Id = "multi-chunk",
    StyledContent = new StyledText(
        TextChunk.Styled("Error: ", fg: Rgba.FromHex("#ef4444"), attributes: TextAttributes.Bold),
        TextChunk.Plain("file not found at "),
        TextChunk.Styled("/data/config.json", fg: Rgba.FromHex("#facc15"), attributes: TextAttributes.Underline)),
});

var statusLine = new TextRenderable(renderer, new TextOptions
{
    Id = "status-line",
    StyledContent = new StyledText(
        TextChunk.Styled(" PASS ", fg: Rgba.White, bg: Rgba.FromHex("#16a34a"), attributes: TextAttributes.Bold),
        TextChunk.Plain(" tests/unit.cs "),
        TextChunk.Styled("(42 tests)", fg: Rgba.FromHex("#94a3b8"), attributes: TextAttributes.Dim)),
});

var kitchenSink = new TextRenderable(renderer, new TextOptions
{
    Id = "kitchen-sink",
    StyledContent = new StyledText(
        TextChunk.Styled("Bold", attributes: TextAttributes.Bold),
        TextChunk.Plain(" + "),
        TextChunk.Styled("Color", fg: Rgba.FromHex("#a78bfa")),
        TextChunk.Plain(" + "),
        TextChunk.Styled("BG", fg: Rgba.Black, bg: Rgba.FromHex("#fbbf24")),
        TextChunk.Plain(" + "),
        TextChunk.Styled("Underline", attributes: TextAttributes.Underline),
        TextChunk.Plain(" + "),
        TextChunk.Styled("All at once!",
            fg: Rgba.FromHex("#34d399"),
            bg: Rgba.FromHex("#1e293b"),
            attributes: TextAttributes.Bold | TextAttributes.Underline | TextAttributes.Italic)),
});

page2.Add(boldUnderline);
page2.Add(boldItalicColor);
page2.Add(multiChunk);
page2.Add(statusLine);
page2.Add(kitchenSink);

// =====================
//  PAGE 3: Dashboard
// =====================
var page3 = MakePage("page-dashboard");

var dashTitle = new TextRenderable(renderer, new TextOptions
{
    Id = "dash-title",
    StyledContent = new StyledText(
        TextChunk.Styled("▸ Live Dashboard", fg: Rgba.FromHex("#38bdf8"), attributes: TextAttributes.Bold)),
});

var dashCounter = new TextRenderable(renderer, new TextOptions
{
    Id = "dash-counter",
    Content = "Ticks: 0",
    Fg = Rgba.FromHex("#e2e8f0"),
});

var dashStatus = new TextRenderable(renderer, new TextOptions
{
    Id = "dash-status",
    StyledContent = new StyledText(
        TextChunk.Styled(" ● ", fg: Rgba.FromHex("#22c55e")),
        TextChunk.Styled("System online", fg: Rgba.FromHex("#94a3b8"))),
});

var dashBar = new TextRenderable(renderer, new TextOptions
{
    Id = "dash-bar",
    Content = "",
    Fg = Rgba.FromHex("#e2e8f0"),
});

page3.Add(dashTitle);
page3.Add(dashCounter);
page3.Add(dashStatus);
page3.Add(dashBar);

// --- Add all pages to content ---
BoxRenderable[] pages = [page0, page1, page2, page3];
foreach (var p in pages) content.Add(p);

// --- Footer ---
var footer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "footer",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#1e40af"),
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
});
var footerText = new TextRenderable(renderer, new TextOptions
{
    Id = "footer-text",
    Content = "N: next page | Ctrl+C: exit",
    Fg = Rgba.FromInts(255, 255, 255),
});
footer.Add(footerText);

// --- Build tree ---
renderer.Root.Add(header);
renderer.Root.Add(content);
renderer.Root.Add(footer);

// --- Helpers ---
void ShowPage(int index)
{
    for (int i = 0; i < pages.Length; i++)
        pages[i].Visible = i == index;

    pageIndicator.Content = new StyledText(
        TextChunk.Styled($"  Page {index + 1}/{pages.Length}: ",
            fg: Rgba.FromHex("#64748b"), attributes: TextAttributes.Dim),
        TextChunk.Styled(pageNames[index],
            fg: Rgba.FromHex("#f8fafc"), attributes: TextAttributes.Bold));
}

void UpdateDashboard()
{
    dashCounter.Content = new StyledText(
        TextChunk.Styled("Ticks: ", fg: Rgba.FromHex("#94a3b8")),
        TextChunk.Styled(tickCount.ToString(), fg: Rgba.FromHex("#38bdf8"), attributes: TextAttributes.Bold));

    // Build a progress bar
    int barWidth = 20;
    int filled = tickCount % (barWidth + 1);
    string filledStr = new('█', filled);
    string emptyStr = new('░', barWidth - filled);

    dashBar.Content = new StyledText(
        TextChunk.Styled("Progress: [", fg: Rgba.FromHex("#64748b")),
        TextChunk.Styled(filledStr, fg: Rgba.FromHex("#22c55e")),
        TextChunk.Styled(emptyStr, fg: Rgba.FromHex("#334155")),
        TextChunk.Styled($"] {filled * 100 / barWidth}%", fg: Rgba.FromHex("#64748b")));

    // Alternate status indicator color
    Rgba statusColor = tickCount % 4 < 2 ? Rgba.FromHex("#22c55e") : Rgba.FromHex("#eab308");
    string statusLabel = tickCount % 4 < 2 ? "System online" : "Processing...";
    dashStatus.Content = new StyledText(
        TextChunk.Styled(" ● ", fg: statusColor),
        TextChunk.Styled(statusLabel, fg: Rgba.FromHex("#94a3b8")));
}

// --- Frame callback for dashboard counter ---
float elapsed = 0f;
renderer.AddFrameCallback(dt =>
{
    elapsed += dt;
    if (elapsed < 500f) return Task.CompletedTask; // update every ~500ms

    elapsed = 0f;
    tickCount++;
    if (currentPage == 3) // dashboard page
    {
        UpdateDashboard();
        renderer.RequestRender();
    }
    return Task.CompletedTask;
});

// --- Key handling ---
renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    switch (e.Name)
    {
        case "n":
            currentPage = (currentPage + 1) % pages.Length;
            ShowPage(currentPage);
            if (currentPage == 3) UpdateDashboard();
            renderer.RequestRender();
            break;
    }
});

renderer.On<(int Width, int Height)>(RendererEventNames.Resize, _ =>
{
    renderer.RequestRender();
});

// --- Start ---
ShowPage(0);
renderer.RequestRender();
await Task.Delay(Timeout.Infinite);
