using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 30,
    UseMouse = true,
    ScreenMode = ScreenMode.SplitFooter,
    FooterHeight = 20,
    ExternalOutputMode = ExternalOutputMode.CaptureStdout,
});

renderer.SetBackgroundColor(Rgba.FromHex("#001122"));

var dashboard = new SplitModeDashboard(renderer);
int outputIntervalMs = 100;
int messageCount = 0;
Timer? outputTimer = null;

renderer.RequestLive();
renderer.AddFrameCallback(deltaTime =>
{
    dashboard.Update(deltaTime);
    return Task.CompletedTask;
});

renderer.On<(int Width, int Height)>(RendererEventNames.Resize, _ => dashboard.SyncLayout());

renderer.KeyInput.On<KeyEvent>("keypress", key =>
{
    switch (key.Name)
    {
        case "+":
            renderer.FooterHeight = Math.Min(renderer.FooterHeight + 1, Math.Max(5, renderer.TerminalHeight - 5));
            dashboard.SyncLayout();
            ReportStatus($"Split height increased to {renderer.FooterHeight}");
            break;
        case "-":
            renderer.FooterHeight = Math.Max(renderer.FooterHeight - 1, 5);
            dashboard.SyncLayout();
            ReportStatus($"Split height decreased to {renderer.FooterHeight}");
            break;
        case "0":
            if (renderer.ScreenMode == ScreenMode.SplitFooter)
            {
                renderer.ExternalOutputMode = ExternalOutputMode.Passthrough;
                renderer.ScreenMode = ScreenMode.MainScreen;
                ReportStatus("Switched to main-screen mode");
            }
            else
            {
                renderer.FooterHeight = 20;
                renderer.ScreenMode = ScreenMode.SplitFooter;
                renderer.ExternalOutputMode = ExternalOutputMode.CaptureStdout;
                RestartOutputTimer();
                ReportStatus("Switched to split-footer mode (height 20)");
            }

            dashboard.SyncLayout();
            break;
        case "m":
            outputIntervalMs = Math.Max(5, outputIntervalMs - 5);
            RestartOutputTimer();
            ReportStatus($"Test output speed increased (interval: {outputIntervalMs}ms)");
            break;
        case "l":
            outputIntervalMs = Math.Min(1000, outputIntervalMs + 5);
            RestartOutputTimer();
            ReportStatus($"Test output speed decreased (interval: {outputIntervalMs}ms)");
            break;
        case "u":
            renderer.UseMouse = !renderer.UseMouse;
            ReportStatus($"Mouse functionality {(renderer.UseMouse ? "enabled" : "disabled")}");
            break;
    }
});

Console.WriteLine("=== Split Mode Demo ===");
Console.WriteLine($"Terminal size: {renderer.TerminalWidth}x{renderer.TerminalHeight}");
Console.WriteLine($"Renderer split height: {renderer.FooterHeight}");
Console.WriteLine($"Renderer offset: {renderer.TerminalHeight - renderer.FooterHeight}");
Console.WriteLine("Console output should appear here and scroll naturally");
Console.WriteLine("The renderer should stay fixed at the bottom as a footer");
Console.WriteLine($"Test output running at {outputIntervalMs}ms intervals (use M/L to adjust speed)");
Console.WriteLine($"Mouse functionality: {(renderer.UseMouse ? "enabled" : "disabled")} (use U to toggle)");

RestartOutputTimer();
dashboard.SyncLayout();
await Task.Delay(Timeout.Infinite);

void RestartOutputTimer()
{
    outputTimer?.Dispose();
    outputTimer = null;

    outputTimer = new Timer(_ =>
    {
        int count = Interlocked.Increment(ref messageCount);
        Console.WriteLine($"Test output {count}: This should appear above the renderer and scroll naturally");
    }, null, outputIntervalMs, outputIntervalMs);
}

void ReportStatus(string message)
{
    dashboard.SetNotice(message);
    Console.WriteLine(message);
}

