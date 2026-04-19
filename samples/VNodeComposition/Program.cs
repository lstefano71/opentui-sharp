using OpenTui.Core;

var textColor = Rgba.FromHex("#FFFFFF");
var globalBgColor = Rgba.FromHex("#333333");
var transparent = Rgba.Transparent;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    UseMouse = true,
    TargetFps = 60,
});

renderer.SetBackgroundColor(globalBgColor);

var mainGroup = new BoxRenderable(renderer, new BoxOptions
{
    Id = "main-group",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Percent(100),
    Padding = DimensionValue.Point(1),
    FlexDirection = FlexDirectionValue.Column,
});
renderer.Root.Add(mainGroup);

mainGroup.Add(CreateExtendedBaseBox(renderer, new BoxOptions
{
    Id = "extended-base-box",
    Width = DimensionValue.Point(20),
    Height = DimensionValue.Point(10),
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(55),
    Top = DimensionValue.Point(10),
    ZIndex = 1000,
}));

var tree = CreateMyRenderable(renderer, new[]
{
    CreateBox(renderer, new BoxOptions
    {
        Id = "child-1",
        Width = DimensionValue.Point(20),
        Height = DimensionValue.Point(3),
        Border = true,
        MarginBottom = DimensionValue.Point(1),
    }, CreateText(renderer, "Hello")),
    CreateBox(renderer, new BoxOptions
    {
        Id = "child-2",
        Width = DimensionValue.Point(24),
        Height = DimensionValue.Point(3),
        Border = true,
    }, CreateText(renderer, "VNode world")),
});
tree.BackgroundColor = new Rgba(0f, 155f / 255f, 155f / 255f, 100f / 255f);
mainGroup.Add(tree);

var labeledInput = CreateLabeledInput(renderer, "labeled-input", "Label:", "Enter your text...");
labeledInput.Focus();
mainGroup.Add(labeledInput);

var delegatedVNode = CreateDelegatedVNodeRenderable(renderer, "delegated-demo-root", new[]
{
    CreateBox(renderer, new BoxOptions
    {
        Id = "child-1",
        Width = DimensionValue.Point(20),
        Height = DimensionValue.Point(3),
        Border = true,
        MarginBottom = DimensionValue.Point(1),
    }, CreateText(renderer, "Hello delegated 1")),
    CreateBox(renderer, new BoxOptions
    {
        Id = "child-2",
        Width = DimensionValue.Point(24),
        Height = DimensionValue.Point(3),
        Border = true,
    }, CreateText(renderer, "VNode world delegated 1")),
}, new Rgba(155f / 255f, 0f, 155f / 255f, 100f / 255f));
mainGroup.Add(delegatedVNode);

var instancedDelegated = CreateInstancedRenderable(renderer, "demo-root", new[]
{
    CreateBox(renderer, new BoxOptions
    {
        Id = "child-1",
        Width = DimensionValue.Point(20),
        Height = DimensionValue.Point(3),
        Border = true,
        MarginBottom = DimensionValue.Point(1),
    }, CreateText(renderer, "Hello 2")),
    CreateBox(renderer, new BoxOptions
    {
        Id = "child-2",
        Width = DimensionValue.Point(24),
        Height = DimensionValue.Point(3),
        Border = true,
    }, CreateText(renderer, "VNode world 2")),
}, null);
mainGroup.Add(instancedDelegated);
instancedDelegated.Add(CreateBox(renderer, new BoxOptions
{
    Id = "child-3",
    Width = DimensionValue.Point(24),
    Height = DimensionValue.Point(3),
    Border = true,
}, CreateText(renderer, "VNode world 3")));
instancedDelegated.Add(CreateButton(renderer, "Click me", () => Console.WriteLine("clicked"), Rgba.Parse("red")));

var renderableDelegated = CreateDelegatedRenderableComponent(renderer, new[]
{
    CreateBox(renderer, new BoxOptions
    {
        Id = "child-1",
        Width = DimensionValue.Point(20),
        Height = DimensionValue.Point(3),
        Border = true,
        MarginBottom = DimensionValue.Point(1),
    }, CreateText(renderer, "Hello 4")),
    CreateBox(renderer, new BoxOptions
    {
        Id = "child-2",
        Width = DimensionValue.Point(24),
        Height = DimensionValue.Point(3),
        Border = true,
    }, CreateText(renderer, "VNode world 4")),
});
mainGroup.Add(renderableDelegated);
renderableDelegated.Add(CreateButton(renderer, "Click me too!", () => Console.WriteLine("clicked"), Rgba.Parse("red")));

