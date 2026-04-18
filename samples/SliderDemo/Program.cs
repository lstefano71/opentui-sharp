// Slider Demo — port of the TypeScript slider-demo.ts sample.
using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 30,
});

var mainBg = Rgba.FromHex("#1a1b26");
var panelBg = Rgba.FromHex("#24283b");
var trackBg = Rgba.FromHex("#414868");
var dimText = Rgba.FromHex("#565f89");
var bodyText = Rgba.FromHex("#c0caf5");
var titleText = Rgba.FromHex("#7aa2f7");
var h1Color = Rgba.FromHex("#e0af68");
var h2Color = Rgba.FromHex("#bb9af7");
var h3Color = Rgba.FromHex("#FF6B6B");
var v1Color = Rgba.FromHex("#f7768e");
var v2Color = Rgba.FromHex("#ff9e64");
var v3Color = Rgba.FromHex("#73daca");

SliderRenderable? horizontalSlider1 = null;
SliderRenderable? horizontalSlider2 = null;
SliderRenderable? horizontalSlider3 = null;
SliderRenderable? verticalSlider1 = null;
SliderRenderable? verticalSlider2 = null;
SliderRenderable? verticalSlider3 = null;
SliderRenderable? animatedVerticalSlider = null;

TextRenderable? h1ValueText = null;
TextRenderable? h2ValueText = null;
TextRenderable? h3ValueText = null;
TextRenderable? v1ValueText = null;
TextRenderable? v2ValueText = null;
TextRenderable? v3ValueText = null;
TextRenderable? vAValueText = null;

static TextChunk Hex(string text, Rgba color, bool bold = false) =>
    TextChunk.Styled(text, fg: color, attributes: bold ? TextAttributes.Bold : TextAttributes.None);

static StyledText Join(params TextChunk[] chunks) => new(chunks);

StyledText MakeHorizontalLabel(string name, Rgba nameColor, string description) => Join(
    Hex(name, nameColor, bold: true),
    TextChunk.Plain(" "),
    Hex(description, dimText));

StyledText MakeHorizontalValue(Rgba color, string value) => Join(
    Hex("Value:", color, bold: true),
    TextChunk.Plain(" "),
    TextChunk.Plain(value));

StyledText MakeVerticalLabel(string name, Rgba color, string widthText) => Join(
    Hex(name, color, bold: true),
    TextChunk.Plain("\n"),
    Hex(widthText, dimText));

StyledText MakeVerticalValue(Rgba color, string value) =>
    Join(Hex(value, color, bold: true));

void UpdateDisplays()
{
    if (h1ValueText is not null && horizontalSlider1 is not null)
        h1ValueText.Content = MakeHorizontalValue(h1Color, horizontalSlider1.Value.ToString("F1"));
    if (h2ValueText is not null && horizontalSlider2 is not null)
        h2ValueText.Content = MakeHorizontalValue(h2Color, horizontalSlider2.Value.ToString("F1"));
    if (h3ValueText is not null && horizontalSlider3 is not null)
        h3ValueText.Content = MakeHorizontalValue(h3Color, horizontalSlider3.Value.ToString("F2"));
    if (v1ValueText is not null && verticalSlider1 is not null)
        v1ValueText.Content = MakeVerticalValue(v1Color, verticalSlider1.Value.ToString("F1"));
    if (v2ValueText is not null && verticalSlider2 is not null)
        v2ValueText.Content = MakeVerticalValue(v2Color, verticalSlider2.Value.ToString("F1"));
    if (v3ValueText is not null && verticalSlider3 is not null)
        v3ValueText.Content = MakeVerticalValue(v3Color, verticalSlider3.Value.ToString("F1"));
    if (vAValueText is not null && animatedVerticalSlider is not null)
        vAValueText.Content = MakeVerticalValue(h3Color, animatedVerticalSlider.Value.ToString("F2"));
}

void ResetSliders()
{
    if (horizontalSlider1 is not null) horizontalSlider1.Value = 25;
    if (horizontalSlider2 is not null) horizontalSlider2.Value = 100;
    if (horizontalSlider3 is not null) horizontalSlider3.Value = 25;
    if (verticalSlider1 is not null) verticalSlider1.Value = 0;
    if (verticalSlider2 is not null) verticalSlider2.Value = 0;
    if (verticalSlider3 is not null) verticalSlider3.Value = 50;
    if (animatedVerticalSlider is not null) animatedVerticalSlider.Value = 50;
    UpdateDisplays();
}

