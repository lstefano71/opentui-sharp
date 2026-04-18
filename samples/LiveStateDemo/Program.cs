using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    EnableMouseMovement = true,
    BackgroundColor = Rgba.FromInts(25, 30, 45, 255),
});

var exitTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
renderer.On(RendererEventNames.Destroy, () => exitTcs.TrySetResult());

BoxRenderable? demoRenderable = null;
TextRenderable? statusText = null;
TextRenderable? rendererStateText = null;
TextRenderable? renderableStateText = null;

int frameCounter = 0;
int animationCounter = 0;

string Timestamp() => DateTime.Now.ToString("T");

void UpdateStatusText(string message)
{
    if (statusText is not null)
        statusText.Content = $"[{Timestamp()}] {message}";
    renderer.RequestRender();
}

void UpdateRendererState()
{
    if (rendererStateText is null)
        return;

    var liveIndicators = new[] { "\u2598", "\u259D", "\u2597", "\u2596" };
    string liveIndicator = renderer.LiveRequestCount > 0
        ? $" {liveIndicators[animationCounter % liveIndicators.Length]}"
        : "";

    rendererStateText.Content =
        $"Renderer State: {(renderer.IsRunning ? "RUNNING" : "STOPPED")} | " +
        $"Live Requests: {renderer.LiveRequestCount}{liveIndicator} | " +
        $"Control State: {renderer.CurrentControlState.ToUpperInvariant()} | " +
        $"Frame: {frameCounter}";
}

void UpdateRenderableState()
{
    if (renderableStateText is null)
        return;

    bool exists = demoRenderable is not null;
    bool live = demoRenderable?.Live ?? false;
    bool visible = demoRenderable?.Visible ?? false;

    renderableStateText.Content =
        $"Demo Renderable: {(exists ? "ADDED" : "NOT ADDED")} | " +
        $"Live: {(live ? "TRUE" : "FALSE")} | " +
        $"Visible: {(visible ? "TRUE" : "FALSE")}";
}

Rgba Brighten(Rgba color, float multiplier) =>
    Rgba.FromValues(
        Math.Min(1f, color.R * multiplier),
        Math.Min(1f, color.G * multiplier),
        Math.Min(1f, color.B * multiplier),
        color.A);

Rgba Darken(Rgba color, float multiplier) =>
    Rgba.FromValues(color.R * multiplier, color.G * multiplier, color.B * multiplier, color.A);

BoxRenderable CreateLiveButton(string id, int left, int top, Rgba backgroundColor, string label, Action onPress)
{
    var hoverBackground = Brighten(backgroundColor, 1.4f);
    var pressBackground = Darken(backgroundColor, 0.6f);

    var button = new BoxRenderable(renderer, new BoxOptions
    {
        Id = id,
        Position = PositionValue.Absolute,
        Left = left,
        Top = top,
        Width = 20,
        Height = 3,
        BackgroundColor = backgroundColor,
        Border = true,
        BorderColor = Brighten(backgroundColor, 1.2f),
        BorderStyle = BorderStyle.Rounded,
    });

    button.RenderAfterHook = (buffer, _) =>
    {
        int startX = button.X + Math.Max(0, (button.Width - label.Length) / 2);
        int centerY = button.Y + Math.Max(0, button.Height / 2);
        buffer.DrawText(label, (uint)Math.Max(0, startX), (uint)Math.Max(0, centerY), Rgba.White, attrs: TextAttributes.Bold);
    };
    button.OnMouseOver = _ =>
    {
        button.BackgroundColor = hoverBackground;
        renderer.RequestRender();
    };
    button.OnMouseOut = _ =>
    {
        button.BackgroundColor = backgroundColor;
        renderer.RequestRender();
    };
    button.OnMouseDown = mouseEvent =>
    {
        button.BackgroundColor = pressBackground;
        onPress();
        mouseEvent.StopPropagation();
        renderer.RequestRender();
    };
    button.OnMouseUp = mouseEvent =>
    {
        button.BackgroundColor = backgroundColor;
        mouseEvent.StopPropagation();
        renderer.RequestRender();
    };

    return button;
}

