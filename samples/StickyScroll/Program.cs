using OpenTui.Core;

static Rgba Hex(string value) => Rgba.FromHex(value);

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 60,
});

renderer.Native.SetBackgroundColor(Hex("#0a0a14"));

var mainContainer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "main-container",
    FlexGrow = 1,
    MaxHeight = DimensionValue.Percent(100),
    MaxWidth = DimensionValue.Percent(100),
    FlexDirection = FlexDirectionValue.Column,
    BackgroundColor = Hex("#0f0f23"),
});

var scrollBox = new ScrollBoxRenderable(renderer, new ScrollBoxOptions
{
    Id = "sticky-scroll-box",
    BoxFocusable = true,
    FlexGrow = 1,
    MaxHeight = DimensionValue.Percent(100),
    MaxWidth = DimensionValue.Percent(100),
    StickyScroll = true,
    StickyStart = "bottom",
    Border = true,
    BorderColor = Hex("#313244"),
    FocusedBorderColor = Hex("#7aa2f7"),
    BackgroundColor = Hex("#1e1e2e"),
    WrapperOptions = new BoxOptions
    {
        BackgroundColor = Hex("#181825"),
    },
    ViewportOptions = new BoxOptions
    {
        BackgroundColor = Hex("#11111b"),
    },
    ContentOptions = new BoxOptions
    {
        BackgroundColor = Hex("#0f0f0f"),
    },
    VerticalScrollbarOptions = new ScrollBarOptions
    {
        Orientation = SliderOrientation.Vertical,
        TrackOptions = new SliderOptions
        {
            Orientation = SliderOrientation.Vertical,
            ForegroundColor = Hex("#7aa2f7"),
            BackgroundColor = Hex("#313244"),
        },
    },
});

var instructionsBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "instructions",
    Width = DimensionValue.Percent(100),
    FlexDirection = FlexDirectionValue.Column,
    FlexShrink = 0,
    BackgroundColor = Hex("#1e1e2e"),
    PaddingLeft = DimensionValue.Point(1),
});

var instructionsText1 = new TextRenderable(renderer, new TextOptions
{
    StyledContent = new StyledText(
        TextChunk.Styled("Sticky Scroll Demo", fg: Hex("#7aa2f7"), attributes: TextAttributes.Bold),
        TextChunk.Plain(" "),
        TextChunk.Styled("-", fg: Hex("#565f89")),
        TextChunk.Plain(" "),
        TextChunk.Styled("S", fg: Hex("#9ece6a"), attributes: TextAttributes.Bold),
        TextChunk.Plain(" "),
        TextChunk.Styled("Toggle sticky scroll", fg: Hex("#c0caf5")),
        TextChunk.Plain(" "),
        TextChunk.Styled("|", fg: Hex("#565f89")),
        TextChunk.Plain(" "),
        TextChunk.Styled("T", fg: Hex("#bb9af7"), attributes: TextAttributes.Bold),
        TextChunk.Plain(" "),
        TextChunk.Styled("Add item at top", fg: Hex("#c0caf5")),
        TextChunk.Plain(" "),
        TextChunk.Styled("|", fg: Hex("#565f89")),
        TextChunk.Plain(" "),
        TextChunk.Styled("B", fg: Hex("#f7768e"), attributes: TextAttributes.Bold),
        TextChunk.Plain(" "),
        TextChunk.Styled("Add item at bottom", fg: Hex("#c0caf5")),
        TextChunk.Plain(" "),
        TextChunk.Styled("|", fg: Hex("#565f89")),
        TextChunk.Plain(" "),
        TextChunk.Styled("E", fg: Hex("#e0af68"), attributes: TextAttributes.Bold),
        TextChunk.Plain(" "),
        TextChunk.Styled("Clear all items", fg: Hex("#c0caf5"))),
});

var instructionsText2 = new TextRenderable(renderer, new TextOptions
{
    StyledContent = new StyledText(
        TextChunk.Styled("Behavior:", fg: Hex("#7aa2f7"), attributes: TextAttributes.Bold),
        TextChunk.Plain(" "),
        TextChunk.Styled("At TOP/BOTTOM, adding items there keeps the viewport pinned to that edge", fg: Hex("#c0caf5"))),
});

