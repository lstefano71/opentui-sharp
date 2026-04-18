// Editor Demo — full text editor with TextareaRenderable
using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 30,
});

byte currentWrapMode = 2; // 0=none, 1=char, 2=word
string[] wrapLabels = ["None", "Char", "Word"];

const string SampleCode = """
    // Welcome to the Editor Demo
    // Use arrow keys to navigate, type to edit
    // Ctrl+Z to undo, Ctrl+Y to redo

    public class HelloWorld
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Hello, OpenTUI!");

            for (int i = 0; i < 10; i++)
            {
                Console.WriteLine($"Line {i}: The quick brown fox jumps over the lazy dog.");
            }
        }
    }
    """;

// --- Header ---
var header = new BoxRenderable(renderer, new BoxOptions
{
    Id = "header",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#7c3aed"),
    BorderStyle = BorderStyle.Rounded,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    Border = true,
});
var headerText = new TextRenderable(renderer, new TextOptions
{
    Id = "header-text",
    Content = "Editor Demo",
    Fg = Rgba.FromInts(255, 255, 255),
});
header.Add(headerText);

// --- Editor area ---
var textarea = new TextareaRenderable(renderer, new TextareaOptions
{
    Id = "editor",
    InitialValue = SampleCode,
    Placeholder = "Type here...",
    PlaceholderColor = Rgba.FromHex("#666666"),
    BackgroundColor = Rgba.FromHex("#1e1e1e"),
    TextColor = Rgba.FromHex("#d4d4d4"),
    FocusedBackgroundColor = Rgba.FromHex("#2d2d2d"),
    FocusedTextColor = Rgba.FromHex("#ffffff"),
    WrapMode = 2,
    Width = DimensionValue.Auto,
    Height = DimensionValue.Auto,
    FlexGrow = 1,
    Buffered = true,
});

// --- Status bar ---
var statusBar = new BoxRenderable(renderer, new BoxOptions
{
    Id = "status-bar",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(1),
    BackgroundColor = Rgba.FromHex("#1e40af"),
    FlexDirection = FlexDirectionValue.Row,
    AlignItems = AlignValue.Center,
    PaddingLeft = DimensionValue.Point(1),
    PaddingRight = DimensionValue.Point(1),
});
var statusText = new TextRenderable(renderer, new TextOptions
{
    Id = "status-text",
    Content = "",
    Fg = Rgba.FromHex("#93c5fd"),
});
statusBar.Add(statusText);

// --- Footer ---
var footer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "footer",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#1e3a5f"),
    BorderStyle = BorderStyle.Rounded,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    Border = true,
});
var footerText = new TextRenderable(renderer, new TextOptions
{
    Id = "footer-text",
    Content = "[Ctrl+W] Cycle wrap mode  [Arrows] Navigate  [Ctrl+Z/Y] Undo/Redo  [Ctrl+C] Quit",
    Fg = Rgba.FromHex("#94a3b8"),
});
footer.Add(footerText);

// --- Build tree ---
renderer.Root.Add(header);
renderer.Root.Add(textarea);
renderer.Root.Add(statusBar);
renderer.Root.Add(footer);

// Focus the editor
textarea.Focus();

// --- Status bar update via frame callback ---
renderer.AddFrameCallback(async (float _) =>
{
    var cursor = textarea.LogicalCursor;
    int lines = textarea.VirtualLineCount;
    string wrap = wrapLabels[currentWrapMode];
    statusText.ContentText = $"Ln {cursor.Row + 1}, Col {cursor.Col + 1}  |  Lines: {lines}  |  Wrap: {wrap}";
    await Task.CompletedTask;
});

// --- Key handling ---
renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    if (e.Ctrl && e.Name is "w")
    {
        currentWrapMode = (byte)((currentWrapMode + 1) % 3);
        textarea.EditorView.SetWrapMode(currentWrapMode);
        renderer.RequestRender();
    }
});

renderer.RequestRender();
await Task.Delay(Timeout.Infinite);