void AddDemoRenderable()
{
    if (demoRenderable is not null)
    {
        UpdateStatusText("Demo renderable already exists!");
        return;
    }

    demoRenderable = new BoxRenderable(renderer, new BoxOptions
    {
        Id = "demo-renderable",
        Position = PositionValue.Absolute,
        Left = 60,
        Top = 15,
        Width = 30,
        Height = 8,
        BackgroundColor = Rgba.FromInts(100, 200, 150, 255),
        BorderColor = Rgba.FromInts(150, 255, 200, 255),
        BorderStyle = BorderStyle.Double,
        Title = "Demo Renderable",
        TitleAlignment = TitleAlignment.Center,
        Border = true,
    });

    renderer.Root.GetRenderable("live-demo-main-group")?.Add(demoRenderable);
    UpdateStatusText("Added demo renderable");
}

void RemoveDemoRenderable()
{
    if (demoRenderable is null)
    {
        UpdateStatusText("No demo renderable to remove!");
        return;
    }

    renderer.Root.GetRenderable("live-demo-main-group")?.Remove(demoRenderable.Id);
    demoRenderable = null;
    UpdateStatusText("Removed demo renderable");
}

var mainGroup = new BoxRenderable(renderer, new BoxOptions
{
    Id = "live-demo-main-group",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Percent(100),
    ZIndex = 10,
});
renderer.Root.Add(mainGroup);

mainGroup.Add(new TextRenderable(renderer, new TextOptions
{
    Id = "live-demo-title",
    Content = "Live State Management Demo",
    Position = PositionValue.Absolute,
    Left = 2,
    Top = 1,
    Fg = Rgba.FromInts(255, 215, 135, 255),
    Attributes = TextAttributes.Bold,
    ZIndex = 1000,
}));

mainGroup.Add(new TextRenderable(renderer, new TextOptions
{
    Id = "live-demo-instructions",
    Content = "Test the live state management system - Escape: exit",
    Position = PositionValue.Absolute,
    Left = 2,
    Top = 2,
    Fg = Rgba.FromInts(176, 196, 222, 255),
    ZIndex = 1000,
}));

statusText = new TextRenderable(renderer, new TextOptions
{
    Id = "live-demo-status",
    Content = "Ready - Click buttons to test live state management",
    Position = PositionValue.Absolute,
    Left = 2,
    Top = 4,
    Fg = Rgba.FromInts(144, 238, 144, 255),
    Attributes = TextAttributes.Italic,
    ZIndex = 1000,
});
mainGroup.Add(statusText);

rendererStateText = new TextRenderable(renderer, new TextOptions
{
    Id = "renderer-state",
    Content = "",
    Position = PositionValue.Absolute,
    Left = 2,
    Top = 6,
    Fg = Rgba.FromInts(255, 255, 100, 255),
    ZIndex = 1000,
});
mainGroup.Add(rendererStateText);

renderableStateText = new TextRenderable(renderer, new TextOptions
{
    Id = "renderable-state",
    Content = "",
    Position = PositionValue.Absolute,
    Left = 2,
    Top = 7,
    Fg = Rgba.FromInts(255, 255, 100, 255),
    ZIndex = 1000,
});
mainGroup.Add(renderableStateText);

var rendererColor = Rgba.FromInts(100, 140, 180, 255);
var renderableColor = Rgba.FromInts(180, 100, 140, 255);
var liveColor = Rgba.FromInts(140, 180, 100, 255);
var visibilityColor = Rgba.FromInts(180, 140, 100, 255);

const int startY = 10;
const int spacing = 22;

mainGroup.Add(new TextRenderable(renderer, new TextOptions
{
    Id = "renderer-label",
    Content = "Renderer Control:",
    Position = PositionValue.Absolute,
    Left = 2,
    Top = startY - 1,
    Fg = rendererColor,
    Attributes = TextAttributes.Bold,
    ZIndex = 500,
}));

mainGroup.Add(CreateLiveButton("request-live-btn", 2, startY, rendererColor, "REQUEST LIVE", () =>
{
    renderer.RequestLive();
    UpdateStatusText("Manually requested live");
    UpdateRendererState();
    UpdateRenderableState();
}));