void FocusSlider(int index)
{
    horizontalSlider1?.Blur();
    horizontalSlider2?.Blur();
    horizontalSlider3?.Blur();
    verticalSlider1?.Blur();
    verticalSlider2?.Blur();
    verticalSlider3?.Blur();
    animatedVerticalSlider?.Blur();

    SliderRenderable? slider = index switch
    {
        1 => horizontalSlider1,
        2 => horizontalSlider2,
        3 => horizontalSlider3,
        4 => verticalSlider1,
        5 => verticalSlider2,
        6 => verticalSlider3,
        7 => animatedVerticalSlider,
        _ => null,
    };

    slider?.Focus();
}

var mainContainer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "slider-demo-main-container",
    FlexGrow = 1,
    MaxHeight = DimensionValue.Percent(100),
    MaxWidth = DimensionValue.Percent(100),
    FlexDirection = FlexDirectionValue.Column,
    BackgroundColor = mainBg,
});

var slidersContainer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "sliders-container",
    Width = DimensionValue.Percent(100),
    FlexGrow = 1,
    FlexDirection = FlexDirectionValue.Column,
    BackgroundColor = mainBg,
    Padding = DimensionValue.Point(2),
});

var h1Container = new BoxRenderable(renderer, new BoxOptions
{
    Id = "h1-container",
    Width = DimensionValue.Percent(100),
    FlexDirection = FlexDirectionValue.Column,
    FlexShrink = 0,
    BackgroundColor = panelBg,
    MarginBottom = DimensionValue.Point(1),
    Padding = DimensionValue.Point(1),
});

var h1Label = new TextRenderable(renderer, new TextOptions
{
    Id = "h1-label",
    StyledContent = MakeHorizontalLabel("H1", h1Color, "- 1h×100w (0-50)"),
});

h1ValueText = new TextRenderable(renderer, new TextOptions
{
    Id = "h1-value",
    StyledContent = MakeHorizontalValue(h1Color, "25.0"),
});

horizontalSlider1 = new SliderRenderable(renderer, new SliderOptions
{
    Id = "horizontal-slider-1",
    Orientation = SliderOrientation.Horizontal,
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Point(1),
    Value = 25,
    Min = 0,
    Max = 50,
    ViewPortSize = 1,
    BackgroundColor = trackBg,
    ForegroundColor = h1Color,
    OnChange = _ => UpdateDisplays(),
});

h1Container.Add(h1Label);
h1Container.Add(h1ValueText);
h1Container.Add(horizontalSlider1);

var h2Container = new BoxRenderable(renderer, new BoxOptions
{
    Id = "h2-container",
    Width = DimensionValue.Percent(100),
    FlexDirection = FlexDirectionValue.Column,
    FlexShrink = 0,
    BackgroundColor = panelBg,
    MarginBottom = DimensionValue.Point(1),
    Padding = DimensionValue.Point(1),
});

var h2Label = new TextRenderable(renderer, new TextOptions
{
    Id = "h2-label",
    StyledContent = MakeHorizontalLabel("H2", h2Color, "- 5h×100w (0-200)"),
});

h2ValueText = new TextRenderable(renderer, new TextOptions
{
    Id = "h2-value",
    StyledContent = MakeHorizontalValue(h2Color, "100.0"),
});

horizontalSlider2 = new SliderRenderable(renderer, new SliderOptions
{
    Id = "horizontal-slider-2",
    Orientation = SliderOrientation.Horizontal,
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Point(5),
    Value = 100,
    Min = 0,
    Max = 200,
    ViewPortSize = 50,
    BackgroundColor = trackBg,
    ForegroundColor = h2Color,
    OnChange = _ => UpdateDisplays(),
});

h2Container.Add(h2Label);
h2Container.Add(h2ValueText);
h2Container.Add(horizontalSlider2);

var h3Container = new BoxRenderable(renderer, new BoxOptions
{
    Id = "h3-container",
    Width = DimensionValue.Percent(100),
    FlexDirection = FlexDirectionValue.Column,
    FlexShrink = 0,
    BackgroundColor = panelBg,
    MarginBottom = DimensionValue.Point(1),
    Padding = DimensionValue.Point(1),
});

var h3Label = new TextRenderable(renderer, new TextOptions
{
    Id = "h3-label",
    StyledContent = MakeHorizontalLabel("H3", h3Color, "- 1h×80w (animated, sub-cell rendering)"),
});

