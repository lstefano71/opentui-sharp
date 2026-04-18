// Focus Restore — 3 focusable boxes with Tab cycling, hide/restore focus
using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 30,
});

renderer.Native.SetBackgroundColor(Rgba.FromHex("#0f172a"));

int focusedIndex = 0;
int? hiddenIndex = null;

string[] boxNames = ["Alpha", "Beta", "Gamma"];
Rgba[] normalBg = [Rgba.FromHex("#1e3a5f"), Rgba.FromHex("#3b1f5e"), Rgba.FromHex("#1a3d2e")];
Rgba[] normalBorder = [Rgba.FromHex("#3b82f6"), Rgba.FromHex("#8b5cf6"), Rgba.FromHex("#22c55e")];
Rgba focusedBorder = Rgba.FromHex("#fbbf24");
Rgba hiddenBg = Rgba.FromHex("#1e293b");

// --- Header ---
var header = new BoxRenderable(renderer, new BoxOptions
{
    Id = "header",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#ca8a04"),
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
});
var headerText = new TextRenderable(renderer, new TextOptions
{
    Id = "header-text",
    StyledContent = new StyledText(
        TextChunk.Styled("Focus Restore Demo", fg: Rgba.White, attributes: TextAttributes.Bold)),
    Fg = Rgba.White,
});
header.Add(headerText);

// --- Box row ---
var boxRow = new BoxRenderable(renderer, new BoxOptions
{
    Id = "box-row",
    Width = DimensionValue.Auto,
    FlexGrow = 1,
    FlexDirection = FlexDirectionValue.Row,
    Padding = DimensionValue.Point(1),
    Gap = 1,
    AlignItems = AlignValue.Stretch,
});

var boxes = new BoxRenderable[3];
var boxLabels = new TextRenderable[3];
var boxStatus = new TextRenderable[3];

for (int i = 0; i < 3; i++)
{
    var box = new BoxRenderable(renderer, new BoxOptions
    {
        Id = $"box-{i}",
        FlexGrow = 1,
        Border = true,
        BorderStyle = BorderStyle.Rounded,
        BorderColor = normalBorder[i],
        BackgroundColor = normalBg[i],
        FlexDirection = FlexDirectionValue.Column,
        AlignItems = AlignValue.Center,
        JustifyContent = JustifyValue.Center,
        BoxFocusable = true,
    });

    var label = new TextRenderable(renderer, new TextOptions
    {
        Id = $"box-label-{i}",
        StyledContent = new StyledText(
            TextChunk.Styled(boxNames[i], fg: Rgba.White, attributes: TextAttributes.Bold)),
    });

    var status = new TextRenderable(renderer, new TextOptions
    {
        Id = $"box-status-{i}",
        Content = "",
        Fg = Rgba.FromHex("#94a3b8"),
    });

    box.Add(label);
    box.Add(status);
    boxes[i] = box;
    boxLabels[i] = label;
    boxStatus[i] = status;
    boxRow.Add(box);
}

// --- Info panel ---
var infoBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "info",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    Padding = DimensionValue.Point(1),
    BackgroundColor = Rgba.FromHex("#111827"),
    FlexDirection = FlexDirectionValue.Column,
});

var infoText = new TextRenderable(renderer, new TextOptions
{
    Id = "info-text",
    Content = "",
    Fg = Rgba.FromHex("#e2e8f0"),
});
infoBox.Add(infoText);

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
    Content = "Tab: cycle focus | H: hide focused | R: restore hidden | Ctrl+C: exit",
    Fg = Rgba.FromHex("#64748b"),
});
footer.Add(footerText);

// --- Build tree ---
renderer.Root.Add(header);
renderer.Root.Add(boxRow);
renderer.Root.Add(infoBox);
renderer.Root.Add(footer);

void UpdateVisuals()
{
    for (int i = 0; i < 3; i++)
    {
        bool isFocused = i == focusedIndex;
        bool isHidden = i == hiddenIndex;

        boxes[i].BorderColor = isFocused ? focusedBorder : normalBorder[i];
        boxes[i].BackgroundColor = isHidden ? hiddenBg : normalBg[i];
        boxes[i].Opacity = isHidden ? 0.3f : 1f;

        string stateStr = isHidden ? "HIDDEN" : isFocused ? "FOCUSED" : "unfocused";
        Rgba stateColor = isHidden
            ? Rgba.FromHex("#ef4444")
            : isFocused ? Rgba.FromHex("#fbbf24") : Rgba.FromHex("#64748b");

        boxStatus[i].Content = new StyledText(
            TextChunk.Styled(stateStr, fg: stateColor, attributes: isFocused ? TextAttributes.Bold : TextAttributes.None));
    }

    string hiddenInfo = hiddenIndex.HasValue
        ? $"  Hidden: {boxNames[hiddenIndex.Value]} (press R to restore)"
        : "";
    infoText.Content = new StyledText(
        TextChunk.Styled($"Focused: {boxNames[focusedIndex]}", fg: Rgba.FromHex("#fbbf24"), attributes: TextAttributes.Bold),
        TextChunk.Styled(hiddenInfo, fg: Rgba.FromHex("#ef4444")));

    renderer.RequestRender();
}

void FocusBox(int index)
{
    boxes[focusedIndex].Blur();
    focusedIndex = index;
    boxes[focusedIndex].Focus();
    UpdateVisuals();
}

int NextAvailable(int from, int direction)
{
    for (int step = 1; step < 3; step++)
    {
        int candidate = (from + direction * step + 3) % 3;
        if (candidate != hiddenIndex) return candidate;
    }
    return from;
}

// --- Key handling ---
renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    switch (e.Name)
    {
        case "tab":
            int next = NextAvailable(focusedIndex, e.Shift ? -1 : 1);
            FocusBox(next);
            e.StopPropagation();
            break;

        case "h":
            if (hiddenIndex.HasValue) break; // only one hidden at a time
            hiddenIndex = focusedIndex;
            boxes[focusedIndex].Blur();
            int afterHide = NextAvailable(focusedIndex, 1);
            focusedIndex = afterHide;
            boxes[focusedIndex].Focus();
            UpdateVisuals();
            break;

        case "r":
            if (!hiddenIndex.HasValue) break;
            int restored = hiddenIndex.Value;
            hiddenIndex = null;
            boxes[restored].Opacity = 1f;
            UpdateVisuals();
            break;
    }
});

renderer.On<(int Width, int Height)>(RendererEventNames.Resize, _ =>
{
    renderer.RequestRender();
});

// --- Start ---
FocusBox(0);
renderer.RequestRender();
await Task.Delay(Timeout.Infinite);
