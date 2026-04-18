// Extmarks Demo — virtual text markers
// Port of extmarks-demo.ts
using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
});

// --- Header ---
var header = new BoxRenderable(renderer, new BoxOptions
{
    Id = "header",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#7c3aed"),
    Border = true,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
});
var headerText = new TextRenderable(renderer, new TextOptions
{
    Id = "header-text",
    Content = "Extmarks Demo — Virtual Text Markers",
    Fg = Rgba.FromInts(255, 255, 255),
});
header.Add(headerText);

// --- Code area with extmarks ---
var codeArea = new BoxRenderable(renderer, new BoxOptions
{
    Id = "code-area",
    Width = DimensionValue.Auto,
    FlexGrow = 1,
    FlexDirection = FlexDirectionValue.Column,
    Padding = DimensionValue.Point(1),
    BackgroundColor = Rgba.FromHex("#1e1e1e"),
});

// Simulate code with inline diagnostics (extmarks)
string[] codeLines =
[
    "fn main() {",
    "    let x: i32 = \"hello\";",
    "    println!(\"{}\", x);",
    "    let y = 42 / 0;",
    "    let z = vec![1, 2, 3];",
    "    z.push(4);",
    "}",
];

// Diagnostic markers
(int line, string message, string color)[] diagnostics =
[
    (1, "  ← error: expected `i32`, found `&str`", "#ef4444"),
    (3, "  ← warning: division by zero", "#f59e0b"),
    (5, "  ← error: cannot borrow `z` as mutable", "#ef4444"),
];

int markerMode = 0;
string[] modes = ["All Markers", "Errors Only", "Warnings Only", "No Markers"];

void RenderCode()
{
    // Remove old code lines
    foreach (var child in codeArea.GetChildren().ToList())
        codeArea.Remove(child.Id);

    for (int i = 0; i < codeLines.Length; i++)
    {
        var lineBox = new BoxRenderable(renderer, new BoxOptions
        {
            Id = $"line-{i}",
            FlexDirection = FlexDirectionValue.Row,
            Height = DimensionValue.Point(1),
            Width = DimensionValue.Auto,
        });

        // Line number
        var lineNum = new TextRenderable(renderer, new TextOptions
        {
            Id = $"linenum-{i}",
            Content = $"{i + 1,3} │ ",
            Fg = Rgba.FromHex("#6b7280"),
        });
        lineBox.Add(lineNum);

        // Code text
        var codeLine = new TextRenderable(renderer, new TextOptions
        {
            Id = $"code-{i}",
            Content = codeLines[i],
            Fg = Rgba.FromHex("#d4d4d4"),
        });
        lineBox.Add(codeLine);

        // Check for diagnostic on this line
        var diag = diagnostics.FirstOrDefault(d => d.line == i);
        if (diag != default)
        {
            bool show = markerMode switch
            {
                0 => true,
                1 => diag.color == "#ef4444",
                2 => diag.color == "#f59e0b",
                _ => false,
            };

            if (show)
            {
                var marker = new TextRenderable(renderer, new TextOptions
                {
                    Id = $"marker-{i}",
                    Content = diag.message,
                    Fg = Rgba.FromHex(diag.color),
                    Attributes = TextAttributes.Italic,
                });
                lineBox.Add(marker);
            }
        }

        codeArea.Add(lineBox);
    }

    renderer.RequestRender();
}

// --- Status ---
var statusText = new TextRenderable(renderer, new TextOptions
{
    Id = "status",
    Content = $"Filter: {modes[markerMode]}",
    Fg = Rgba.FromHex("#a78bfa"),
    Attributes = TextAttributes.Bold,
});
codeArea.Add(statusText);

// --- Footer ---
var footer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "footer",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#7c3aed"),
    Border = true,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
});
var footerText = new TextRenderable(renderer, new TextOptions
{
    Id = "footer-text",
    Content = "M: cycle marker filter | Ctrl+C: exit",
    Fg = Rgba.FromInts(255, 255, 255),
});
footer.Add(footerText);

renderer.Root.Add(header);
renderer.Root.Add(codeArea);
renderer.Root.Add(footer);

RenderCode();

renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    if (e.Name == "m")
    {
        markerMode = (markerMode + 1) % modes.Length;
        statusText.ContentText = $"Filter: {modes[markerMode]}";
        RenderCode();
    }
});

await Task.Delay(Timeout.Infinite);
