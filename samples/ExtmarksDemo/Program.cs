using OpenTui.Core;

const string initialContent = """
Welcome to the Extmarks Demo!

This demo showcases virtual extmarks - text ranges that the cursor jumps over.

Try moving your cursor through the [VIRTUAL] markers below:
- Use arrow keys to navigate
- Notice how the cursor skips over [VIRTUAL] ranges
- Try backspacing at the end of a [VIRTUAL] marker
- It will delete the entire marker!

Example text with [LINK:https://example.com] embedded links.
You can also have [TAG:important] tags that act like atoms.

Regular text here can be edited normally.

Press Ctrl+L to add a new [MARKER] at cursor position.
Press Ctrl+C to exit.
""";

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 60,
});

renderer.SetBackgroundColor(Rgba.FromHex("#0D1117"));

using var syntaxStyle = SyntaxStyle.Create();
uint virtualStyleId = syntaxStyle.Register(
    "virtual",
    fg: new Rgba(0.3f, 0.7f, 1.0f, 1.0f),
    bg: new Rgba(0.1f, 0.2f, 0.3f, 1.0f));

var parentContainer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "parent-container",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Percent(100),
    ZIndex = 10,
    Padding = DimensionValue.Point(1),
    FlexDirection = FlexDirectionValue.Column,
});
renderer.Root.Add(parentContainer);

var editorBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "editor-box",
    Width = DimensionValue.Percent(100),
    FlexGrow = 1,
    BorderStyle = BorderStyle.Single,
    BorderColor = Rgba.FromHex("#6BCF7F"),
    BackgroundColor = Rgba.FromHex("#0D1117"),
    Title = "Extmarks Demo - Virtual Text Ranges",
    TitleAlignment = TitleAlignment.Left,
    PaddingLeft = DimensionValue.Point(1),
    PaddingRight = DimensionValue.Point(1),
    Border = true,
});
parentContainer.Add(editorBox);

var editor = new TextareaRenderable(renderer, new TextareaOptions
{
    Id = "editor",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Percent(100),
    InitialValue = initialContent,
    TextColor = Rgba.FromHex("#F0F6FC"),
    SelectionBg = Rgba.FromHex("#264F78"),
    SelectionFg = Rgba.FromHex("#FFFFFF"),
    WrapMode = (byte)WrapMode.Word,
    ShowCursor = true,
    CursorColor = Rgba.FromHex("#4ECDC4"),
    SyntaxStyle = syntaxStyle,
});
editorBox.Add(editor);

var helpText = new TextRenderable(renderer, new TextOptions
{
    Id = "help",
    Content = "Move cursor with arrows. Try backspacing at the end of [VIRTUAL] markers!",
    Fg = Rgba.FromHex("#FFA657"),
    Height = DimensionValue.Point(1),
});
parentContainer.Add(helpText);

var statusText = new TextRenderable(renderer, new TextOptions
{
    Id = "status",
    Content = string.Empty,
    Fg = Rgba.FromHex("#A5D6FF"),
    Height = DimensionValue.Point(1),
});
parentContainer.Add(statusText);

FindAndMarkVirtualRanges(editor.Extmarks, editor.PlainText, virtualStyleId);
editor.Focus();

renderer.AddFrameCallback(_ =>
{
    if (!editor.IsDestroyed)
    {
        var cursor = editor.LogicalCursor;
        uint offset = editor.CursorOffset;
        var extmarksAtCursor = editor.Extmarks.GetAtOffset(offset);
        int virtualCount = editor.Extmarks.GetVirtual().Count;

        string extmarkInfo = extmarksAtCursor.Count > 0
            ? $" | Inside extmark(s): {extmarksAtCursor.Count}"
            : string.Empty;

        statusText.ContentText =
            $"Line {cursor.Row + 1}, Col {cursor.Col + 1}, Offset {offset} | Virtual extmarks: {virtualCount}{extmarkInfo}";
    }

    return Task.CompletedTask;
});

renderer.KeyInput.On<KeyEvent>(KeyHandlerEvents.Keypress, keyEvent =>
{
    if (!keyEvent.Ctrl || !string.Equals(keyEvent.Name, "l", StringComparison.OrdinalIgnoreCase))
        return;

    keyEvent.PreventDefault();
    keyEvent.StopPropagation();

    if (editor.IsDestroyed)
        return;

    uint offset = editor.CursorOffset;
    const string markerText = "[MARKER]";
    editor.InsertText(markerText);
    editor.Extmarks.Create(new ExtmarkOptions
    {
        Start = offset,
        End = offset + (uint)markerText.Length,
        Virtual = true,
        StyleId = virtualStyleId,
        Data = new MarkerData("marker", "manual")
    });
    helpText.ContentText = $"Added virtual marker at offset {offset}!";
});

await Task.Delay(Timeout.Infinite);

static void FindAndMarkVirtualRanges(ExtmarksController controller, string text, uint styleId)
{
    const string pattern = @"\[(VIRTUAL|LINK:[^\]]+|TAG:[^\]]+|MARKER)\]";
    foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(text, pattern))
    {
        controller.Create(new ExtmarkOptions
        {
            Start = (uint)match.Index,
            End = (uint)(match.Index + match.Length),
            Virtual = true,
            StyleId = styleId,
            Data = new MarkerData("auto-detected", match.Value)
        });
    }
}

sealed record MarkerData(string Type, string Content);
