using System.Net.Http;
using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 30,
    EnableMouseMovement = true,
    BackgroundColor = Rgba.FromHex("#0a0a14"),
});

using var httpClient = new HttpClient();

const string BabylonUrl = "https://cdnjs.cloudflare.com/ajax/libs/babylonjs/8.20.0/babylon.js";
const int MinTextBoxWidth = 10;
const int MinTextBoxHeight = 5;
const int ContentPadding = 1;

BoxRenderable? mainContainer = null;
BoxRenderable? contentBox = null;
ScrollBoxRenderable? textBox = null;
TextRenderable? textRenderable = null;
BoxRenderable? instructionsBox = null;
TextRenderable? instructionsText1 = null;
TextRenderable? instructionsText2 = null;
InputRenderable? filePathInput = null;
BoxRenderable? fileInputContainer = null;
bool isInputVisible = false;

bool isResizing = false;
string? resizeDirection = null;
int resizeStartX = 0;
int resizeStartY = 0;
int resizeStartLeft = 0;
int resizeStartTop = 0;
int resizeStartWidth = 0;
int resizeStartHeight = 0;

string babylonCachePath = Path.Combine(AppContext.BaseDirectory, "text-wrap-babylon.js");

static Rgba Hex(string value) => Rgba.FromHex(value);

static TextChunk Chunk(
    string text,
    string? fg = null,
    string? bg = null,
    TextAttributes attributes = TextAttributes.None) =>
    TextChunk.Styled(
        text,
        fg: fg is null ? null : Hex(fg),
        bg: bg is null ? null : Hex(bg),
        attributes: attributes);

static StyledText BuildInstructionsLine1() => new(
    Chunk("Text Wrap Demo", "#7aa2f7", attributes: TextAttributes.Bold),
    Chunk(" - ", "#565f89"),
    Chunk("W", "#9ece6a", attributes: TextAttributes.Bold),
    Chunk(" Cycle wrap mode ", "#c0caf5"),
    Chunk("|", "#565f89"),
    Chunk(" M", "#bb9af7", attributes: TextAttributes.Bold),
    Chunk(" Toggle char/word ", "#c0caf5"),
    Chunk("|", "#565f89"),
    Chunk(" D", "#f7768e", attributes: TextAttributes.Bold),
    Chunk(" Download Babylon.js ", "#c0caf5"),
    Chunk("|", "#565f89"),
    Chunk(" L", "#e0af68", attributes: TextAttributes.Bold),
    Chunk(" Load file ", "#c0caf5"),
    Chunk("|", "#565f89"),
    Chunk(" Drag", "#ff9e64", attributes: TextAttributes.Bold),
    Chunk(" borders/corners to resize", "#c0caf5"));

static string WrapModeName(WrapMode mode) => mode switch
{
    WrapMode.Word => "word",
    WrapMode.Char => "char",
    _ => "none",
};

StyledText BuildWrapModeStatus() => new(
    Chunk("Status:", "#7aa2f7", attributes: TextAttributes.Bold),
    Chunk(" Wrap mode:", "#c0caf5"),
    Chunk($" {WrapModeName(textRenderable?.WrapMode ?? WrapMode.Word)}", "#bb9af7"));

StyledText BuildLoadingStatus(string text) => new(
    Chunk("Status:", "#7aa2f7", attributes: TextAttributes.Bold),
    Chunk($" {text}", "#f7768e"));

StyledText BuildErrorStatus(string text) => new(
    Chunk("Status:", "#7aa2f7", attributes: TextAttributes.Bold),
    Chunk(" Error: ", "#f7768e"),
    Chunk(text, "#c0caf5"));

StyledText BuildFileStatus(string label, string sizeMb, string bufferMb) => new(
    Chunk("Status:", "#7aa2f7", attributes: TextAttributes.Bold),
    Chunk($" {label}: ", "#c0caf5"),
    Chunk(sizeMb, "#9ece6a"),
    Chunk(" MB, Buffer: ", "#c0caf5"),
    Chunk(bufferMb, "#9ece6a"),
    Chunk(" MB, Mode: ", "#c0caf5"),
    Chunk(WrapModeName(textRenderable?.WrapMode ?? WrapMode.Word), "#bb9af7"),
    Chunk(")", "#c0caf5"));

