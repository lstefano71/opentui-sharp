using OpenTui.Core;

static Rgba Hex(string value) => Rgba.FromHex(value);

static bool IsPlainKey(KeyEvent key, string name) =>
    string.Equals(key.Name, name, StringComparison.OrdinalIgnoreCase)
    && !key.Ctrl
    && !key.Meta
    && !key.Option
    && !key.Super
    && !key.Hyper;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 30,
});

renderer.SetBackgroundColor(Hex("#000028"));

bool debugOverlayEnabled = false;
var interactiveBorderSides = BorderSides.All;
int partialBorderPhase = -1;
float animPosition = 5f;
int animDirection = 1;
const float AnimSpeed = 15f;

var activeWheelPixels = new HashSet<string>(StringComparer.Ordinal);

var tabController = new TabControllerRenderable(renderer, new TabControllerOptions
{
    Id = "main-tab-controller",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(0),
    Top = DimensionValue.Point(0),
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Percent(100),
    TabBarHeight = 4,
    TextColor = Hex("#E2E8F0"),
    TabBarBackgroundColor = Hex("#000028"),
    SelectedBackgroundColor = Hex("#1E3A5F"),
    SelectedTextColor = Hex("#38BDF8"),
    SelectedDescriptionColor = Hex("#94A3B8"),
    ShowDescription = true,
    ShowUnderline = true,
    ShowScrollArrows = true,
});

renderer.Root.Add(tabController);

tabController.AddTab(new TabControllerTab
{
    Title = "Text & Attributes",
    Description = "Text styling, gradients, and animated HSV color wheel",
    Initialize = InitializeTextAndAttributesTab,
    Update = UpdateTextAndAttributesTab,
    Show = () => activeWheelPixels.Clear(),
    Hide = () =>
    {
        if (tabController.GetCurrentTabGroup() is { } group)
        {
            foreach (var pixelId in activeWheelPixels.ToArray())
                group.Remove(pixelId);
        }

        activeWheelPixels.Clear();
    },
});

tabController.AddTab(new TabControllerTab
{
    Title = "Basics",
    Description = "Basic text, box rendering, and animated terminal cursor styles",
    Initialize = InitializeBasicsTab,
    Update = UpdateBasicsTab,
    Show = () => renderer.SetCursorPosition(15, 13, true),
    Hide = () => renderer.SetCursorPosition(0, 0, false),
});

tabController.AddTab(new TabControllerTab
{
    Title = "Borders",
    Description = "Border styles, partial borders, and custom border characters",
    Initialize = InitializeBordersTab,
    Update = UpdateBordersTab,
});

tabController.AddTab(new TabControllerTab
{
    Title = "Animation",
    Description = "Moving elements and an animated HSV background box",
    Initialize = InitializeAnimationTab,
    Update = UpdateAnimationTab,
});

tabController.AddTab(new TabControllerTab
{
    Title = "Titles",
    Description = "Box title rendering with left, center, and right alignment",
    Initialize = InitializeTitlesTab,
});

tabController.AddTab(new TabControllerTab
{
    Title = "Interactive",
    Description = "Toggle individual border sides with keyboard input",
    Initialize = InitializeInteractiveTab,
    Update = UpdateInteractiveTab,
});

tabController.Focus();
renderer.RequestLive();

renderer.KeyInput.On<KeyEvent>("keypress", key =>
{
    if (key.Ctrl && string.Equals(key.Name, "g", StringComparison.OrdinalIgnoreCase))
    {
        key.PreventDefault();
        renderer.Native.DumpHitGrid();
        return;
    }

    if (IsPlainKey(key, ".") || IsPlainKey(key, "d"))
    {
        key.PreventDefault();
        debugOverlayEnabled = !debugOverlayEnabled;
        renderer.SetDebugOverlay(debugOverlayEnabled, DebugOverlayCorner.BottomRight);
        return;
    }

    if (tabController.GetCurrentTab()?.Title != "Interactive")
        return;

    if (IsPlainKey(key, "t"))
    {
        key.PreventDefault();
        interactiveBorderSides ^= BorderSides.Top;
    }
    else if (IsPlainKey(key, "r"))
    {
        key.PreventDefault();
        interactiveBorderSides ^= BorderSides.Right;
    }
    else if (IsPlainKey(key, "b"))
    {
        key.PreventDefault();
        interactiveBorderSides ^= BorderSides.Bottom;
    }
    else if (IsPlainKey(key, "l"))
    {
        key.PreventDefault();
        interactiveBorderSides ^= BorderSides.Left;
    }
});

