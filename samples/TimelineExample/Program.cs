using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig { ExitOnCtrlC = true, TargetFps = 30 });
TimelineEngine.Instance.Attach(renderer);

var root = new BoxRenderable(renderer, new BoxOptions
{
    Id = "root",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Percent(100),
    FlexDirection = FlexDirectionValue.Column,
    BackgroundColor = Rgba.FromHex("#0f0f23"),
    ShouldFill = true,
});

// Title
var titleBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "title-box",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#1a1a3e"),
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Rgba.FromHex("#ff6b6b"),
    JustifyContent = JustifyValue.Center,
    AlignItems = AlignValue.Center,
});
titleBox.Add(new TextRenderable(renderer, new TextOptions
{
    Id = "title",
    Content = "Timeline Animation Demo",
    Fg = Rgba.FromHex("#ff6b6b"),
    Attributes = TextAttributes.Bold,
}));

// Animation stage
var stage = new BoxRenderable(renderer, new BoxOptions
{
    Id = "stage",
    FlexGrow = 1,
    Width = DimensionValue.Percent(100),
    BackgroundColor = Rgba.FromHex("#0f0f23"),
    ShouldFill = true,
});

// The animated box
var animBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "anim-box",
    Position = PositionValue.Absolute,
    Width = DimensionValue.Point(20),
    Height = DimensionValue.Point(5),
    Top = DimensionValue.Point(2),
    Left = DimensionValue.Point(2),
    BackgroundColor = Rgba.FromHex("#4ecdc4"),
    ShouldFill = true,
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Rgba.White,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    Opacity = 1.0f,
});
var animLabel = new TextRenderable(renderer, new TextOptions
{
    Id = "anim-label",
    Content = "▶ Animating",
    Fg = Rgba.White,
    Attributes = TextAttributes.Bold,
});
animBox.Add(animLabel);
stage.Add(animBox);

// Status display
var statusBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "status-box",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Point(5),
    BackgroundColor = Rgba.FromHex("#1a1a3e"),
    ShouldFill = true,
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Rgba.FromHex("#333366"),
    FlexDirection = FlexDirectionValue.Column,
    Padding = DimensionValue.Point(1),
    Title = " Status ",
});
var statusText = new TextRenderable(renderer, new TextOptions
{
    Id = "status-text",
    Fg = Rgba.FromHex("#aaaaaa"),
});
statusBox.Add(statusText);

// Footer
var footer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "footer",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#1a1a3e"),
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Rgba.FromHex("#555555"),
    JustifyContent = JustifyValue.Center,
    AlignItems = AlignValue.Center,
});
footer.Add(new TextRenderable(renderer, new TextOptions
{
    Id = "footer-text",
    StyledContent = new StyledText(
        TextChunk.Styled("space", fg: Rgba.FromHex("#4ecdc4"), attributes: TextAttributes.Bold),
        TextChunk.Styled(" play/pause  |  ", fg: Rgba.FromHex("#888888")),
        TextChunk.Styled("r", fg: Rgba.FromHex("#4ecdc4"), attributes: TextAttributes.Bold),
        TextChunk.Styled(" restart  |  ", fg: Rgba.FromHex("#888888")),
        TextChunk.Styled("Ctrl+C", fg: Rgba.FromHex("#4ecdc4"), attributes: TextAttributes.Bold),
        TextChunk.Styled(" exit", fg: Rgba.FromHex("#888888"))
    ),
}));

root.Add(titleBox);
root.Add(stage);
root.Add(statusBox);
root.Add(footer);
renderer.Root.Add(root);

// Create the main timeline: 6 seconds, loops with alternate
float totalDuration = 6000;
var tl = TimelineFactory.CreateTimeline(new TimelineOptions
{
    Duration = totalDuration,
    Loop = true,
});

// Track position as floats for smooth animation
float posX = 2, posY = 2;

// Animation 1: Move horizontally (0-2s, outQuad)
tl.Add(new AnimationOptions
{
    Duration = 2000,
    Ease = "outQuad",
    Properties =
    [
        new TweenProperty
        {
            Get = () => posX,
            Set = v => { posX = v; animBox.Left = DimensionValue.Point(v); },
            EndValue = 50,
        },
    ],
}, startTime: 0);

// Animation 2: Move vertically + fade (1s-3s, outBounce)
tl.Add(new AnimationOptions
{
    Duration = 2000,
    Ease = "outBounce",
    Properties =
    [
        new TweenProperty
        {
            Get = () => posY,
            Set = v => { posY = v; animBox.Top = DimensionValue.Point(v); },
            EndValue = 12,
        },
    ],
}, startTime: 1000);

// Animation 3: Opacity pulse (2s-4s, inOutSine, alternate loop)
tl.Add(new AnimationOptions
{
    Duration = 1000,
    Ease = "inOutSine",
    Loop = true,
    LoopCount = 2,
    Alternate = true,
    Properties =
    [
        new TweenProperty
        {
            Get = () => animBox.Opacity,
            Set = v => animBox.Opacity = v,
            EndValue = 0.3f,
        },
    ],
}, startTime: 2000);

// Animation 4: Return to origin (4s-6s, outElastic)
tl.Add(new AnimationOptions
{
    Duration = 2000,
    Ease = "outElastic",
    Properties =
    [
        new TweenProperty
        {
            Get = () => posX,
            Set = v => { posX = v; animBox.Left = DimensionValue.Point(v); },
            EndValue = 2,
        },
        new TweenProperty
        {
            Get = () => posY,
            Set = v => { posY = v; animBox.Top = DimensionValue.Point(v); },
            EndValue = 2,
        },
    ],
}, startTime: 4000);

// Update status display every frame
renderer.AddFrameCallback(dt =>
{
    float time = tl.CurrentTime;
    float progress = totalDuration > 0 ? (time / totalDuration) * 100f : 0;
    string state = tl.IsPlaying ? "▶ Playing" : tl.IsComplete ? "⏹ Complete" : "⏸ Paused";

    int barWidth = 30;
    int filled = (int)(progress / 100f * barWidth);
    string bar = new string('█', Math.Min(filled, barWidth)) +
                 new string('░', Math.Max(barWidth - filled, 0));

    statusText.Content = new StyledText(
        TextChunk.Styled($"{state}", fg: tl.IsPlaying ? Rgba.FromHex("#4ecdc4") : Rgba.FromHex("#ff6b6b"),
            attributes: TextAttributes.Bold),
        TextChunk.Styled($"  Time: {time / 1000:F1}s / {totalDuration / 1000:F1}s", fg: Rgba.FromHex("#aaaaaa")),
        TextChunk.Styled($"  [{bar}] {progress:F0}%", fg: Rgba.FromHex("#666666"))
    );

    return Task.CompletedTask;
});

// Keyboard controls
renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    switch (e.Name)
    {
        case " ":
            if (tl.IsPlaying) tl.Pause();
            else tl.Play();
            break;
        case "r":
            tl.Restart();
            tl.Play();
            break;
    }
});

await Task.Delay(Timeout.Infinite);
