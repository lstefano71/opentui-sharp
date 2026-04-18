namespace OpenTui.Core;

/// <summary>
/// A chunk of styled text: plain text with optional foreground, background, attributes, and link.
/// Matches the TypeScript TextChunk / StyledChunkStruct.
/// </summary>
public sealed class TextChunk
{
    public required string Text { get; init; }
    public Rgba? Fg { get; init; }
    public Rgba? Bg { get; init; }
    public TextAttributes Attributes { get; init; }
    public string? Link { get; init; }

    /// <summary>Create a plain (unstyled) chunk.</summary>
    public static TextChunk Plain(string text) => new() { Text = text, Attributes = TextAttributes.None };

    /// <summary>Create a styled chunk.</summary>
    public static TextChunk Styled(string text, Rgba? fg = null, Rgba? bg = null,
        TextAttributes attributes = TextAttributes.None, string? link = null) =>
        new() { Text = text, Fg = fg, Bg = bg, Attributes = attributes, Link = link };

    /// <summary>Returns a new chunk with additional attributes merged in.</summary>
    public TextChunk WithAttributes(TextAttributes extra) =>
        new() { Text = Text, Fg = Fg, Bg = Bg, Attributes = Attributes | extra, Link = Link };

    /// <summary>Returns a new chunk with the given foreground color.</summary>
    public TextChunk WithFg(Rgba fg) =>
        new() { Text = Text, Fg = fg, Bg = Bg, Attributes = Attributes, Link = Link };

    /// <summary>Returns a new chunk with the given background color.</summary>
    public TextChunk WithBg(Rgba bg) =>
        new() { Text = Text, Fg = Fg, Bg = bg, Attributes = Attributes, Link = Link };

    public override string ToString() => Text;
}

/// <summary>
/// Styled text composed of one or more TextChunks.
/// Matches the TypeScript StyledText class.
/// </summary>
public sealed class StyledText
{
    public IReadOnlyList<TextChunk> Chunks { get; }

    public StyledText(IReadOnlyList<TextChunk> chunks)
    {
        Chunks = chunks;
    }

    public StyledText(params TextChunk[] chunks)
    {
        Chunks = chunks;
    }

    /// <summary>Get the plain (unstyled) text by concatenating all chunks.</summary>
    public string PlainText => string.Concat(Chunks.Select(c => c.Text));

    /// <summary>Total character length across all chunks.</summary>
    public int Length => Chunks.Sum(c => c.Text.Length);

    /// <summary>Implicit conversion from string to plain StyledText.</summary>
    public static implicit operator StyledText(string text)
        => new(TextChunk.Plain(text));

    /// <summary>Concatenate two StyledTexts.</summary>
    public static StyledText operator +(StyledText a, StyledText b)
        => new(a.Chunks.Concat(b.Chunks).ToList());

    public override string ToString() => PlainText;
}

/// <summary>
/// Builder for creating styled text using a fluent API.
/// Analogous to the TypeScript `t` tagged template literal.
/// </summary>
public sealed class StyledTextBuilder
{
    private readonly List<TextChunk> _chunks = [];

    public StyledTextBuilder Add(string text)
    {
        _chunks.Add(TextChunk.Plain(text));
        return this;
    }

    public StyledTextBuilder Add(TextChunk chunk)
    {
        _chunks.Add(chunk);
        return this;
    }

    public StyledText Build() => new(_chunks.ToList());

    // Convenience style methods that create styled chunks inline

    public StyledTextBuilder Bold(string text)
    {
        _chunks.Add(TextChunk.Plain(text).WithAttributes(TextAttributes.Bold));
        return this;
    }

    public StyledTextBuilder Italic(string text)
    {
        _chunks.Add(TextChunk.Plain(text).WithAttributes(TextAttributes.Italic));
        return this;
    }

    public StyledTextBuilder Underline(string text)
    {
        _chunks.Add(TextChunk.Plain(text).WithAttributes(TextAttributes.Underline));
        return this;
    }

    public StyledTextBuilder Fg(string text, Rgba color)
    {
        _chunks.Add(TextChunk.Styled(text, fg: color));
        return this;
    }

    public StyledTextBuilder Bg(string text, Rgba color)
    {
        _chunks.Add(TextChunk.Styled(text, bg: color));
        return this;
    }

    public StyledTextBuilder Styled(string text, Rgba? fg = null, Rgba? bg = null,
        TextAttributes attributes = TextAttributes.None)
    {
        _chunks.Add(TextChunk.Styled(text, fg, bg, attributes));
        return this;
    }
}

/// <summary>
/// Static helper methods for creating styled text chunks, matching the
/// TypeScript named exports (bold, italic, fg, bg, red, green, etc.).
/// </summary>
public static class Style
{
    // Attribute styles
    public static TextChunk Bold(string text) => TextChunk.Plain(text).WithAttributes(TextAttributes.Bold);
    public static TextChunk Dim(string text) => TextChunk.Plain(text).WithAttributes(TextAttributes.Dim);
    public static TextChunk Italic(string text) => TextChunk.Plain(text).WithAttributes(TextAttributes.Italic);
    public static TextChunk Underline(string text) => TextChunk.Plain(text).WithAttributes(TextAttributes.Underline);
    public static TextChunk Strikethrough(string text) => TextChunk.Plain(text).WithAttributes(TextAttributes.Strikethrough);
    public static TextChunk Blink(string text) => TextChunk.Plain(text).WithAttributes(TextAttributes.Blink);
    public static TextChunk Reverse(string text) => TextChunk.Plain(text).WithAttributes(TextAttributes.Inverse);

    // Named foreground colors
    public static TextChunk Black(string text) => TextChunk.Styled(text, fg: Rgba.Parse("black"));
    public static TextChunk Red(string text) => TextChunk.Styled(text, fg: Rgba.Parse("red"));
    public static TextChunk Green(string text) => TextChunk.Styled(text, fg: Rgba.Parse("green"));
    public static TextChunk Yellow(string text) => TextChunk.Styled(text, fg: Rgba.Parse("yellow"));
    public static TextChunk Blue(string text) => TextChunk.Styled(text, fg: Rgba.Parse("blue"));
    public static TextChunk Magenta(string text) => TextChunk.Styled(text, fg: Rgba.Parse("magenta"));
    public static TextChunk Cyan(string text) => TextChunk.Styled(text, fg: Rgba.Parse("cyan"));
    public static TextChunk White(string text) => TextChunk.Styled(text, fg: Rgba.Parse("white"));

    // Custom color functions (curried like the TS `fg(color)(text)` pattern)
    public static Func<string, TextChunk> Fg(Rgba color) => text => TextChunk.Styled(text, fg: color);
    public static Func<string, TextChunk> Fg(string color) => text => TextChunk.Styled(text, fg: Rgba.Parse(color));
    public static Func<string, TextChunk> Bg(Rgba color) => text => TextChunk.Styled(text, bg: color);
    public static Func<string, TextChunk> Bg(string color) => text => TextChunk.Styled(text, bg: Rgba.Parse(color));

    // Link
    public static Func<string, TextChunk> Link(string url) =>
        text => TextChunk.Styled(text, link: url);
}