await Task.Delay(Timeout.Infinite);

void InitializeTextAndAttributesTab(BoxRenderable group)
{
    AddText(group, "text-title", "Text Styling & Color Gradients", 10, 5, "#FFFF00",
        TextAttributes.Bold | TextAttributes.Underline);
    AddText(group, "attr-bold", "Bold Text", 10, 8, "#FFFFFF", TextAttributes.Bold);
    AddText(group, "attr-italic", "Italic Text", 10, 9, "#FFFFFF", TextAttributes.Italic);
    AddText(group, "attr-underline", "Underlined Text", 10, 10, "#FFFFFF", TextAttributes.Underline);
    AddText(group, "attr-dim", "Dim Text", 10, 11, "#FFFFFF", TextAttributes.Dim);
    AddText(group, "attr-combined", "Bold + Italic + Underline", 10, 12, "#FF6464",
        TextAttributes.Bold | TextAttributes.Italic | TextAttributes.Underline);

    AddText(group, "gradient-title", "Rainbow Gradient:", 10, 15, "#CCCCCC");

    for (int i = 0; i < 40; i++)
    {
        var pixel = new TextRenderable(renderer, new TextOptions
        {
            Id = $"gradient-{i}",
            Content = "█",
            Position = PositionValue.Absolute,
            Left = DimensionValue.Point(10 + i),
            Top = DimensionValue.Point(17),
            Fg = Rgba.FromHsv(i / 40f * 360f, 1f, 1f),
            ZIndex = 10,
        });
        group.Add(pixel);
    }
}

void UpdateTextAndAttributesTab(float deltaTime, BoxRenderable group)
{
    const int wheelRadius = 7;
    const int wheelCenterX = 70;
    const int wheelCenterY = 15;

    double time = Environment.TickCount64 / 1000.0;
    double rotationRadians = ((time * 45d) % 360d) * (Math.PI / 180d);
    var newWheelPixels = new HashSet<string>(StringComparer.Ordinal);

    for (int y = wheelCenterY - wheelRadius; y <= wheelCenterY + wheelRadius; y++)
    {
        for (int x = wheelCenterX - wheelRadius * 2; x <= wheelCenterX + wheelRadius * 2; x++)
        {
            double dx = (x - wheelCenterX) / 2d;
            double dy = y - wheelCenterY;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            if (distance > wheelRadius)
                continue;

            double angle = Math.Atan2(dy, dx) + rotationRadians;
            float hue = (float)(((angle / Math.PI) * 180d + 180d) % 360d);
            float saturation = (float)(distance / wheelRadius);
            var color = Rgba.FromHsv(hue, saturation, 1f);

            string pixelId = $"wheel-{x}-{y}";
            newWheelPixels.Add(pixelId);

            if (group.GetRenderable(pixelId) is TextRenderable existingPixel)
            {
                existingPixel.X = x;
                existingPixel.Y = y;
                existingPixel.Fg = color;
            }
            else
            {
                group.Add(new TextRenderable(renderer, new TextOptions
                {
                    Id = pixelId,
                    Content = "█",
                    Position = PositionValue.Absolute,
                    Left = DimensionValue.Point(x),
                    Top = DimensionValue.Point(y),
                    Fg = color,
                    ZIndex = 10,
                }));
            }
        }
    }

    foreach (var pixelId in activeWheelPixels)
    {
        if (!newWheelPixels.Contains(pixelId))
            group.Remove(pixelId);
    }

    activeWheelPixels.Clear();
    activeWheelPixels.UnionWith(newWheelPixels);
}

void InitializeBasicsTab(BoxRenderable group)
{
    AddText(group, "opentui-title", "Basic CLI Renderer Demo", 10, 5, "#FFFF00",
        TextAttributes.Bold | TextAttributes.Underline);

    AddBox(group, "box1", 10, 8, 20, 8, "#333366", BorderStyle.Single, "#FFFFFF");
    AddText(group, "box1-title", "Simple Box", 12, 10, "#FFFFFF", TextAttributes.Bold);

    AddBox(group, "box2", 35, 10, 25, 6, "#663333", BorderStyle.Double, "#FFFF00");
    AddText(group, "box2-title", "Double Border Box", 37, 12, "#FFFFFF", TextAttributes.Bold);

    AddText(group, "description",
        "This tab demonstrates basic box and text rendering with different border styles.",
        10, 18, "#CCCCCC");
    AddText(group, "cursor-info", "Cursor: (0,0) - Style: block", 10, 20, "#FFFFFF", TextAttributes.Bold);
}