var statusText = new TextRenderable(renderer, new TextOptions
{
    StyledContent = CreateStatusText(scrollBox),
});

instructionsBox.Add(instructionsText1);
instructionsBox.Add(instructionsText2);
instructionsBox.Add(statusText);

mainContainer.Add(scrollBox);
mainContainer.Add(instructionsBox);
renderer.Root.Add(mainContainer);
scrollBox.Focus();

int itemCount = 0;
var animatedItems = new Dictionary<string, AnimatedItem>();
AnimationTickerRenderable? animationTicker = null;
animationTicker = new AnimationTickerRenderable(renderer, UpdateAnimations);
mainContainer.Add(animationTicker);

for (int i = 0; i < 10; i++)
    AddItem(atTop: false);

renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    if (e.Ctrl || e.Meta)
        return;

    switch (e.Name)
    {
        case "s":
            scrollBox.StickyScroll = !scrollBox.StickyScroll;
            statusText.Content = CreateStatusText(scrollBox);
            renderer.RequestRender();
            break;
        case "t":
            AddItem(atTop: true);
            break;
        case "b":
            AddItem(atTop: false);
            break;
        case "e":
            ClearAllItems();
            renderer.RequestRender();
            break;
    }
});

renderer.RequestRender();
await Task.Delay(Timeout.Infinite);

void ClearAllItems()
{
    animatedItems.Clear();
    animationTicker.Live = false;

    foreach (var child in scrollBox.GetChildren().ToArray())
    {
        scrollBox.Remove(child.Id);
        child.DestroyRecursively();
    }

    itemCount = 0;
}

void AddItem(bool atTop)
{
    itemCount++;

    string boxId = $"item-{itemCount}";
    var normalBackground = itemCount % 2 == 0 ? Hex("#24283b") : Hex("#1f2335");
    var createdAt = DateTimeOffset.Now;

    var box = new BoxRenderable(renderer, new BoxOptions
    {
        Id = boxId,
        Width = DimensionValue.Percent(100),
        Padding = DimensionValue.Point(1),
        MarginBottom = DimensionValue.Point(1),
        BackgroundColor = Hex("#4c4f69"),
    });

    var text = new TextRenderable(renderer, new TextOptions
    {
        Width = DimensionValue.Percent(100),
        WrapMode = WrapMode.None,
        StyledContent = CreateItemText(itemCount, createdAt, atTop, Hex("#bb9af7")),
    });

    box.Add(text);

    if (atTop)
        scrollBox.Add(box, 0);
    else
        scrollBox.Add(box);

    animatedItems[boxId] = new AnimatedItem
    {
        Box = box,
        Text = text,
        ItemNumber = itemCount,
        CreatedAt = createdAt,
        AgeMs = 0,
        NormalBackground = normalBackground,
        IsAtTop = atTop,
    };

    animationTicker.Live = true;
    renderer.RequestRender();
}

void UpdateAnimations(float deltaTime)
{
    statusText.Content = CreateStatusText(scrollBox);

    if (animatedItems.Count == 0)
    {
        animationTicker!.Live = false;
        return;
    }

    List<string>? completedIds = null;

    foreach (var pair in animatedItems)
    {
        string id = pair.Key;
        var item = pair.Value;
        item.AgeMs += deltaTime;
        const float durationMs = 800;

        if (item.AgeMs >= durationMs)
        {
            item.Box.BackgroundColor = item.NormalBackground;
            item.Text.Content = CreateItemText(item.ItemNumber, item.CreatedAt, item.IsAtTop, Hex("#7aa2f7"));
            completedIds ??= [];
            completedIds.Add(id);
            continue;
        }

        float progress = item.AgeMs / durationMs;
        float easeProgress = 1f - MathF.Pow(1f - progress, 3f);

        item.Box.BackgroundColor = InterpolateColor(Hex("#4c4f69"), item.NormalBackground, easeProgress);
        item.Text.Content = CreateItemText(
            item.ItemNumber,
            item.CreatedAt,
            item.IsAtTop,
            InterpolateColor(Hex("#bb9af7"), Hex("#7aa2f7"), easeProgress));
    }

    if (completedIds == null)
        return;

    foreach (string id in completedIds)
        animatedItems.Remove(id);

    if (animatedItems.Count == 0)
        animationTicker!.Live = false;
}