file sealed class SplitModeDashboard
{
    private readonly CliRenderer _renderer;
    private readonly BoxRenderable _container;
    private readonly TextRenderable _headerText;
    private readonly TextRenderable _instructionsText;
    private readonly TextRenderable _statusText;
    private readonly BoxRenderable _statusPanel;
    private readonly BoxRenderable _statsPanel;
    private readonly BoxRenderable[] _systemBackgroundBars;
    private readonly BoxRenderable[] _systemLoadingBars;
    private readonly TextRenderable[] _statusCounters;
    private readonly BoxRenderable[] _movingOrbs;
    private readonly BoxRenderable[] _pulsingElements;
    private float _elapsedMs;
    private string? _notice;

    public SplitModeDashboard(CliRenderer renderer)
    {
        _renderer = renderer;

        _container = new BoxRenderable(renderer, new BoxOptions
        {
            Id = "split-mode-container",
            Position = PositionValue.Absolute,
            Left = 0,
            Top = 0,
            Width = renderer.Width,
            Height = renderer.Height,
            ZIndex = 5,
        });
        renderer.Root.Add(_container);

        _headerText = new TextRenderable(renderer, new TextOptions
        {
            Id = "demo-text",
            Position = PositionValue.Absolute,
            Left = 2,
            Top = 0,
            ZIndex = 10,
            StyledContent = new StyledText(
                TextChunk.Styled("◆ SPLIT MODE DEMO - ANIMATED DASHBOARD ◆", fg: Rgba.FromHex("#00ffff"), attributes: TextAttributes.Bold)),
        });
        _container.Add(_headerText);

        _statusPanel = new BoxRenderable(renderer, new BoxOptions
        {
            Id = "status-panel",
            Position = PositionValue.Absolute,
            Left = 2,
            Top = 5,
            Width = Math.Max(1, renderer.Width - 6),
            Height = 8,
            BackgroundColor = Rgba.FromHex("#1a1a2e"),
            ZIndex = 1,
            Border = true,
            BorderStyle = BorderStyle.Double,
            BorderColor = Rgba.FromHex("#4a4a6a"),
            Title = "◆ SYSTEM MONITOR ◆",
            TitleAlignment = TitleAlignment.Center,
        });
        _container.Add(_statusPanel);

        string[] systems = ["CPU", "MEM", "NET", "DSK"];
        string[] systemColors = ["#6a5acd", "#4682b4", "#20b2aa", "#daa520"];
        _systemBackgroundBars = new BoxRenderable[systems.Length];
        _systemLoadingBars = new BoxRenderable[systems.Length];
        for (int i = 0; i < systems.Length; i++)
        {
            int y = 6 + i;
            _container.Add(new TextRenderable(renderer, new TextOptions
            {
                Id = $"{systems[i].ToLowerInvariant()}-label",
                Content = $"{systems[i]}:",
                Position = PositionValue.Absolute,
                Left = 4,
                Top = y,
                Fg = Rgba.FromHex(systemColors[i]),
                ZIndex = 2,
            }));

            _systemBackgroundBars[i] = new BoxRenderable(renderer, new BoxOptions
            {
                Id = $"{systems[i].ToLowerInvariant()}-bg",
                Position = PositionValue.Absolute,
                Left = 9,
                Top = y,
                Width = Math.Max(1, renderer.Width - 16),
                Height = 1,
                BackgroundColor = Rgba.FromHex("#333333"),
                ZIndex = 1,
            });
            _container.Add(_systemBackgroundBars[i]);

            _systemLoadingBars[i] = new BoxRenderable(renderer, new BoxOptions
            {
                Id = $"{systems[i].ToLowerInvariant()}-progress",
                Position = PositionValue.Absolute,
                Left = 9,
                Top = y,
                Width = 1,
                Height = 1,
                BackgroundColor = Rgba.FromHex(systemColors[i]),
                ZIndex = 2,
            });
            _container.Add(_systemLoadingBars[i]);
        }

        _statsPanel = new BoxRenderable(renderer, new BoxOptions
        {
            Id = "stats-panel",
            Position = PositionValue.Absolute,
            Left = 2,
            Top = 14,
            Width = Math.Max(1, renderer.Width - 6),
            Height = 4,
            BackgroundColor = Rgba.FromHex("#2d1b2e"),
            ZIndex = 1,
            Border = true,
            BorderStyle = BorderStyle.Single,
            BorderColor = Rgba.FromHex("#8a4a8a"),
            Title = "◇ REAL-TIME STATS ◇",
            TitleAlignment = TitleAlignment.Center,
        });
        _container.Add(_statsPanel);

        _statusCounters = new TextRenderable[4];
        string[] counterLabels = ["PACKETS", "CONN", "PROC", "UP"];
        for (int i = 0; i < _statusCounters.Length; i++)
        {
            _statusCounters[i] = new TextRenderable(renderer, new TextOptions
            {
                Id = $"counter-{i}",
                Position = PositionValue.Absolute,
                Left = 4 + (i * 15),
                Top = 15,
                Fg = Rgba.FromHex("#9a9acd"),
                ZIndex = 2,
                Content = $"{counterLabels[i]}: 0",
            });
            _container.Add(_statusCounters[i]);
        }

        _movingOrbs = new[]
        {
            CreateOrb(renderer, "orb-0", "#ff6b9d"),
            CreateOrb(renderer, "orb-1", "#4ecdc4"),
            CreateOrb(renderer, "orb-2", "#ffe66d"),
        };

        _pulsingElements = new[]
        {
            CreatePulse(renderer, "pulse-0", "#ff8a80"),
            CreatePulse(renderer, "pulse-1", "#80cbc4"),
            CreatePulse(renderer, "pulse-2", "#fff176"),
        };

        _instructionsText = new TextRenderable(renderer, new TextOptions
        {
            Id = "split-mode-instructions",
            Position = PositionValue.Absolute,
            Left = 2,
            Top = 19,
            ZIndex = 10,
            StyledContent = new StyledText(
                TextChunk.Styled("[+/-] Split height | [0] Toggle fullscreen | [M/L] Output speed | [U] Toggle mouse", fg: Rgba.FromHex("#cccccc"), attributes: TextAttributes.Bold)),
        });
        _container.Add(_instructionsText);

        _statusText = new TextRenderable(renderer, new TextOptions
        {
            Id = "split-mode-status",
            Position = PositionValue.Absolute,
            Left = 2,
            Top = 2,
            ZIndex = 10,
            Fg = Rgba.FromHex("#9ad1ff"),
        });
        _container.Add(_statusText);

        SyncLayout();
        UpdateStatus();
    }

    public void SyncLayout()
    {
        _container.WidthDimension = DimensionValue.Point(Math.Max(1, _renderer.Width));
        _container.HeightDimension = DimensionValue.Point(Math.Max(1, _renderer.Height));

        _statusPanel.WidthDimension = DimensionValue.Point(Math.Max(1, _renderer.Width - 6));
        _statsPanel.WidthDimension = DimensionValue.Point(Math.Max(1, _renderer.Width - 6));

        int backgroundBarWidth = Math.Max(1, _renderer.Width - 16);
        for (int i = 0; i < _systemBackgroundBars.Length; i++)
            _systemBackgroundBars[i].WidthDimension = DimensionValue.Point(backgroundBarWidth);

        _headerText.WidthDimension = DimensionValue.Point(Math.Max(10, _renderer.Width - 4));
        _headerText.HeightDimension = DimensionValue.Point(2);

        _instructionsText.Top = DimensionValue.Point(Math.Max(0, _renderer.Height - 1));
        _instructionsText.WidthDimension = DimensionValue.Point(Math.Max(10, _renderer.Width - 4));
        _instructionsText.HeightDimension = DimensionValue.Point(1);

        _statusText.WidthDimension = DimensionValue.Point(Math.Max(10, _renderer.Width - 4));
        _statusText.HeightDimension = DimensionValue.Point(2);

        UpdateStatus();
    }

    public void Update(float deltaTime)
    {
        _elapsedMs += deltaTime;
        float t = _elapsedMs / 1000f;
        int maxBarWidth = Math.Max(1, _renderer.Width - 16);

        float[] progress =
        [
            NormalizeWave(t * 1.10f, 20f, 85f),
            NormalizeWave(t * 0.90f + 0.7f, 30f, 70f),
            NormalizeWave(t * 1.35f + 1.2f, 15f, 95f),
            NormalizeWave(t * 0.75f + 1.8f, 25f, 60f),
        ];

        for (int i = 0; i < _systemLoadingBars.Length; i++)
            _systemLoadingBars[i].WidthDimension = DimensionValue.Point(Math.Max(1, (int)MathF.Floor((progress[i] / 100f) * maxBarWidth)));

        _statusCounters[0].SetContent($"PACKETS: {(int)(12847 + (_elapsedMs * 7.5f))}");
        _statusCounters[1].SetContent($"CONN: {(int)NormalizeWave(t * 0.6f, 120f, 234f)}");
        _statusCounters[2].SetContent($"PROC: {(int)NormalizeWave(t * 0.8f + 0.8f, 140f, 187f)}");
        _statusCounters[3].SetContent($"UP: {(int)(_elapsedMs / 1000f)}s");

        int travelWidth = Math.Max(0, _renderer.Width - 13);
        for (int i = 0; i < _movingOrbs.Length; i++)
        {
            float wave = (MathF.Sin((t * 1.1f) + (i * 0.8f)) + 1f) * 0.5f;
            _movingOrbs[i].X = 2 + (int)MathF.Floor(wave * travelWidth);
        }

        int pulseBaseX = Math.Max(2, _renderer.Width - 8);
        for (int i = 0; i < _pulsingElements.Length; i++)
        {
            _pulsingElements[i].X = pulseBaseX + (i * 2);
            _pulsingElements[i].HeightDimension = DimensionValue.Point(Math.Clamp(1 + (int)MathF.Floor((MathF.Sin((t * 3f) + (i * 0.6f)) + 1f)), 1, 3));
        }

        UpdateStatus();
    }

    public void SetNotice(string? notice)
    {
        _notice = notice;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        var chunks = new List<TextChunk>
        {
            TextChunk.Styled("Mode: ", fg: Rgba.FromHex("#7dd3fc"), attributes: TextAttributes.Bold),
            TextChunk.Styled($"{_renderer.ScreenMode}  ", fg: Rgba.White),
            TextChunk.Styled("Footer: ", fg: Rgba.FromHex("#c4b5fd"), attributes: TextAttributes.Bold),
            TextChunk.Styled($"{_renderer.FooterHeight}  ", fg: Rgba.White),
            TextChunk.Styled("Terminal: ", fg: Rgba.FromHex("#86efac"), attributes: TextAttributes.Bold),
            TextChunk.Styled($"{_renderer.TerminalWidth}x{_renderer.TerminalHeight}  ", fg: Rgba.White),
            TextChunk.Styled("Mouse: ", fg: Rgba.FromHex("#f9a8d4"), attributes: TextAttributes.Bold),
            TextChunk.Styled(_renderer.UseMouse ? "on" : "off", fg: Rgba.White),
        };

        if (!string.IsNullOrWhiteSpace(_notice))
        {
            chunks.Add(TextChunk.Styled("  |  ", fg: Rgba.FromHex("#64748b")));
            chunks.Add(TextChunk.Styled(_notice, fg: Rgba.FromHex("#fbbf24"), attributes: TextAttributes.Bold));
        }

        _statusText.Content = new StyledText([.. chunks]);
    }

    private BoxRenderable CreateOrb(CliRenderer renderer, string id, string color)
    {
        var orb = new BoxRenderable(renderer, new BoxOptions
        {
            Id = id,
            Position = PositionValue.Absolute,
            Left = 2,
            Top = id[^1] switch
            {
                '0' => 2,
                '1' => 3,
                _ => 2,
            },
            Width = 3,
            Height = 1,
            BackgroundColor = Rgba.FromHex(color),
            ZIndex = 3,
        });
        _container.Add(orb);
        return orb;
    }

    private BoxRenderable CreatePulse(CliRenderer renderer, string id, string color)
    {
        var pulse = new BoxRenderable(renderer, new BoxOptions
        {
            Id = id,
            Position = PositionValue.Absolute,
            Left = 2,
            Top = 1,
            Width = 1,
            Height = 1,
            BackgroundColor = Rgba.FromHex(color),
            ZIndex = 3,
        });
        _container.Add(pulse);
        return pulse;
    }

    private static float NormalizeWave(float time, float min, float max) =>
        min + (((MathF.Sin(time) + 1f) * 0.5f) * (max - min));
}
