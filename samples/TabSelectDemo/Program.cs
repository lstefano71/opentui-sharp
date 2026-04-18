// Tab Select Demo — horizontal tab bar with 12 tabs and dynamic content
// Port of tab-select-demo.ts
using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 30,
});

// --- Tab data ---
TabSelectOption[] tabs =
[
    new() { Name = "Dashboard",  Description = "Overview of key metrics and activity" },
    new() { Name = "Analytics",  Description = "Detailed charts and trend analysis" },
    new() { Name = "Reports",    Description = "Generate and export reports" },
    new() { Name = "Settings",   Description = "Configure application preferences" },
    new() { Name = "Users",      Description = "Manage user accounts and roles" },
    new() { Name = "Logs",       Description = "View system and audit logs" },
    new() { Name = "Config",     Description = "Edit runtime configuration" },
    new() { Name = "Help",       Description = "Documentation and support links" },
    new() { Name = "Profile",    Description = "Your account and personal settings" },
    new() { Name = "Billing",    Description = "Subscription plans and invoices" },
    new() { Name = "Support",    Description = "Contact support and submit tickets" },
    new() { Name = "API",        Description = "API keys, tokens, and usage stats" },
];

string[] contentTexts =
[
    "📊  Welcome to the Dashboard. Here you'll find a summary of all key metrics.",
    "📈  Analytics view — dive deep into trends, cohorts, and funnels.",
    "📋  Reports hub — create, schedule, and export PDF/CSV reports.",
    "⚙️   Settings panel — adjust theme, notifications, and integrations.",
    "👥  User management — invite, deactivate, and assign roles.",
    "📜  Logs viewer — filter by severity, service, or time range.",
    "🔧  Configuration editor — tweak feature flags and limits.",
    "❓  Help center — browse the docs or search for answers.",
    "🧑  Profile page — update your avatar, email, and password.",
    "💳  Billing dashboard — view invoices, update payment method.",
    "🛟  Support desk — open a ticket or check existing ones.",
    "🔑  API console — manage keys, view rate limits and usage.",
];

// --- Header ---
var header = new BoxRenderable(renderer, new BoxOptions
{
    Id = "header",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#1e3a5f"),
    BorderStyle = BorderStyle.Single,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    Border = true,
});
var headerText = new TextRenderable(renderer, new TextOptions
{
    Id = "header-text",
    Content = "TAB SELECT DEMO",
    Fg = Rgba.FromHex("#60a5fa"),
});
header.Add(headerText);

// --- Tab Select ---
var tabSelect = new TabSelectRenderable(renderer, new TabSelectOptions
{
    Id = "tabs",
    Options = tabs,
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    Buffered = true,
    ShowUnderline = true,
    ShowDescription = true,
    WrapSelection = true,
    TabWidth = 14,
    BackgroundColor = Rgba.FromHex("#0f172a"),
    TextColor = Rgba.FromHex("#94a3b8"),
    SelectedBackgroundColor = Rgba.FromHex("#1e40af"),
    SelectedTextColor = Rgba.FromHex("#fbbf24"),
    SelectedDescriptionColor = Rgba.FromHex("#93c5fd"),
});

// --- Content area ---
var contentBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "content",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Auto,
    FlexGrow = 1,
    FlexShrink = 1,
    BackgroundColor = Rgba.FromHex("#1e293b"),
    BorderStyle = BorderStyle.Rounded,
    Border = true,
    BorderColor = Rgba.FromHex("#334155"),
    Padding = DimensionValue.Point(1),
});

var contentTitle = new TextRenderable(renderer, new TextOptions
{
    Id = "content-title",
    Content = tabs[0].Name,
    Fg = Rgba.FromHex("#f59e0b"),
});

var contentBody = new TextRenderable(renderer, new TextOptions
{
    Id = "content-body",
    Content = contentTexts[0],
    Fg = Rgba.FromHex("#e2e8f0"),
});

contentBox.Add(contentTitle);
contentBox.Add(contentBody);

// --- Footer ---
var footer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "footer",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#0f172a"),
    BorderStyle = BorderStyle.Single,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    Border = true,
    BorderColor = Rgba.FromHex("#334155"),
});
var footerText = new TextRenderable(renderer, new TextOptions
{
    Id = "footer-text",
    Content = "",
    Fg = Rgba.FromHex("#64748b"),
});
footer.Add(footerText);

// --- Build tree ---
renderer.Root.Add(header);
renderer.Root.Add(tabSelect);
renderer.Root.Add(contentBox);
renderer.Root.Add(footer);

// --- Events ---
tabSelect.On<(int Index, TabSelectOption? Option)>(TabSelectRenderable.Events.SelectionChanged, args =>
{
    int idx = Math.Clamp(args.Index, 0, tabs.Length - 1);
    contentTitle.ContentText = tabs[idx].Name;
    contentBody.ContentText = contentTexts[idx];
});

tabSelect.On<(int Index, TabSelectOption? Option)>(TabSelectRenderable.Events.ItemSelected, args =>
{
    contentBody.ContentText = $"✅ Selected: {args.Option?.Name ?? "unknown"}";
});

// --- Update footer text ---
void UpdateFooter()
{
    string ul = tabSelect.ShowUnderline ? "ON" : "OFF";
    string desc = tabSelect.ShowDescription ? "ON" : "OFF";
    string wrap = tabSelect.WrapSelection ? "ON" : "OFF";
    footerText.ContentText = $"←/→: navigate  ENTER: select  U: underline ({ul})  D: descriptions ({desc})  W: wrap ({wrap})  Ctrl+C: quit";
}

// --- Key handling ---
renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    switch (e.Name)
    {
        case "u":
            tabSelect.ShowUnderline = !tabSelect.ShowUnderline;
            UpdateFooter();
            break;
        case "d":
            tabSelect.ShowDescription = !tabSelect.ShowDescription;
            UpdateFooter();
            break;
        case "w":
            tabSelect.WrapSelection = !tabSelect.WrapSelection;
            UpdateFooter();
            break;
    }
});

// --- Start ---
renderer.Native.SetBackgroundColor(Rgba.FromHex("#0f172a"));
UpdateFooter();
tabSelect.SelectedIndex = 0;
renderer.RequestRender();

await Task.Delay(Timeout.Infinite);