TextNodeRenderable CreateDemoText()
{
    var titleNode = TextNodeRenderable.FromString("🎨 OpenTUI Text Wrapping Demo", new TextNodeOptions
    {
        Fg = Hex("#7aa2f7"),
        Attributes = TextAttributes.Bold,
    });

    var introNode = TextNodeRenderable.FromString("\n\nWelcome to the ", new TextNodeOptions
    {
        Fg = Hex("#c0caf5"),
    });

    var highlightNode = TextNodeRenderable.FromString("text wrapping demonstration", new TextNodeOptions
    {
        Fg = Hex("#9ece6a"),
        Attributes = TextAttributes.Bold,
    });

    var introContNode = TextNodeRenderable.FromString(
        ". This example showcases how OpenTUI handles automatic text wrapping with styled content using TextNodes.",
        new TextNodeOptions
        {
            Fg = Hex("#c0caf5"),
        });

    var featuresTitle = TextNodeRenderable.FromString("\n\n✨ Key Features:", new TextNodeOptions
    {
        Fg = Hex("#bb9af7"),
        Attributes = TextAttributes.Bold,
    });

    var feature1Node = TextNodeRenderable.FromNodes([
        TextNodeRenderable.FromString("\n• ", new TextNodeOptions { Fg = Hex("#9ece6a") }),
        TextNodeRenderable.FromString("Word-based wrapping", new TextNodeOptions
        {
            Fg = Hex("#c0caf5"),
            Attributes = TextAttributes.Bold,
        }),
        TextNodeRenderable.FromString(" - Preserves word boundaries when breaking lines 📖", new TextNodeOptions
        {
            Fg = Hex("#565f89"),
        }),
    ]);

    var feature2Node = TextNodeRenderable.FromNodes([
        TextNodeRenderable.FromString("\n• ", new TextNodeOptions { Fg = Hex("#9ece6a") }),
        TextNodeRenderable.FromString("Character-based wrapping", new TextNodeOptions
        {
            Fg = Hex("#c0caf5"),
            Attributes = TextAttributes.Bold,
        }),
        TextNodeRenderable.FromString(" - Breaks at any character for precise control ✂️", new TextNodeOptions
        {
            Fg = Hex("#565f89"),
        }),
    ]);

    var feature3Node = TextNodeRenderable.FromNodes([
        TextNodeRenderable.FromString("\n• ", new TextNodeOptions { Fg = Hex("#9ece6a") }),
        TextNodeRenderable.FromString("Dynamic resizing", new TextNodeOptions
        {
            Fg = Hex("#c0caf5"),
            Attributes = TextAttributes.Bold,
        }),
        TextNodeRenderable.FromString(" - Text reflows automatically as container dimensions change 🔄", new TextNodeOptions
        {
            Fg = Hex("#565f89"),
        }),
    ]);

    var feature4Node = TextNodeRenderable.FromNodes([
        TextNodeRenderable.FromString("\n• ", new TextNodeOptions { Fg = Hex("#9ece6a") }),
        TextNodeRenderable.FromString("Rich styling", new TextNodeOptions
        {
            Fg = Hex("#c0caf5"),
            Attributes = TextAttributes.Bold,
        }),
        TextNodeRenderable.FromString(" - Individual text segments can have different colors and attributes 🎨", new TextNodeOptions
        {
            Fg = Hex("#565f89"),
        }),
    ]);

    var demoTitle = TextNodeRenderable.FromString("\n\n🔧 How It Works:", new TextNodeOptions
    {
        Fg = Hex("#bb9af7"),
        Attributes = TextAttributes.Bold,
    });

    var demoText = TextNodeRenderable.FromString(
        "\n\nTextNodes are created with specific styling and then composed together to form rich, formatted text content. Each node can contain different foreground colors, background colors, and text attributes like ",
        new TextNodeOptions
        {
            Fg = Hex("#c0caf5"),
        });

    var boldExample = TextNodeRenderable.FromString("bold", new TextNodeOptions
    {
        Fg = Hex("#f7768e"),
        Attributes = TextAttributes.Bold,
    });

    var demoCont = TextNodeRenderable.FromString(", ", new TextNodeOptions
    {
        Fg = Hex("#c0caf5"),
    });

    var italicExample = TextNodeRenderable.FromString("italic", new TextNodeOptions
    {
        Fg = Hex("#f7768e"),
        Attributes = TextAttributes.Italic,
    });

    var demoCont2 = TextNodeRenderable.FromString(", and ", new TextNodeOptions
    {
        Fg = Hex("#c0caf5"),
    });

    var underlineExample = TextNodeRenderable.FromString("underline", new TextNodeOptions
    {
        Fg = Hex("#f7768e"),
        Attributes = TextAttributes.Underline,
    });

    var demoCont3 = TextNodeRenderable.FromString(
        ". When the container is resized, the text automatically reflows to fit the new dimensions while maintaining the specified wrapping mode.",
        new TextNodeOptions
        {
            Fg = Hex("#c0caf5"),
        });

    var codeTitle = TextNodeRenderable.FromString("\n\n💻 Example Code: 🖥️", new TextNodeOptions
    {
        Fg = Hex("#bb9af7"),
        Attributes = TextAttributes.Bold,
    });

    var codeBlock = TextNodeRenderable.FromString(
        """

        const styledText = TextNodeRenderable.fromNodes([
          TextNodeRenderable.fromString("Hello ", { fg: "#9ece6a" }),
          TextNodeRenderable.fromString("World", { fg: "#7aa2f7", attributes: 1 }),
          TextNodeRenderable.fromString("!", { fg: "#f7768e" })
        ]);

        textRenderable.add(styledText);
        """,
        new TextNodeOptions
        {
            Fg = Hex("#c0caf5"),
            Bg = Hex("#1a1a2e"),
        });

    var interactionTitle = TextNodeRenderable.FromString("\n\n🎮 Try It Out:", new TextNodeOptions
    {
        Fg = Hex("#bb9af7"),
        Attributes = TextAttributes.Bold,
    });

    var interactionText = TextNodeRenderable.FromString(
        "\n\nDrag the borders or corners of this text box to resize it and watch how the text wrapping adapts in real-time. Press ",
        new TextNodeOptions
        {
            Fg = Hex("#c0caf5"),
        });

    var keyW = TextNodeRenderable.FromString("W", new TextNodeOptions
    {
        Fg = Hex("#9ece6a"),
        Attributes = TextAttributes.Bold,
    });

    var interactionCont = TextNodeRenderable.FromString(" to toggle wrapping on/off, ", new TextNodeOptions
    {
        Fg = Hex("#c0caf5"),
    });

    var keyM = TextNodeRenderable.FromString("M", new TextNodeOptions
    {
        Fg = Hex("#bb9af7"),
        Attributes = TextAttributes.Bold,
    });

    var interactionCont2 = TextNodeRenderable.FromString(" to switch between word and character wrapping modes, and ", new TextNodeOptions
    {
        Fg = Hex("#c0caf5"),
    });

    var keyD = TextNodeRenderable.FromString("D", new TextNodeOptions
    {
        Fg = Hex("#f7768e"),
        Attributes = TextAttributes.Bold,
    });

    var interactionCont3 = TextNodeRenderable.FromString(
        " to download and display the Babylon.js library source code. The text will reflow instantly to demonstrate the different wrapping behaviors.",
        new TextNodeOptions
        {
            Fg = Hex("#c0caf5"),
        });

    var conclusionNode = TextNodeRenderable.FromString(
        "\n\n🚀 This demonstrates the power of OpenTUI's flexible text rendering system, combining rich styling with dynamic layout capabilities! ✨🎨📝",
        new TextNodeOptions
        {
            Fg = Hex("#9ece6a"),
            Attributes = TextAttributes.Bold,
        });

    return TextNodeRenderable.FromNodes([
        titleNode,
        introNode,
        highlightNode,
        introContNode,
        featuresTitle,
        feature1Node,
        feature2Node,
        feature3Node,
        feature4Node,
        demoTitle,
        demoText,
        boldExample,
        demoCont,
        italicExample,
        demoCont2,
        underlineExample,
        demoCont3,
        codeTitle,
        codeBlock,
        interactionTitle,
        interactionText,
        keyW,
        interactionCont,
        keyM,
        interactionCont2,
        keyD,
        interactionCont3,
        conclusionNode,
    ]);
}

