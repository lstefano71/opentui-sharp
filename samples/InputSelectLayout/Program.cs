// Input + Select Layout — filter a list by typing in an input field
using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 30,
});

renderer.Native.SetBackgroundColor(Rgba.FromHex("#0f172a"));

// --- Data ---
string[] allItems =
[
    "Apple", "Apricot", "Avocado", "Banana", "Blueberry", "Cherry",
    "Coconut", "Cranberry", "Date", "Dragonfruit", "Elderberry", "Fig",
    "Grape", "Guava", "Honeydew", "Jackfruit", "Kiwi", "Lemon",
    "Lime", "Lychee", "Mango", "Melon", "Nectarine", "Orange",
    "Papaya", "Peach", "Pear", "Pineapple", "Plum", "Pomegranate",
    "Raspberry", "Strawberry", "Tangerine", "Watermelon",
];

string? lastSelected = null;
bool inputFocused = true;

// --- Header ---
var header = new BoxRenderable(renderer, new BoxOptions
{
    Id = "header",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#16a34a"),
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
});
var headerText = new TextRenderable(renderer, new TextOptions
{
    Id = "header-text",
    StyledContent = new StyledText(
        TextChunk.Styled("Input + Select Layout", fg: Rgba.White, attributes: TextAttributes.Bold)),
    Fg = Rgba.White,
});
header.Add(headerText);

// --- Content ---
var contentRow = new BoxRenderable(renderer, new BoxOptions
{
    Id = "content",
    Width = DimensionValue.Auto,
    FlexGrow = 1,
    FlexDirection = FlexDirectionValue.Row,
    Padding = DimensionValue.Point(1),
    Gap = 1,
});

// --- Left: filter + select ---
var leftCol = new BoxRenderable(renderer, new BoxOptions
{
    Id = "left",
    FlexGrow = 1,
    FlexDirection = FlexDirectionValue.Column,
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Rgba.FromHex("#22c55e"),
    Padding = DimensionValue.Point(1),
});

var filterLabel = new TextRenderable(renderer, new TextOptions
{
    Id = "filter-label",
    StyledContent = new StyledText(
        TextChunk.Styled("🔍 Filter:", fg: Rgba.FromHex("#4ade80"), attributes: TextAttributes.Bold)),
});

var input = new InputRenderable(renderer, new InputOptions
{
    Id = "filter-input",
    Value = "",
    Placeholder = "Type to filter...",
    PlaceholderColor = Rgba.FromHex("#666666"),
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(1),
    MaxLength = 40,
    Buffered = true,
    MarginBottom = DimensionValue.Point(1),
});

var select = new SelectRenderable(renderer, new SelectOptions
{
    Id = "fruit-select",
    Options = allItems.Select(name => new SelectOption { Name = name, Value = name }).ToArray(),
    SelectedIndex = 0,
    ShowScrollIndicator = true,
    WrapSelection = true,
    FlexGrow = 1,
    Buffered = true,
    Border = true,
    BorderStyle = BorderStyle.Single,
    BorderColor = Rgba.FromHex("#475569"),
});

leftCol.Add(filterLabel);
leftCol.Add(input);
leftCol.Add(select);

// --- Right: status ---
var rightCol = new BoxRenderable(renderer, new BoxOptions
{
    Id = "right",
    Width = DimensionValue.Point(30),
    FlexDirection = FlexDirectionValue.Column,
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Rgba.FromHex("#475569"),
    Padding = DimensionValue.Point(1),
});

var focusText = new TextRenderable(renderer, new TextOptions
{
    Id = "focus-text",
    Content = "",
    Fg = Rgba.FromHex("#94a3b8"),
});

var matchText = new TextRenderable(renderer, new TextOptions
{
    Id = "match-text",
    Content = "",
    Fg = Rgba.FromHex("#e2e8f0"),
});

var selectedText = new TextRenderable(renderer, new TextOptions
{
    Id = "selected-text",
    Content = "",
    Fg = Rgba.FromHex("#22c55e"),
});

var highlightText = new TextRenderable(renderer, new TextOptions
{
    Id = "highlight-text",
    Content = "",
    Fg = Rgba.FromHex("#94a3b8"),
});

rightCol.Add(focusText);
rightCol.Add(matchText);
rightCol.Add(selectedText);
rightCol.Add(highlightText);

contentRow.Add(leftCol);
contentRow.Add(rightCol);

// --- Footer ---
var footer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "footer",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#1e293b"),
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
});
var footerText = new TextRenderable(renderer, new TextOptions
{
    Id = "footer-text",
    Content = "Tab: switch focus | ↑↓: navigate list | Enter: select | Ctrl+C: exit",
    Fg = Rgba.FromHex("#64748b"),
});
footer.Add(footerText);

// --- Build tree ---
renderer.Root.Add(header);
renderer.Root.Add(contentRow);
renderer.Root.Add(footer);

// --- Filtering logic ---
void ApplyFilter()
{
    string filter = input.Value.Trim().ToLowerInvariant();
    var filtered = string.IsNullOrEmpty(filter)
        ? allItems
        : allItems.Where(item => item.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToArray();

    select.Options = filtered.Select(name => new SelectOption { Name = name, Value = name }).ToArray();
    select.SelectedIndex = 0;
    UpdateStatus();
}

void UpdateStatus()
{
    string focusLabel = inputFocused ? "Input" : "Select";
    focusText.Content = new StyledText(
        TextChunk.Styled("Focus: ", fg: Rgba.FromHex("#64748b")),
        TextChunk.Styled(focusLabel, fg: Rgba.FromHex("#38bdf8"), attributes: TextAttributes.Bold));

    matchText.Content = new StyledText(
        TextChunk.Styled("Matches: ", fg: Rgba.FromHex("#64748b")),
        TextChunk.Styled($"{select.Options.Length}/{allItems.Length}", fg: Rgba.FromHex("#e2e8f0")));

    if (lastSelected is not null)
        selectedText.Content = new StyledText(
            TextChunk.Styled("Selected: ", fg: Rgba.FromHex("#64748b")),
            TextChunk.Styled(lastSelected, fg: Rgba.FromHex("#22c55e"), attributes: TextAttributes.Bold));

    var current = select.GetSelectedOption();
    if (current is not null)
        highlightText.Content = new StyledText(
            TextChunk.Styled("Highlighted: ", fg: Rgba.FromHex("#64748b")),
            TextChunk.Styled(current.Name, fg: Rgba.FromHex("#fbbf24")));

    renderer.RequestRender();
}

void SetFocus(bool toInput)
{
    inputFocused = toInput;
    if (toInput)
    {
        select.Blur();
        input.Focus();
    }
    else
    {
        input.Blur();
        select.Focus();
    }
    UpdateStatus();
}

// --- Events ---
input.On<string>(InputRenderable.Events.Input, _ =>
{
    ApplyFilter();
});

select.On<(int Index, SelectOption? Option)>(SelectRenderable.Events.SelectionChanged, _ =>
{
    UpdateStatus();
});

select.On<(int Index, SelectOption? Option)>(SelectRenderable.Events.ItemSelected, e =>
{
    lastSelected = e.Option?.Name ?? "?";
    UpdateStatus();
});

renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    if (e.Name == "tab")
    {
        SetFocus(!inputFocused);
        e.StopPropagation();
    }
});

renderer.On<(int Width, int Height)>(RendererEventNames.Resize, _ =>
{
    renderer.RequestRender();
});

// --- Start ---
SetFocus(true);
ApplyFilter();
renderer.RequestRender();
await Task.Delay(Timeout.Infinite);
