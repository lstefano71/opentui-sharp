using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 60,
    BackgroundColor = Rgba.FromHex("#000028"),
});

var example = new TimelineExample(renderer);

renderer.AddFrameCallback(deltaTime =>
{
    example.Update(deltaTime);
    return Task.CompletedTask;
});

renderer.KeyInput.On("keypress", (KeyEvent key) =>
{
    switch (key.Name)
    {
        case "p":
            example.Pause();
            break;
        case "r":
            example.Start();
            break;
    }
});

example.Start();
renderer.RequestRender();
await Task.Delay(Timeout.Infinite);

file sealed class TimelineExample
{
    private readonly Timeline _mainTimeline;
    private readonly Timeline _subTimeline1;
    private readonly Timeline _subTimeline2;
    private readonly CliRenderer _renderer;
    private readonly BoxRenderable _parentContainer;
    private readonly BoxRenderable _boxObject;
    private readonly BoxRenderable _colorObject;
    private readonly BoxRenderable _physicsObject;
    private readonly BoxRenderable _alternatingObject;
    private readonly BoxRenderable _mainProgressBox;
    private readonly BoxRenderable _sub1ProgressBox;
    private readonly BoxRenderable _sub2ProgressBox;
    private readonly TextRenderable _statusLine1;
    private readonly TextRenderable _statusLine2;
    private readonly TextRenderable _statusLine3;
    private readonly TextRenderable _statusLine4;
    private readonly TextRenderable _statusLine5;
    private readonly TextRenderable _statusLine6;
    private readonly TextRenderable _statusLine7;
    private readonly TextRenderable _statusLine8;
    private readonly TextRenderable _statusLine9;

    public TimelineExample(CliRenderer renderer)
    {
        using var suspendRenderRequests = renderer.SuspendRenderRequests();

        _renderer = renderer;
        _mainTimeline = new Timeline(new TimelineOptions
        {
            Duration = 10000,
            Loop = true,
        });
        _subTimeline1 = new Timeline(new TimelineOptions
        {
            Duration = 8000,
            AutoPlay = false,
        });
        _subTimeline2 = new Timeline(new TimelineOptions
        {
            Duration = 6000,
            AutoPlay = false,
        });

        _mainTimeline.Sync(_subTimeline1, 0);
        _mainTimeline.Sync(_subTimeline2, 3000);

        _parentContainer = new BoxRenderable(renderer, new BoxOptions
        {
            Id = "timeline-container",
            ZIndex = 10,
        });

        _boxObject = new BoxRenderable(renderer, new BoxOptions
        {
            Id = "box-object",
            Position = PositionValue.Absolute,
            Left = 10,
            Top = 8,
            Width = 8,
            Height = 4,
            BackgroundColor = Rgba.FromHex("#FF6B6B"),
            ZIndex = 1,
            Border = true,
            BorderStyle = BorderStyle.Single,
            BorderColor = Rgba.White,
            Title = "Box",
            TitleAlignment = TitleAlignment.Center,
        });
        _parentContainer.Add(_boxObject);

        _colorObject = new BoxRenderable(renderer, new BoxOptions
        {
            Id = "color-object",
            Position = PositionValue.Absolute,
            Left = 25,
            Top = 8,
            Width = 12,
            Height = 4,
            BackgroundColor = Rgba.FromHex("#FF0000"),
            ZIndex = 1,
            Border = true,
            BorderStyle = BorderStyle.Single,
            BorderColor = Rgba.White,
            Title = "Color",
            TitleAlignment = TitleAlignment.Center,
        });
        _parentContainer.Add(_colorObject);

        _physicsObject = new BoxRenderable(renderer, new BoxOptions
        {
            Id = "physics-object",
            Position = PositionValue.Absolute,
            Left = 45,
            Top = 8,
            Width = 12,
            Height = 4,
            BackgroundColor = Rgba.FromHex("#4ECDC4"),
            ZIndex = 1,
            Border = true,
            BorderStyle = BorderStyle.Single,
            BorderColor = Rgba.White,
            Title = "Physics",
            TitleAlignment = TitleAlignment.Center,
        });
        _parentContainer.Add(_physicsObject);

        _alternatingObject = new BoxRenderable(renderer, new BoxOptions
        {
            Id = "alternating-object",
            Position = PositionValue.Absolute,
            Left = 1,
            Top = 1,
            Width = 8,
            Height = 4,
            BackgroundColor = Rgba.FromHex("#9B59B6"),
            ZIndex = 1,
            Border = true,
            BorderStyle = BorderStyle.Single,
            BorderColor = Rgba.White,
            Title = "Alternate",
            TitleAlignment = TitleAlignment.Center,
        });
        _parentContainer.Add(_alternatingObject);

        _parentContainer.Add(new BoxRenderable(renderer, new BoxOptions
        {
            Id = "main-timeline",
            Position = PositionValue.Absolute,
            Left = 2,
            Top = 15,
            Width = 60,
            Height = 3,
            BackgroundColor = Rgba.FromHex("#333366"),
            ZIndex = 1,
            Border = true,
            BorderStyle = BorderStyle.Single,
            BorderColor = Rgba.White,
            Title = "Main Timeline (20s)",
            TitleAlignment = TitleAlignment.Left,
        }));

        _parentContainer.Add(new BoxRenderable(renderer, new BoxOptions
        {
            Id = "sub-timeline-1",
            Position = PositionValue.Absolute,
            Left = 2,
            Top = 19,
            Width = 30,
            Height = 3,
            BackgroundColor = Rgba.FromHex("#333366"),
            ZIndex = 1,
            Border = true,
            BorderStyle = BorderStyle.Single,
            BorderColor = Rgba.White,
            Title = "Sub Timeline 1 (8s)",
            TitleAlignment = TitleAlignment.Left,
        }));

        _parentContainer.Add(new BoxRenderable(renderer, new BoxOptions
        {
            Id = "sub-timeline-2",
            Position = PositionValue.Absolute,
            Left = 35,
            Top = 19,
            Width = 27,
            Height = 3,
            BackgroundColor = Rgba.FromHex("#333366"),
            ZIndex = 1,
            Border = true,
            BorderStyle = BorderStyle.Single,
            BorderColor = Rgba.White,
            Title = "Sub Timeline 2 (6s)",
            TitleAlignment = TitleAlignment.Left,
        }));

        _parentContainer.Add(new BoxRenderable(renderer, new BoxOptions
        {
            Id = "status",
            Position = PositionValue.Absolute,
            Left = 2,
            Top = 24,
            Width = 60,
            Height = 14,
            BackgroundColor = Rgba.FromHex("#1A1A2E"),
            ZIndex = 1,
            Border = true,
            BorderStyle = BorderStyle.Single,
            BorderColor = Rgba.White,
            Title = "Animation Values",
            TitleAlignment = TitleAlignment.Center,
        }));

        _mainProgressBox = CreateProgressBox(renderer, "main-progress", 3, 16, "#FFE66D");
        _sub1ProgressBox = CreateProgressBox(renderer, "sub1-progress", 3, 20, "#FF6B6B");
        _sub2ProgressBox = CreateProgressBox(renderer, "sub2-progress", 36, 20, "#4ECDC4");
        _parentContainer.Add(_mainProgressBox);
        _parentContainer.Add(_sub1ProgressBox);
        _parentContainer.Add(_sub2ProgressBox);

        _statusLine1 = CreateStatusLine(renderer, "status-line1", 25, "Timeline: Initializing...", "#FFFFFF");
        _statusLine2 = CreateStatusLine(renderer, "status-line2", 26, "Box Position: x=0.0, y=0.0", "#FFFF00");
        _statusLine3 = CreateStatusLine(renderer, "status-line3", 27, "Box Scale/Rot: scale=1.0, rot=0.0", "#FFE66D");
        _statusLine4 = CreateStatusLine(renderer, "status-line4", 28, "Color: rgb(255, 0, 0)", "#FF6B6B");
        _statusLine5 = CreateStatusLine(renderer, "status-line5", 29, "Color Opacity: 1.0", "#FF9999");
        _statusLine6 = CreateStatusLine(renderer, "status-line6", 30, "Physics: v=0.0, a=0.0, m=1.0", "#4ECDC4");
        _statusLine7 = CreateStatusLine(renderer, "status-line7", 31, "Progress: Main=0% Sub1=0% Sub2=0%", "#CCCCCC");
        _statusLine8 = CreateStatusLine(renderer, "status-line8", 32, "Example Value: 0.000 (0.0 → 0.5)", "#FFE66D");
        _statusLine9 = CreateStatusLine(renderer, "status-line9", 33, "Alternating: x=65 (left/right loop=5)", "#9B59B6");

        _parentContainer.Add(_statusLine1);
        _parentContainer.Add(_statusLine2);
        _parentContainer.Add(_statusLine3);
        _parentContainer.Add(_statusLine4);
        _parentContainer.Add(_statusLine5);
        _parentContainer.Add(_statusLine6);
        _parentContainer.Add(_statusLine7);
        _parentContainer.Add(_statusLine8);
        _parentContainer.Add(_statusLine9);

        SetupAnimations();
        _renderer.Root.Add(_parentContainer);
        UpdateVisuals();
    }

    public void Update(float deltaTime)
    {
        _mainTimeline.Update(deltaTime);
        UpdateVisuals();
    }

    public void Start()
    {
        _statusLine1.ContentText = "Starting nested timeline example...";
        _mainTimeline.Play();
        _renderer.RequestRender();
    }

    public void Pause()
    {
        _mainTimeline.Pause();
        _renderer.RequestRender();
    }

    private void UpdateVisuals()
    {
        _mainProgressBox.WidthDimension = DimensionValue.Point(Math.Max(1, (int)Math.Floor((_mainTimeline.CurrentTime / _mainTimeline.Duration) * 58)));
        _sub1ProgressBox.WidthDimension = DimensionValue.Point(Math.Max(1, (int)Math.Floor((_subTimeline1.CurrentTime / _subTimeline1.Duration) * 28)));
        _sub2ProgressBox.WidthDimension = DimensionValue.Point(Math.Max(1, (int)Math.Floor((_subTimeline2.CurrentTime / _subTimeline2.Duration) * 25)));

        int mainPercent = (int)Math.Floor((_mainTimeline.CurrentTime / _mainTimeline.Duration) * 100);
        int sub1Percent = (int)Math.Floor((_subTimeline1.CurrentTime / _subTimeline1.Duration) * 100);
        int sub2Percent = (int)Math.Floor((_subTimeline2.CurrentTime / _subTimeline2.Duration) * 100);
        _statusLine7.ContentText = $"Progress: Main={mainPercent}% Sub1={sub1Percent}% Sub2={sub2Percent}%";
    }

    private void SetupAnimations()
    {
        float boxX = 0;
        float boxY = 0;
        float boxScale = 1.0f;
        float boxRotation = 0;

        float colorRed = 255;
        float colorGreen = 0;
        float colorBlue = 0;
        float colorOpacity = 1.0f;

        float physicsVelocity = 0;
        float physicsAcceleration = 0;
        float physicsMass = 1.0f;

        float exampleValue = 0.0f;
        float alternatingX = 1;

        _subTimeline1.Add(new AnimationOptions
        {
            Duration = 2000,
            Ease = "inOutQuad",
            Properties =
            [
                new TweenProperty { Get = () => boxX, Set = v => boxX = v, EndValue = 100 },
                new TweenProperty { Get = () => boxY, Set = v => boxY = v, EndValue = 50 },
            ],
            OnUpdate = _ =>
            {
                _boxObject.Left = DimensionValue.Point(Math.Clamp(10 + MathF.Round(boxX / 3), 1, 70));
                _boxObject.Top = DimensionValue.Point(Math.Clamp(8 + MathF.Round(boxY / 5), 1, 30));
                _statusLine2.ContentText = $"Box Position: x={boxX:F1}, y={boxY:F1}";
            },
        }, 0);

        _subTimeline1.Add(new AnimationOptions
        {
            Duration = 1500,
            Ease = "inOutQuad",
            Properties =
            [
                new TweenProperty { Get = () => boxScale, Set = v => boxScale = v, EndValue = 2.0f },
                new TweenProperty { Get = () => boxRotation, Set = v => boxRotation = v, EndValue = MathF.PI },
            ],
            OnUpdate = _ =>
            {
                int size = Math.Max(4, (int)MathF.Round(4 * boxScale));
                _boxObject.WidthDimension = DimensionValue.Point(size);
                _boxObject.HeightDimension = DimensionValue.Point(Math.Max(2, (int)MathF.Round(size / 2f)));
                _statusLine3.ContentText = $"Box Scale/Rot: scale={boxScale:F2}, rot={boxRotation:F2}";
            },
        }, 1000);

        _subTimeline1.Add(new AnimationOptions
        {
            Duration = 3000,
            Ease = "inOutSine",
            Properties =
            [
                new TweenProperty { Get = () => boxX, Set = v => boxX = v, EndValue = -50 },
                new TweenProperty { Get = () => boxY, Set = v => boxY = v, EndValue = -25 },
                new TweenProperty { Get = () => boxScale, Set = v => boxScale = v, EndValue = 0.5f },
                new TweenProperty { Get = () => boxRotation, Set = v => boxRotation = v, EndValue = 0f },
            ],
            OnUpdate = _ =>
            {
                _boxObject.Left = DimensionValue.Point(Math.Clamp(10 + MathF.Round(boxX / 3), 1, 70));
                _boxObject.Top = DimensionValue.Point(Math.Clamp(8 + MathF.Round(boxY / 5), 1, 30));

                int size = Math.Max(2, (int)MathF.Round(4 * boxScale));
                _boxObject.WidthDimension = DimensionValue.Point(size);
                _boxObject.HeightDimension = DimensionValue.Point(Math.Max(1, (int)MathF.Round(size / 2f)));

                _statusLine2.ContentText = $"Box Position (Reset): x={boxX:F1}, y={boxY:F1}";
                _statusLine3.ContentText = $"Box Scale/Rot (Reset): scale={boxScale:F2}, rot={boxRotation:F2}";
            },
        }, 4000);

        _subTimeline2.Add(new AnimationOptions
        {
            Duration = 2000,
            Ease = "linear",
            Properties =
            [
                new TweenProperty { Get = () => colorRed, Set = v => colorRed = v, EndValue = 0 },
                new TweenProperty { Get = () => colorGreen, Set = v => colorGreen = v, EndValue = 255 },
                new TweenProperty { Get = () => colorBlue, Set = v => colorBlue = v, EndValue = 128 },
            ],
            OnUpdate = _ =>
            {
                int r = (int)MathF.Round(colorRed);
                int g = (int)MathF.Round(colorGreen);
                int b = (int)MathF.Round(colorBlue);
                _colorObject.BackgroundColor = Rgba.FromInts(r, g, b);
                _statusLine4.ContentText = $"Color: rgb({r}, {g}, {b})";
            },
        }, 0);

        _subTimeline2.Add(new AnimationOptions
        {
            Duration = 1000,
            Ease = "inExpo",
            Properties = [new TweenProperty { Get = () => colorOpacity, Set = v => colorOpacity = v, EndValue = 0.2f }],
            OnUpdate = _ => _statusLine5.ContentText = $"Color Opacity: {colorOpacity:F2}",
        }, 1500);

        _subTimeline2.Add(new AnimationOptions
        {
            Duration = 2500,
            Ease = "outExpo",
            Properties =
            [
                new TweenProperty { Get = () => colorRed, Set = v => colorRed = v, EndValue = 255 },
                new TweenProperty { Get = () => colorGreen, Set = v => colorGreen = v, EndValue = 255 },
                new TweenProperty { Get = () => colorBlue, Set = v => colorBlue = v, EndValue = 0 },
                new TweenProperty { Get = () => colorOpacity, Set = v => colorOpacity = v, EndValue = 1.0f },
            ],
            OnUpdate = _ =>
            {
                int r = (int)MathF.Round(colorRed);
                int g = (int)MathF.Round(colorGreen);
                int b = (int)MathF.Round(colorBlue);
                _colorObject.BackgroundColor = Rgba.FromInts(r, g, b);
                _statusLine4.ContentText = $"Final Color: rgb({r}, {g}, {b}), opacity={colorOpacity:F2}";
            },
        }, 3500);

        _mainTimeline.Call(() => _statusLine1.ContentText = "=== STARTING ANIMATION CYCLE ===", 0);

        _mainTimeline.Add(new AnimationOptions
        {
            Duration = 10000,
            Ease = "inOutSine",
            Properties = [new TweenProperty { Get = () => exampleValue, Set = v => exampleValue = v, EndValue = 0.5f }],
            OnUpdate = _ => _statusLine8.ContentText = $"Example Value: {exampleValue:F3} (0.0 → 0.5)",
        }, 0);

        _mainTimeline.Add(new AnimationOptions
        {
            Duration = 800,
            Ease = "inOutQuad",
            LoopCount = 5,
            LoopDelay = 200,
            Alternate = true,
            Properties = [new TweenProperty { Get = () => alternatingX, Set = v => alternatingX = v, EndValue = 50 }],
            OnUpdate = _ =>
            {
                _alternatingObject.Left = DimensionValue.Point(MathF.Round(alternatingX));
                _alternatingObject.Top = DimensionValue.Point(1);
                _statusLine9.ContentText = $"Alternating: x={alternatingX:F1} (left/right loop=5)";
            },
        }, 1000);

        _mainTimeline.Add(new AnimationOptions
        {
            Duration = 4000,
            Ease = "inOutSine",
            Properties =
            [
                new TweenProperty { Get = () => physicsVelocity, Set = v => physicsVelocity = v, EndValue = 50 },
                new TweenProperty { Get = () => physicsAcceleration, Set = v => physicsAcceleration = v, EndValue = 9.8f },
                new TweenProperty { Get = () => physicsMass, Set = v => physicsMass = v, EndValue = 2.5f },
            ],
            OnUpdate = _ =>
            {
                int velocityHeight = Math.Max(1, (int)MathF.Round(physicsVelocity / 6f));
                _physicsObject.HeightDimension = DimensionValue.Point(Math.Min(6, velocityHeight));
                _statusLine6.ContentText = $"Physics: v={physicsVelocity:F1}, a={physicsAcceleration:F1}, m={physicsMass:F1}";
            },
        }, 1000);

        _mainTimeline.Add(new AnimationOptions
        {
            Duration = 3000,
            Ease = "inOutSine",
            Properties =
            [
                new TweenProperty { Get = () => physicsVelocity, Set = v => physicsVelocity = v, EndValue = -20 },
                new TweenProperty { Get = () => physicsAcceleration, Set = v => physicsAcceleration = v, EndValue = -5 },
                new TweenProperty { Get = () => physicsMass, Set = v => physicsMass = v, EndValue = 0.8f },
            ],
            OnUpdate = _ =>
            {
                int velocityHeight = Math.Max(1, Math.Abs((int)MathF.Round(physicsVelocity / 4f)));
                _physicsObject.HeightDimension = DimensionValue.Point(Math.Min(6, velocityHeight));
                _statusLine6.ContentText = $"Physics Reverse: v={physicsVelocity:F1}, a={physicsAcceleration:F1}, m={physicsMass:F1}";
            },
        }, 8000);

        _mainTimeline.Call(() => _statusLine1.ContentText = "=== CYCLE COMPLETE ===", 9000);
    }

    private static BoxRenderable CreateProgressBox(CliRenderer renderer, string id, int left, int top, string color) =>
        new(renderer, new BoxOptions
        {
            Id = id,
            Position = PositionValue.Absolute,
            Left = left,
            Top = top,
            Width = 1,
            Height = 1,
            BackgroundColor = Rgba.FromHex(color),
            ZIndex = 2,
        });

    private static TextRenderable CreateStatusLine(CliRenderer renderer, string id, int top, string text, string fg) =>
        new(renderer, new TextOptions
        {
            Id = id,
            Content = text,
            Position = PositionValue.Absolute,
            Left = 4,
            Top = top,
            Fg = Rgba.FromHex(fg),
            ZIndex = 2,
        });
}