void SetTextContent(TextNodeRenderable root)
{
    if (textRenderable is null)
        return;

    textRenderable.Content = new StyledText(root.GatherWithInheritedStyle());
    textBox?.ScrollTo(y: 0);
}

void SetPlainTextContent(string text, string fg)
{
    SetTextContent(TextNodeRenderable.FromString(text, new TextNodeOptions
    {
        Fg = Hex(fg),
    }));
}

void ShowFileInput()
{
    if (fileInputContainer is null || filePathInput is null)
        return;

    fileInputContainer.Visible = true;
    filePathInput.Value = "";
    filePathInput.Focus();
    isInputVisible = true;
}

void HideFileInput()
{
    if (fileInputContainer is null || filePathInput is null)
        return;

    fileInputContainer.Visible = false;
    filePathInput.Blur();
    isInputVisible = false;
}

static string? GetResizeDirection(int mouseX, int mouseY, int boxLeft, int boxTop, int boxWidth, int boxHeight)
{
    bool onLeftBorder = mouseX == boxLeft;
    bool onRightBorder = mouseX == boxLeft + boxWidth - 1;
    bool onTopBorder = mouseY == boxTop;
    bool onBottomBorder = mouseY == boxTop + boxHeight - 1;

    bool withinHorizontalBounds = mouseX >= boxLeft && mouseX <= boxLeft + boxWidth - 1;
    bool withinVerticalBounds = mouseY >= boxTop && mouseY <= boxTop + boxHeight - 1;

    bool left = onLeftBorder && withinVerticalBounds;
    bool right = onRightBorder && withinVerticalBounds;
    bool top = onTopBorder && withinHorizontalBounds;
    bool bottom = onBottomBorder && withinHorizontalBounds;

    if (top && left) return "nw";
    if (top && right) return "ne";
    if (bottom && left) return "sw";
    if (bottom && right) return "se";
    if (top) return "n";
    if (bottom) return "s";
    if (left) return "w";
    if (right) return "e";
    return null;
}

