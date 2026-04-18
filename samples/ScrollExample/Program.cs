// Scroll Example — demonstrates ScrollBoxRenderable with dynamic content
using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 30,
});

int itemCount = 100;

// --- Header ---
var header = new BoxRenderable(renderer, new BoxOptions
{
    Id = "header",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#3b82f6"),
    BorderStyle = BorderStyle.Rounded,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    Border = true,
});
var headerText = new TextRenderable(renderer, new TextOptions
{
    Id = "header-text",
    Content = "Scroll Example",
    Fg = Rgba.FromInts(255, 255, 255),
});
header.Add(headerText);

// --- ScrollBox ---
var scrollBox = new ScrollBoxRenderable(renderer, new ScrollBoxOptions
{
    Id = "scroll",
    ScrollY = true,
    ScrollX = false,
    Width = DimensionValue.Auto,
    FlexGrow = 1,
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Rgba.FromHex("#4b5563"),
    BackgroundColor = Rgba.FromHex("#111827"),
});

Rgba colorA = Rgba.FromHex("#1f2937");
Rgba colorB = Rgba.FromHex("#111827");

void AddItems(int startIndex, int count)
{
    for (int i = startIndex; i < startIndex + count; i++)
    {
        if (i % 10 == 0)
        {
            // Every 10th item gets a large ASCII number
            var asciiBanner = new ASCIIFontRenderable(renderer, new ASCIIFontOptions
            {
                Id = $"ascii-{i}",
                Text = $"{i}",
                Font = "small",
                Color = Rgba.FromHex("#fbbf24"),
                BackgroundColor = Rgba.FromHex("#292524"),
            });
            scrollBox.Add(asciiBanner);
        }

        var row = new BoxRenderable(renderer, new BoxOptions
        {
            Id = $"item-{i}",
            Width = DimensionValue.Auto,
            Height = DimensionValue.Point(1),
            BackgroundColor = i % 2 == 0 ? colorA : colorB,
            FlexDirection = FlexDirectionValue.Row,
            AlignItems = AlignValue.Center,
            PaddingLeft = DimensionValue.Point(2),
        });
        var label = new TextRenderable(renderer, new TextOptions
        {
            Id = $"item-text-{i}",
            Content = $"Item {i}",
            Fg = Rgba.FromHex("#e5e7eb"),
        });
        row.Add(label);
        scrollBox.Add(row);
    }
}

AddItems(0, itemCount);

// --- Footer ---
var footer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "footer",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#1e40af"),
    BorderStyle = BorderStyle.Rounded,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    Border = true,
});
var footerText = new TextRenderable(renderer, new TextOptions
{
    Id = "footer-text",
    Content = "[a] Add 10 items  [j/k/🖱] Scroll  [t/b] Top/Bottom  [Ctrl+C] Quit",
    Fg = Rgba.FromHex("#93c5fd"),
});
footer.Add(footerText);

// --- Build tree ---
renderer.Root.Add(header);
renderer.Root.Add(scrollBox);
renderer.Root.Add(footer);

void UpdateHeader()
{
    headerText.ContentText = $"Scroll Example — {itemCount} items";
}

// --- Key handling ---
renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    switch (e.Name)
    {
        case "a":
            int start = itemCount;
            itemCount += 10;
            AddItems(start, 10);
            UpdateHeader();
            break;
        case "t":
            scrollBox.ScrollTo(y: 0);
            break;
        case "b":
            scrollBox.ScrollTo(y: scrollBox.ScrollHeight);
            break;
        case "j":
            scrollBox.ScrollBy(0, 5);
            break;
        case "k":
            scrollBox.ScrollBy(0, -5);
            break;
    }
});

UpdateHeader();
renderer.RequestRender();
await Task.Delay(Timeout.Infinite);
