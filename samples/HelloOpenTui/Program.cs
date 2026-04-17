using OpenTui;

using var app = new App(new AppOptions { TargetFps = 30 });

app.Root.Border = BorderStyle.Rounded;
app.Root.Title = "Hello OpenTUI";

app.Root.Add(new Text("Welcome to OpenTUI-sharp! 🎉")
{
    Fg = Rgba.FromHex("#00FF88"),
    Padding = 1
});

app.Root.Add(new Text("Press Ctrl+C to exit.")
{
    Fg = Rgba.FromInts(128, 128, 128),
    AlignSelf = Align.Center
});

// Note: Full rendering requires the native opentui.dll.
// This sample demonstrates the widget tree and layout API.
Console.WriteLine("Widget tree created successfully!");
Console.WriteLine($"Root has {app.Root.Children.Count} children");
Console.WriteLine("(Full TUI rendering requires native renderer integration)");
