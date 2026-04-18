// Diff Demo — demonstrates DiffRenderable with unified/split view and theme cycling
using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig { ExitOnCtrlC = true, TargetFps = 30 });

int themeIndex = 0;
int diffIndex = 0;
bool splitView = false;

// --- Diff content ---
string[] diffs =
[
    // Diff 0: Multi-hunk refactoring diff
    """
    @@ -1,12 +1,14 @@
     using System;
    -using System.Collections.Generic;
    +using System.Collections.Concurrent;
    +using System.Threading.Tasks;
     
     namespace Services
     {
    -    public class UserCache
    +    public class UserCache : IDisposable
         {
    -        private readonly Dictionary<string, User> _cache = new();
    +        private readonly ConcurrentDictionary<string, User> _cache = new();
    +        private readonly SemaphoreSlim _lock = new(1, 1);
     
    -        public User? Get(string key)
    +        public async Task<User?> GetAsync(string key)
             {
    @@ -15,10 +17,16 @@
    -            if (_cache.TryGetValue(key, out var user))
    -                return user;
    -            return null;
    +            await _lock.WaitAsync();
    +            try
    +            {
    +                return _cache.GetValueOrDefault(key);
    +            }
    +            finally
    +            {
    +                _lock.Release();
    +            }
             }
     
    -        public void Set(string key, User user)
    +        public async Task SetAsync(string key, User user)
             {
    -            _cache[key] = user;
    +            await _lock.WaitAsync();
    +            try
    +            {
    +                _cache[key] = user;
    +            }
    +            finally
    +            {
    +                _lock.Release();
    +            }
             }
    +
    +        public void Dispose() => _lock.Dispose();
         }
     }
    """,
    // Diff 1: Config file changes
    """
    @@ -1,8 +1,10 @@
     {
       "name": "my-app",
    -  "version": "1.2.3",
    +  "version": "2.0.0",
       "dependencies": {
    -    "express": "^4.18.0",
    -    "lodash": "^4.17.21"
    +    "fastify": "^4.24.0",
    +    "zod": "^3.22.0",
    +    "pino": "^8.16.0"
       },
    +  "type": "module",
       "scripts": {
    @@ -10,5 +12,7 @@
    -    "start": "node index.js",
    -    "test": "jest"
    +    "start": "node --experimental-modules index.mjs",
    +    "test": "vitest",
    +    "lint": "eslint . --ext .mjs,.ts",
    +    "build": "tsc && esbuild src/index.ts --bundle --outfile=dist/index.mjs"
       }
     }
    """,
];
string[] diffNames = ["Refactoring", "Config Update"];

// --- Themes ---
var themes = new (string Name, Rgba AddedBg, Rgba RemovedBg, Rgba AddedSign, Rgba RemovedSign)[]
{
    ("Dark",     Rgba.FromHex("#1a3d1a"), Rgba.FromHex("#3d1a1a"), Rgba.FromHex("#22c55e"), Rgba.FromHex("#ef4444")),
    ("GitHub",   Rgba.FromHex("#0d4429"), Rgba.FromHex("#67060c"), Rgba.FromHex("#3fb950"), Rgba.FromHex("#f85149")),
    ("Blue",     Rgba.FromHex("#0c2d48"), Rgba.FromHex("#441122"), Rgba.FromHex("#58a6ff"), Rgba.FromHex("#ff7b72")),
};

// --- Header ---
var header = new BoxRenderable(renderer, new BoxOptions
{
    Id = "header",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#b91c1c"),
    BorderStyle = BorderStyle.Single,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    Border = true,
});
var headerText = new TextRenderable(renderer, new TextOptions
{
    Id = "header-text",
    Content = "DIFF DEMO",
    Fg = Rgba.FromInts(255, 255, 255),
});
header.Add(headerText);

// --- Diff container ---
var diffContainer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "diff-container",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Auto,
    FlexGrow = 1,
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Rgba.FromHex("#444444"),
});

