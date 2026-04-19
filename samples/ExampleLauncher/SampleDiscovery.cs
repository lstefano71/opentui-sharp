namespace ExampleLauncher;

/// <summary>
/// Information about a discovered sample executable.
/// </summary>
internal sealed record SampleInfo(string DirectoryName, string DisplayName, string Description, string ExePath);

/// <summary>
/// Scans the samples/ directory for built sample executables.
/// </summary>
internal static class SampleDiscovery
{
    /// <summary>
    /// Discovers sample executables by scanning sibling directories of the launcher.
    /// Looks for: samples/{Name}/bin/Debug/net10.0/{Name}.exe
    /// </summary>
    public static List<SampleInfo> Discover(string? samplesRoot = null)
    {
        samplesRoot ??= FindSamplesRoot();
        if (samplesRoot is null || !Directory.Exists(samplesRoot))
            return [];

        var results = new List<SampleInfo>();

        foreach (var dir in Directory.GetDirectories(samplesRoot).Order())
        {
            var dirName = Path.GetFileName(dir);
            if (dirName == "ExampleLauncher")
                continue;

            // Try Debug first, then Release
            var exePath = FindExecutable(dir, dirName, "Debug")
                       ?? FindExecutable(dir, dirName, "Release");

            if (exePath is null)
                continue;

            var displayName = FormatDisplayName(dirName);
            var description = SampleDescriptions.Get(dirName);
            results.Add(new SampleInfo(dirName, displayName, description, exePath));
        }

        return results;
    }

    private static string? FindExecutable(string sampleDir, string dirName, string config)
    {
        var candidate = Path.Combine(sampleDir, "bin", config, "net10.0", $"{dirName}.exe");
        return File.Exists(candidate) ? candidate : null;
    }

    private static string? FindSamplesRoot()
    {
        // Walk up from the launcher's exe directory to find the samples/ folder
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            var parent = Path.GetDirectoryName(dir);
            if (parent is null) break;

            // Check if parent contains the solution file (we're in the repo root)
            if (File.Exists(Path.Combine(parent, "openTUI-sharp.slnx")))
                return Path.Combine(parent, "samples");

            dir = parent;
        }

        return null;
    }

    private static string FormatDisplayName(string dirName)
    {
        // Insert spaces before uppercase letters: "WidgetShowcase" → "Widget Showcase"
        var chars = new List<char>(dirName.Length + 8);
        for (int i = 0; i < dirName.Length; i++)
        {
            char c = dirName[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(dirName[i - 1]))
                chars.Add(' ');
            chars.Add(c);
        }
        return new string(chars.ToArray());
    }
}