mainGroup.Add(CreateAnimatedVNodeButton(renderer, "Animated VNode", () => Console.WriteLine("vnode 1 clicked"), Rgba.FromInts(0, 0, 255)));
mainGroup.Add(CreateAnimatedVNodeButton(renderer, "Same VNode, different props", () => Console.WriteLine("vnode 2 clicked"), Rgba.FromInts(255, 0, 255)));
mainGroup.Add(CreateClassRenderedButton(renderer, "ClassRender", () => Console.WriteLine("clicked"), Rgba.FromInts(0, 0, 255)));

mainGroup.Add(CreateStyleExamples(renderer));

await Task.Delay(Timeout.Infinite);

BoxRenderable CreateMyRenderable(IRenderContext ctx, IEnumerable<Renderable> children)
{
    void MouseHandler(UiMouseEvent evt) => Console.WriteLine($"mouseHandler {evt.Type}");

    return CreateBox(ctx, new BoxOptions
    {
        Id = "inner",
    }, CreateBox(ctx, new BoxOptions
    {
        Border = true,
        BorderStyle = BorderStyle.Double,
        Padding = DimensionValue.Point(1),
        OnMouseDown = MouseHandler,
        FlexDirection = FlexDirectionValue.Row,
    }, children.ToArray()));
}

DelegatingRenderable CreateLabeledInput(IRenderContext ctx, string id, string label, string placeholder)
{
    var root = CreateBox(ctx, new BoxOptions
    {
        Id = $"{id}-labeled-outer",
        FlexDirection = FlexDirectionValue.Row,
    },
    CreateText(ctx, label + " "),
    new InputRenderable(ctx, new InputOptions
    {
        Id = $"{id}-input",
        Placeholder = placeholder,
        Width = DimensionValue.Point(20),
        BackgroundColor = Rgba.Parse("white"),
        TextColor = Rgba.Parse("black"),
        CursorColor = Rgba.Parse("blue"),
        FocusedBackgroundColor = Rgba.Parse("orange"),
    }));

    return new DelegatingRenderable(ctx, new DelegatingOptions
    {
        Id = $"{id}-delegate",
        Root = root,
        FocusTargetId = $"{id}-input",
    });
}

DelegatingRenderable CreateDelegatedVNodeRenderable(
    IRenderContext ctx,
    string id,
    IEnumerable<Renderable> children,
    Rgba? backgroundColor)
{
    var outer = CreateBox(ctx, new BoxOptions
    {
        Id = $"{id}_outer3",
        Border = true,
        BorderColor = Rgba.Parse("blue"),
    }, CreateBox(ctx, new BoxOptions
    {
        Id = $"{id}_inner3",
        Border = true,
        BorderColor = Rgba.Parse("magenta"),
    }, CreateBox(ctx, new BoxOptions
    {
        Id = $"{id}_box3",
        FlexDirection = FlexDirectionValue.Row,
        Border = true,
        Padding = DimensionValue.Point(1),
    }, children.ToArray())));

    if (backgroundColor is { } bg)
        outer.BackgroundColor = bg;

    return new DelegatingRenderable(ctx, new DelegatingOptions
    {
        Id = $"{id}-delegate",
        Root = outer,
        AddTargetId = $"{id}_box3",
        RemoveTargetId = $"{id}_box3",
    });
}

DelegatingRenderable CreateInstancedRenderable(
    IRenderContext ctx,
    string id,
    IEnumerable<Renderable> children,
    Rgba? backgroundColor) =>
    CreateDelegatedVNodeRenderable(ctx, id, children, backgroundColor);

DelegatingRenderable CreateDelegatedRenderableComponent(IRenderContext ctx, IEnumerable<Renderable> children)
{
    var root = CreateBox(ctx, new BoxOptions
    {
        Id = "__outer4",
        Border = true,
        BorderColor = Rgba.Parse("blue"),
    }, CreateBox(ctx, new BoxOptions
    {
        Id = "__inner4",
        Border = true,
        BorderColor = Rgba.Parse("magenta"),
    }, CreateBox(ctx, new BoxOptions
    {
        Id = "__box4",
        FlexDirection = FlexDirectionValue.Row,
        Border = true,
        Padding = DimensionValue.Point(1),
    }, children.ToArray())));

    return new DelegatingRenderable(ctx, new DelegatingOptions
    {
        Id = "renderable-delegate",
        Root = root,
        AddTargetId = "__box4",
        RemoveTargetId = "__box4",
    });
}

BoxRenderable CreateButton(IRenderContext ctx, string title, Action onClick, Rgba? borderColor = null, params Renderable[] children)
{
    return CreateBox(ctx, new BoxOptions
    {
        Id = "button",
        Border = true,
        OnMouseDown = _ => onClick(),
        BorderColor = borderColor,
        PaddingLeft = DimensionValue.Point(1),
        PaddingRight = DimensionValue.Point(1),
    }, [CreateText(ctx, title, selectable: false), .. children]);
}

