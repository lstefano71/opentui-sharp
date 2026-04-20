using OpenTui.Core;
using static OpenTui.Core.Constructs;
using static OpenTui.Core.VStyles;

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
renderer.Start();

var mainGroup = new BoxRenderable(renderer, new BoxOptions
{
    Id = "main-group",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Percent(100),
    Padding = DimensionValue.Point(1),
    FlexDirection = FlexDirectionValue.Column,
});
renderer.Root.Add(mainGroup);

// BaseBox example (imperative — renderAfter needs self-reference, no C# equivalent of TS `this` binding)
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

// Declarative VNode tree — Constructs.Box/Text compose a descriptor, VNodeRuntime.Instantiate materializes it.
var tree = (BoxRenderable)VNodeRuntime.Instantiate(renderer, MyRenderable(
    Box(new BoxOptions { Id = "child-1", Width = DimensionValue.Point(20), Height = DimensionValue.Point(3), Border = true, MarginBottom = DimensionValue.Point(1) },
        Text(new TextOptions { Content = "Hello" })),
    Box(new BoxOptions { Id = "child-2", Width = DimensionValue.Point(24), Height = DimensionValue.Point(3), Border = true },
        Text(new TextOptions { Content = "VNode world" }))));
tree.BackgroundColor = new Rgba(0f, 155f / 255f, 155f / 255f, 100f / 255f);
mainGroup.Add(tree);

// Delegate construct — focus routes to the inner input
var input = VNodeRuntime.Instantiate(renderer, LabeledInput("labeled-input", "Label:", "Enter your text..."));
input.Focus();
mainGroup.Add(input);

// VNode delegated version — Delegate() wraps a VNode tree with add/remove routing
mainGroup.Add(MyDelegateToVNodeRenderable("delegated-demo-root",
[
    Box(new BoxOptions { Id = "child-1", Width = DimensionValue.Point(20), Height = DimensionValue.Point(3), Border = true, MarginBottom = DimensionValue.Point(1) },
        Text(new TextOptions { Content = "Hello delegated 1" })),
    Box(new BoxOptions { Id = "child-2", Width = DimensionValue.Point(24), Height = DimensionValue.Point(3), Border = true },
        Text(new TextOptions { Content = "VNode world delegated 1" })),
], new Rgba(155f / 255f, 0f, 155f / 255f, 100f / 255f)));

// Instanced delegated version
var instance = VNodeRuntime.Instantiate(renderer, MyDelegateToVNodeRenderable("demo-root",
[
    Box(new BoxOptions { Id = "child-1", Width = DimensionValue.Point(20), Height = DimensionValue.Point(3), Border = true, MarginBottom = DimensionValue.Point(1) },
        Text(new TextOptions { Content = "Hello 2" })),
    Box(new BoxOptions { Id = "child-2", Width = DimensionValue.Point(24), Height = DimensionValue.Point(3), Border = true },
        Text(new TextOptions { Content = "VNode world 2" })),
]));
mainGroup.Add(instance);

// Delegated to _box3, would otherwise end up in the top-level group!
instance.Add(Box(new BoxOptions { Id = "child-3", Width = DimensionValue.Point(24), Height = DimensionValue.Point(3), Border = true },
    Text(new TextOptions { Content = "VNode world 3" })));
instance.Add(Button("Click me", () => Console.WriteLine("clicked"), Rgba.Parse("red")));

// Renderable delegated version
var renderableInstance = VNodeRuntime.Instantiate(renderer, MyDelegateToRenderableComponent(
[
    Box(new BoxOptions { Id = "child-1", Width = DimensionValue.Point(20), Height = DimensionValue.Point(3), Border = true, MarginBottom = DimensionValue.Point(1) },
        Text(new TextOptions { Content = "Hello 4" })),
    Box(new BoxOptions { Id = "child-2", Width = DimensionValue.Point(24), Height = DimensionValue.Point(3), Border = true },
        Text(new TextOptions { Content = "VNode world 4" })),
]));
mainGroup.Add(renderableInstance);