void UpdateBasicsTab(float deltaTime, BoxRenderable group)
{
    double cursorTime = Environment.TickCount64 / 1000.0;
    int cursorX = 15 + (int)Math.Floor(3 * Math.Cos(cursorTime));
    int cursorY = 13 + (int)Math.Floor(2 * Math.Sin(cursorTime));

    int cursorStyleIndex = (int)Math.Floor(cursorTime / 2d) % 6;
    CursorStyle cursorStyle = cursorStyleIndex switch
    {
        0 => CursorStyle.SteadyBlock,
        1 => CursorStyle.BlinkingBlock,
        2 => CursorStyle.SteadyBar,
        3 => CursorStyle.BlinkingBar,
        4 => CursorStyle.SteadyUnderline,
        _ => CursorStyle.BlinkingUnderline,
    };

    string cursorLabel = cursorStyle switch
    {
        CursorStyle.SteadyBlock => "block",
        CursorStyle.BlinkingBlock => "block (blinking)",
        CursorStyle.SteadyBar => "line",
        CursorStyle.BlinkingBar => "line (blinking)",
        CursorStyle.SteadyUnderline => "underline",
        CursorStyle.BlinkingUnderline => "underline (blinking)",
        _ => "default",
    };

    renderer.SetCursorStyle(new CursorStyleOptions { Style = (byte)cursorStyle });
    renderer.SetCursorPosition(cursorX, cursorY, true);

    if (group.GetRenderable("cursor-info") is TextRenderable cursorInfo)
        cursorInfo.ContentText = $"Cursor: ({cursorX},{cursorY}) - Style: {cursorLabel}";
}

void InitializeBordersTab(BoxRenderable group)
{
    AddText(group, "border-title", "Border Styles & Partial Borders", 10, 5, "#FFFF00",
        TextAttributes.Bold | TextAttributes.Underline);

    AddBox(group, "single-box", 10, 8, 15, 5, "#222244", BorderStyle.Single, "#FFFFFF");
    AddText(group, "single-label", "Single", 12, 10, "#FFFFFF", TextAttributes.Bold);

    AddBox(group, "double-box", 30, 8, 15, 5, "#442222", BorderStyle.Double, "#FFFFFF");
    AddText(group, "double-label", "Double", 32, 10, "#FFFFFF", TextAttributes.Bold);

    AddBox(group, "rounded-box", 50, 8, 15, 5, "#224422", BorderStyle.Rounded, "#FFFFFF");
    AddText(group, "rounded-label", "Rounded", 52, 10, "#FFFFFF", TextAttributes.Bold);

    AddText(group, "partial-title", "Partial Borders:", 10, 15, "#CCCCCC", TextAttributes.Underline);
    AddBox(group, "partial-left", 10, 17, 12, 4, "#222244", BorderStyle.Single, "#FFFFFF",
        sides: BorderSides.Left);
    AddText(group, "partial-left-label", "Left Only", 12, 18, "#FFFFFF");

    AddBox(group, "partial-animated", 30, 17, 20, 4, "#334455", BorderStyle.Single, "#FFFFFF");
    AddText(group, "partial-animated-label", "Animated Borders", 32, 18, "#FFFFFF");
    AddText(group, "partial-phase", "Phase: 1/8", 30, 22, "#AAAAAA");

    AddText(group, "custom-border-title", "Custom Border Characters:", 10, 25, "#CCCCCC",
        TextAttributes.Underline);

    var asciiBorders = new BorderCharacters
    {
        TopLeft = '+',
        TopRight = '+',
        BottomLeft = '+',
        BottomRight = '+',
        Horizontal = '-',
        Vertical = '|',
        TopT = '+',
        BottomT = '+',
        LeftT = '+',
        RightT = '+',
        Cross = '+',
    };

    var blockBorders = new BorderCharacters
    {
        TopLeft = '█',
        TopRight = '█',
        BottomLeft = '█',
        BottomRight = '█',
        Horizontal = '█',
        Vertical = '█',
        TopT = '█',
        BottomT = '█',
        LeftT = '█',
        RightT = '█',
        Cross = '█',
    };

    var starBorders = new BorderCharacters
    {
        TopLeft = '*',
        TopRight = '*',
        BottomLeft = '*',
        BottomRight = '*',
        Horizontal = '*',
        Vertical = '*',
        TopT = '*',
        BottomT = '*',
        LeftT = '*',
        RightT = '*',
        Cross = '*',
    };

    AddBox(group, "ascii-box", 10, 27, 15, 5, "#222244", BorderStyle.Single, "#FFFFFF",
        customBorderChars: asciiBorders);
    AddText(group, "ascii-label", "ASCII Border", 12, 29, "#FFFFFF", TextAttributes.Bold);

    AddBox(group, "block-box", 30, 27, 15, 5, "#442222", BorderStyle.Single, "#FFFFFF",
        customBorderChars: blockBorders);
    AddText(group, "block-label", "Block Border", 32, 29, "#FFFFFF", TextAttributes.Bold);

    AddBox(group, "star-box", 50, 27, 15, 5, "#224422", BorderStyle.Single, "#FFFFFF",
        customBorderChars: starBorders);
    AddText(group, "star-label", "Star Border", 52, 29, "#FFFFFF", TextAttributes.Bold);
}