h3ValueText = new TextRenderable(renderer, new TextOptions
{
    Id = "h3-value",
    StyledContent = MakeHorizontalValue(h3Color, "25.00"),
});

horizontalSlider3 = new SliderRenderable(renderer, new SliderOptions
{
    Id = "horizontal-slider-3",
    Orientation = SliderOrientation.Horizontal,
    Height = DimensionValue.Point(1),
    Value = 25,
    Min = 0,
    Max = 50,
    ViewPortSize = 0.1f,
    BackgroundColor = trackBg,
    ForegroundColor = h3Color,
    OnChange = _ => UpdateDisplays(),
});

h3Container.Add(h3Label);
h3Container.Add(h3ValueText);
h3Container.Add(horizontalSlider3);

var verticalContainer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "vertical-container",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Point(17),
    FlexDirection = FlexDirectionValue.Row,
    FlexShrink = 0,
    BackgroundColor = mainBg,
    MarginBottom = DimensionValue.Point(1),
    Padding = DimensionValue.Point(1),
});

var v1Container = new BoxRenderable(renderer, new BoxOptions
{
    Id = "v1-container",
    Width = DimensionValue.Point(8),
    Height = DimensionValue.Percent(100),
    FlexDirection = FlexDirectionValue.Column,
    AlignItems = AlignValue.FlexEnd,
    BackgroundColor = panelBg,
    MarginRight = DimensionValue.Point(1),
    Padding = DimensionValue.Point(1),
});

var v1SliderWrapper = new BoxRenderable(renderer, new BoxOptions
{
    Id = "v1-slider-wrapper",
    FlexDirection = FlexDirectionValue.Row,
    Height = DimensionValue.Percent(100),
    FlexGrow = 1,
});

var v1Label = new TextRenderable(renderer, new TextOptions
{
    Id = "v1-label",
    StyledContent = MakeVerticalLabel("V1", v1Color, "1w"),
    Width = DimensionValue.Point(3),
});

verticalSlider1 = new SliderRenderable(renderer, new SliderOptions
{
    Id = "vertical-slider-1",
    Orientation = SliderOrientation.Vertical,
    Width = DimensionValue.Point(1),
    Height = DimensionValue.Percent(100),
    Value = 0,
    Min = -10,
    Max = 10,
    ViewPortSize = 1,
    BackgroundColor = trackBg,
    ForegroundColor = v1Color,
    OnChange = _ => UpdateDisplays(),
});

v1ValueText = new TextRenderable(renderer, new TextOptions
{
    Id = "v1-value",
    StyledContent = MakeVerticalValue(v1Color, "0.0"),
});

v1SliderWrapper.Add(v1Label);
v1SliderWrapper.Add(verticalSlider1);
v1Container.Add(v1SliderWrapper);
v1Container.Add(v1ValueText);

var v2Container = new BoxRenderable(renderer, new BoxOptions
{
    Id = "v2-container",
    Width = DimensionValue.Point(10),
    Height = DimensionValue.Percent(100),
    FlexDirection = FlexDirectionValue.Column,
    AlignItems = AlignValue.FlexEnd,
    BackgroundColor = panelBg,
    MarginRight = DimensionValue.Point(1),
    Padding = DimensionValue.Point(1),
});

var v2SliderWrapper = new BoxRenderable(renderer, new BoxOptions
{
    Id = "v2-slider-wrapper",
    FlexDirection = FlexDirectionValue.Row,
    Height = DimensionValue.Percent(100),
    FlexGrow = 1,
});

var v2Label = new TextRenderable(renderer, new TextOptions
{
    Id = "v2-label",
    StyledContent = MakeVerticalLabel("V2", v2Color, "3w"),
    Width = DimensionValue.Point(3),
});

verticalSlider2 = new SliderRenderable(renderer, new SliderOptions
{
    Id = "vertical-slider-2",
    Orientation = SliderOrientation.Vertical,
    Width = DimensionValue.Point(3),
    Height = DimensionValue.Percent(100),
    Value = 0,
    Min = -50,
    Max = 50,
    ViewPortSize = 5,
    BackgroundColor = trackBg,
    ForegroundColor = v2Color,
    OnChange = _ => UpdateDisplays(),
});

v2ValueText = new TextRenderable(renderer, new TextOptions
{
    Id = "v2-value",
    StyledContent = MakeVerticalValue(v2Color, "0.0"),
});

