using System.Diagnostics;

using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
});

renderer.SetBackgroundColor(Rgba.FromInts(15, 23, 42));

BoxRenderable contentContainer = null!;
InputRenderable paletteSizeInput = null!;
TextRenderable statusText = null!;
PaletteGridRenderable paletteGrid = null!;
FrameBufferRenderable specialColorsBuffer = null!;
HexListRenderable hexList = null!;

var mainContainer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "main-container",
    FlexGrow = 1,
    FlexDirection = FlexDirectionValue.Column,
});
renderer.Root.Add(mainContainer);

    var scrollBox = new ScrollBoxRenderable(renderer, new ScrollBoxOptions
    {
        Id = "terminal-scroll-box",
        FlexGrow = 1,
        StickyScroll = false,
        Border = true,
        BorderColor = Rgba.FromHex("#8B5CF6"),
        Title = "Terminal Palette Demo (Ctrl+C to exit)",
        TitleAlignment = TitleAlignment.Center,
        ContentOptions = new BoxOptions
        {
            PaddingLeft = DimensionValue.Point(2),
            PaddingRight = DimensionValue.Point(2),
            PaddingTop = DimensionValue.Point(1),
        },
    });
    mainContainer.Add(scrollBox);

    contentContainer = new BoxRenderable(renderer, new BoxOptions
    {
        Id = "terminal-palette-container",
        Width = DimensionValue.Auto,
        FlexDirection = FlexDirectionValue.Column,
    });
    scrollBox.Add(contentContainer);

    contentContainer.Add(new TextRenderable(renderer, new TextOptions
    {
        Id = "terminal-subtitle",
        Content = "Enter palette size (1-256) and press Enter to fetch | Press 'c' to clear cache",
        Fg = Rgba.FromInts(148, 163, 184),
    }));

    var inputContainer = new BoxRenderable(renderer, new BoxOptions
    {
        Id = "input-container",
        FlexDirection = FlexDirectionValue.Row,
        MarginTop = DimensionValue.Point(1),
    });
    contentContainer.Add(inputContainer);

    inputContainer.Add(new TextRenderable(renderer, new TextOptions
    {
        Id = "input-label",
        Content = "Palette Size: ",
        Fg = Rgba.FromInts(148, 163, 184),
    }));

    paletteSizeInput = new InputRenderable(renderer, new InputOptions
    {
        Id = "palette-size-input",
        Width = DimensionValue.Point(10),
        BackgroundColor = Rgba.FromInts(30, 41, 59),
        TextColor = Rgba.White,
        Placeholder = "16",
        PlaceholderColor = Rgba.FromInts(100, 116, 139),
        CursorColor = Rgba.FromInts(139, 92, 246),
        Value = "16",
        MaxLength = 3,
    });
    inputContainer.Add(paletteSizeInput);

    statusText = new TextRenderable(renderer, new TextOptions
    {
        Id = "terminal-status",
        Content = "Status: Ready to fetch palette",
        MarginTop = DimensionValue.Point(1),
        Fg = Rgba.FromInts(56, 189, 248),
    });
    contentContainer.Add(statusText);

    paletteGrid = new PaletteGridRenderable(renderer, "palette-grid", []);
    paletteGrid.MarginTop = DimensionValue.Point(2);
    contentContainer.Add(paletteGrid);

    specialColorsBuffer = new FrameBufferRenderable(renderer, new FrameBufferOptions
    {
        Id = "special-colors-buffer",
        Width = DimensionValue.Point(30),
        Height = DimensionValue.Point(18),
        MarginTop = DimensionValue.Point(2),
        BackgroundColor = Rgba.FromInts(30, 41, 59),
    });
    contentContainer.Add(specialColorsBuffer);
    DrawSpecialColors(null);

hexList = new HexListRenderable(renderer, "hex-list", []);
hexList.MarginTop = DimensionValue.Point(2);
contentContainer.Add(hexList);

paletteSizeInput.On<string>(InputRenderable.Events.Enter, value =>
{
    if (!TryParsePaletteSize(value, out int size))
    {
        statusText.ContentText = "Status: Invalid palette size. Please enter a number between 1 and 256.";
        statusText.Fg = Rgba.FromInts(239, 68, 68);
        return;
    }

    _ = FetchAndDisplayPaletteAsync(size);
});

renderer.KeyInput.On<KeyEvent>("keypress", key =>
{
    if (key.Name == "c" && !key.Ctrl && !key.Meta && !key.Option && !key.Super && !key.Hyper)
    {
        key.PreventDefault();
        renderer.ClearPaletteCache();
        statusText.ContentText = "Status: Cache cleared. Enter a size and press Enter to fetch palette again.";
        statusText.Fg = Rgba.FromInts(148, 163, 184);
    }
});