var diffLayout = new BoxRenderable(renderer, new BoxOptions
{
    Id = "diff-layout",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Auto,
    FlexGrow = 1,
    FlexDirection = FlexDirectionValue.Row,
});

var theme = themes[themeIndex];
var diff = new DiffRenderable(renderer, new DiffOptions
{
    Id = "diff",
    Diff = diffs[diffIndex],
    View = "unified",
    ShowLineNumbers = true,
    AddedBg = theme.AddedBg,
    RemovedBg = theme.RemovedBg,
    AddedSignColor = theme.AddedSign,
    RemovedSignColor = theme.RemovedSign,
    Width = DimensionValue.Auto,
    FlexGrow = 1,
});
var diffScrollbar = new ScrollBarRenderable(renderer, new ScrollBarOptions
{
    Orientation = SliderOrientation.Vertical,
    OnChange = position => diff.ScrollTop = (int)position,
});
diffScrollbar.WidthDimension = DimensionValue.Point(1);
diffLayout.Add(diff);
diffLayout.Add(diffScrollbar);
diffContainer.Add(diffLayout);

// --- Footer ---
var footer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "footer",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#1e293b"),
    BorderStyle = BorderStyle.Single,
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
renderer.Root.Add(diffContainer);
renderer.Root.Add(footer);

void RebuildDiff()
{
    diffLayout.Remove("diff");
    var t = themes[themeIndex];
    diff = new DiffRenderable(renderer, new DiffOptions
    {
        Id = "diff",
        Diff = diffs[diffIndex],
        View = splitView ? "split" : "unified",
        ShowLineNumbers = true,
        AddedBg = t.AddedBg,
        RemovedBg = t.RemovedBg,
        AddedSignColor = t.AddedSign,
        RemovedSignColor = t.RemovedSign,
        Width = DimensionValue.Auto,
        FlexGrow = 1,
    });
    diff.On<int>(DiffRenderable.Events.Scroll, _ => UpdateDiffScrollbar());
    diff.OnSizeChange = UpdateDiffScrollbar;
    diffLayout.Add(diff, 0);
    UpdateDiffScrollbar();
}

void UpdateDiffScrollbar()
{
    diffScrollbar.ScrollSize = diff.ScrollHeight;
    diffScrollbar.ViewportSize = Math.Max(1, diff.ViewportHeight);
    diffScrollbar.ScrollPosition = diff.ScrollTop;
}

void UpdateDisplay()
{
    string viewLabel = splitView ? "split" : "unified";
    headerText.ContentText = $"DIFF DEMO — {diffNames[diffIndex]} ({diffIndex + 1}/{diffNames.Length})";
    footerText.ContentText = $"[N] Diff ({diffNames[diffIndex]})  [V] View ({viewLabel})  [T] Theme ({themes[themeIndex].Name})  [↑/↓/🖱] Scroll";
}

// --- Key handling ---
renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    switch (e.Name)
    {
        case "v":
            splitView = !splitView;
            RebuildDiff();
            break;
        case "t":
            themeIndex = (themeIndex + 1) % themes.Length;
            RebuildDiff();
            break;
        case "n":
            diffIndex = (diffIndex + 1) % diffs.Length;
            RebuildDiff();
            break;
        case "up":
            diff.ScrollBy(-1);
            UpdateDiffScrollbar();
            break;
        case "down":
            diff.ScrollBy(1);
            UpdateDiffScrollbar();
            break;
        case "pageup":
            diff.ScrollBy(-10);
            UpdateDiffScrollbar();
            break;
        case "pagedown":
            diff.ScrollBy(10);
            UpdateDiffScrollbar();
            break;
    }
    UpdateDisplay();
});

diff.On<int>(DiffRenderable.Events.Scroll, _ => UpdateDiffScrollbar());
diff.OnSizeChange = UpdateDiffScrollbar;
UpdateDisplay();
renderer.RequestRender();
await Task.Delay(Timeout.Infinite);
