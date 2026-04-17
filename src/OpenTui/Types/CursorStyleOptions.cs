namespace OpenTui;

/// <summary>Options for configuring cursor appearance.</summary>
public sealed record CursorStyleOptions
{
    /// <summary>The cursor visual style, or null to leave unchanged.</summary>
    public CursorStyle? Style { get; init; }

    /// <summary>Whether the cursor blinks, or null to leave unchanged.</summary>
    public bool? Blinking { get; init; }

    /// <summary>The cursor color, or null to leave unchanged.</summary>
    public Rgba? Color { get; init; }

    /// <summary>The mouse pointer style, or null to leave unchanged.</summary>
    public MousePointerStyle? Cursor { get; init; }
}
