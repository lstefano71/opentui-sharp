using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    BackgroundColor = Rgba.FromInts(15, 15, 35),
    EnableMouseMovement = true,
    ExitOnCtrlC = true,
    TargetFps = 30,
    UseMouse = true,
});

TimelineEngine.Instance.Attach(renderer);

var trailCells = new Dictionary<string, TrailCell>();
var activatedCells = new HashSet<string>();
const int TrailFadeDurationMs = 3000;
int nextZIndex = 101;

var backgroundColor = Rgba.FromInts(15, 15, 35);
var trailColor = Rgba.FromInts(64, 224, 208);
var dragColor = Rgba.FromInts(255, 165, 0);
var activatedColor = Rgba.FromInts(255, 20, 147);
var cursorColor = Rgba.White;

var mouseBackground = new FrameBufferRenderable(renderer, new FrameBufferOptions
{
    Id = "mouse-demo-buffer",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Percent(100),
    ZIndex = 0,
    BackgroundColor = backgroundColor,
    OnMouse = evt =>
    {
        string key = $"{evt.X},{evt.Y}";
        switch (evt.Type)
        {
            case MouseEventType.Move:
                trailCells[key] = new TrailCell(evt.X, evt.Y, Environment.TickCount64, false);
                renderer.RequestRender();
                break;
            case MouseEventType.Drag:
                trailCells[key] = new TrailCell(evt.X, evt.Y, Environment.TickCount64, true);
                renderer.RequestRender();
                break;
            case MouseEventType.Down:
                if (!activatedCells.Add(key))
                    activatedCells.Remove(key);
                renderer.RequestRender();
                break;
        }
    },
});
renderer.Root.Add(mouseBackground);

var titleText = new TextRenderable(renderer, new TextOptions
{
    Id = "mouse-demo-title",
    Content = "Mouse Interaction Demo with Draggable Objects",
    Position = PositionValue.Absolute,
    Left = 2,
    Top = 1,
    Fg = Rgba.FromInts(72, 209, 204),
    Attributes = TextAttributes.Bold,
    ZIndex = 1000,
});
renderer.Root.Add(titleText);

var instructionsText = new TextRenderable(renderer, new TextOptions
{
    Id = "mouse-demo-instructions",
    Content = "Drag boxes around • Move mouse: turquoise trails\nHold + move: orange drag trails • Click cells: toggle pink\nScroll on boxes: shows direction • Ctrl+C: quit",
    Position = PositionValue.Absolute,
    Left = 2,
    Top = 2,
    Width = DimensionValue.Percent(100),
    Height = 3,
    Fg = Rgba.FromInts(176, 196, 222),
    ZIndex = 1000,
});
renderer.Root.Add(instructionsText);