GenericRenderable CreateAnimatedVNodeButton(IRenderContext ctx, string title, Action onClick, Rgba borderColor)
{
    int width = Math.Max(title.Length + 4, 12);
    var button = new GenericRenderable(ctx, new GenericOptions
    {
        Id = $"{title}-generic",
        Width = DimensionValue.Point(width),
        Height = DimensionValue.Point(3),
        Margin = DimensionValue.Point(1),
        Render = (buffer, deltaTime, renderable) => DemoRenderFn(title, borderColor, buffer, renderable, textColor, globalBgColor, transparent),
    });

    button.Add(CreateBox(ctx, new BoxOptions
    {
        Id = "button",
        Width = DimensionValue.Percent(100),
        Height = DimensionValue.Percent(100),
        OnMouseDown = _ => onClick(),
    }));

    return button;
}

GenericRenderable CreateClassRenderedButton(IRenderContext ctx, string title, Action onClick, Rgba borderColor)
{
    var rendererRoot = new MyRoot(title, borderColor);
    var button = new GenericRenderable(ctx, new GenericOptions
    {
        Id = $"{title}-class-render",
        Width = DimensionValue.Point(rendererRoot.Width),
        Height = DimensionValue.Point(3),
        MarginLeft = DimensionValue.Point(1),
        Render = (buffer, deltaTime, renderable) => rendererRoot.Render(buffer, deltaTime, renderable, textColor, globalBgColor, transparent),
    });

    button.Add(CreateBox(ctx, new BoxOptions
    {
        Id = "button",
        Width = DimensionValue.Percent(100),
        Height = DimensionValue.Percent(100),
        OnMouseDown = _ => onClick(),
    }));

    return button;
}

BoxRenderable CreateBaseBox(IRenderContext ctx, BoxOptions props, params Renderable[] children)
{
    var renderAfter = props.RenderAfter;
    BoxRenderable? box = null;
    box = CreateBox(ctx, new BoxOptions
    {
        Id = props.Id ?? "base-box",
        Border = true,
        BorderColor = Rgba.Parse("blue"),
        BackgroundColor = Rgba.Parse("orange"),
        Width = props.Width,
        Height = props.Height,
        Position = props.Position,
        Left = props.Left,
        Top = props.Top,
        ZIndex = props.ZIndex,
        RenderAfter = (buffer, deltaTime) =>
        {
            if (box is null)
                return;

            buffer.DrawText("Hello", (uint)(box.X + 1), (uint)(box.Y + 1), Rgba.White);
            renderAfter?.Invoke(buffer, deltaTime);
        },
    }, children);

    return box;
}

BoxRenderable CreateExtendedBaseBox(IRenderContext ctx, BoxOptions props)
{
    BoxRenderable? extended = null;
    extended = CreateBaseBox(ctx, new BoxOptions
    {
        Id = props.Id,
        Width = props.Width,
        Height = props.Height,
        Position = props.Position,
        Left = props.Left,
        Top = props.Top,
        ZIndex = props.ZIndex,
        RenderAfter = (buffer, deltaTime) =>
        {
            if (extended is null)
                return;

            buffer.DrawText("Extended", (uint)(extended.X + 1), (uint)(extended.Y + 2), Rgba.White);
        },
    });

    return extended;
}

BoxRenderable CreateStyleExamples(IRenderContext ctx)
{
    return CreateBox(ctx, new BoxOptions
    {
        Id = "style-examples",
        FlexDirection = FlexDirectionValue.Column,
        MarginTop = DimensionValue.Point(2),
    },
    CreateStyledLine(ctx, new StyledText(TextChunk.Plain("Bold Text").WithAttributes(TextAttributes.Bold))),
    CreateStyledLine(ctx, new StyledText(TextChunk.Plain("Italic Text").WithAttributes(TextAttributes.Italic))),
    CreateStyledLine(ctx, new StyledText(TextChunk.Plain("Underlined Text").WithAttributes(TextAttributes.Underline))),
    CreateStyledLine(ctx, new StyledText(TextChunk.Plain("Dim Text").WithAttributes(TextAttributes.Dim))),
    CreateStyledLine(ctx, new StyledText(TextChunk.Plain("Bold and Italic").WithAttributes(TextAttributes.Bold | TextAttributes.Italic))),
    CreateStyledLine(ctx, new StyledText(TextChunk.Plain("Bold and Underlined").WithAttributes(TextAttributes.Bold | TextAttributes.Underline))),
    CreateStyledLine(ctx, new StyledText(TextChunk.Plain("Italic and Underlined").WithAttributes(TextAttributes.Italic | TextAttributes.Underline))),
    CreateStyledLine(ctx, new StyledText(TextChunk.Styled("Red Text", fg: Rgba.FromHex("#ff6b6b")))),
    CreateStyledLine(ctx, new StyledText(TextChunk.Styled("Text with Background", bg: Rgba.FromHex("#4ecdc4")))),
    CreateStyledLine(ctx, new StyledText(TextChunk.Plain("Custom Styled").WithAttributes(TextAttributes.Bold | TextAttributes.Underline))),
    CreateStyledLine(ctx, new StyledText(
        TextChunk.Plain("hello").WithAttributes(TextAttributes.Bold | TextAttributes.Underline),
        TextChunk.Plain(" world").WithAttributes(TextAttributes.Bold))),
    CreateStyledLine(ctx, new StyledText(
        TextChunk.Styled("Bold Red", fg: Rgba.FromHex("#ff6b6b"), attributes: TextAttributes.Bold),
        TextChunk.Plain(" normal"))),
    CreateStyledLine(ctx, new StyledText(
        TextChunk.Styled("Green Italic", fg: Rgba.FromHex("#4ecdc4"), attributes: TextAttributes.Italic),
        TextChunk.Plain(" normal again"))));
}