// Delegated to __box4, would otherwise end up in the top-level group!
renderableInstance.Add(Button("Click me too!", () => Console.WriteLine("clicked"), Rgba.Parse("red")));

// Custom rendering — Generic() construct with VNode children
mainGroup.Add(VNodeButton("Animated VNode", () => Console.WriteLine("vnode 1 clicked"), Rgba.FromInts(0, 0, 255)));
mainGroup.Add(VNodeButton("Same VNode, different props", () => Console.WriteLine("vnode 2 clicked"), Rgba.FromInts(255, 0, 255)));

// Class method rendering — Generic() construct wrapping a class instance's render method
mainGroup.Add(ButtonWithClassRender("ClassRender", () => Console.WriteLine("clicked"), Rgba.FromInts(0, 0, 255)));

// VStyles — composable styled text matching TS vstyles API
mainGroup.Add(StyleExamples());

await Task.Delay(Timeout.Infinite);

// --- Declarative Component Functions (return VNode) ---

// Wraps children in a bordered double-line container
static VNode MyRenderable(params VChild[] children) =>
    Box(new BoxOptions { Id = "inner" },
        Box(new BoxOptions
        {
            Border = true,
            BorderStyle = BorderStyle.Double,
            Padding = DimensionValue.Point(1),
            OnMouseDown = evt => Console.WriteLine($"mouseHandler {evt.Type}"),
            FlexDirection = FlexDirectionValue.Row,
        }, children));

// Simple button with border and click handler
static VNode Button(string title, Action onClick, Rgba? borderColor = null) =>
    Box(new BoxOptions
    {
        Id = "button",
        Border = true,
        OnMouseDown = _ => onClick(),
        BorderColor = borderColor,
        PaddingLeft = DimensionValue.Point(1),
        PaddingRight = DimensionValue.Point(1),
    }, Text(new TextOptions { Content = title, Selectable = false }));

// Delegate construct — add/remove route to inner _box3
static VNode MyDelegateToVNodeRenderable(string id, VChild[] children, Rgba? backgroundColor = null) =>
    Delegate(
        new DelegateMapping { Add = $"{id}_box3", Remove = $"{id}_box3" },
        Box(new BoxOptions { Id = $"{id}_outer3", Border = true, BorderColor = Rgba.Parse("blue"), BackgroundColor = backgroundColor },
            Box(new BoxOptions { Id = $"{id}_inner3", Border = true, BorderColor = Rgba.Parse("magenta") },
                Box(new BoxOptions { Id = $"{id}_box3", FlexDirection = FlexDirectionValue.Row, Border = true, Padding = DimensionValue.Point(1) },
                    children))));

// Delegate construct — same pattern, different IDs
static VNode MyDelegateToRenderableComponent(VChild[] children) =>
    Delegate(
        new DelegateMapping { Add = "__box4", Remove = "__box4" },
        Box(new BoxOptions { Id = "__outer4", Border = true, BorderColor = Rgba.Parse("blue") },
            Box(new BoxOptions { Id = "__inner4", Border = true, BorderColor = Rgba.Parse("magenta") },
                Box(new BoxOptions { Id = "__box4", FlexDirection = FlexDirectionValue.Row, Border = true, Padding = DimensionValue.Point(1) },
                    children))));

// Delegate construct — focus routes to the inner input
static VNode LabeledInput(string id, string label, string placeholder) =>
    Delegate(
        new DelegateMapping { Focus = $"{id}-input" },
        Box(new BoxOptions { FlexDirection = FlexDirectionValue.Row, Id = $"{id}-labeled-outer" },
            Text(new TextOptions { Content = label + " " }),
            Input(new InputOptions
            {
                Id = $"{id}-input",
                Placeholder = placeholder,
                Width = DimensionValue.Point(20),
                BackgroundColor = Rgba.Parse("white"),
                TextColor = Rgba.Parse("black"),
                CursorColor = Rgba.Parse("blue"),
                FocusedBackgroundColor = Rgba.Parse("orange"),
            })));

