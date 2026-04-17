namespace OpenTui;

/// <summary>A segment of styled text with optional formatting.</summary>
public sealed record StyledChunk(
    string Text,
    Rgba? Fg = null,
    Rgba? Bg = null,
    TextAttribute Attributes = TextAttribute.None,
    string? Link = null);
