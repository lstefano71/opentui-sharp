namespace OpenTui.Core;

/// <summary>
/// A text part that can be either a plain string or a <see cref="StyledText"/>.
/// Used as input to <see cref="VStyles"/> composition functions.
/// Implicit conversions allow natural mixing: <c>Bold(Underline("hello"), " world")</c>.
/// </summary>
public readonly struct TextPart
{
    private readonly StyledText? _styled;
    private readonly string? _plain;

    private TextPart(StyledText styled) => (_styled, _plain) = (styled, null);
    private TextPart(string plain) => (_styled, _plain) = (null, plain);

    /// <summary>Implicitly wraps a plain string.</summary>
    public static implicit operator TextPart(string text) => new(text);

    /// <summary>Implicitly wraps a StyledText.</summary>
    public static implicit operator TextPart(StyledText styled) => new(styled);

    internal IReadOnlyList<TextChunk> GetChunks() =>
        _styled?.Chunks ?? (IReadOnlyList<TextChunk>)[TextChunk.Plain(_plain!)];
}

/// <summary>
/// Composable styled text factory functions matching the upstream <c>vstyles</c> API.
/// Functions return <see cref="StyledText"/> and can be nested for style composition.
/// Outer styles merge with inner styles: attributes are OR'd, fg/bg default to outer when not set by inner.
/// </summary>
/// <example>
/// <code>
/// using static OpenTui.Core.VStyles;
///
/// Text(Bold("Bold Text"));
/// Text(Bold(Underline("hello"), " world"));
/// Text(Color("#ff6b6b", Bold("Bold Red"), " normal"));
/// </code>
/// </example>
public static class VStyles
{
    // Attribute styles
    /// <summary>Applies bold to all parts.</summary>
    public static StyledText Bold(params TextPart[] parts) =>
        Apply(TextAttributes.Bold, null, null, parts);

    /// <summary>Applies italic to all parts.</summary>
    public static StyledText Italic(params TextPart[] parts) =>
        Apply(TextAttributes.Italic, null, null, parts);

    /// <summary>Applies underline to all parts.</summary>
    public static StyledText Underline(params TextPart[] parts) =>
        Apply(TextAttributes.Underline, null, null, parts);

    /// <summary>Applies dim to all parts.</summary>
    public static StyledText Dim(params TextPart[] parts) =>
        Apply(TextAttributes.Dim, null, null, parts);

    /// <summary>Applies blink to all parts.</summary>
    public static StyledText Blink(params TextPart[] parts) =>
        Apply(TextAttributes.Blink, null, null, parts);

    /// <summary>Applies inverse to all parts.</summary>
    public static StyledText Inverse(params TextPart[] parts) =>
        Apply(TextAttributes.Inverse, null, null, parts);

    /// <summary>Applies hidden to all parts.</summary>
    public static StyledText Hidden(params TextPart[] parts) =>
        Apply(TextAttributes.Hidden, null, null, parts);

    /// <summary>Applies strikethrough to all parts.</summary>
    public static StyledText Strikethrough(params TextPart[] parts) =>
        Apply(TextAttributes.Strikethrough, null, null, parts);

    // Combined attribute shortcuts
    /// <summary>Applies bold + italic.</summary>
    public static StyledText BoldItalic(params TextPart[] parts) =>
        Apply(TextAttributes.Bold | TextAttributes.Italic, null, null, parts);

    /// <summary>Applies bold + underline.</summary>
    public static StyledText BoldUnderline(params TextPart[] parts) =>
        Apply(TextAttributes.Bold | TextAttributes.Underline, null, null, parts);

    /// <summary>Applies italic + underline.</summary>
    public static StyledText ItalicUnderline(params TextPart[] parts) =>
        Apply(TextAttributes.Italic | TextAttributes.Underline, null, null, parts);

    /// <summary>Applies bold + italic + underline.</summary>
    public static StyledText BoldItalicUnderline(params TextPart[] parts) =>
        Apply(TextAttributes.Bold | TextAttributes.Italic | TextAttributes.Underline, null, null, parts);

    // Color styles
    /// <summary>Applies a foreground color (by name or hex string) to all parts.</summary>
    public static StyledText Color(string color, params TextPart[] parts) =>
        Apply(TextAttributes.None, Rgba.Parse(color), null, parts);

    /// <summary>Applies a foreground color to all parts.</summary>
    public static StyledText Color(Rgba color, params TextPart[] parts) =>
        Apply(TextAttributes.None, color, null, parts);

    /// <summary>Applies a background color (by name or hex string) to all parts.</summary>
    public static StyledText BgColor(string color, params TextPart[] parts) =>
        Apply(TextAttributes.None, null, Rgba.Parse(color), parts);

    /// <summary>Applies a background color to all parts.</summary>
    public static StyledText BgColor(Rgba color, params TextPart[] parts) =>
        Apply(TextAttributes.None, null, color, parts);

    /// <summary>Applies arbitrary attributes to all parts.</summary>
    public static StyledText Styled(TextAttributes attributes, params TextPart[] parts) =>
        Apply(attributes, null, null, parts);

    /// <summary>
    /// Core composition: merges the given style onto every chunk from every part.
    /// Attributes are OR'd. Fg/bg from the inner chunk takes precedence over outer.
    /// </summary>
    private static StyledText Apply(TextAttributes attrs, Rgba? fg, Rgba? bg, TextPart[] parts)
    {
        var chunks = new List<TextChunk>();
        foreach (var part in parts)
        {
            foreach (var chunk in part.GetChunks())
            {
                chunks.Add(new TextChunk
                {
                    Text = chunk.Text,
                    Fg = chunk.Fg ?? fg,
                    Bg = chunk.Bg ?? bg,
                    Attributes = chunk.Attributes | attrs,
                    Link = chunk.Link,
                });
            }
        }
        return new StyledText(chunks);
    }
}
