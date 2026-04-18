using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig { ExitOnCtrlC = true });

var root = new BoxRenderable(renderer, new BoxOptions
{
    Id = "root",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Percent(100),
    FlexDirection = FlexDirectionValue.Column,
    BackgroundColor = Rgba.FromHex("#1e1e2e"),
    ShouldFill = true,
});

// Title
var titleBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "title-box",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#313244"),
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Rgba.FromHex("#cba6f7"),
    JustifyContent = JustifyValue.Center,
    AlignItems = AlignValue.Center,
});
titleBox.Add(new TextRenderable(renderer, new TextOptions
{
    Id = "title",
    Content = "TextNode Inline Composition Demo",
    Fg = Rgba.FromHex("#cba6f7"),
    Attributes = TextAttributes.Bold,
}));

// Display area
var displayBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "display",
    FlexGrow = 1,
    Width = DimensionValue.Percent(100),
    FlexDirection = FlexDirectionValue.Column,
    Padding = DimensionValue.Point(2),
    Gap = 1,
    BackgroundColor = Rgba.FromHex("#1e1e2e"),
    ShouldFill = true,
});

// Build example sets using TextNodeRenderable -> StyledText -> TextRenderable
var examples = BuildExamples();
int currentExample = 0;

var exampleLabel = new TextRenderable(renderer, new TextOptions
{
    Id = "example-label",
    Fg = Rgba.FromHex("#f9e2af"),
    Attributes = TextAttributes.Bold,
});

var textDisplay = new TextRenderable(renderer, new TextOptions
{
    Id = "text-display",
    Fg = Rgba.White,
});

displayBox.Add(exampleLabel);
displayBox.Add(textDisplay);

// Footer
var footer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "footer",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#313244"),
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Rgba.FromHex("#a6e3a1"),
    JustifyContent = JustifyValue.Center,
    AlignItems = AlignValue.Center,
});
footer.Add(new TextRenderable(renderer, new TextOptions
{
    Id = "footer-text",
    StyledContent = new StyledText(
        TextChunk.Styled("n", fg: Rgba.FromHex("#f38ba8"), attributes: TextAttributes.Bold),
        TextChunk.Styled(" next example  |  ", fg: Rgba.FromHex("#6c7086")),
        TextChunk.Styled("Ctrl+C", fg: Rgba.FromHex("#f38ba8"), attributes: TextAttributes.Bold),
        TextChunk.Styled(" exit", fg: Rgba.FromHex("#6c7086"))
    ),
}));

root.Add(titleBox);
root.Add(displayBox);
root.Add(footer);
renderer.Root.Add(root);

ShowExample(currentExample);

renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    if (e.Name == "n")
    {
        currentExample = (currentExample + 1) % examples.Length;
        ShowExample(currentExample);
        renderer.RequestRender();
    }
});

void ShowExample(int idx)
{
    var (name, rootNode) = examples[idx];
    exampleLabel.ContentText = $"[{idx + 1}/{examples.Length}] {name}";

    var chunks = rootNode.GatherWithInheritedStyle();
    textDisplay.Content = new StyledText(chunks);
}

