using ExampleLauncher;
using OpenTui.Core;

var samples = SampleDiscovery.Discover();

if (samples.Count == 0)
{
    Console.WriteLine("No built samples found.");
    Console.WriteLine("Run 'dotnet build' from the repo root first, then re-run the launcher.");
    return;
}

int lastSelectedIndex = 0;
using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = false,
    TargetFps = 30,
});

renderer.SetBackgroundColor(Rgba.Transparent);
renderer.Start();

while (true)
{
    var selector = new ExampleSelector(renderer, samples, lastSelectedIndex);
    var selected = await selector.WaitForSelectionAsync();

    if (selected is null)
        break; // User pressed Ctrl+C — exit

    lastSelectedIndex = samples.IndexOf(selected);

    // Suspend the renderer (tears down terminal I/O) while the sub-process runs
    renderer.Suspend();

    ExampleSelector.RunSample(selected);

    // Resume the renderer (restores terminal I/O and forces full repaint)
    renderer.Resume();

    // Reset for next menu cycle
    selector.Cleanup();
    renderer.SetBackgroundColor(Rgba.Transparent);
}

renderer.Stop();
