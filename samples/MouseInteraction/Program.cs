// Mouse Interaction — demonstrates mouse events, color changes, coordinate display, and drag
using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 30,
    EnableMouseMovement = true,
    UseMouse = true,
});

renderer.Native.SetBackgroundColor(Rgba.FromHex("#0f172a"));

// --- State ---
const int TrailSize = 12;
var trail = new List<(int X, int Y)>();
bool isDragging = false;
(int X, int Y) dragStart = (0, 0);

// --- Header ---
var header = new BoxRenderable(renderer, new BoxOptions
{
    Id = "header",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#059669"),
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
});
var headerText = new TextRenderable(renderer, new TextOptions
{
    Id = "header-text",
    StyledContent = new StyledText(
        TextChunk.Styled("Mouse Interaction Demo", fg: Rgba.White, attributes: TextAttributes.Bold)),
    Fg = Rgba.White,
});
header.Add(headerText);

// --- Interactive boxes row ---
var boxRow = new BoxRenderable(renderer, new BoxOptions
{
    Id = "box-row",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(8),
    FlexDirection = FlexDirectionValue.Row,
    Padding = DimensionValue.Point(1),
    Gap = 2,
    AlignItems = AlignValue.Stretch,
});

Rgba[] defaultColors = [Rgba.FromHex("#ef4444"), Rgba.FromHex("#3b82f6"), Rgba.FromHex("#eab308")];
Rgba[] clickColors = [Rgba.FromHex("#fca5a5"), Rgba.FromHex("#93c5fd"), Rgba.FromHex("#fde68a")];
string[] boxLabels = ["Red Box", "Blue Box", "Yellow Box"];
var interactiveBoxes = new BoxRenderable[3];
var boxTexts = new TextRenderable[3];

for (int i = 0; i < 3; i++)
{
    int idx = i;
    var box = new BoxRenderable(renderer, new BoxOptions
    {
        Id = $"ibox-{i}",
        FlexGrow = 1,
        Border = true,
        BorderStyle = BorderStyle.Rounded,
        BackgroundColor = defaultColors[i],
        FlexDirection = FlexDirectionValue.Column,
        AlignItems = AlignValue.Center,
        JustifyContent = JustifyValue.Center,
        OnMouseDown = me =>
        {
            interactiveBoxes[idx].BackgroundColor = clickColors[idx];
            boxTexts[idx].Content = new StyledText(
                TextChunk.Styled($"CLICKED ({me.X},{me.Y})", fg: Rgba.White, attributes: TextAttributes.Bold));
            renderer.RequestRender();
        },
        OnMouseUp = _ =>
        {
            interactiveBoxes[idx].BackgroundColor = defaultColors[idx];
            boxTexts[idx].SetContent(boxLabels[idx]);
            renderer.RequestRender();
        },
        OnMouseMove = me =>
        {
            boxTexts[idx].Content = new StyledText(
                TextChunk.Styled($"{boxLabels[idx]} ({me.X},{me.Y})", fg: Rgba.White));
            renderer.RequestRender();
        },
    });

    var text = new TextRenderable(renderer, new TextOptions
    {
        Id = $"ibox-text-{i}",
        Content = boxLabels[i],
        Fg = Rgba.White,
    });

    box.Add(text);
    interactiveBoxes[i] = box;
    boxTexts[i] = text;
    boxRow.Add(box);
}

// --- Drag area ---
var dragText = new TextRenderable(renderer, new TextOptions
{
    Id = "drag-text",
    Content = "Click and drag here",
    Fg = Rgba.FromHex("#a5b4fc"),
});