void UpdateBordersTab(float deltaTime, BoxRenderable group)
{
    int phase = (int)Math.Floor((Environment.TickCount64 / 500d) % 8d);
    if (phase == partialBorderPhase)
        return;

    partialBorderPhase = phase;

    if (group.GetRenderable("partial-animated") is BoxRenderable animatedBox)
        animatedBox.ActiveBorderSides = phase switch
        {
            0 => BorderSides.Top,
            1 => BorderSides.Right,
            2 => BorderSides.Bottom,
            3 => BorderSides.Top | BorderSides.Right | BorderSides.Bottom,
            4 => BorderSides.Left,
            5 => BorderSides.Top | BorderSides.Bottom | BorderSides.Left,
            6 => BorderSides.Right | BorderSides.Bottom | BorderSides.Left,
            _ => BorderSides.All,
        };

    if (group.GetRenderable("partial-phase") is TextRenderable partialPhase)
        partialPhase.ContentText = $"Phase: {phase + 1}/8";
}

void InitializeAnimationTab(BoxRenderable group)
{
    AddText(group, "anim-title", "Animation Demonstrations", 10, 5, "#FFFF00",
        TextAttributes.Bold | TextAttributes.Underline);

    AddText(group, "moving-text", "Moving Text", (int)animPosition, 8, "#00FF00",
        TextAttributes.Bold | TextAttributes.Underline);

    AddBox(group, "animated-box", (int)animPosition, 10, 10, 3, "#550055", BorderStyle.Rounded, "#FF00FF");
    AddBox(group, "color-box", 50, 12, 18, 5, "#550055", BorderStyle.Double, "#FFFFFF");
    AddText(group, "color-box-title", "Animated Color", 52, 14, "#FFFFFF", TextAttributes.Bold);
}

void UpdateAnimationTab(float deltaTime, BoxRenderable group)
{
    float deltaSeconds = Math.Min(deltaTime / 1000f, 0.1f);
    animPosition += AnimSpeed * animDirection * deltaSeconds;

    if (animPosition > 40f)
    {
        animPosition = 40f;
        animDirection = -1;
    }
    else if (animPosition < 5f)
    {
        animPosition = 5f;
        animDirection = 1;
    }

    int x = (int)Math.Round(animPosition);

    if (group.GetRenderable("moving-text") is TextRenderable movingText)
        movingText.X = x;

    if (group.GetRenderable("animated-box") is BoxRenderable animatedBox)
        animatedBox.X = x;

    if (group.GetRenderable("color-box") is BoxRenderable colorBox)
    {
        float hue = (float)((Environment.TickCount64 / 1000d * 30d) % 360d);
        colorBox.BackgroundColor = Rgba.FromHsv(hue, 1f, 0.7f);
    }
}