(string Name, TextNodeRenderable Root)[] BuildExamples()
{
    // Example 1: Basic style composition
    var ex1Root = new TextNodeRenderable(new TextNodeOptions { Fg = Rgba.FromHex("#cdd6f4") });
    ex1Root.Add("This is ");
    ex1Root.Add(new TextNodeRenderable(new TextNodeOptions
    {
        Fg = Rgba.FromHex("#f38ba8"),
        Attributes = TextAttributes.Bold,
    }));
    ((TextNodeRenderable)ex1Root.Children[1]).Add("bold red");
    ex1Root.Add(" and ");
    ex1Root.Add(new TextNodeRenderable(new TextNodeOptions
    {
        Fg = Rgba.FromHex("#89b4fa"),
        Attributes = TextAttributes.Italic,
    }));
    ((TextNodeRenderable)ex1Root.Children[3]).Add("italic blue");
    ex1Root.Add(" text.");

    // Example 2: Nested style inheritance
    var ex2Root = new TextNodeRenderable(new TextNodeOptions
    {
        Fg = Rgba.FromHex("#a6e3a1"),
    });
    var ex2Bold = new TextNodeRenderable(new TextNodeOptions
    {
        Attributes = TextAttributes.Bold,
    });
    ex2Bold.Add("Bold green, ");
    var ex2BoldUnderline = new TextNodeRenderable(new TextNodeOptions
    {
        Attributes = TextAttributes.Underline,
        Fg = Rgba.FromHex("#f9e2af"),
    });
    ex2BoldUnderline.Add("bold+underline yellow");
    ex2Bold.Add(ex2BoldUnderline);
    ex2Bold.Add(", back to bold green");
    ex2Root.Add(ex2Bold);

    // Example 3: Mixed inline elements
    var ex3Root = new TextNodeRenderable(new TextNodeOptions { Fg = Rgba.White });
    ex3Root.Add("Status: ");
    ex3Root.Add(TextNodeRenderable.FromString("PASS", new TextNodeOptions
    {
        Fg = Rgba.FromHex("#a6e3a1"),
        Bg = Rgba.FromHex("#1e3a2e"),
        Attributes = TextAttributes.Bold,
    }));
    ex3Root.Add(" | Warnings: ");
    ex3Root.Add(TextNodeRenderable.FromString("3", new TextNodeOptions
    {
        Fg = Rgba.FromHex("#f9e2af"),
        Attributes = TextAttributes.Bold,
    }));
    ex3Root.Add(" | Errors: ");
    ex3Root.Add(TextNodeRenderable.FromString("0", new TextNodeOptions
    {
        Fg = Rgba.FromHex("#a6e3a1"),
    }));

    // Example 4: Deep nesting with overrides
    var ex4Root = new TextNodeRenderable(new TextNodeOptions
    {
        Fg = Rgba.FromHex("#cdd6f4"),
    });
    ex4Root.Add("L0: default ");
    var l1 = new TextNodeRenderable(new TextNodeOptions
    {
        Fg = Rgba.FromHex("#89dceb"),
        Attributes = TextAttributes.Italic,
    });
    l1.Add("L1: cyan italic ");
    var l2 = new TextNodeRenderable(new TextNodeOptions
    {
        Fg = Rgba.FromHex("#f38ba8"),
        Attributes = TextAttributes.Bold,
    });
    l2.Add("L2: red bold+italic ");
    var l3 = new TextNodeRenderable(new TextNodeOptions
    {
        Attributes = TextAttributes.Underline,
    });
    l3.Add("L3: +underline");
    l2.Add(l3);
    l1.Add(l2);
    l1.Add(" back to L1");
    ex4Root.Add(l1);
    ex4Root.Add(" back to L0");

    // Example 5: Factory methods
    var ex5Root = TextNodeRenderable.FromNodes([
        TextNodeRenderable.FromString("Hello ", new TextNodeOptions
        {
            Fg = Rgba.FromHex("#fab387"),
            Attributes = TextAttributes.Bold,
        }),
        TextNodeRenderable.FromString("beautiful ", new TextNodeOptions
        {
            Fg = Rgba.FromHex("#cba6f7"),
            Attributes = TextAttributes.Italic | TextAttributes.Underline,
        }),
        TextNodeRenderable.FromString("world!", new TextNodeOptions
        {
            Fg = Rgba.FromHex("#94e2d5"),
            Attributes = TextAttributes.Bold,
        }),
    ]);

    // Example 6: StyledText integration
    var ex6Root = new TextNodeRenderable(new TextNodeOptions { Fg = Rgba.FromHex("#cdd6f4") });
    ex6Root.Add("Prefix: ");
    ex6Root.Add(new StyledText(
        TextChunk.Styled("styled ", fg: Rgba.FromHex("#f38ba8"), attributes: TextAttributes.Bold),
        TextChunk.Styled("text ", fg: Rgba.FromHex("#89b4fa")),
        TextChunk.Styled("chunks", fg: Rgba.FromHex("#a6e3a1"), attributes: TextAttributes.Underline)
    ));
    ex6Root.Add(" :Suffix");

    return [
        ("Basic Style Composition", ex1Root),
        ("Nested Style Inheritance", ex2Root),
        ("Mixed Inline Elements", ex3Root),
        ("Deep Nesting with Overrides", ex4Root),
        ("Factory Methods (FromNodes/FromString)", ex5Root),
        ("StyledText Integration", ex6Root),
    ];
}

await Task.Delay(Timeout.Infinite);