TextRenderable CreateStyledLine(IRenderContext ctx, StyledText content) =>
    new(ctx, new TextOptions
    {
        StyledContent = content,
    });

BoxRenderable CreateBox(IRenderContext ctx, BoxOptions options, params Renderable[] children)
{
    var box = new BoxRenderable(ctx, options);
    foreach (var child in children)
        box.Add(child);

    return box;
}

TextRenderable CreateText(IRenderContext ctx, string content, bool selectable = true) =>
    new(ctx, new TextOptions
    {
        Content = content,
        Selectable = selectable,
    });

static void DemoRenderFn(
    string title,
    Rgba borderColor,
    OptimizedBuffer buffer,
    Renderable renderable,
    Rgba textColor,
    Rgba globalBgColor,
    Rgba transparent) =>
    VNodeCompositionRenderHelpers.DemoRenderFn(title, borderColor, buffer, renderable, textColor, globalBgColor, transparent);

sealed class MyRoot(string title, Rgba borderColor)
{
    public int Width { get; } = Math.Max(title.Length + 4, 12);

    public void Render(OptimizedBuffer buffer, float deltaTime, Renderable renderable, Rgba textColor, Rgba globalBgColor, Rgba transparent) =>
        VNodeCompositionRenderHelpers.DemoRenderFn(title, borderColor, buffer, renderable, textColor, globalBgColor, transparent);
}

static class VNodeCompositionRenderHelpers
{
    public static void DemoRenderFn(
        string title,
        Rgba borderColor,
        OptimizedBuffer buffer,
        Renderable renderable,
        Rgba textColor,
        Rgba globalBgColor,
        Rgba transparent)
    {
        int x = renderable.X;
        int y = renderable.Y;
        int width = renderable.Width;
        int height = renderable.Height;

        double timeInSeconds = DateTime.UtcNow.TimeOfDay.TotalSeconds;
        float pulse = (float)(Math.Sin(timeInSeconds * 4) * 0.5 + 0.5);
        var pulsingBorderColor = new Rgba(
            borderColor.R * (0.1f + pulse * 0.9f),
            borderColor.G * (0.1f + pulse * 0.9f),
            borderColor.B * (0.1f + pulse * 0.9f),
            borderColor.A);

        float bgPulse = (float)(Math.Sin(timeInSeconds * 2 + (Math.PI / 2)) * 0.4 + 0.6);
        var pulsingBgColor = new Rgba(
            globalBgColor.R * bgPulse,
            globalBgColor.G * bgPulse,
            globalBgColor.B * bgPulse,
            globalBgColor.A);

        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                bool isBorder = row == 0 || row == height - 1 || col == 0 || col == width - 1;
                buffer.SetCell((uint)(x + col), (uint)(y + row), isBorder ? (uint)'█' : (uint)' ', isBorder ? pulsingBorderColor : textColor, pulsingBgColor);
            }
        }

        float titlePulse = (float)(Math.Sin(timeInSeconds * 6) * 0.5 + 0.5);
        float textScale = 0.3f + titlePulse * 0.7f;
        var pulsingTextColor = new Rgba(
            textColor.R * textScale,
            textColor.G * textScale,
            textColor.B * textScale,
            textColor.A);

        int titleX = x + Math.Max(0, (width - title.Length) / 2);
        int titleY = y + (height / 2);
        if (titleY >= y && titleY < y + height)
            buffer.DrawText(title, (uint)titleX, (uint)titleY, pulsingTextColor, transparent);
    }
}
