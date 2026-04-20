namespace OpenTui.Core;

/// <summary>
/// A chunk of styled text: plain text with optional foreground, background, attributes, and link.
/// Matches the TypeScript TextChunk / StyledChunkStruct.
/// </summary>
public sealed class TextChunk
{
    /// <summary>
    /// Gets or sets the text.
    /// </summary>
    public required string Text { get; init; }
    /// <summary>
    /// Gets or sets the fg.
    /// </summary>
    public Rgba? Fg { get; init; }
    /// <summary>
    /// Gets or sets the bg.
    /// </summary>
    public Rgba? Bg { get; init; }
    /// <summary>
    /// Gets or sets the attributes.
    /// </summary>
    public TextAttributes Attributes { get; init; }
    /// <summary>
    /// Gets or sets the link.
    /// </summary>
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

    /// <inheritdoc />
    public override string ToString() => Text;
}

/// <summary>
/// Styled text composed of one or more TextChunks.
/// Matches the TypeScript StyledText class.
/// </summary>
public sealed class StyledText
{
    /// <summary>
    /// Gets the chunks.
    /// </summary>
    public IReadOnlyList<TextChunk> Chunks { get; }

    /// <summary>
    /// Initializes a new instance of the StyledText class.
    /// </summary>
    /// <param name="chunks">The chunks.</param>
    public StyledText(IReadOnlyList<TextChunk> chunks)
    {
        Chunks = chunks;
    }

    /// <summary>
    /// Initializes a new instance of the StyledText class.
    /// </summary>
    /// <param name="chunks">The chunks.</param>
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

    /// <inheritdoc />
    public override string ToString() => PlainText;
}

/// <summary>
/// Builder for creating styled text using a fluent API.
/// Analogous to the TypeScript `t` tagged template literal.
/// </summary>
public sealed class StyledTextBuilder
{
    private readonly List<TextChunk> _chunks = [];

    /// <summary>
    /// Performs add.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <returns>The result of add.</returns>
    public StyledTextBuilder Add(string text)
    {
        _chunks.Add(TextChunk.Plain(text));
        return this;
    }

    /// <summary>
    /// Performs add.
    /// </summary>
    /// <param name="chunk">The chunk.</param>
    /// <returns>The result of add.</returns>
    public StyledTextBuilder Add(TextChunk chunk)
    {
        _chunks.Add(chunk);
        return this;
    }

    /// <summary>
    /// Performs build.
    /// </summary>
    /// <returns>The result of build.</returns>
    public StyledText Build() => new(_chunks.ToList());

    // Convenience style methods that create styled chunks inline

    /// <summary>
    /// Performs bold.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <returns>The result of bold.</returns>
    public StyledTextBuilder Bold(string text)
    {
        _chunks.Add(TextChunk.Plain(text).WithAttributes(TextAttributes.Bold));
        return this;
    }

    /// <summary>
    /// Performs italic.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <returns>The result of italic.</returns>
    public StyledTextBuilder Italic(string text)
    {
        _chunks.Add(TextChunk.Plain(text).WithAttributes(TextAttributes.Italic));
        return this;
    }

    /// <summary>
    /// Performs underline.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <returns>The result of underline.</returns>
    public StyledTextBuilder Underline(string text)
    {
        _chunks.Add(TextChunk.Plain(text).WithAttributes(TextAttributes.Underline));
        return this;
    }

    /// <summary>
    /// Performs fg.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <param name="color">The color.</param>
    /// <returns>The result of fg.</returns>
    public StyledTextBuilder Fg(string text, Rgba color)
    {
        _chunks.Add(TextChunk.Styled(text, fg: color));
        return this;
    }

    /// <summary>
    /// Performs bg.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <param name="color">The color.</param>
    /// <returns>The result of bg.</returns>
    public StyledTextBuilder Bg(string text, Rgba color)
    {
        _chunks.Add(TextChunk.Styled(text, bg: color));
        return this;
    }