mainGroup.Add(CreateLiveButton("drop-live-btn", 2 + spacing, startY, rendererColor, "DROP LIVE", () =>
{
    renderer.DropLive();
    UpdateStatusText("Manually dropped live");
    UpdateRendererState();
    UpdateRenderableState();
}));

mainGroup.Add(new TextRenderable(renderer, new TextOptions
{
    Id = "renderable-label",
    Content = "Renderable Management:",
    Position = PositionValue.Absolute,
    Left = 2,
    Top = startY + 4,
    Fg = renderableColor,
    Attributes = TextAttributes.Bold,
    ZIndex = 500,
}));

mainGroup.Add(CreateLiveButton("add-renderable-btn", 2, startY + 5, renderableColor, "ADD RENDERABLE", () =>
{
    AddDemoRenderable();
    UpdateRendererState();
    UpdateRenderableState();
}));

mainGroup.Add(CreateLiveButton("remove-renderable-btn", 2 + spacing, startY + 5, renderableColor, "REMOVE RENDERABLE", () =>
{
    RemoveDemoRenderable();
    UpdateRendererState();
    UpdateRenderableState();
}));

mainGroup.Add(new TextRenderable(renderer, new TextOptions
{
    Id = "live-label",
    Content = "Live State Control:",
    Position = PositionValue.Absolute,
    Left = 2,
    Top = startY + 9,
    Fg = liveColor,
    Attributes = TextAttributes.Bold,
    ZIndex = 500,
}));

mainGroup.Add(CreateLiveButton("set-live-true-btn", 2, startY + 10, liveColor, "LIVE = TRUE", () =>
{
    if (demoRenderable is not null)
    {
        demoRenderable.Live = true;
        UpdateStatusText("Set demo renderable live = true");
    }
    else
    {
        UpdateStatusText("No demo renderable to set live!");
    }
    UpdateRendererState();
    UpdateRenderableState();
}));

mainGroup.Add(CreateLiveButton("set-live-false-btn", 2 + spacing, startY + 10, liveColor, "LIVE = FALSE", () =>
{
    if (demoRenderable is not null)
    {
        demoRenderable.Live = false;
        UpdateStatusText("Set demo renderable live = false");
    }
    else
    {
        UpdateStatusText("No demo renderable to set live!");
    }
    UpdateRendererState();
    UpdateRenderableState();
}));

mainGroup.Add(new TextRenderable(renderer, new TextOptions
{
    Id = "visibility-label",
    Content = "Visibility Control:",
    Position = PositionValue.Absolute,
    Left = 2,
    Top = startY + 14,
    Fg = visibilityColor,
    Attributes = TextAttributes.Bold,
    ZIndex = 500,
}));

mainGroup.Add(CreateLiveButton("set-visible-true-btn", 2, startY + 15, visibilityColor, "VISIBLE = TRUE", () =>
{
    if (demoRenderable is not null)
    {
        demoRenderable.Visible = true;
        UpdateStatusText("Set demo renderable visible = true");
    }
    else
    {
        UpdateStatusText("No demo renderable to set visible!");
    }
    UpdateRendererState();
    UpdateRenderableState();
}));

mainGroup.Add(CreateLiveButton("set-visible-false-btn", 2 + spacing, startY + 15, visibilityColor, "VISIBLE = FALSE", () =>
{
    if (demoRenderable is not null)
    {
        demoRenderable.Visible = false;
        UpdateStatusText("Set demo renderable visible = false");
    }
    else
    {
        UpdateStatusText("No demo renderable to set visible!");
    }
    UpdateRendererState();
    UpdateRenderableState();
}));

Func<float, Task> frameCallback = deltaTime =>
{
    frameCounter++;
    if (frameCounter % 10 == 0)
    {
        animationCounter++;
        UpdateRendererState();
        UpdateRenderableState();
        renderer.RequestRender();
    }
    return Task.CompletedTask;
};
renderer.AddFrameCallback(frameCallback);

renderer.KeyInput.On("keypress", (KeyEvent keyEvent) =>
{
    if (keyEvent.Name == "escape")
    {
        renderer.Destroy();
        keyEvent.StopPropagation();
    }
});

UpdateRendererState();
UpdateRenderableState();
renderer.RequestRender();

await exitTcs.Task;