// Generic() construct — custom rendering with VNode hit-area child
VNode VNodeButton(string title, Action onClick, Rgba borderColor) =>
    Generic(new GenericOptions
    {
        Id = $"{title}-generic",
        Width = DimensionValue.Point(Math.Max(title.Length + 4, 12)),
        Height = DimensionValue.Point(3),
        Margin = DimensionValue.Point(1),
        Render = (buffer, deltaTime, renderable) =>
            RenderHelpers.DemoRenderFn(title, borderColor, buffer, renderable, textColor, globalBgColor, transparent),
    }, Box(new BoxOptions
    {
        Id = "button",
        Width = DimensionValue.Percent(100),
        Height = DimensionValue.Percent(100),
        OnMouseDown = _ => onClick(),
    }));

// Generic() construct — class instance provides the render method
VNode ButtonWithClassRender(string title, Action onClick, Rgba borderColor)
{
    var root = new MyRoot(title, borderColor);
    return Generic(new GenericOptions
    {
        Id = $"{title}-class-render",
        Width = DimensionValue.Point(root.Width),
        Height = DimensionValue.Point(3),
        MarginLeft = DimensionValue.Point(1),
        Render = (buffer, deltaTime, renderable) =>
            root.Render(buffer, deltaTime, renderable, textColor, globalBgColor, transparent),
    }, Box(new BoxOptions
    {
        Id = "button",
        Width = DimensionValue.Percent(100),
        Height = DimensionValue.Percent(100),
        OnMouseDown = _ => onClick(),
    }));
}

// VStyles — entire styled text section as a declarative VNode tree
static VNode StyleExamples() =>
    Box(new BoxOptions { Id = "style-examples", FlexDirection = FlexDirectionValue.Column, MarginTop = DimensionValue.Point(2) },
        // Basic styles
        Text(Bold("Bold Text")),
        Text(Italic("Italic Text")),
        Text(Underline("Underlined Text")),
        Text(Dim("Dim Text")),
        // Combined styles
        Text(BoldItalic("Bold and Italic")),
        Text(BoldUnderline("Bold and Underlined")),
        Text(ItalicUnderline("Italic and Underlined")),
        // Colors
        Text(Color("#ff6b6b", "Red Text")),
        Text(BgColor("#4ecdc4", "Text with Background")),
        // Custom styling
        Text(Styled(TextAttributes.Bold | TextAttributes.Underline, "Custom Styled")),
        // Stacked styles — inner styles compose with outer
        Text(Bold(Underline("hello"), " world")),
        Text(Color("#ff6b6b", Bold("Bold Red"), " normal")),
        Text(Italic(Color("#4ecdc4", "Green Italic"), " normal again")));

// --- Imperative Helpers (renderAfter needs self-reference) ---

static BoxRenderable CreateBaseBox(IRenderContext ctx, BoxOptions props, params Renderable[] children)
{
    var renderAfter = props.RenderAfter;
    BoxRenderable? box = null;
    box = new BoxRenderable(ctx, new BoxOptions
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
    });

    foreach (var child in children)
        box.Add(child);

    return box;
}

static BoxRenderable CreateExtendedBaseBox(IRenderContext ctx, BoxOptions props)
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

// --- Render Helpers ---

// Type declarations must follow all top-level statements and local functions.
static class RenderHelpers
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

sealed class MyRoot(string title, Rgba borderColor)
{
    public int Width { get; } = Math.Max(title.Length + 4, 12);

    public void Render(OptimizedBuffer buffer, float deltaTime, Renderable renderable, Rgba textColor, Rgba globalBgColor, Rgba transparent) =>
        RenderHelpers.DemoRenderFn(title, borderColor, buffer, renderable, textColor, globalBgColor, transparent);
}