void InitializeTitlesTab(BoxRenderable group)
{
    AddText(group, "layout-title", "Box Titles", 10, 5, "#FFFF00",
        TextAttributes.Bold | TextAttributes.Underline);

    AddBox(group, "titled-left", 10, 8, 20, 5, "#222244", BorderStyle.Single, "#FFFFFF",
        title: "Left Aligned", titleAlignment: TitleAlignment.Left);

    AddBox(group, "titled-center", 35, 8, 20, 5, "#442222", BorderStyle.Double, "#FFFFFF",
        title: "Centered Title", titleAlignment: TitleAlignment.Center);

    AddBox(group, "titled-right", 60, 8, 20, 5, "#224422", BorderStyle.Rounded, "#FFFFFF",
        title: "Right Aligned", titleAlignment: TitleAlignment.Right);
}

void InitializeInteractiveTab(BoxRenderable group)
{
    AddText(group, "interactive-title", "Interactive Controls", 10, 5, "#FFFF00",
        TextAttributes.Bold | TextAttributes.Underline);

    AddBox(group, "interactive-border", 15, 8, 40, 8, "#333344", BorderStyle.Double, "#FFFFFF");
    AddText(group, "interactive-label", "Press keys to toggle borders", 22, 12, "#FFFFFF", TextAttributes.Bold);

    AddText(group, "interactive-instructions", "Keyboard Controls:", 10, 18, "#FFFFFF",
        TextAttributes.Underline);
    AddText(group, "key-t", "T - Toggle top border", 10, 19, "#CCCCCC");
    AddText(group, "key-r", "R - Toggle right border", 10, 20, "#CCCCCC");
    AddText(group, "key-b", "B - Toggle bottom border", 10, 21, "#CCCCCC");
    AddText(group, "key-l", "L - Toggle left border", 10, 22, "#CCCCCC");
    AddText(group, "border-state", "Active borders: Top Right Bottom Left", 10, 24, "#AAAAAA");
}

void UpdateInteractiveTab(float deltaTime, BoxRenderable group)
{
    if (group.GetRenderable("interactive-border") is BoxRenderable borderBox)
        borderBox.ActiveBorderSides = interactiveBorderSides;

    string description = interactiveBorderSides == BorderSides.None
        ? "None"
        : string.Join(' ',
        [
            .. ((interactiveBorderSides & BorderSides.Top) != 0 ? new[] { "Top" } : Array.Empty<string>()),
            .. ((interactiveBorderSides & BorderSides.Right) != 0 ? new[] { "Right" } : Array.Empty<string>()),
            .. ((interactiveBorderSides & BorderSides.Bottom) != 0 ? new[] { "Bottom" } : Array.Empty<string>()),
            .. ((interactiveBorderSides & BorderSides.Left) != 0 ? new[] { "Left" } : Array.Empty<string>()),
        ]);

    if (group.GetRenderable("border-state") is TextRenderable borderState)
        borderState.ContentText = $"Active borders: {description}";
}

TextRenderable AddText(
    BoxRenderable group,
    string id,
    string content,
    int left,
    int top,
    string fgHex,
    TextAttributes attributes = TextAttributes.None,
    int zIndex = 10)
{
    var text = new TextRenderable(renderer, new TextOptions
    {
        Id = id,
        Content = content,
        Position = PositionValue.Absolute,
        Left = DimensionValue.Point(left),
        Top = DimensionValue.Point(top),
        Fg = Hex(fgHex),
        Attributes = attributes,
        ZIndex = zIndex,
    });

    group.Add(text);
    return text;
}

BoxRenderable AddBox(
    BoxRenderable group,
    string id,
    int left,
    int top,
    int width,
    int height,
    string backgroundHex,
    BorderStyle borderStyle,
    string borderHex,
    string? title = null,
    TitleAlignment titleAlignment = TitleAlignment.Left,
    int zIndex = 0,
    BorderSides sides = BorderSides.All,
    BorderCharacters? customBorderChars = null)
{
    var box = new BoxRenderable(renderer, new BoxOptions
    {
        Id = id,
        Position = PositionValue.Absolute,
        Left = DimensionValue.Point(left),
        Top = DimensionValue.Point(top),
        Width = DimensionValue.Point(width),
        Height = DimensionValue.Point(height),
        BackgroundColor = Hex(backgroundHex),
        ZIndex = zIndex,
        BorderStyle = borderStyle,
        BorderColor = Hex(borderHex),
        Border = sides != BorderSides.None,
        BorderSidesOverride = sides,
        Title = title,
        TitleAlignment = titleAlignment,
        CustomBorderChars = customBorderChars,
    });

    group.Add(box);
    return box;
}
