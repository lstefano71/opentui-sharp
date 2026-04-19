using ExampleLauncher;
using OpenTui.Core;

var samples = SampleDiscovery.Discover();

if (samples.Count == 0)
{
    Console.WriteLine("No built samples found.");
    Console.WriteLine("Run 'dotnet build' from the repo root first, then re-run the launcher.");
    return;
}

while (true)
{
    using var renderer = CliRenderer.Create(new CliRendererConfig
    {
        ExitOnCtrlC = false,
        TargetFps = 30,
    });

    renderer.SetBackgroundColor(Rgba.Transparent);

    var selector = new ExampleSelector(renderer, samples);
    var selected = await selector.WaitForSelectionAsync();

    renderer.Destroy();

    if (selected is null)
        break; // User pressed Ctrl+C — exit

    // Small delay to let the terminal restore
    await Task.Delay(50);

    ExampleSelector.RunSample(selected);

    // Small delay before re-showing the menu
    await Task.Delay(100);
}