void HandleTextBoxMouse(UiMouseEvent mouseEvent)
{
    if (textBox is null)
        return;

    switch (mouseEvent.Type)
    {
        case MouseEventType.Move:
        case MouseEventType.Over:
            if (!isResizing)
            {
                resizeDirection = GetResizeDirection(
                    mouseEvent.X,
                    mouseEvent.Y,
                    (int)textBox.ScreenX,
                    (int)textBox.ScreenY,
                    textBox.Width,
                    textBox.Height);
            }
            break;

        case MouseEventType.Down:
            if (resizeDirection is not null)
            {
                isResizing = true;
                resizeStartX = mouseEvent.X;
                resizeStartY = mouseEvent.Y;
                resizeStartWidth = textBox.Width;
                resizeStartHeight = textBox.Height;
                resizeStartLeft = textBox.Left?.IsPoint == true ? (int)textBox.Left.Value.Value : 0;
                resizeStartTop = textBox.Top?.IsPoint == true ? (int)textBox.Top.Value.Value : 0;
                mouseEvent.StopPropagation();
            }
            break;

        case MouseEventType.Out:
            if (!isResizing)
                resizeDirection = null;
            break;
    }
}

void HandleGlobalMouse(UiMouseEvent mouseEvent)
{
    switch (mouseEvent.Type)
    {
        case MouseEventType.Move:
        case MouseEventType.Drag:
            if (isResizing && resizeDirection is not null && textBox is not null)
            {
                int deltaX = mouseEvent.X - resizeStartX;
                int deltaY = mouseEvent.Y - resizeStartY;

                int newWidth = resizeStartWidth;
                int newHeight = resizeStartHeight;
                int newLeft = resizeStartLeft;
                int newTop = resizeStartTop;

                switch (resizeDirection)
                {
                    case "nw":
                        newWidth = Math.Max(MinTextBoxWidth, resizeStartWidth - deltaX);
                        newHeight = Math.Max(MinTextBoxHeight, resizeStartHeight - deltaY);
                        newLeft = resizeStartLeft + (resizeStartWidth - newWidth);
                        newTop = resizeStartTop + (resizeStartHeight - newHeight);
                        break;
                    case "ne":
                        newWidth = Math.Max(MinTextBoxWidth, resizeStartWidth + deltaX);
                        newHeight = Math.Max(MinTextBoxHeight, resizeStartHeight - deltaY);
                        newTop = resizeStartTop + (resizeStartHeight - newHeight);
                        break;
                    case "sw":
                        newWidth = Math.Max(MinTextBoxWidth, resizeStartWidth - deltaX);
                        newHeight = Math.Max(MinTextBoxHeight, resizeStartHeight + deltaY);
                        newLeft = resizeStartLeft + (resizeStartWidth - newWidth);
                        break;
                    case "se":
                        newWidth = Math.Max(MinTextBoxWidth, resizeStartWidth + deltaX);
                        newHeight = Math.Max(MinTextBoxHeight, resizeStartHeight + deltaY);
                        break;
                    case "n":
                        newHeight = Math.Max(MinTextBoxHeight, resizeStartHeight - deltaY);
                        newTop = resizeStartTop + (resizeStartHeight - newHeight);
                        break;
                    case "s":
                        newHeight = Math.Max(MinTextBoxHeight, resizeStartHeight + deltaY);
                        break;
                    case "w":
                        newWidth = Math.Max(MinTextBoxWidth, resizeStartWidth - deltaX);
                        newLeft = resizeStartLeft + (resizeStartWidth - newWidth);
                        break;
                    case "e":
                        newWidth = Math.Max(MinTextBoxWidth, resizeStartWidth + deltaX);
                        break;
                }

                if (contentBox is not null)
                {
                    int maxWidth = contentBox.Width - (2 * ContentPadding);
                    int maxHeight = contentBox.Height - (2 * ContentPadding);
                    int minLeft = ContentPadding;
                    int minTop = ContentPadding;
                    int maxLeft = contentBox.Width - newWidth - ContentPadding;
                    int maxTop = contentBox.Height - newHeight - ContentPadding;

                    newWidth = Math.Min(newWidth, maxWidth);
                    newHeight = Math.Min(newHeight, maxHeight);
                    newLeft = Math.Max(minLeft, Math.Min(newLeft, maxLeft));
                    newTop = Math.Max(minTop, Math.Min(newTop, maxTop));
                }

                textBox.WidthDimension = DimensionValue.Point(newWidth);
                textBox.HeightDimension = DimensionValue.Point(newHeight);
                textBox.Left = DimensionValue.Point(newLeft);
                textBox.Top = DimensionValue.Point(newTop);
            }
            break;

        case MouseEventType.Up:
        case MouseEventType.DragEnd:
            if (isResizing)
            {
                isResizing = false;
                resizeDirection = null;
            }
            break;
    }
}

