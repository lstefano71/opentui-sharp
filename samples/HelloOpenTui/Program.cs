using OpenTui;

using var app = new App(new AppOptions { TargetFps = 30 });

app.Root.Border = BorderStyle.Rounded;
app.Root.Title = "Hello OpenTUI";
app.Root.Bg = Rgba.FromHex("#1a1a2e");

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

app.Run();
