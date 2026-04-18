using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 60,
});

renderer.Native.SetBackgroundColor(Rgba.FromHex("#001122"));

int counter = 0;
int updateFrequency = 1;
int complexTemplateCounter = 0;
DateTime startTime = DateTime.UtcNow;

var parentContainer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "styled-text-container",
    ZIndex = 15,
});
renderer.Root.Add(parentContainer);

var houseDisplay = new TextRenderable(renderer, new TextOptions
{
    Id = "house-text",
    StyledContent = new StyledText(
        TextChunk.Plain("  \nThere's a "),
        TextChunk.Styled("house", fg: Rgba.Parse("blue"), attributes: TextAttributes.Underline),
        TextChunk.Plain(",\nWith a "),
        TextChunk.Styled("window", fg: Rgba.Parse("blue"), attributes: TextAttributes.Bold),
        TextChunk.Plain(",\nAnd a "),
        TextChunk.Styled("corvette", fg: Rgba.Parse("blue")),
        TextChunk.Plain("\nAnd everything is blue")),
    Width = DimensionValue.Point(30),
    Height = DimensionValue.Point(6),
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(2),
    Top = DimensionValue.Point(2),
    ZIndex = 1,
});
parentContainer.Add(houseDisplay);

var statusDisplay = new TextRenderable(renderer, new TextOptions
{
    Id = "status-text",
    StyledContent = new StyledText(
        TextChunk.Styled("ERROR:", fg: Rgba.Parse("red"), attributes: TextAttributes.Bold),
        TextChunk.Plain(" Connection failed\n"),
        TextChunk.Styled("SUCCESS:", fg: Rgba.Parse("green"), attributes: TextAttributes.Bold),
        TextChunk.Plain(" Data loaded\n"),
        TextChunk.Styled("WARNING:", fg: Rgba.FromHex("#FFA500"), attributes: TextAttributes.Bold),
        TextChunk.Plain(" Low memory\n"),
        TextChunk.Styled(" NOTICE ", fg: Rgba.Parse("black"), bg: Rgba.Parse("yellow")),
        TextChunk.Plain(" System update available")),
    Width = DimensionValue.Point(50),
    Height = DimensionValue.Point(6),
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(2),
    Top = DimensionValue.Point(8),
    ZIndex = 1,
});
parentContainer.Add(statusDisplay);

var dashboardBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "dashboard-box",
    Width = DimensionValue.Point(72),
    Height = DimensionValue.Point(21),
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(2),
    Top = DimensionValue.Point(27),
    ZIndex = 1,
    BackgroundColor = Rgba.FromHex("#001122"),
    BorderColor = Rgba.FromHex("#00FFFF"),
    BorderStyle = BorderStyle.Single,
    Title = "COMPLEX REAL-TIME DASHBOARD",
    TitleAlignment = TitleAlignment.Center,
    Border = true,
});
parentContainer.Add(dashboardBox);

var complexDisplay = new TextRenderable(renderer, new TextOptions
{
    Id = "complex-template",
    StyledContent = BuildComplexText(0, 1, 0, startTime),
    Left = DimensionValue.Point(1),
    Top = DimensionValue.Point(1),
    ZIndex = 1,
});
dashboardBox.Add(complexDisplay);