async Task LoadFileAsync(string rawPath)
{
    if (textRenderable is null || instructionsText2 is null)
        return;

    string filePath = rawPath.Trim();
    if (filePath.Length == 0)
    {
        HideFileInput();
        return;
    }

    HideFileInput();
    instructionsText2.Content = BuildLoadingStatus("Loading file...");

    try
    {
        var fileInfo = new FileInfo(filePath);
        long fileSizeBytes = fileInfo.Length;
        string fileSizeMb = (fileSizeBytes / (1024d * 1024d)).ToString("0.00");
        string content = await File.ReadAllTextAsync(filePath);

        var headerNode = TextNodeRenderable.FromString(
            $"// Loaded from: {filePath}\n// Size: {fileSizeMb} MB\n\n",
            new TextNodeOptions
            {
                Fg = Hex("#9ece6a"),
            });

        var contentNode = TextNodeRenderable.FromString(content, new TextNodeOptions
        {
            Fg = Hex("#c0caf5"),
        });

        SetTextContent(TextNodeRenderable.FromNodes([headerNode, contentNode]));

        string bufferMb = (textRenderable.TextBuffer.ByteSize / (1024d * 1024d)).ToString("0.00");
        instructionsText2.Content = BuildFileStatus("File", fileSizeMb, bufferMb);
    }
    catch (Exception ex)
    {
        SetPlainTextContent($"ERROR: {ex.Message}\n\nPress L to try again.", "#f7768e");
        instructionsText2.Content = BuildErrorStatus("loading file");
    }
}

