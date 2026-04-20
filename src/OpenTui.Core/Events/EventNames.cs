namespace OpenTui.Core;

/// <summary>Well-known layout event names. Matches TS LayoutEvents enum.</summary>
public static class LayoutEvents
{
    /// <summary>
    /// Stores the layout changed.
    /// </summary>
    public const string LayoutChanged = "layout-changed";
    /// <summary>
    /// Stores the added.
    /// </summary>
    public const string Added = "added";
    /// <summary>
    /// Stores the removed.
    /// </summary>
    public const string Removed = "removed";
    /// <summary>
    /// Stores the resized.
    /// </summary>
    public const string Resized = "resized";
}

/// <summary>Well-known renderable event names. Matches TS RenderableEvents enum.</summary>
public static class RenderableEventNames
{
    /// <summary>
    /// Stores the focused.
    /// </summary>
    public const string Focused = "focused";
    /// <summary>
    /// Stores the blurred.
    /// </summary>
    public const string Blurred = "blurred";
    /// <summary>
    /// Stores the resize.
    /// </summary>
    public const string Resize = "resize";
}

/// <summary>Well-known renderer event names. Matches TS RendererEvents interface.</summary>
public static class RendererEventNames
{
    /// <summary>
    /// Stores the resize.
    /// </summary>
    public const string Resize = "resize";
    /// <summary>
    /// Stores the key.
    /// </summary>
    public const string Key = "key";
    /// <summary>
    /// Stores the memory snapshot.
    /// </summary>
    public const string MemorySnapshot = "memory:snapshot";
    /// <summary>
    /// Stores the selection.
    /// </summary>
    public const string Selection = "selection";
    /// <summary>
    /// Stores the focus.
    /// </summary>
    public const string Focus = "focus";
    /// <summary>
    /// Stores the blur.
    /// </summary>
    public const string Blur = "blur";
    /// <summary>
    /// Stores the focused editor.
    /// </summary>
    public const string FocusedEditor = "focused_editor";
    /// <summary>
    /// Stores the debug overlay toggle.
    /// </summary>
    public const string DebugOverlayToggle = "debugOverlay:toggle";
    /// <summary>
    /// Stores the theme mode.
    /// </summary>
    public const string ThemeMode = "theme_mode";
    /// <summary>
    /// Stores the destroy.
    /// </summary>
    public const string Destroy = "destroy";
}