var instructionsDisplay = new TextRenderable(renderer, new TextOptions
{
    Id = "instructions",
    StyledContent = new StyledText(
        TextChunk.Styled("Styled Text Demo", attributes: TextAttributes.Bold),
        TextChunk.Plain("\n"),
        TextChunk.Styled("ESC to return, ↑/↓ to control speed", fg: Rgba.FromHex("#888888")),
        TextChunk.Plain("\n\n"),
        TextChunk.Styled("Features demonstrated:", attributes: TextAttributes.Underline),
        TextChunk.Plain("\n"),
        TextChunk.Plain("• Template literals with "),
        TextChunk.Styled("colors", fg: Rgba.Parse("blue")),
        TextChunk.Plain("\n• "),
        TextChunk.Styled("Bold", attributes: TextAttributes.Bold),
        TextChunk.Plain(", "),
        TextChunk.Styled("underlined", attributes: TextAttributes.Underline),
        TextChunk.Plain(", and other styles\n• Background colors like "),
        TextChunk.Styled("this", fg: Rgba.Parse("black"), bg: Rgba.Parse("yellow")),
        TextChunk.Plain("\n• Custom hex colors like "),
        TextChunk.Styled("this red", fg: Rgba.FromHex("#FF6B6B")),
        TextChunk.Plain("\n• Dynamic updates with "),
        TextChunk.Styled("controllable frequency", fg: Rgba.Parse("green")),
        TextChunk.Plain("\n• Complex templates with "),
        TextChunk.Styled("many variables", fg: Rgba.Parse("red")),
        TextChunk.Plain("\n• Hyperlinks: "),
        TextChunk.Styled("opentui", fg: Rgba.Parse("blue"), attributes: TextAttributes.Underline, link: "https://opentui.com")),
    Width = DimensionValue.Point(60),
    Height = DimensionValue.Point(12),
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(40),
    Top = DimensionValue.Point(2),
    ZIndex = 1,
    Fg = Rgba.FromHex("#CCCCCC"),
});
parentContainer.Add(instructionsDisplay);

var typesDisplay = new TextRenderable(renderer, new TextOptions
{
    Id = "types-text",
    StyledContent = new StyledText(
        TextChunk.Styled("Type Examples:", attributes: TextAttributes.Bold),
        TextChunk.Plain("\nNumber: "),
        TextChunk.Styled("42", fg: Rgba.Parse("green")),
        TextChunk.Plain("\nBoolean: "),
        TextChunk.Styled("True", fg: Rgba.Parse("red")),
        TextChunk.Plain("\nFloat: "),
        TextChunk.Styled(3.14159.ToString("0.00"), fg: Rgba.Parse("blue")),
        TextChunk.Plain("\nCalculated: "),
        TextChunk.Styled(Random.Shared.Next(100).ToString(), fg: Rgba.FromHex("#00FFFF"))),
    Width = DimensionValue.Point(30),
    Height = DimensionValue.Point(6),
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(2),
    Top = DimensionValue.Point(20),
    ZIndex = 1,
});
parentContainer.Add(typesDisplay);

Func<float, Task> frameCallback = deltaTime =>
{
    counter++;
    complexTemplateCounter++;

    if (counter % 60 == 0)
    {
        var dynamicText = new StyledText(
            TextChunk.Styled("Frame:", attributes: TextAttributes.Bold),
            TextChunk.Plain(" "),
            TextChunk.Styled(counter.ToString(), fg: Rgba.White),
            TextChunk.Plain("\n"),
            TextChunk.Styled("Time:", fg: Rgba.Parse("blue")),
            TextChunk.Plain(" "),
            TextChunk.Plain((counter / 60d).ToString("0.0")),
            TextChunk.Plain("s\n"),
            TextChunk.Styled("Dynamic:", attributes: TextAttributes.Underline),
            TextChunk.Plain(" "),
            TextChunk.Styled(
                Math.Sin(counter * 0.1) > 0 ? "UP" : "DOWN",
                fg: Rgba.FromHex("#FF6B6B"),
                attributes: TextAttributes.Bold));

        if (parentContainer.GetRenderable("dynamic-text") is TextRenderable dynamicDisplay)
        {
            dynamicDisplay.Content = dynamicText;
        }
        else
        {
            parentContainer.Add(new TextRenderable(renderer, new TextOptions
            {
                Id = "dynamic-text",
                StyledContent = dynamicText,
                Width = DimensionValue.Point(40),
                Height = DimensionValue.Point(4),
                Position = PositionValue.Absolute,
                Left = DimensionValue.Point(2),
                Top = DimensionValue.Point(15),
                ZIndex = 1,
            }));
        }
    }

    if (complexTemplateCounter % updateFrequency == 0)
    {
        complexDisplay.Content = BuildComplexText(counter, updateFrequency, complexTemplateCounter, startTime);
    }

    return Task.CompletedTask;
};

renderer.AddFrameCallback(frameCallback);