async Task DownloadBabylonAsync()
{
    if (textRenderable is null || instructionsText2 is null)
        return;

    instructionsText2.Content = BuildLoadingStatus("Downloading Babylon.js...");

    try
    {
        string content = await httpClient.GetStringAsync(BabylonUrl);
        await File.WriteAllTextAsync(babylonCachePath, content);
        string loadedContent = await File.ReadAllTextAsync(babylonCachePath);

        string fileSizeMb = (System.Text.Encoding.UTF8.GetByteCount(loadedContent) / (1024d * 1024d)).ToString("0.00");
        SetPlainTextContent(
            $"// Downloaded Babylon.js ({loadedContent.Length:N0} chars, {fileSizeMb} MB)\n// Stored at: {babylonCachePath}\n\n{loadedContent}",
            "#c0caf5");

        string bufferMb = (textRenderable.TextBuffer.ByteSize / (1024d * 1024d)).ToString("0.00");
        instructionsText2.Content = BuildFileStatus("Downloaded", fileSizeMb, bufferMb);
    }
    catch (Exception ex)
    {
        instructionsText2.Content = BuildErrorStatus($"download failed: {ex.Message}");
    }
}

renderer.Root.OnMouse = HandleGlobalMouse;

    mainContainer = new BoxRenderable(renderer, new BoxOptions
    {
        Id = "mainContainer",
        Width = DimensionValue.Percent(100),
        Height = DimensionValue.Percent(100),
        FlexGrow = 1,
        MaxWidth = DimensionValue.Percent(100),
        MaxHeight = DimensionValue.Percent(100),
        BackgroundColor = Hex("#0f0f23"),
        FlexDirection = FlexDirectionValue.Column,
    });
    renderer.Root.Add(mainContainer);

    contentBox = new BoxRenderable(renderer, new BoxOptions
    {
        Id = "content-box",
        FlexGrow = 1,
        BackgroundColor = Hex("#1e1e2e"),
        Border = true,
        BorderColor = Hex("#565f89"),
        Padding = DimensionValue.Point(1),
    });

    textBox = new ScrollBoxRenderable(renderer, new ScrollBoxOptions
    {
        Id = "text-box",
        Position = PositionValue.Absolute,
        Left = DimensionValue.Point(2),
        Top = DimensionValue.Point(2),
        Width = DimensionValue.Point(80),
        Height = DimensionValue.Point(15),
        Border = true,
        BorderStyle = BorderStyle.Rounded,
        BorderColor = Hex("#9ece6a"),
        BackgroundColor = Hex("#11111b"),
        OnMouse = HandleTextBoxMouse,
    });
    contentBox.Add(textBox);

    textRenderable = new TextRenderable(renderer, new TextOptions
    {
        Id = "text-renderable",
        Fg = Hex("#c0caf5"),
        WrapMode = WrapMode.Word,
    });
    textBox.Add(textRenderable);
    SetTextContent(CreateDemoText());

    instructionsBox = new BoxRenderable(renderer, new BoxOptions
    {
        Id = "instructions-box",
        Width = DimensionValue.Percent(100),
        FlexDirection = FlexDirectionValue.Column,
        BackgroundColor = Hex("#1e1e2e"),
        Border = true,
        BorderColor = Hex("#565f89"),
        Padding = DimensionValue.Point(1),
    });

    instructionsText1 = new TextRenderable(renderer, new TextOptions
    {
        Id = "instructions-1",
        StyledContent = BuildInstructionsLine1(),
    });
    instructionsBox.Add(instructionsText1);

    instructionsText2 = new TextRenderable(renderer, new TextOptions
    {
        Id = "instructions-2",
        StyledContent = BuildWrapModeStatus(),
    });
    instructionsBox.Add(instructionsText2);

    fileInputContainer = new BoxRenderable(renderer, new BoxOptions
    {
        Id = "file-input-container",
        Position = PositionValue.Absolute,
        Left = DimensionValue.Percent(50),
        Top = DimensionValue.Percent(50),
        Width = DimensionValue.Point(60),
        Height = DimensionValue.Point(3),
        MarginLeft = DimensionValue.Point(-30),
        MarginTop = DimensionValue.Point(-2),
        ZIndex = 200,
        Border = true,
        BorderStyle = BorderStyle.Rounded,
        BorderColor = Hex("#7aa2f7"),
        BackgroundColor = Hex("#1e1e2e"),
        Visible = false,
        JustifyContent = JustifyValue.Center,
    });
    mainContainer.Add(fileInputContainer);

    filePathInput = new InputRenderable(renderer, new InputOptions
    {
        Id = "file-path-input",
        Width = DimensionValue.Percent(100),
        BackgroundColor = Hex("#1e1e2e"),
        TextColor = Hex("#c0caf5"),
        FocusedBackgroundColor = Hex("#1e1e2e"),
        FocusedTextColor = Hex("#c0caf5"),
        Placeholder = "Enter file path (relative to cwd or absolute)...",
        PlaceholderColor = Hex("#565f89"),
        CursorColor = Hex("#7aa2f7"),
        Value = "",
        MaxLength = 500,
        OnKeyDown = keyEvent =>
        {
            if (keyEvent.Name == "backspace" && filePathInput?.Value == "" && isInputVisible)
                HideFileInput();
        },
    });
    fileInputContainer.Add(filePathInput);

    filePathInput.On<string>(InputRenderable.Events.Enter, async value =>
    {
        await LoadFileAsync(value);
    });

    mainContainer.Add(contentBox);
    mainContainer.Add(instructionsBox);

