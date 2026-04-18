namespace OpenTui.Core;

/// <summary>Well-known layout event names. Matches TS LayoutEvents enum.</summary>
public static class LayoutEvents
{
    public const string LayoutChanged = "layout-changed";
    public const string Added = "added";
    public const string Removed = "removed";
    public const string Resized = "resized";
}

/// <summary>Well-known renderable event names. Matches TS RenderableEvents enum.</summary>
public static class RenderableEventNames
{
    public const string Focused = "focused";
    public const string Blurred = "blurred";
    public const string Resize = "resize";
}

/// <summary>Well-known renderer event names. Matches TS RendererEvents interface.</summary>
public static class RendererEventNames
{
    public const string Resize = "resize";
    public const string Key = "key";
    public const string MemorySnapshot = "memory:snapshot";
    public const string Selection = "selection";
    public const string FocusedEditor = "focused_editor";
    public const string DebugOverlayToggle = "debugOverlay:toggle";
    public const string ThemeMode = "theme_mode";
    public const string Destroy = "destroy";
}
