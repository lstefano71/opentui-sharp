using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    BackgroundColor = Rgba.FromHex("#001122"),
    ExitOnCtrlC = true,
    TargetFps = 30,
});

var colorOptions = new[]
{
    new SelectOption { Name = "Red", Description = "A warm primary color", Value = "#ff0000" },
    new SelectOption { Name = "Blue", Description = "A cool primary color", Value = "#0066ff" },
    new SelectOption { Name = "Green", Description = "A natural color", Value = "#00aa00" },
    new SelectOption { Name = "Purple", Description = "A regal color", Value = "#8a2be2" },
    new SelectOption { Name = "Orange", Description = "A vibrant color", Value = "#ff8c00" },
    new SelectOption { Name = "Teal", Description = "A calming color", Value = "#008080" },
};

var sizeOptions = new[]
{
    new SelectOption { Name = "Small", Description = "Compact size (8px)", Value = 8 },
    new SelectOption { Name = "Medium", Description = "Standard size (12px)", Value = 12 },
    new SelectOption { Name = "Large", Description = "Big size (16px)", Value = 16 },
    new SelectOption { Name = "Extra Large", Description = "Huge size (20px)", Value = 20 },
};

var headerBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "header-box",
    Width = DimensionValue.Auto,
    Height = 3,
    BackgroundColor = Rgba.FromHex("#3b82f6"),
    BorderStyle = BorderStyle.Single,
    BorderColor = Rgba.FromHex("#2563eb"),
    Border = true,
});

var header = new TextRenderable(renderer, new TextOptions
{
    Id = "header",
    Content = "INPUT & SELECT LAYOUT DEMO",
    Fg = Rgba.White,
    FlexGrow = 1,
});

headerBox.Add(header);

var selectContainerBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "select-container-box",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Auto,
    FlexGrow = 1,
    FlexShrink = 1,
    MinHeight = 10,
    BackgroundColor = Rgba.FromHex("#1e293b"),
    BorderStyle = BorderStyle.Single,
    BorderColor = Rgba.FromHex("#475569"),
    Border = true,
});

var selectContainer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "select-container",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Auto,
    FlexDirection = FlexDirectionValue.Row,
    FlexGrow = 1,
    FlexShrink = 1,
});
selectContainerBox.Add(selectContainer);

var leftSelectBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "color-select-box",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Auto,
    MinHeight = 8,
    BorderStyle = BorderStyle.Single,
    BorderColor = Rgba.FromHex("#475569"),
    FocusedBorderColor = Rgba.FromHex("#3b82f6"),
    Title = "Color Selection",
    TitleAlignment = TitleAlignment.Center,
    FlexGrow = 1,
    FlexShrink = 1,
    Border = true,
});

var leftSelect = new SelectRenderable(renderer, new SelectOptions
{
    Id = "color-select",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Auto,
    MinHeight = 6,
    Options = colorOptions,
    BackgroundColor = Rgba.FromHex("#1e293b"),
    FocusedBackgroundColor = Rgba.FromHex("#2d3748"),
    TextColor = Rgba.FromHex("#e2e8f0"),
    FocusedTextColor = Rgba.FromHex("#f7fafc"),
    SelectedBackgroundColor = Rgba.FromHex("#3b82f6"),
    SelectedTextColor = Rgba.White,
    DescriptionColor = Rgba.FromHex("#94a3b8"),
    SelectedDescriptionColor = Rgba.FromHex("#cbd5e1"),
    ShowScrollIndicator = true,
    WrapSelection = true,
    ShowDescription = true,
    FlexGrow = 1,
    FlexShrink = 1,
});
leftSelectBox.Add(leftSelect);

var rightSelectBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "size-select-box",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Auto,
    MinHeight = 8,
    BorderStyle = BorderStyle.Single,
    BorderColor = Rgba.FromHex("#475569"),
    FocusedBorderColor = Rgba.FromHex("#059669"),
    Title = "Size Selection",
    TitleAlignment = TitleAlignment.Center,
    FlexGrow = 1,
    FlexShrink = 1,
    Border = true,
});

var rightSelect = new SelectRenderable(renderer, new SelectOptions
{
    Id = "size-select",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Auto,
    MinHeight = 6,
    Options = sizeOptions,
    BackgroundColor = Rgba.FromHex("#1e293b"),
    FocusedBackgroundColor = Rgba.FromHex("#2d3748"),
    TextColor = Rgba.FromHex("#e2e8f0"),
    FocusedTextColor = Rgba.FromHex("#f7fafc"),
    SelectedBackgroundColor = Rgba.FromHex("#059669"),
    SelectedTextColor = Rgba.White,
    DescriptionColor = Rgba.FromHex("#94a3b8"),
    SelectedDescriptionColor = Rgba.FromHex("#cbd5e1"),
    ShowScrollIndicator = true,
    WrapSelection = true,
    ShowDescription = true,
    FlexGrow = 1,
    FlexShrink = 1,
});
rightSelectBox.Add(rightSelect);

var inputContainerBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "input-container-box",
    Width = DimensionValue.Auto,
    Height = 7,
    BackgroundColor = Rgba.FromHex("#0f172a"),
    BorderStyle = BorderStyle.Single,
    BorderColor = Rgba.FromHex("#334155"),
    Border = true,
});

var inputContainer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "input-container",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Auto,
    FlexDirection = FlexDirectionValue.Column,
    FlexGrow = 1,
    FlexShrink = 1,
});
inputContainerBox.Add(inputContainer);

var inputLabel = new TextRenderable(renderer, new TextOptions
{
    Id = "input-label",
    Content = "Enter your text:",
    Fg = Rgba.FromHex("#f1f5f9"),
});

var textInputBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "text-input-box",
    Width = DimensionValue.Auto,
    Height = 3,
    BorderStyle = BorderStyle.Single,
    BorderColor = Rgba.FromHex("#475569"),
    FocusedBorderColor = Rgba.FromHex("#eab308"),
    MarginTop = 1,
    Border = true,
});

var textInput = new InputRenderable(renderer, new InputOptions
{
    Id = "text-input",
    Width = DimensionValue.Auto,
    Height = 1,
    Placeholder = "Type something here...",
    BackgroundColor = Rgba.FromHex("#1e293b"),
    FocusedBackgroundColor = Rgba.FromHex("#334155"),
    TextColor = Rgba.FromHex("#f1f5f9"),
    FocusedTextColor = Rgba.White,
    PlaceholderColor = Rgba.FromHex("#64748b"),
    CursorColor = Rgba.FromHex("#f1f5f9"),
    MaxLength = 100,
    FlexGrow = 1,
    FlexShrink = 1,
});
textInputBox.Add(textInput);

var footerBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "footer-box",
    Width = DimensionValue.Auto,
    Height = 3,
    BackgroundColor = Rgba.FromHex("#1e40af"),
    BorderStyle = BorderStyle.Single,
    BorderColor = Rgba.FromHex("#1d4ed8"),
    Border = true,
});

var footer = new TextRenderable(renderer, new TextOptions
{
    Id = "footer",
    Content = "TAB: focus next | SHIFT+TAB: focus prev | ARROWS/JK: navigate | ESC: quit",
    Fg = Rgba.FromHex("#dbeafe"),
    FlexGrow = 1,
});
footerBox.Add(footer);

selectContainer.Add(leftSelectBox);
selectContainer.Add(rightSelectBox);
inputContainer.Add(inputLabel);
inputContainer.Add(textInputBox);

renderer.Root.Add(headerBox);
renderer.Root.Add(selectContainerBox);
renderer.Root.Add(inputContainerBox);
renderer.Root.Add(footerBox);

var focusableElements = new Renderable[] { leftSelect, rightSelect, textInput };
var focusableBoxes = new BoxRenderable[] { leftSelectBox, rightSelectBox, textInputBox };
int currentFocusIndex = 0;

void UpdateDisplay()
{
    var selectedColor = leftSelect.GetSelectedOption();
    var selectedSize = rightSelect.GetSelectedOption();

    string displayText = "Enter your text:";
    if (!string.IsNullOrEmpty(textInput.Value))
        displayText += $" \"{textInput.Value}\"";
    if (selectedColor is not null)
        displayText += $" in {selectedColor.Name}";
    if (selectedSize is not null)
        displayText += $" ({selectedSize.Name})";

    inputLabel.ContentText = displayText;
    renderer.RequestRender();
}

void UpdateFocus()
{
    foreach (var element in focusableElements)
        element.Blur();

    foreach (var box in focusableBoxes)
        box.Blur();

    focusableElements[currentFocusIndex].Focus();
    focusableBoxes[currentFocusIndex].Focus();
    renderer.RequestRender();
}

leftSelect.On<(int Index, SelectOption? Option)>(SelectRenderable.Events.SelectionChanged, _ => UpdateDisplay());
leftSelect.On<(int Index, SelectOption? Option)>(SelectRenderable.Events.ItemSelected, _ => UpdateDisplay());
rightSelect.On<(int Index, SelectOption? Option)>(SelectRenderable.Events.SelectionChanged, _ => UpdateDisplay());
rightSelect.On<(int Index, SelectOption? Option)>(SelectRenderable.Events.ItemSelected, _ => UpdateDisplay());
textInput.On<string>(InputRenderable.Events.Input, _ => UpdateDisplay());
textInput.On<string>(InputRenderable.Events.Change, _ => UpdateDisplay());

renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    if (e.Name == "tab")
    {
        currentFocusIndex = e.Shift
            ? (currentFocusIndex - 1 + focusableElements.Length) % focusableElements.Length
            : (currentFocusIndex + 1) % focusableElements.Length;
        UpdateFocus();
        e.StopPropagation();
        return;
    }

    if (e.Name == "escape")
    {
        renderer.Destroy();
        return;
    }
});

UpdateFocus();
UpdateDisplay();

await Task.Delay(Timeout.Infinite);
