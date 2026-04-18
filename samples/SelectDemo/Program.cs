// Select Demo — 20-item scrollable list with descriptions
// Port of select-demo.ts using OpenTui.Core
using OpenTui.Core;

// --- Create renderer ---
using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 30,
});

// --- Header ---
var header = new BoxRenderable(renderer, new BoxOptions
{
    Id = "header",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#8b5cf6"),
    Border = true,
    BorderStyle = BorderStyle.Single,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
});

var headerText = new TextRenderable(renderer, new TextOptions
{
    Id = "header-text",
    Content = "Select Demo",
    Fg = Rgba.FromInts(255, 255, 255),
});
header.Add(headerText);

// --- Content area (select + status side-by-side) ---
var contentArea = new BoxRenderable(renderer, new BoxOptions
{
    Id = "content",
    Width = DimensionValue.Auto,
    FlexDirection = FlexDirectionValue.Row,
    FlexGrow = 1,
    FlexShrink = 1,
    Padding = DimensionValue.Point(1),
});

// --- Build 20 options ---
var options = new SelectOption[20];
for (int i = 0; i < 20; i++)
{
    options[i] = new SelectOption
    {
        Name = $"Item {i + 1}",
        Description = $"Description for item {i + 1}",
        Value = i + 1,
    };
}

// --- Select widget ---
var select = new SelectRenderable(renderer, new SelectOptions
{
    Id = "my-select",
    Options = options,
    SelectedIndex = 0,
    ShowScrollIndicator = true,
    ShowDescription = true,
    WrapSelection = true,
    Width = DimensionValue.Point(40),
    FlexGrow = 1,
    FlexShrink = 1,
    Buffered = true,
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Rgba.FromHex("#6d28d9"),
});

// --- Status panel ---
var statusBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "status",
    Width = DimensionValue.Point(35),
    FlexDirection = FlexDirectionValue.Column,
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Rgba.FromHex("#475569"),
    Padding = DimensionValue.Point(1),
    MarginLeft = DimensionValue.Point(1),
});

var statusTitle = new TextRenderable(renderer, new TextOptions
{
    Id = "status-title",
    Content = "Status",
    Fg = Rgba.FromHex("#c084fc"),
    Attributes = TextAttributes.Bold,
});

var statusText = new TextRenderable(renderer, new TextOptions
{
    Id = "status-text",
    Content = "",
    Fg = Rgba.FromHex("#e2e8f0"),
});

var togglesText = new TextRenderable(renderer, new TextOptions
{
    Id = "toggles-text",
    Content = "",
    Fg = Rgba.FromHex("#94a3b8"),
});

statusBox.Add(statusTitle);
statusBox.Add(statusText);
statusBox.Add(togglesText);

contentArea.Add(select);
contentArea.Add(statusBox);

// --- Footer ---
var footer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "footer",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(1),
    BackgroundColor = Rgba.FromHex("#1e293b"),
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
});

var footerText = new TextRenderable(renderer, new TextOptions
{
    Id = "footer-text",
    Content = "↑↓: navigate | ENTER: select | D: toggle descriptions | W: toggle wrap",
    Fg = Rgba.FromHex("#94a3b8"),
});
footer.Add(footerText);

// --- Build tree ---
renderer.Root.Add(header);
renderer.Root.Add(contentArea);
renderer.Root.Add(footer);

// --- Update status display ---
string? lastSelected = null;

void UpdateStatus()
{
    var current = select.GetSelectedOption();
    string highlighted = current is not null
        ? $"Highlighted: {current.Name}\n{current.Description}"
        : "No item highlighted";
    string selected = lastSelected is not null
        ? $"\nSelected: {lastSelected}"
        : "";
    statusText.SetContent($"{highlighted}{selected}");

    string onOff(bool v) => v ? "ON" : "OFF";
    togglesText.SetContent(
        $"\nDescriptions: {onOff(select.ShowDescription)}" +
        $"\nWrap: {onOff(select.WrapSelection)}");

    renderer.RequestRender();
}

// --- Select events ---
select.On<(int Index, SelectOption? Option)>(SelectRenderable.Events.SelectionChanged, e =>
{
    UpdateStatus();
});

select.On<(int Index, SelectOption? Option)>(SelectRenderable.Events.ItemSelected, e =>
{
    lastSelected = e.Option?.Name ?? $"Item {e.Index + 1}";
    UpdateStatus();
});

// --- Key handling (toggle descriptions/wrap) ---
renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    switch (e.Name)
    {
        case "d":
            select.ShowDescription = !select.ShowDescription;
            UpdateStatus();
            break;
        case "w":
            select.WrapSelection = !select.WrapSelection;
            UpdateStatus();
            break;
    }
});

// --- Start ---
select.Focus();
UpdateStatus();
renderer.RequestRender();

await Task.Delay(Timeout.Infinite);