var dragArea = new BoxRenderable(renderer, new BoxOptions
{
    Id = "drag-area",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(6),
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Rgba.FromHex("#6366f1"),
    BackgroundColor = Rgba.FromHex("#1e1b4b"),
    FlexDirection = FlexDirectionValue.Column,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    OnMouseDown = me =>
    {
        isDragging = true;
        dragStart = (me.X, me.Y);
        dragText.Content = new StyledText(
            TextChunk.Styled($"Drag started at ({me.X},{me.Y})", fg: Rgba.FromHex("#a5b4fc")));
        renderer.RequestRender();
    },
    OnMouseMove = me =>
    {
        if (isDragging)
        {
            int dx = me.X - dragStart.X;
            int dy = me.Y - dragStart.Y;
            dragText.Content = new StyledText(
                TextChunk.Styled($"Dragging: Δ({dx},{dy}) at ({me.X},{me.Y})", fg: Rgba.FromHex("#818cf8")));
            renderer.RequestRender();
        }
    },
    OnMouseUp = me =>
    {
        if (isDragging)
        {
            isDragging = false;
            int dx = me.X - dragStart.X;
            int dy = me.Y - dragStart.Y;
            dragText.Content = new StyledText(
                TextChunk.Styled($"Drag ended: Δ({dx},{dy})", fg: Rgba.FromHex("#c7d2fe")));
            renderer.RequestRender();
        }
    },
});

var dragLabel = new TextRenderable(renderer, new TextOptions
{
    Id = "drag-label",
    StyledContent = new StyledText(
        TextChunk.Styled("Drag Area", fg: Rgba.FromHex("#c7d2fe"), attributes: TextAttributes.Bold)),
    Fg = Rgba.White,
});
dragArea.Add(dragLabel);
dragArea.Add(dragText);

// --- Mouse trail display ---
var trailText = new TextRenderable(renderer, new TextOptions
{
    Id = "trail-text",
    Content = "No positions yet",
    Fg = Rgba.FromHex("#94a3b8"),
});

var trailBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "trail-box",
    Width = DimensionValue.Auto,
    FlexGrow = 1,
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Rgba.FromHex("#475569"),
    BackgroundColor = Rgba.FromHex("#111827"),
    FlexDirection = FlexDirectionValue.Column,
    Padding = DimensionValue.Point(1),
    OnMouseMove = me =>
    {
        trail.Add((me.X, me.Y));
        if (trail.Count > TrailSize) trail.RemoveAt(0);
        UpdateTrail();
        renderer.RequestRender();
    },
});

var trailTitle = new TextRenderable(renderer, new TextOptions
{
    Id = "trail-title",
    StyledContent = new StyledText(
        TextChunk.Styled("Mouse Trail (move mouse here)", fg: Rgba.FromHex("#38bdf8"), attributes: TextAttributes.Bold)),
    Fg = Rgba.White,
});
trailBox.Add(trailTitle);
trailBox.Add(trailText);

void UpdateTrail()
{
    if (trail.Count == 0)
    {
        trailText.SetContent("No positions yet");
        return;
    }

    var chunks = new List<TextChunk>();
    for (int i = 0; i < trail.Count; i++)
    {
        float brightness = (float)(i + 1) / trail.Count;
        byte g = (byte)(100 + (int)(155 * brightness));
        var color = Rgba.FromInts(50, g, 255);
        string marker = i == trail.Count - 1 ? "●" : "○";
        chunks.Add(TextChunk.Styled($"{marker}({trail[i].X},{trail[i].Y}) ", fg: color));
    }
    trailText.Content = new StyledText(chunks);
}

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
    Content = "Click boxes to change color | Drag in purple area | Move mouse in trail area | Ctrl+C: exit",
    Fg = Rgba.FromHex("#93c5fd"),
});
footer.Add(footerText);

// --- Build tree ---
renderer.Root.Add(header);
renderer.Root.Add(boxRow);
renderer.Root.Add(dragArea);
renderer.Root.Add(trailBox);
renderer.Root.Add(footer);

renderer.On<(int Width, int Height)>(RendererEventNames.Resize, _ =>
{
    renderer.RequestRender();
});

renderer.RequestRender();
await Task.Delay(Timeout.Infinite);