paletteSizeInput.Focus();
renderer.RequestRender();
await Task.Delay(Timeout.Infinite);

async Task FetchAndDisplayPaletteAsync(int size)
{
    bool wasAlreadyCached = renderer.PaletteDetectionStatus == "cached";
    statusText.ContentText = $"Status: {(wasAlreadyCached ? "Using cached palette" : "Fetching palette...")}";
    statusText.Fg = Rgba.FromInts(250, 204, 21);

    var stopwatch = Stopwatch.StartNew();
    var terminalColors = await renderer.GetPalette(new GetPaletteOptions { Size = size });
    stopwatch.Stop();

    statusText.ContentText =
        $"Status: Palette ({size} colors) fetched in {stopwatch.ElapsedMilliseconds}ms ({(wasAlreadyCached ? "from cache" : "from terminal")})";
    statusText.Fg = Rgba.FromInts(34, 197, 94);

    paletteGrid.Colors = terminalColors.Palette.Take(size).ToArray();
    DrawSpecialColors(terminalColors);
    hexList.Colors = terminalColors.Palette.Take(size).ToArray();
}

static bool TryParsePaletteSize(string value, out int size)
{
    if (int.TryParse(value, out size) && size >= 1 && size <= 256)
        return true;

    size = 0;
    return false;
}

void DrawSpecialColors(TerminalColors? terminalColors)
{
    if (specialColorsBuffer.Buffer is not { } buffer)
        return;

    var slate800 = Rgba.FromInts(30, 41, 59);
    var slate400 = Rgba.FromInts(148, 163, 184);
    var slate500 = Rgba.FromInts(100, 116, 139);
    buffer.Clear(slate800);

    var specialColors = new (string Label, string? Value)[]
    {
        ("Default FG", terminalColors?.DefaultForeground),
        ("Default BG", terminalColors?.DefaultBackground),
        ("Cursor", terminalColors?.CursorColor),
        ("Mouse FG", terminalColors?.MouseForeground),
        ("Mouse BG", terminalColors?.MouseBackground),
        ("Tek FG", terminalColors?.TekForeground),
        ("Tek BG", terminalColors?.TekBackground),
        ("Highlight BG", terminalColors?.HighlightBackground),
        ("Highlight FG", terminalColors?.HighlightForeground),
    };

    const int boxWidth = 4;

    for (int i = 0; i < specialColors.Length; i++)
    {
        int y = i * 2;
        string label = specialColors[i].Label;
        string? value = specialColors[i].Value;

        if (value is not null)
        {
            var rgba = Rgba.FromHex(value);
            for (int dy = 0; dy < 2; dy++)
            {
                for (int dx = 0; dx < boxWidth; dx++)
                {
                    buffer.SetCell((uint)dx, (uint)(y + dy), (uint)' ', Rgba.White, rgba);
                }
            }

            buffer.DrawText($"{label}: {value.ToUpperInvariant()}", (uint)(boxWidth + 1), (uint)y, slate400, slate800);
        }
        else
        {
            buffer.DrawText($"{label}: N/A", (uint)(boxWidth + 1), (uint)y, slate500, slate800);
        }
    }

    specialColorsBuffer.RequestRender();
}

