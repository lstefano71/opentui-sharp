using OpenTui;

using var app = new App(new AppOptions { TargetFps = 30 });

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

// Content area
var content = new Box
{
    Border = BorderStyle.Rounded,
    Title = "Editor",
    FlexGrow = 1,
    Bg = Rgba.FromHex("#1a1a2eCC")
};
content.Add(new Text("using System;") { Fg = Rgba.FromHex("#569CD6") });
content.Add(new Text(""));
content.Add(new Text("Console.WriteLine(\"Hello!\");") { Fg = Rgba.FromHex("#CE9178") });
content.Add(new Progress { Value = 67, Label = "Build Progress", Height = 1 });

// Status bar
var statusBar = new StatusBar { Height = 1 };
statusBar.LeftSections.Add(new StatusSection("NORMAL", Fg: Rgba.FromHex("#000000"), Bg: Rgba.FromHex("#00FF88")));
statusBar.LeftSections.Add(new StatusSection("main.cs"));
statusBar.RightSections.Add(new StatusSection("Ln 42, Col 8"));
statusBar.RightSections.Add(new StatusSection("UTF-8"));

app.Root.Add(header);
app.Root.Add(content);
app.Root.Add(statusBar);

app.Run();
