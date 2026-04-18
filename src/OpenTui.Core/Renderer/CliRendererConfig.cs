namespace OpenTui.Core;

/// <summary>
/// Configuration for <see cref="CliRenderer"/>.
/// Matches TypeScript CliRendererConfig (renderer.ts L85-176).
/// </summary>
public sealed class CliRendererConfig
{
    /// <summary>Skip terminal setup. Useful in tests.</summary>
    public bool Testing { get; init; }

    /// <summary>Tell the native renderer it is driving a remote terminal.</summary>
    public bool Remote { get; init; }

    /// <summary>Call destroy when Ctrl+C is pressed. Defaults to true.</summary>
    public bool ExitOnCtrlC { get; init; } = true;

    /// <summary>Wait this long (ms) before handling resize events. Defaults to 100.</summary>
    public int DebounceDelay { get; init; } = 100;

    /// <summary>Target frames per second in continuous (live) mode. Defaults to 30.</summary>
    public int TargetFps { get; init; } = 30;

    /// <summary>Cap immediate re-renders at this fps. Defaults to 60.</summary>
    public int MaxFps { get; init; } = 60;

    /// <summary>Forward these env var names to native terminal detection.</summary>
    public string[]? ForwardEnvKeys { get; init; }

    /// <summary>Track mouse move events. Defaults to true.</summary>
    public bool EnableMouseMovement { get; init; } = true;

    /// <summary>Enable mouse input. Defaults to true.</summary>
    public bool UseMouse { get; init; } = true;

    /// <summary>Focus the nearest focusable renderable on left click. Defaults to true.</summary>
    public bool AutoFocus { get; init; } = true;

    /// <summary>Screen mode: alternate-screen, main-screen, split-footer.</summary>
    public ScreenMode ScreenMode { get; init; } = ScreenMode.AlternateScreen;

    /// <summary>Fill the render buffer with this background color.</summary>
    public Rgba? BackgroundColor { get; init; }

    /// <summary>Kitty keyboard protocol config, or null to disable.</summary>
    public KittyKeyboardOptions? UseKittyKeyboard { get; init; } = new();

    /// <summary>Render from a separate thread when supported. Defaults to false on .NET.</summary>
    public bool UseThread { get; init; }

    /// <summary>Collect frame timing stats for the debug overlay.</summary>
    public bool GatherStats { get; init; }

    /// <summary>Run after destroy() finishes cleanup.</summary>
    public Action? OnDestroy { get; init; }

    /// <summary>Run these hooks after each render pass.</summary>
    public List<Action<OptimizedBuffer, float>>? PostProcessFns { get; init; }

    /// <summary>Width override (defaults to Console.WindowWidth).</summary>
    public int? Width { get; init; }

    /// <summary>Height override (defaults to Console.WindowHeight).</summary>
    public int? Height { get; init; }
}

/// <summary>Controls how the renderer uses terminal space.</summary>
public enum ScreenMode
{
    AlternateScreen,
    MainScreen,
    SplitFooter,
}

/// <summary>
/// Kitty keyboard protocol options.
/// See: https://sw.kovidgoyal.net/kitty/keyboard-protocol/
/// </summary>
public sealed class KittyKeyboardOptions
{
    /// <summary>Disambiguate escape codes. Default: true.</summary>
    public bool Disambiguate { get; init; } = true;

    /// <summary>Report alternate keys (numpad, shifted). Default: true.</summary>
    public bool AlternateKeys { get; init; } = true;

    /// <summary>Report event types (press/repeat/release). Default: false.</summary>
    public bool Events { get; init; }

    /// <summary>Report all keys as escape codes. Default: false.</summary>
    public bool AllKeysAsEscapes { get; init; }

    /// <summary>Report text associated with key events. Default: false.</summary>
    public bool ReportText { get; init; }

    internal byte BuildFlags()
    {
        byte flags = 0;
        if (Disambiguate) flags |= 0b1;
        if (Events) flags |= 0b10;
        if (AlternateKeys) flags |= 0b100;
        if (AllKeysAsEscapes) flags |= 0b1000;
        if (ReportText) flags |= 0b10000;
        return flags;
    }
}