v2SliderWrapper.Add(v2Label);
v2SliderWrapper.Add(verticalSlider2);
v2Container.Add(v2SliderWrapper);
v2Container.Add(v2ValueText);

var v3Container = new BoxRenderable(renderer, new BoxOptions
{
    Id = "v3-container",
    Width = DimensionValue.Point(12),
    Height = DimensionValue.Percent(100),
    FlexDirection = FlexDirectionValue.Column,
    AlignItems = AlignValue.FlexEnd,
    BackgroundColor = panelBg,
    MarginRight = DimensionValue.Point(1),
    Padding = DimensionValue.Point(1),
});

var v3SliderWrapper = new BoxRenderable(renderer, new BoxOptions
{
    Id = "v3-slider-wrapper",
    FlexDirection = FlexDirectionValue.Row,
    Height = DimensionValue.Percent(100),
    FlexGrow = 1,
});

var v3Label = new TextRenderable(renderer, new TextOptions
{
    Id = "v3-label",
    StyledContent = MakeVerticalLabel("V3", v3Color, "5w"),
    Width = DimensionValue.Point(3),
});

verticalSlider3 = new SliderRenderable(renderer, new SliderOptions
{
    Id = "vertical-slider-3",
    Orientation = SliderOrientation.Vertical,
    Width = DimensionValue.Point(5),
    Height = DimensionValue.Percent(100),
    Value = 50,
    Min = 0,
    Max = 100,
    ViewPortSize = 10,
    BackgroundColor = trackBg,
    ForegroundColor = v3Color,
    OnChange = _ => UpdateDisplays(),
});

v3ValueText = new TextRenderable(renderer, new TextOptions
{
    Id = "v3-value",
    StyledContent = MakeVerticalValue(v3Color, "50.0"),
});

v3SliderWrapper.Add(v3Label);
v3SliderWrapper.Add(verticalSlider3);
v3Container.Add(v3SliderWrapper);
v3Container.Add(v3ValueText);

var animatedVContainer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "animated-v-container",
    Width = DimensionValue.Point(10),
    Height = DimensionValue.Percent(100),
    FlexDirection = FlexDirectionValue.Column,
    AlignItems = AlignValue.FlexEnd,
    BackgroundColor = panelBg,
    MarginRight = DimensionValue.Point(1),
    Padding = DimensionValue.Point(1),
});

var animatedVSliderWrapper = new BoxRenderable(renderer, new BoxOptions
{
    Id = "animated-v-slider-wrapper",
    FlexDirection = FlexDirectionValue.Row,
    Height = DimensionValue.Percent(100),
    FlexGrow = 1,
});

var animatedVLabel = new TextRenderable(renderer, new TextOptions
{
    Id = "animated-v-label",
    StyledContent = MakeVerticalLabel("VA", h3Color, "2w"),
    Width = DimensionValue.Point(3),
});

animatedVerticalSlider = new SliderRenderable(renderer, new SliderOptions
{
    Id = "animated-vertical-slider",
    Orientation = SliderOrientation.Vertical,
    Width = DimensionValue.Point(2),
    Height = DimensionValue.Percent(100),
    Value = 50,
    Min = 0,
    Max = 100,
    ViewPortSize = 0.2f,
    BackgroundColor = trackBg,
    ForegroundColor = h3Color,
    OnChange = _ => UpdateDisplays(),
});

vAValueText = new TextRenderable(renderer, new TextOptions
{
    Id = "animated-v-value",
    StyledContent = MakeVerticalValue(h3Color, "50.00"),
});

animatedVSliderWrapper.Add(animatedVLabel);
animatedVSliderWrapper.Add(animatedVerticalSlider);
animatedVContainer.Add(animatedVSliderWrapper);
animatedVContainer.Add(vAValueText);

verticalContainer.Add(v1Container);
verticalContainer.Add(v2Container);
verticalContainer.Add(v3Container);
verticalContainer.Add(animatedVContainer);

var spacer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "spacer",
    Width = DimensionValue.Percent(100),
    FlexGrow = 1,
});

slidersContainer.Add(h1Container);
slidersContainer.Add(h2Container);
slidersContainer.Add(h3Container);
slidersContainer.Add(verticalContainer);
slidersContainer.Add(spacer);

var instructionsBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "instructions",
    Width = DimensionValue.Percent(100),
    FlexDirection = FlexDirectionValue.Column,
    FlexShrink = 0,
    BackgroundColor = Rgba.FromHex("#2a2b3a"),
    PaddingLeft = DimensionValue.Point(1),
});

