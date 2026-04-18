// Input Demo — form with 4 input fields and tab navigation
// Port of input-demo.ts using OpenTui.Core
using OpenTui.Core;

// --- Create renderer ---
using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 30,
});

// --- Field definitions ---
string[] fieldNames = ["Name", "Email", "Password", "Comment"];
string[] placeholders =
[
    "Enter your name...",
    "Enter email...",
    "Enter password...",
    "Add a comment...",
];

int focusedIndex = 0;

// --- Header ---
var header = new BoxRenderable(renderer, new BoxOptions
{
    Id = "header",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#3b82f6"),
    Border = true,
    BorderStyle = BorderStyle.Single,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
});

var headerText = new TextRenderable(renderer, new TextOptions
{
    Id = "header-text",
    Content = "Input Demo",
    Fg = Rgba.FromInts(255, 255, 255),
});
header.Add(headerText);

// --- Form container ---
var formBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "form",
    Width = DimensionValue.Auto,
    FlexDirection = FlexDirectionValue.Column,
    FlexGrow = 1,
    Padding = DimensionValue.Point(1),
});

// --- Create labeled input rows ---
var inputs = new InputRenderable[fieldNames.Length];

for (int i = 0; i < fieldNames.Length; i++)
{
    var row = new BoxRenderable(renderer, new BoxOptions
    {
        Id = $"row-{i}",
        Width = DimensionValue.Auto,
        Height = DimensionValue.Point(1),
        FlexDirection = FlexDirectionValue.Row,
        AlignItems = AlignValue.Center,
        MarginBottom = DimensionValue.Point(1),
    });

    var label = new TextRenderable(renderer, new TextOptions
    {
        Id = $"label-{i}",
        Content = $"{fieldNames[i],10}: ",
        Fg = Rgba.FromHex("#94a3b8"),
        Width = DimensionValue.Point(12),
    });

    var input = new InputRenderable(renderer, new InputOptions
    {
        Id = $"input-{i}",
        Value = "",
        Placeholder = placeholders[i],
        PlaceholderColor = Rgba.FromHex("#666666"),
        BackgroundColor = Rgba.FromHex("#0f172a"),
        TextColor = Rgba.FromHex("#f8fafc"),
        FocusedBackgroundColor = Rgba.FromHex("#111827"),
        FocusedTextColor = Rgba.FromHex("#f8fafc"),
        CursorColor = Rgba.FromHex("#fbbf24"),
        Width = DimensionValue.Auto,
        Height = DimensionValue.Point(1),
        MaxLength = 50,
        FlexGrow = 1,
        Buffered = true,
    });

    inputs[i] = input;
    row.Add(label);
    row.Add(input);
    formBox.Add(row);
}

// --- Status panel ---
var statusBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "status",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(5),
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Rgba.FromHex("#475569"),
    FlexDirection = FlexDirectionValue.Column,
    Padding = DimensionValue.Point(1),
});

var statusText = new TextRenderable(renderer, new TextOptions
{
    Id = "status-text",
    Content = "",
    Fg = Rgba.FromHex("#e2e8f0"),
});
statusBox.Add(statusText);

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
    Content = "TAB: next field | ENTER: submit | Ctrl+C: exit",
    Fg = Rgba.FromHex("#94a3b8"),
});
footer.Add(footerText);

// --- Build tree ---
renderer.Root.Add(header);
renderer.Root.Add(formBox);
renderer.Root.Add(statusBox);
renderer.Root.Add(footer);

// --- Update status display ---
void UpdateStatus(string? extra = null)
{
    string lines = $"Focused: {fieldNames[focusedIndex]}";
    for (int i = 0; i < fieldNames.Length; i++)
        lines += $"\n  {fieldNames[i]}: {inputs[i].Value}";
    if (extra is not null)
        lines += $"\n{extra}";
    statusText.SetContent(lines);
    renderer.RequestRender();
}

// --- Focus management ---
void FocusField(int index)
{
    inputs[focusedIndex].Blur();
    focusedIndex = index;
    inputs[focusedIndex].Focus();
    UpdateStatus();
}

// Wire up input events for live status updates
for (int i = 0; i < inputs.Length; i++)
{
    inputs[i].On<string>(InputRenderable.Events.Input, _ => UpdateStatus());
    inputs[i].On<string>(InputRenderable.Events.Enter, _ => UpdateStatus("✓ Submitted!"));
}

// --- Key handling (tab navigation + enter submit) ---
renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    switch (e.Name)
    {
        case "tab" when !e.Shift:
            FocusField((focusedIndex + 1) % fieldNames.Length);
            e.StopPropagation();
            break;
        case "tab" when e.Shift:
            FocusField((focusedIndex - 1 + fieldNames.Length) % fieldNames.Length);
            e.StopPropagation();
            break;
    }
});

// --- Start ---
FocusField(0);
renderer.RequestRender();

await Task.Delay(Timeout.Infinite);