BoxRenderable CreateDraggableBox(
    string id,
    int x,
    int y,
    int width,
    int height,
    Rgba baseColor,
    string label,
    string? childText = null,
    OverflowValue? overflow = null)
{
    bool isDragging = false;
    int dragOffsetX = 0;
    int dragOffsetY = 0;
    string stateText = "";
    string scrollText = "";
    long scrollTimestamp = 0;
    int baseWidth = width;
    int baseHeight = height;
    var normalBackground = Rgba.FromInts((int)(baseColor.R * 255), (int)(baseColor.G * 255), (int)(baseColor.B * 255), 204);
    var dragBackground = Rgba.FromInts((int)(baseColor.R * 255), (int)(baseColor.G * 255), (int)(baseColor.B * 255), 77);
    var normalBorder = Rgba.FromInts(
        Math.Min(255, (int)(baseColor.R * 255 * 1.2f)),
        Math.Min(255, (int)(baseColor.G * 255 * 1.2f)),
        Math.Min(255, (int)(baseColor.B * 255 * 1.2f)));
    var dragBorder = Rgba.FromInts(
        Math.Min(255, (int)(baseColor.R * 255 * 1.2f)),
        Math.Min(255, (int)(baseColor.G * 255 * 1.2f)),
        Math.Min(255, (int)(baseColor.B * 255 * 1.2f)),
        128);

    var box = new BoxRenderable(renderer, new BoxOptions
    {
        Id = id,
        Position = PositionValue.Absolute,
        Left = x,
        Top = y,
        Width = width,
        Height = height,
        BackgroundColor = normalBackground,
        BorderColor = normalBorder,
        BorderStyle = BorderStyle.Rounded,
        Title = label,
        TitleAlignment = TitleAlignment.Center,
        Border = true,
        ZIndex = 100,
        Overflow = overflow,
    });

    var content = new TextRenderable(renderer, new TextOptions
    {
        Id = $"{id}-content",
        Content = "",
        Fg = Rgba.FromInts(147, 226, 255),
        WrapMode = WrapMode.Word,
        Width = DimensionValue.Percent(100),
    });
    box.Add(content);

    if (!string.IsNullOrEmpty(childText))
    {
        content.ContentText = childText;
    }

    void RefreshContent()
    {
        var lines = new List<string>();
        if (isDragging)
            lines.Add("drag");
        if (!string.IsNullOrEmpty(scrollText) && Environment.TickCount64 - scrollTimestamp <= 2000)
            lines.Add(scrollText);
        if (!string.IsNullOrEmpty(stateText))
            lines.Add(stateText);
        if (!string.IsNullOrEmpty(childText))
            lines.Add(childText);

        content.ContentText = string.Join('\n', lines);
    }

    void Bounce()
    {
        var scaleState = new ScaleState(1f);
        var timeline = TimelineFactory.CreateTimeline(new TimelineOptions { Duration = 600, AutoPlay = false });
        timeline.Add(new AnimationOptions
        {
            Duration = 200,
            Ease = "outExpo",
            Properties =
            [
                new TweenProperty
                {
                    Get = () => scaleState.Value,
                    Set = value =>
                    {
                        scaleState.Value = value;
                        box.WidthDimension = DimensionValue.Point(Math.Max(4, (int)MathF.Round(baseWidth * value)));
                        box.HeightDimension = DimensionValue.Point(Math.Max(2, (int)MathF.Round(baseHeight * value)));
                    },
                    EndValue = 1.5f,
                },
            ],
        }, 0);
        timeline.Add(new AnimationOptions
        {
            Duration = 400,
            Ease = "outExpo",
            Properties =
            [
                new TweenProperty
                {
                    Get = () => scaleState.Value,
                    Set = value =>
                    {
                        scaleState.Value = value;
                        box.WidthDimension = DimensionValue.Point(Math.Max(4, (int)MathF.Round(baseWidth * value)));
                        box.HeightDimension = DimensionValue.Point(Math.Max(2, (int)MathF.Round(baseHeight * value)));
                    },
                    EndValue = 1f,
                },
            ],
        }, 200);
        timeline.Play();
    }

    box.OnMouse = evt =>
    {
        switch (evt.Type)
        {
            case MouseEventType.Down:
                stateText = "";
                isDragging = true;
                dragOffsetX = evt.X - box.X;
                dragOffsetY = evt.Y - box.Y;
                box.ZIndex = nextZIndex++;
                box.BackgroundColor = dragBackground;
                box.BorderColor = dragBorder;
                evt.StopPropagation();
                break;

            case MouseEventType.Drag:
                if (isDragging)
                {
                    int newX = evt.X - dragOffsetX;
                    int newY = evt.Y - dragOffsetY;
                    int boundedX = Math.Max(0, Math.Min(newX, renderer.Width - box.Width));
                    int boundedY = Math.Max(4, Math.Min(newY, renderer.Height - box.Height));
                    box.X = boundedX;
                    box.Y = boundedY;
                    evt.StopPropagation();
                }
                break;

            case MouseEventType.DragEnd:
                if (isDragging)
                {
                    isDragging = false;
                    box.ZIndex = 100;
                    box.BackgroundColor = normalBackground;
                    box.BorderColor = normalBorder;
                    evt.StopPropagation();
                }
                break;

            case MouseEventType.Over:
                stateText = $"over {evt.Source ?? ""}".TrimEnd();
                break;

            case MouseEventType.Out:
                stateText = "out";
                break;

            case MouseEventType.Drop:
                stateText = evt.Source ?? "";
                Bounce();
                break;

            case MouseEventType.Scroll:
                if (evt.Scroll is not null)
                {
                    scrollText = $"scroll {evt.Scroll.Value.Direction}";
                    scrollTimestamp = Environment.TickCount64;
                    evt.StopPropagation();
                }
                break;
        }

        RefreshContent();
        renderer.RequestRender();
    };

    RefreshContent();
    return box;
}

