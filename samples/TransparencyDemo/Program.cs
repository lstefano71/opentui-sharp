using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
});

ThemeName currentTheme = ThemeName.Dark;
ThemeMode currentThemeMode = renderer.ThemeMode ?? ThemeMode.Dark;
Rgba transparentBackgroundColor = GetTransparentFallbackBackgroundColor(currentThemeMode);
int transparentPaletteRequestVersion = 0;

TextRenderable headerDisplay = null!;
TextRenderable textUnderAlpha = null!;
TextRenderable moreTextUnder = null!;
List<DraggableTransparentBox> draggableBoxes = [];

var parentContainer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "parent-container",
    ZIndex = 10,
});
renderer.Root.Add(parentContainer);

    headerDisplay = new TextRenderable(renderer, new TextOptions
    {
        Id = "header-text",
        StyledContent = BuildHeaderText(currentTheme),
        Width = DimensionValue.Point(85),
        Height = DimensionValue.Point(3),
        Position = PositionValue.Absolute,
        Left = DimensionValue.Point(10),
        Top = DimensionValue.Point(2),
        ZIndex = 1,
        Selectable = false,
    });
    parentContainer.Add(headerDisplay);

    textUnderAlpha = new TextRenderable(renderer, new TextOptions
    {
        Id = "text-under-alpha",
        Content = "This text should not be selectable",
        Position = PositionValue.Absolute,
        Left = DimensionValue.Point(10),
        Top = DimensionValue.Point(6),
        Fg = TransparencyThemes.Get(currentTheme).TextUnderAlpha,
        Attributes = TextAttributes.Bold,
        ZIndex = 4,
        Selectable = false,
    });
    parentContainer.Add(textUnderAlpha);

    moreTextUnder = new TextRenderable(renderer, new TextOptions
    {
        Id = "more-text-under",
        Content = "Selectable text to show character preservation",
        Position = PositionValue.Absolute,
        Left = DimensionValue.Point(15),
        Top = DimensionValue.Point(10),
        Fg = TransparencyThemes.Get(currentTheme).MoreTextUnder,
        Attributes = TextAttributes.Bold,
        ZIndex = 1,
    });
    parentContainer.Add(moreTextUnder);

    AddDraggableBox(parentContainer, "alpha-box-50", 15, 5, 25, 8, Rgba.FromValues(64f / 255f, 176f / 255f, 1f, 128f / 255f), 50);
    AddDraggableBox(parentContainer, "alpha-box-75", 30, 7, 25, 8, Rgba.FromValues(1f, 107f / 255f, 129f / 255f, 192f / 255f), 30);
    AddDraggableBox(parentContainer, "alpha-box-25", 45, 9, 25, 8, Rgba.FromValues(139f / 255f, 69f / 255f, 193f / 255f, 64f / 255f), 10);
    AddDraggableBox(parentContainer, "alpha-green", 20, 11, 30, 5, Rgba.FromValues(88f / 255f, 214f / 255f, 141f / 255f, 96f / 255f), 20);
    AddDraggableBox(parentContainer, "alpha-yellow", 25, 13, 20, 6, Rgba.FromValues(1f, 183f / 255f, 77f / 255f, 128f / 255f), 40);
    AddDraggableBox(parentContainer, "alpha-overlay", 10, 17, 65, 4, Rgba.FromValues(200f / 255f, 162f / 255f, 1f, 32f / 255f), 60);

ApplyTheme(currentTheme);

renderer.KeyInput.On<KeyEvent>("keypress", key =>
{
    if (key.Name != "b")
        return;

    key.PreventDefault();
    currentTheme = NextTheme(currentTheme);
    ApplyTheme(currentTheme);
});

renderer.On<ThemeMode>(RendererEventNames.ThemeMode, mode =>
{
    currentThemeMode = mode;
    renderer.ClearPaletteCache();

    if (currentTheme == ThemeName.Transparent)
        _ = UpdateTransparentBackgroundColorAsync();
});

renderer.RequestRender();
await Task.Delay(Timeout.Infinite);

void AddDraggableBox(BoxRenderable parent, string id, int x, int y, int width, int height, Rgba bg, int zIndex)
{
    var box = new DraggableTransparentBox(renderer, id, x, y, width, height, bg, zIndex);
    parent.Add(box);
    draggableBoxes.Add(box);
}

void ApplyTheme(ThemeName themeName)
{
    currentTheme = themeName;
    var theme = TransparencyThemes.Get(themeName);

    renderer.SetBackgroundColor(themeName == ThemeName.Transparent ? transparentBackgroundColor : theme.BackgroundColor);
    headerDisplay.Content = BuildHeaderText(themeName);
    textUnderAlpha.Fg = theme.TextUnderAlpha;
    moreTextUnder.Fg = theme.MoreTextUnder;

    foreach (var box in draggableBoxes)
        box.SetLabelColor(theme.BoxLabelColor);

    if (themeName == ThemeName.Transparent)
        _ = UpdateTransparentBackgroundColorAsync();
}

async Task UpdateTransparentBackgroundColorAsync()
{
    int requestVersion = ++transparentPaletteRequestVersion;
    transparentBackgroundColor = GetTransparentFallbackBackgroundColor(currentThemeMode);

    if (currentTheme == ThemeName.Transparent)
        renderer.SetBackgroundColor(transparentBackgroundColor);

    var palette = await renderer.GetPalette();
    if (requestVersion != transparentPaletteRequestVersion)
        return;

    if (palette.DefaultBackground is { } defaultBackground)
    {
        var backgroundColor = Rgba.FromHex(defaultBackground);
        transparentBackgroundColor = new Rgba(backgroundColor.R, backgroundColor.G, backgroundColor.B, 0f);
    }

    if (currentTheme == ThemeName.Transparent)
        renderer.SetBackgroundColor(transparentBackgroundColor);
}