static StyledText CreateStatusText(ScrollBoxRenderable scrollBox)
{
    string anchor = GetAnchor(scrollBox);
    Rgba anchorColor = anchor switch
    {
        "TOP" => Hex("#bb9af7"),
        "BOTTOM" => Hex("#9ece6a"),
        _ => Hex("#f7768e"),
    };

    return new StyledText(
        TextChunk.Styled("Status:", fg: Hex("#7aa2f7"), attributes: TextAttributes.Bold),
        TextChunk.Plain(" "),
        TextChunk.Styled("Sticky Scroll:", fg: Hex("#c0caf5")),
        TextChunk.Plain(" "),
        TextChunk.Styled(
            scrollBox.StickyScroll ? "ENABLED" : "DISABLED",
            fg: scrollBox.StickyScroll ? Hex("#9ece6a") : Hex("#f7768e")),
        TextChunk.Plain(" "),
        TextChunk.Styled("|", fg: Hex("#565f89")),
        TextChunk.Plain(" "),
        TextChunk.Styled("Anchor:", fg: Hex("#c0caf5")),
        TextChunk.Plain(" "),
        TextChunk.Styled(anchor, fg: anchorColor, attributes: TextAttributes.Bold));
}

static string GetAnchor(ScrollBoxRenderable scrollBox)
{
    float maxScroll = Math.Max(0, scrollBox.ScrollHeight - scrollBox.ViewportHeight);
    if (scrollBox.ScrollTop <= 0)
        return "TOP";
    if (scrollBox.ScrollTop >= maxScroll)
        return "BOTTOM";
    return "FREE";
}

static StyledText CreateItemText(int itemNumber, DateTimeOffset createdAt, bool isAtTop, Rgba titleColor)
{
    string timeString = createdAt.ToString("T");

    return new StyledText(
        TextChunk.Styled($"Item #{itemNumber}", fg: titleColor, attributes: TextAttributes.Bold),
        TextChunk.Plain("\n"),
        TextChunk.Styled("This is a dynamically added item with enhanced content.", fg: Hex("#9aa5ce")),
        TextChunk.Plain("\n"),
        TextChunk.Styled("Contains additional information and styling.", fg: Hex("#c0caf5")),
        TextChunk.Plain("\n"),
        TextChunk.Styled("Added at:", fg: Hex("#565f89")),
        TextChunk.Plain(" "),
        TextChunk.Plain(timeString),
        TextChunk.Plain("\n"),
        TextChunk.Styled("Position:", fg: Hex("#565f89")),
        TextChunk.Plain(" "),
        TextChunk.Plain(isAtTop ? "TOP" : "BOTTOM"),
        TextChunk.Plain("\n"),
        TextChunk.Styled("Status:", fg: Hex("#565f89")),
        TextChunk.Plain(" "),
        TextChunk.Styled("ACTIVE", fg: Hex("#9ece6a")));
}

static Rgba InterpolateColor(Rgba start, Rgba end, float factor)
{
    return new Rgba(
        start.R + ((end.R - start.R) * factor),
        start.G + ((end.G - start.G) * factor),
        start.B + ((end.B - start.B) * factor),
        start.A + ((end.A - start.A) * factor));
}

sealed class AnimatedItem
{
    public required BoxRenderable Box { get; init; }
    public required TextRenderable Text { get; init; }
    public required int ItemNumber { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required float AgeMs { get; set; }
    public required Rgba NormalBackground { get; init; }
    public required bool IsAtTop { get; init; }
}

sealed class AnimationTickerRenderable : BoxRenderable
{
    private readonly Action<float> _onTick;

    public AnimationTickerRenderable(IRenderContext ctx, Action<float> onTick)
        : base(ctx, new BoxOptions
        {
            Id = "animation-ticker",
            Position = PositionValue.Absolute,
            Width = DimensionValue.Point(0),
            Height = DimensionValue.Point(0),
            ShouldFill = false,
        })
    {
        _onTick = onTick;
    }

    protected override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);
        _onTick(deltaTime);
    }
}