renderer.Root.Add(CreateDraggableBox(
    "drag-box-1",
    10,
    8,
    20,
    10,
    Rgba.FromInts(200, 100, 150),
    "Box 1"));

renderer.Root.Add(CreateDraggableBox(
    "drag-box-2",
    30,
    12,
    18,
    10,
    Rgba.FromInts(100, 200, 150),
    "Box 2"));

renderer.Root.Add(CreateDraggableBox(
    "drag-box-3",
    50,
    15,
    20,
    11,
    Rgba.FromInts(150, 150, 200),
    "Box 3"));

renderer.Root.Add(CreateDraggableBox(
    "drag-box-4",
    15,
    20,
    18,
    11,
    Rgba.FromInts(200, 200, 100),
    "O hidden",
    "This should be cut off to the right",
    OverflowValue.Hidden));

renderer.AddFrameCallback(_ =>
{
    var buffer = mouseBackground.Buffer;
    if (buffer is null)
        return Task.CompletedTask;

    mouseBackground.Clear();

    long now = Environment.TickCount64;
    foreach (string key in trailCells.Where(pair => now - pair.Value.Timestamp > TrailFadeDurationMs).Select(pair => pair.Key).ToArray())
        trailCells.Remove(key);

    foreach (var cell in trailCells.Values)
    {
        float fadeRatio = 1f - Math.Clamp((float)(now - cell.Timestamp) / TrailFadeDurationMs, 0f, 1f);
        if (fadeRatio <= 0f)
            continue;

        var sourceColor = cell.IsDrag ? dragColor : trailColor;
        var faded = Rgba.FromValues(sourceColor.R, sourceColor.G, sourceColor.B, fadeRatio);
        if (cell.X >= 0 && cell.Y >= 0 && cell.X < mouseBackground.Width && cell.Y < mouseBackground.Height)
            buffer.SetCellWithAlphaBlending((uint)cell.X, (uint)cell.Y, '█', faded, backgroundColor);
    }

    foreach (string cellKey in activatedCells)
    {
        var parts = cellKey.Split(',');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out int x) &&
            int.TryParse(parts[1], out int y) &&
            x >= 0 && y >= 0 &&
            x < mouseBackground.Width && y < mouseBackground.Height)
        {
            buffer.DrawText("█", (uint)x, (uint)y, activatedColor, backgroundColor);
        }
    }

    var latest = trailCells.Values
        .Where(cell => now - cell.Timestamp < 100)
        .OrderByDescending(cell => cell.Timestamp)
        .FirstOrDefault();

    if (latest is not null &&
        latest.X >= 0 && latest.Y >= 0 &&
        latest.X < mouseBackground.Width && latest.Y < mouseBackground.Height)
    {
        buffer.SetCellWithAlphaBlending((uint)latest.X, (uint)latest.Y, '+', cursorColor, backgroundColor);
    }

    renderer.RequestRender();
    return Task.CompletedTask;
});

await Task.Delay(Timeout.Infinite);

sealed record TrailCell(int X, int Y, long Timestamp, bool IsDrag);
sealed class ScaleState(float value)
{
    public float Value { get; set; } = value;
}