static Rgba GetTransparentFallbackBackgroundColor(ThemeMode themeMode) =>
    themeMode == ThemeMode.Light
        ? new Rgba(1f, 1f, 1f, 0f)
        : new Rgba(0f, 0f, 0f, 0f);

static StyledText BuildHeaderText(ThemeName themeName)
{
    var theme = TransparencyThemes.Get(themeName);
    return new StyledText(
        TextChunk.Styled(
            "Interactive Alpha Transparency & Blending Demo - Drag the boxes!",
            fg: theme.HeaderAccent,
            attributes: TextAttributes.Bold | TextAttributes.Underline),
        TextChunk.Plain("\n"),
        TextChunk.Styled(
            $"Drag boxes with the mouse • Press B to cycle dark/light/transparent (current: {theme.Label})",
            fg: theme.HeaderMuted));
}

static ThemeName NextTheme(ThemeName themeName) => themeName switch
{
    ThemeName.Dark => ThemeName.Light,
    ThemeName.Light => ThemeName.Transparent,
    _ => ThemeName.Dark,
};

file enum ThemeName
{
    Dark,
    Light,
    Transparent,
}

file readonly record struct TransparencyTheme(
    string Label,
    Rgba BackgroundColor,
    Rgba HeaderAccent,
    Rgba HeaderMuted,
    Rgba TextUnderAlpha,
    Rgba MoreTextUnder,
    Rgba BoxLabelColor);

file static class TransparencyThemes
{
    public static TransparencyTheme Get(ThemeName themeName) => themeName switch
    {
        ThemeName.Dark => new TransparencyTheme(
            "dark",
            Rgba.FromHex("#0A0E14"),
            Rgba.FromHex("#00D4AA"),
            Rgba.FromHex("#A8A8B2"),
            Rgba.FromHex("#FFB84D"),
            Rgba.FromHex("#7B68EE"),
            Rgba.FromInts(255, 255, 255, 220)),
        ThemeName.Light => new TransparencyTheme(
            "light",
            Rgba.FromHex("#F6F1E5"),
            Rgba.FromHex("#0F766E"),
            Rgba.FromHex("#4B5563"),
            Rgba.FromHex("#B45309"),
            Rgba.FromHex("#6D28D9"),
            Rgba.FromInts(17, 24, 39, 220)),
        _ => new TransparencyTheme(
            "transparent",
            Rgba.Transparent,
            Rgba.FromHex("#0284C7"),
            Rgba.FromHex("#64748B"),
            Rgba.FromHex("#D97706"),
            Rgba.FromHex("#7C3AED"),
            Rgba.FromInts(255, 255, 255, 220)),
    };
}

file static class TransparencyDemoZIndex
{
    private static int _nextZIndex = 101;

    public static int Allocate() => _nextZIndex++;
}

file sealed class DraggableTransparentBox : BoxRenderable
{
    private bool _isDragging;
    private int _dragOffsetX;
    private int _dragOffsetY;
    private readonly int _alphaPercentage;
    private Rgba _labelColor = TransparencyThemes.Get(ThemeName.Dark).BoxLabelColor;

    public DraggableTransparentBox(IRenderContext ctx, string id, int x, int y, int width, int height, Rgba backgroundColor, int zIndex)
        : base(ctx, new BoxOptions
        {
            Id = id,
            Width = DimensionValue.Point(width),
            Height = DimensionValue.Point(height),
            ZIndex = zIndex,
            BackgroundColor = backgroundColor,
            Position = PositionValue.Absolute,
            Left = DimensionValue.Point(x),
            Top = DimensionValue.Point(y),
        })
    {
        _alphaPercentage = (int)MathF.Round(backgroundColor.A * 100f);
    }

    public void SetLabelColor(Rgba color)
    {
        _labelColor = color;
        RequestRender();
    }

    protected override void RenderSelf(OptimizedBuffer buffer, float deltaTime)
    {
        base.RenderSelf(buffer, deltaTime);

        string alphaText = $"{_alphaPercentage}%";
        int textX = (int)_screenX + Math.Max(0, (_widthValue - alphaText.Length) / 2);
        int textY = (int)_screenY + (_heightValue / 2);
        buffer.DrawText(alphaText, (uint)Math.Max(0, textX), (uint)Math.Max(0, textY), _labelColor);
    }

    protected override void OnMouseEvent(UiMouseEvent evt)
    {
        switch (evt.Type)
        {
            case MouseEventType.Down:
                _isDragging = true;
                _dragOffsetX = evt.X - (int)ScreenX;
                _dragOffsetY = evt.Y - (int)ScreenY;
                ZIndex = TransparencyDemoZIndex.Allocate();
                evt.StopPropagation();
                break;

            case MouseEventType.DragEnd:
                if (_isDragging)
                {
                    _isDragging = false;
                    evt.StopPropagation();
                }
                break;

            case MouseEventType.Drag:
                if (_isDragging)
                {
                    int maxX = Math.Max(0, Ctx.Width - Width);
                    int maxY = Math.Max(4, Ctx.Height - Height);
                    int newX = Math.Clamp(evt.X - _dragOffsetX, 0, maxX);
                    int newY = Math.Clamp(evt.Y - _dragOffsetY, 4, maxY);

                    Left = DimensionValue.Point(newX);
                    Top = DimensionValue.Point(newY);
                    evt.StopPropagation();
                }
                break;
        }
    }
}
