using OpenTui;

Console.WriteLine("=== OpenTUI Widget Showcase ===\n");

// Create widget tree
using var app = new App();

// Header
var header = new Box
{
    Border = BorderStyle.Double,
    Title = "Widget Showcase",
    FlexDirection = FlexDirection.Row,
    JustifyContent = Justify.SpaceBetween,
    Padding = 1
};
header.Add(new Text("OpenTUI-sharp") { Fg = Rgba.FromHex("#FF6600") });
header.Add(new Text("v0.1.0") { Fg = Rgba.FromInts(128, 128, 128) });

// Content area with split pane
var splitPane = new SplitPane
{
    Orientation = SplitOrientation.Horizontal,
    SplitRatio = 0.3f,
    First = CreateSidebar(),
    Second = CreateMainContent()
};

// Status bar
var statusBar = new StatusBar();
statusBar.LeftSections.Add(new StatusSection("NORMAL", Fg: Rgba.FromHex("#000000"), Bg: Rgba.FromHex("#00FF88")));
statusBar.LeftSections.Add(new StatusSection("main.cs"));
statusBar.RightSections.Add(new StatusSection("Ln 42, Col 8"));
statusBar.RightSections.Add(new StatusSection("UTF-8"));

app.Root.Add(header);
app.Root.Add(splitPane);
app.Root.Add(statusBar);

// Print widget tree summary
PrintTree(app.Root, 0);

static Widget CreateSidebar()
{
    var sidebar = new Box { Border = BorderStyle.Single, Title = "Explorer" };
    sidebar.Add(new Text("📁 src/"));
    sidebar.Add(new Text("  📄 main.cs"));
    sidebar.Add(new Text("  📄 utils.cs"));
    sidebar.Add(new Text("📁 tests/"));
    sidebar.Add(new Text("  📄 test.cs"));
    return sidebar;
}

static Widget CreateMainContent()
{
    var content = new Box { Border = BorderStyle.Rounded, Title = "Editor" };
    content.Add(new Text("using System;") { Fg = Rgba.FromHex("#569CD6") });
    content.Add(new Text(""));
    content.Add(new Text("Console.WriteLine(\"Hello!\");") { Fg = Rgba.FromHex("#CE9178") });

    var progress = new Progress { Value = 67, Label = "Build Progress" };
    content.Add(progress);

    return content;
}

static void PrintTree(Widget widget, int depth)
{
    string indent = new string(' ', depth * 2);
    string name = widget.GetType().Name;
    string extra = widget switch
    {
        Text t => $" \"{t.Content}\"",
        Box b when b.Title is not null => $" [{b.Title}]",
        _ => ""
    };
    Console.WriteLine($"{indent}{name}{extra}");
    foreach (var child in widget)
        PrintTree(child, depth + 1);
}