renderer.KeyInput.On<KeyEvent>("keypress", keyEvent =>
{
    if (isInputVisible)
        return;

    if (string.Equals(keyEvent.Name, "l", StringComparison.OrdinalIgnoreCase))
    {
        ShowFileInput();
    }
    else if (string.Equals(keyEvent.Name, "w", StringComparison.OrdinalIgnoreCase))
    {
        if (textRenderable is null || instructionsText2 is null)
            return;

        textRenderable.WrapMode = textRenderable.WrapMode switch
        {
            WrapMode.Word => WrapMode.Char,
            WrapMode.Char => WrapMode.None,
            _ => WrapMode.Word,
        };
        instructionsText2.Content = BuildWrapModeStatus();
    }
    else if (string.Equals(keyEvent.Name, "m", StringComparison.OrdinalIgnoreCase))
    {
        if (textRenderable is null || instructionsText2 is null)
            return;

        textRenderable.WrapMode = textRenderable.WrapMode switch
        {
            WrapMode.None => WrapMode.Word,
            WrapMode.Char => WrapMode.Word,
            _ => WrapMode.Char,
        };
        instructionsText2.Content = BuildWrapModeStatus();
    }
    else if (string.Equals(keyEvent.Name, "d", StringComparison.OrdinalIgnoreCase))
    {
        _ = DownloadBabylonAsync();
    }
});

await Task.Delay(Timeout.Infinite);