var instructionsText1 = new TextRenderable(renderer, new TextOptions
{
    Id = "instructions-1",
    StyledContent = Join(
        Hex("Slider Demo", titleText, bold: true),
        TextChunk.Plain(" "),
        Hex("-", dimText),
        TextChunk.Plain(" "),
        Hex("Mouse", Rgba.FromHex("#FFFF00"), bold: true),
        TextChunk.Plain(" "),
        Hex("Click & drag on sliders", bodyText),
        TextChunk.Plain(" "),
        Hex("|", dimText),
        TextChunk.Plain(" "),
        Hex("R", Rgba.FromHex("#FFAA00"), bold: true),
        TextChunk.Plain(" "),
        Hex("Reset all", bodyText),
        TextChunk.Plain(" "),
        Hex("|", dimText),
        TextChunk.Plain(" "),
        Hex("1-7", Rgba.FromHex("#00FF00"), bold: true),
        TextChunk.Plain(" "),
        Hex("Focus sliders", bodyText)),
});

var instructionsText2 = new TextRenderable(renderer, new TextOptions
{
    Id = "instructions-2",
    StyledContent = Join(
        Hex("Features:", titleText, bold: true),
        TextChunk.Plain(" "),
        Hex("Different ranges, step sizes, orientations & dimensions (1-5 height/width)", bodyText)),
});

instructionsBox.Add(instructionsText1);
instructionsBox.Add(instructionsText2);

void ApplyResponsiveLayout(int terminalHeight)
{
    bool compact = terminalHeight < 42;
    bool veryCompact = terminalHeight < 26;

    slidersContainer.Padding = DimensionValue.Point(compact ? 0 : 2);

    DimensionValue panelPadding = DimensionValue.Point(compact ? 0 : 1);
    DimensionValue panelMargin = DimensionValue.Point(compact ? 0 : 1);

    h1Container.Padding = panelPadding;
    h1Container.MarginBottom = panelMargin;
    h2Container.Padding = panelPadding;
    h2Container.MarginBottom = panelMargin;
    h3Container.Padding = panelPadding;
    h3Container.MarginBottom = panelMargin;

    verticalContainer.Padding = DimensionValue.Point(compact ? 0 : 1);
    verticalContainer.MarginBottom = panelMargin;

    int footerRows = veryCompact ? 1 : 2;
    instructionsText2.Visible = !veryCompact;
    instructionsBox.HeightDimension = DimensionValue.Auto;
    instructionsBox.PaddingLeft = DimensionValue.Point(compact ? 0 : 1);

    if (!compact)
    {
        verticalContainer.HeightDimension = DimensionValue.Point(17);
        return;
    }

    int verticalRows = Math.Clamp(terminalHeight - (3 * 3) - footerRows, 8, 17);
    verticalContainer.HeightDimension = DimensionValue.Point(verticalRows);
}

mainContainer.Add(slidersContainer);
mainContainer.Add(instructionsBox);
renderer.Root.Add(mainContainer);

using var resizeSubscription = renderer.On<(int Width, int Height)>(RendererEventNames.Resize, size =>
{
    ApplyResponsiveLayout(size.Height);
});

using var keypressSubscription = renderer.KeyInput.On<KeyEvent>(KeyHandlerEvents.Keypress, key =>
{
    switch (key.Name)
    {
        case "r":
            ResetSliders();
            key.StopPropagation();
            break;
        case "1":
        case "2":
        case "3":
        case "4":
        case "5":
        case "6":
        case "7":
            FocusSlider(int.Parse(key.Name));
            key.StopPropagation();
            break;
    }
});

float animationTime = 0f;
renderer.AddFrameCallback(dt =>
{
    animationTime += dt;

    if (horizontalSlider3 is { Focused: false })
    {
        float hValue = 25f + MathF.Sin(animationTime * 0.002f) * 25f;
        horizontalSlider3.Value = Math.Clamp(hValue, 0f, 50f);
    }

    if (animatedVerticalSlider is { Focused: false })
    {
        float vValue = 50f + MathF.Cos(animationTime * 0.0015f) * 50f;
        animatedVerticalSlider.Value = Math.Clamp(vValue, 0f, 100f);
    }

    return Task.CompletedTask;
});

renderer.Native.SetBackgroundColor(mainBg);
ApplyResponsiveLayout(renderer.Height);
UpdateDisplays();
renderer.RequestRender();

await Task.Delay(Timeout.Infinite);