renderer.KeyInput.On("keypress", (KeyEvent keyEvent) =>
{
    if (keyEvent.Name is "up" or "arrowup")
    {
        updateFrequency = Math.Max(1, updateFrequency - 1);
    }
    else if (keyEvent.Name is "down" or "arrowdown")
    {
        updateFrequency = Math.Min(60, updateFrequency + 1);
    }
});

renderer.RequestRender();
await Task.Delay(Timeout.Infinite);

static StyledText BuildComplexText(int counter, int updateFrequency, int complexTemplateCounter, DateTime startTime)
{
    double elapsedSeconds = (DateTime.UtcNow - startTime).TotalSeconds;
    double cpuLoad = Math.Sin(elapsedSeconds * 0.5) * 50 + 50;
    double memoryUsage = Math.Cos(elapsedSeconds * 0.3) * 30 + 70;
    double networkSpeed = Math.Abs(Math.Sin(elapsedSeconds * 2)) * 1000;
    double temperature = Math.Sin(elapsedSeconds * 0.1) * 20 + 60;
    double batteryLevel = Math.Max(0, 100 - elapsedSeconds * 0.5);
    int randomValue = Random.Shared.Next(10000);
    double waveValue = Math.Sin(elapsedSeconds * 3) * 10;
    string progressBar = new string('█', (int)(((elapsedSeconds % 10) / 10) * 20)).PadRight(20, '░');

    string connectionStatus = Math.Sin(elapsedSeconds) > 0 ? "ONLINE" : "OFFLINE";
    string systemHealth = cpuLoad < 80 ? "GOOD" : "HIGH";
    string alertLevel = temperature > 75 ? "CRITICAL" : "NORMAL";

    return new StyledText(
        TextChunk.Styled("System Stats:", attributes: TextAttributes.Bold),
        TextChunk.Plain(" "),
        TextChunk.Styled(
            updateFrequency == 1 ? "[Update: Every Frame]" : $"[Update: Every {updateFrequency} frames]",
            fg: Rgba.FromHex("#888888")),
        TextChunk.Plain("\n"),
        TextChunk.Styled("Uptime:", fg: Rgba.Parse("blue")),
        TextChunk.Plain(" "),
        TextChunk.Styled(elapsedSeconds.ToString("0.00"), fg: Rgba.FromHex("#00FF00")),
        TextChunk.Plain("s "),
        TextChunk.Styled($"({(int)(elapsedSeconds / 60)}m {(int)(elapsedSeconds % 60)}s)", fg: Rgba.FromHex("#666666")),
        TextChunk.Plain("\n"),
        TextChunk.Styled("CPU Load:", fg: Rgba.Parse("red")),
        TextChunk.Plain(" "),
        TextChunk.Styled($"{cpuLoad:0.0}%", fg: cpuLoad > 80 ? Rgba.Parse("red") : Rgba.Parse("green"), attributes: cpuLoad > 80 ? TextAttributes.Bold : TextAttributes.None),
        TextChunk.Plain(" "),
        TextChunk.Styled(new string('█', (int)(cpuLoad / 5)), fg: Rgba.FromHex("#444444")),
        TextChunk.Plain("\n"),
        TextChunk.Styled("Memory:", fg: Rgba.FromHex("#FF6B6B")),
        TextChunk.Plain(" "),
        TextChunk.Styled($"{memoryUsage:0.0}%", fg: memoryUsage > 85 ? Rgba.Parse("red") : Rgba.FromHex("#FFA500"), attributes: memoryUsage > 85 ? TextAttributes.Bold : TextAttributes.None),
        TextChunk.Plain("\n"),
        TextChunk.Styled("Network:", fg: Rgba.FromHex("#9B59B6")),
        TextChunk.Plain(" "),
        TextChunk.Styled($"{networkSpeed:0} KB/s", fg: networkSpeed > 500 ? Rgba.Parse("green") : Rgba.FromHex("#FFA500"), attributes: networkSpeed > 500 ? TextAttributes.Bold : TextAttributes.None),
        TextChunk.Plain("\n"),
        TextChunk.Styled("Temp:", fg: Rgba.FromHex("#E74C3C")),
        TextChunk.Plain(" "),
        TextChunk.Styled($"{temperature:0.0}°C", fg: temperature > 75 ? Rgba.Parse("red") : Rgba.Parse("blue"), attributes: temperature > 75 ? TextAttributes.Bold : TextAttributes.None),
        TextChunk.Plain("\n"),
        TextChunk.Styled("Battery:", fg: Rgba.FromHex("#F39C12")),
        TextChunk.Plain(" "),
        TextChunk.Styled($"{batteryLevel:0}%", fg: batteryLevel < 20 ? Rgba.Parse("red") : Rgba.Parse("green"), attributes: batteryLevel < 20 ? TextAttributes.Bold : TextAttributes.None),
        TextChunk.Plain("\n"),
        TextChunk.Styled("Connection:", attributes: TextAttributes.Underline),
        TextChunk.Plain(" "),
        TextChunk.Styled(connectionStatus, fg: connectionStatus == "ONLINE" ? Rgba.Parse("green") : Rgba.Parse("red"), attributes: TextAttributes.Bold),
        TextChunk.Plain("\n"),
        TextChunk.Styled("Health:", attributes: TextAttributes.Underline),
        TextChunk.Plain(" "),
        TextChunk.Styled(systemHealth, fg: systemHealth == "GOOD" ? Rgba.Parse("green") : Rgba.Parse("red"), attributes: TextAttributes.Bold),
        TextChunk.Plain("\n"),
        TextChunk.Styled("Alert:", attributes: TextAttributes.Underline),
        TextChunk.Plain(" "),
        TextChunk.Styled(alertLevel, fg: alertLevel == "NORMAL" ? Rgba.Parse("green") : Rgba.Parse("red"), bg: alertLevel == "NORMAL" ? null : Rgba.Parse("yellow"), attributes: TextAttributes.Bold),
        TextChunk.Plain("\n"),
        TextChunk.Styled("Random ID:", fg: Rgba.FromHex("#3498DB")),
        TextChunk.Plain(" "),
        TextChunk.Styled(randomValue.ToString("0000"), fg: Rgba.FromHex("#E67E22")),
        TextChunk.Plain("\n"),
        TextChunk.Styled("Wave:", fg: Rgba.FromHex("#1ABC9C")),
        TextChunk.Plain(" "),
        TextChunk.Styled(waveValue >= 0 ? $"+{waveValue:0.00}" : $"{waveValue:0.00}", fg: waveValue >= 0 ? Rgba.Parse("green") : Rgba.Parse("red")),
        TextChunk.Plain("\n"),
        TextChunk.Styled("Progress:", fg: Rgba.FromHex("#9B59B6")),
        TextChunk.Plain(" "),
        TextChunk.Styled(progressBar, fg: Rgba.FromHex("#00FF00")),
        TextChunk.Plain("\n"),
        TextChunk.Styled("Frame:", fg: Rgba.FromHex("#34495E")),
        TextChunk.Plain(" "),
        TextChunk.Styled(complexTemplateCounter.ToString(), fg: Rgba.FromHex("#ECF0F1")),
        TextChunk.Plain(" "),
        TextChunk.Styled($"(Total: {counter})", fg: Rgba.FromHex("#7F8C8D")),
        TextChunk.Plain("\n"),
        TextChunk.Styled("Status:", fg: Rgba.FromHex("#2ECC71")),
        TextChunk.Plain(" "),
        TextChunk.Styled("●", fg: Rgba.FromHex("#E74C3C"), attributes: TextAttributes.Bold),
        TextChunk.Plain(" "),
        TextChunk.Styled(alertLevel == "CRITICAL" ? "SYSTEM ALERT" : "ALL SYSTEMS GO", fg: alertLevel == "CRITICAL" ? Rgba.Parse("red") : Rgba.Parse("green")),
        TextChunk.Plain("\n\n"),
        TextChunk.Styled("Controls:", fg: Rgba.FromHex("#F1C40F"), attributes: TextAttributes.Bold),
        TextChunk.Plain(" "),
        TextChunk.Styled("↑/↓ = Speed, ESC = Exit", fg: Rgba.FromHex("#BDC3C7")));
}