file sealed class PaletteGridRenderable : FrameBufferRenderable
{
    private IReadOnlyList<string?> _colors;
    private readonly int _blockWidth;
    private readonly int _blockHeight;
    private readonly int _colorsPerRow;
    private readonly int _maxHeight;
    private static readonly Rgba Slate800 = Rgba.FromInts(30, 41, 59);

    public PaletteGridRenderable(
        IRenderContext ctx,
        string id,
        IReadOnlyList<string?> colors,
        int blockWidth = 4,
        int blockHeight = 2,
        int colorsPerRow = 16,
        int maxHeight = 32)
        : base(ctx, new FrameBufferOptions
        {
            Id = id,
            Width = DimensionValue.Point(colorsPerRow * blockWidth),
            Height = DimensionValue.Point(CalculateHeight(colors.Count, colorsPerRow, blockHeight, maxHeight)),
            BackgroundColor = Slate800,
        })
    {
        _colors = colors;
        _blockWidth = blockWidth;
        _blockHeight = blockHeight;
        _colorsPerRow = colorsPerRow;
        _maxHeight = maxHeight;

        RenderPalette();
    }

    public IReadOnlyList<string?> Colors
    {
        get => _colors;
        set
        {
            _colors = value;
            HeightDimension = DimensionValue.Point(CalculateHeight(_colors.Count, _colorsPerRow, _blockHeight, _maxHeight));
            RenderPalette();
            RequestRender();
        }
    }

    protected override void OnResize(int width, int height)
    {
        base.OnResize(width, height);
        RenderPalette();
    }

    private void RenderPalette()
    {
        if (Buffer is not { } buffer)
            return;

        buffer.Clear(Slate800);

        for (int i = 0; i < _colors.Count; i++)
        {
            string? color = _colors[i];
            if (color is null)
                continue;

            int row = i / _colorsPerRow;
            int col = i % _colorsPerRow;
            int x = col * _blockWidth;
            int y = row * _blockHeight;
            var rgba = Rgba.FromHex(color);
            var ints = rgba.ToInts();
            float brightness = (ints.R * 299 + ints.G * 587 + ints.B * 114) / 1000f;
            var textColor = brightness > 128 ? Rgba.Black : Rgba.White;

            for (int dy = 0; dy < _blockHeight; dy++)
            {
                for (int dx = 0; dx < _blockWidth; dx++)
                {
                    buffer.SetCell((uint)(x + dx), (uint)(y + dy), (uint)' ', Rgba.White, rgba);
                }
            }

            string indexText = i.ToString();
            if (indexText.Length <= _blockWidth)
            {
                int textX = x + ((_blockWidth - indexText.Length) / 2);
                int textY = y + (_blockHeight / 2);
                buffer.DrawText(indexText, (uint)textX, (uint)textY, textColor, rgba);
            }
        }
    }

    private static int CalculateHeight(int colorCount, int colorsPerRow, int blockHeight, int maxHeight)
    {
        int numRows = (int)Math.Ceiling(colorCount / (double)colorsPerRow);
        int requiredHeight = numRows * blockHeight;
        return Math.Max(1, Math.Min(requiredHeight, maxHeight));
    }
}

file sealed class HexListRenderable : FrameBufferRenderable
{
    private IReadOnlyList<string?> _colors;
    private readonly int _columns;
    private readonly int _blockWidth;
    private readonly int _blockHeight;
    private readonly int _maxHeight;
    private readonly int _itemWidth;
    private static readonly Rgba Slate800 = Rgba.FromInts(30, 41, 59);
    private static readonly Rgba Slate400 = Rgba.FromInts(148, 163, 184);

    public HexListRenderable(
        IRenderContext ctx,
        string id,
        IReadOnlyList<string?> colors,
        int columns = 4,
        int blockWidth = 4,
        int blockHeight = 2,
        int? maxHeight = null)
        : base(ctx, new FrameBufferOptions
        {
            Id = id,
            Width = DimensionValue.Point(columns * 18),
            Height = DimensionValue.Point(CalculateHeight(colors.Count, columns, blockHeight, maxHeight ?? (int)Math.Ceiling(256d / columns) * (blockHeight + 1))),
            BackgroundColor = Slate800,
        })
    {
        _colors = colors;
        _columns = columns;
        _blockWidth = blockWidth;
        _blockHeight = blockHeight;
        _maxHeight = maxHeight ?? (int)Math.Ceiling(256d / columns) * (blockHeight + 1);
        _itemWidth = 18;

        RenderHexList();
    }

    public IReadOnlyList<string?> Colors
    {
        get => _colors;
        set
        {
            _colors = value;
            HeightDimension = DimensionValue.Point(CalculateHeight(_colors.Count, _columns, _blockHeight, _maxHeight));
            RenderHexList();
            RequestRender();
        }
    }

    protected override void OnResize(int width, int height)
    {
        base.OnResize(width, height);
        RenderHexList();
    }

    private void RenderHexList()
    {
        if (Buffer is not { } buffer)
            return;

        buffer.Clear(Slate800);

        for (int i = 0; i < _colors.Count && i < 256; i++)
        {
            string? color = _colors[i];
            if (color is null)
                continue;

            int row = i / _columns;
            int col = i % _columns;
            int x = col * _itemWidth;
            int y = row * (_blockHeight + 1);
            var rgba = Rgba.FromHex(color);

            for (int dy = 0; dy < _blockHeight; dy++)
            {
                for (int dx = 0; dx < _blockWidth; dx++)
                {
                    buffer.SetCell((uint)(x + dx), (uint)(y + dy), (uint)' ', Rgba.White, rgba);
                }
            }

            string text = $"{i,3}: {color.ToUpperInvariant()}";
            buffer.DrawText(text, (uint)(x + _blockWidth + 1), (uint)y, Slate400, Slate800);
        }
    }

    private static int CalculateHeight(int colorCount, int columns, int blockHeight, int maxHeight)
    {
        int numRows = (int)Math.Ceiling(colorCount / (double)columns);
        int requiredHeight = numRows * (blockHeight + 1);
        return Math.Max(1, Math.Min(requiredHeight, maxHeight));
    }
}