    /// <summary>
    /// Performs styled.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <param name="fg">The fg.</param>
    /// <param name="bg">The bg.</param>
    /// <param name="attributes">The attributes.</param>
    /// <returns>The result of styled.</returns>
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
    /// <summary>
    /// Performs bold.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <returns>The result of bold.</returns>
    public static TextChunk Bold(string text) => TextChunk.Plain(text).WithAttributes(TextAttributes.Bold);
    /// <summary>
    /// Performs dim.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <returns>The result of dim.</returns>
    public static TextChunk Dim(string text) => TextChunk.Plain(text).WithAttributes(TextAttributes.Dim);
    /// <summary>
    /// Performs italic.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <returns>The result of italic.</returns>
    public static TextChunk Italic(string text) => TextChunk.Plain(text).WithAttributes(TextAttributes.Italic);
    /// <summary>
    /// Performs underline.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <returns>The result of underline.</returns>
    public static TextChunk Underline(string text) => TextChunk.Plain(text).WithAttributes(TextAttributes.Underline);
    /// <summary>
    /// Performs strikethrough.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <returns>The result of strikethrough.</returns>
    public static TextChunk Strikethrough(string text) => TextChunk.Plain(text).WithAttributes(TextAttributes.Strikethrough);
    /// <summary>
    /// Performs blink.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <returns>The result of blink.</returns>
    public static TextChunk Blink(string text) => TextChunk.Plain(text).WithAttributes(TextAttributes.Blink);
    /// <summary>
    /// Performs reverse.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <returns>The result of reverse.</returns>
    public static TextChunk Reverse(string text) => TextChunk.Plain(text).WithAttributes(TextAttributes.Inverse);

    // Named foreground colors
    /// <summary>
    /// Performs black.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <returns>The result of black.</returns>
    public static TextChunk Black(string text) => TextChunk.Styled(text, fg: Rgba.Parse("black"));
    /// <summary>
    /// Performs red.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <returns>The result of red.</returns>
    public static TextChunk Red(string text) => TextChunk.Styled(text, fg: Rgba.Parse("red"));
    /// <summary>
    /// Performs green.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <returns>The result of green.</returns>
    public static TextChunk Green(string text) => TextChunk.Styled(text, fg: Rgba.Parse("green"));
    /// <summary>
    /// Performs yellow.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <returns>The result of yellow.</returns>
    public static TextChunk Yellow(string text) => TextChunk.Styled(text, fg: Rgba.Parse("yellow"));
    /// <summary>
    /// Performs blue.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <returns>The result of blue.</returns>
    public static TextChunk Blue(string text) => TextChunk.Styled(text, fg: Rgba.Parse("blue"));
    /// <summary>
    /// Performs magenta.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <returns>The result of magenta.</returns>
    public static TextChunk Magenta(string text) => TextChunk.Styled(text, fg: Rgba.Parse("magenta"));
    /// <summary>
    /// Performs cyan.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <returns>The result of cyan.</returns>
    public static TextChunk Cyan(string text) => TextChunk.Styled(text, fg: Rgba.Parse("cyan"));
    /// <summary>
    /// Performs white.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <returns>The result of white.</returns>
    public static TextChunk White(string text) => TextChunk.Styled(text, fg: Rgba.Parse("white"));

    // Custom color functions (curried like the TS `fg(color)(text)` pattern)
    /// <summary>
    /// Performs fg.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <returns>The result of fg.</returns>
    public static Func<string, TextChunk> Fg(Rgba color) => text => TextChunk.Styled(text, fg: color);
    /// <summary>
    /// Performs fg.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <returns>The result of fg.</returns>
    public static Func<string, TextChunk> Fg(string color) => text => TextChunk.Styled(text, fg: Rgba.Parse(color));
    /// <summary>
    /// Performs bg.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <returns>The result of bg.</returns>
    public static Func<string, TextChunk> Bg(Rgba color) => text => TextChunk.Styled(text, bg: color);
    /// <summary>
    /// Performs bg.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <returns>The result of bg.</returns>
    public static Func<string, TextChunk> Bg(string color) => text => TextChunk.Styled(text, bg: Rgba.Parse(color));

    // Link
    /// <summary>
    /// Performs link.
    /// </summary>
    /// <param name="url">The url.</param>
    /// <returns>The result of link.</returns>
    public static Func<string, TextChunk> Link(string url) =>
        text => TextChunk.Styled(text, link: url);
}
