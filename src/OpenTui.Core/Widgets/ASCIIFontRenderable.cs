using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace OpenTui.Core;

/// <summary>
/// Represents configuration options for Ascii Font Render.
/// </summary>
public sealed class AsciiFontRenderOptions
{
    /// <summary>
    /// Gets or sets the text.
    /// </summary>
    public required string Text { get; init; }
    /// <summary>
    /// Gets or sets the x.
    /// </summary>
    public int X { get; init; }
    /// <summary>
    /// Gets or sets the y.
    /// </summary>
    public int Y { get; init; }
    /// <summary>
    /// Gets or sets the font.
    /// </summary>
    public string Font { get; init; } = "tiny";
    /// <summary>
    /// Gets or sets the color.
    /// </summary>
    public Rgba? Color { get; init; }
    /// <summary>
    /// Gets or sets the colors.
    /// </summary>
    public Rgba[]? Colors { get; init; }
    /// <summary>
    /// Gets or sets the background color.
    /// </summary>
    public Rgba? BackgroundColor { get; init; }
}

/// <summary>
/// Represents an Ascii Font.
/// </summary>
public static partial class AsciiFont
{
    private const string DefaultFont = "tiny";
    private static readonly Regex s_colorTagRegex = ColorTagRegex();
    private static readonly Lazy<Dictionary<string, ParsedAsciiFontDefinition>> s_fonts = new(LoadFonts);
    private static readonly string[] s_embeddedFontNames = ["block", "grid", "huge", "pallet", "shade", "slick", "tiny"];

    /// <summary>
    /// Gets the font names.
    /// </summary>
    public static IReadOnlyCollection<string> FontNames => s_fonts.Value.Keys;

    /// <summary>
    /// Performs is font supported.
    /// </summary>
    /// <param name="font">The font.</param>
    /// <returns>true if is font supported; otherwise, false.</returns>
    public static bool IsFontSupported(string font) => s_fonts.Value.ContainsKey(NormalizeFont(font));

    /// <summary>
    /// Measures the text.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <param name="font">The font.</param>
    /// <returns>The result of measure text.</returns>
    public static (int Width, int Height) MeasureText(string text, string font = DefaultFont)
    {
        var fontDef = GetFont(font);
        int currentX = 0;

        for (int i = 0; i < text.Length; i++)
        {
            currentX += GetGlyphWidth(fontDef, text[i]);
            if (i < text.Length - 1)
                currentX += fontDef.LetterspaceSize;
        }

        return (currentX, fontDef.Lines);
    }

    /// <summary>
    /// Gets a character positions.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <param name="font">The font.</param>
    /// <returns>The character positions.</returns>
    public static int[] GetCharacterPositions(string text, string font = DefaultFont)
    {
        var fontDef = GetFont(font);
        var positions = new int[text.Length + 1];
        int currentX = 0;
        positions[0] = 0;

        for (int i = 0; i < text.Length; i++)
        {
            currentX += GetGlyphWidth(fontDef, text[i]);
            if (i < text.Length - 1)
                currentX += fontDef.LetterspaceSize;
            positions[i + 1] = currentX;
        }

        return positions;
    }

    /// <summary>
    /// Performs coordinate to character index.
    /// </summary>
    /// <param name="x">The horizontal position.</param>
    /// <param name="text">The text value.</param>
    /// <param name="font">The font.</param>
    /// <returns>The result of coordinate to character index.</returns>
    public static int CoordinateToCharacterIndex(int x, string text, string font = DefaultFont)
    {
        int[] positions = GetCharacterPositions(text, font);

        if (x < 0)
            return 0;

        for (int i = 0; i < positions.Length - 1; i++)
        {
            int currentPos = positions[i];
            int nextPos = positions[i + 1];

            if (x >= currentPos && x < nextPos)
            {
                double midpoint = currentPos + ((nextPos - currentPos) / 2.0);
                return x < midpoint ? i : i + 1;
            }
        }

        if (positions.Length > 0 && x >= positions[^1])
            return text.Length;

        return 0;
    }

    /// <summary>
    /// Renders the to buffer.
    /// </summary>
    /// <param name="buffer">The target buffer.</param>
    /// <param name="options">The configuration options.</param>
    /// <returns>The result of render to buffer.</returns>
    public static (int Width, int Height) RenderToBuffer(OptimizedBuffer buffer, AsciiFontRenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(options);

        var fontDef = GetFont(options.Font);
        var colors = GetColors(options);
        var backgroundColor = options.BackgroundColor ?? Rgba.Transparent;
        int height = (int)buffer.Height;
        int width = (int)buffer.Width;

        if (options.Y < 0 || options.Y + fontDef.Lines > height)
            return (0, fontDef.Lines);

        int currentX = options.X;
        int startX = options.X;

        for (int i = 0; i < options.Text.Length; i++)
        {
            char key = char.ToUpperInvariant(options.Text[i]);
            if (!fontDef.Chars.TryGetValue(key, out var glyph))
            {
                currentX += GetSpaceWidth(fontDef);
                continue;
            }

            int glyphWidth = GetGlyphWidth(glyph, fontDef);
            if (currentX >= width)
                break;

            if (currentX + glyphWidth < 0)
            {
                currentX += glyphWidth + fontDef.LetterspaceSize;
                continue;
            }

            for (int lineIndex = 0; lineIndex < fontDef.Lines && lineIndex < glyph.Length; lineIndex++)
            {
                int renderY = options.Y + lineIndex;
                if (renderY < 0 || renderY >= height)
                    continue;

                int segmentX = currentX;
                foreach (var segment in glyph[lineIndex])
                {
                    Rgba segmentColor = colors[Math.Min(segment.ColorIndex, colors.Length - 1)];
                    for (int charIndex = 0; charIndex < segment.Text.Length; charIndex++)
                    {
                        int renderX = segmentX + charIndex;
                        if (renderX >= 0 && renderX < width)
                        {
                            char fontChar = segment.Text[charIndex];
                            if (fontChar != ' ')
                            {
                                buffer.SetCellWithAlphaBlending(
                                    (uint)renderX,
                                    (uint)renderY,
                                    fontChar,
                                    segmentColor,
                                    backgroundColor);
                            }
                        }
                    }

                    segmentX += segment.Text.Length;
                }
            }

            currentX += glyphWidth;
            if (i < options.Text.Length - 1)
                currentX += fontDef.LetterspaceSize;
        }

        return (currentX - startX, fontDef.Lines);
    }

    private static Rgba[] GetColors(AsciiFontRenderOptions options)
    {
        if (options.Colors is { Length: > 0 })
            return options.Colors;
        if (options.Color is { } color)
            return [color];
        return [Rgba.White];
    }

    private static ParsedAsciiFontDefinition GetFont(string font)
    {
        string fontKey = NormalizeFont(font);
        if (s_fonts.Value.TryGetValue(fontKey, out var parsed))
            return parsed;

        return s_fonts.Value[DefaultFont];
    }

    private static string NormalizeFont(string font) =>
        string.IsNullOrWhiteSpace(font) ? DefaultFont : font.Trim().ToLowerInvariant();

    private static int GetSpaceWidth(ParsedAsciiFontDefinition fontDef)
    {
        if (fontDef.Chars.TryGetValue(' ', out var spaceGlyph))
            return GetGlyphWidth(spaceGlyph, fontDef);

        return 1;
    }

    private static int GetGlyphWidth(ParsedAsciiFontDefinition fontDef, char value)
    {
        char key = char.ToUpperInvariant(value);
        if (fontDef.Chars.TryGetValue(key, out var glyph))
            return GetGlyphWidth(glyph, fontDef);

        return GetSpaceWidth(fontDef);
    }

    private static int GetGlyphWidth(FontSegment[][] glyph, ParsedAsciiFontDefinition fontDef)
    {
        if (glyph.Length == 0)
            return fontDef.LetterspaceSize > 0 ? fontDef.LetterspaceSize : 1;

        int width = 0;
        foreach (var segment in glyph[0])
            width += segment.Text.Length;

        return width;
    }

    private static Dictionary<string, ParsedAsciiFontDefinition> LoadFonts()
    {
        var fonts = new Dictionary<string, ParsedAsciiFontDefinition>(StringComparer.OrdinalIgnoreCase);
        var assembly = typeof(ASCIIFontRenderable).Assembly;

        foreach (string fontName in s_embeddedFontNames)
        {
            string resourceName = $"OpenTui.Core.Widgets.Fonts.{fontName}.json";
            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
                throw new InvalidOperationException($"Missing embedded ASCII font resource '{resourceName}'.");

            var definition = JsonSerializer.Deserialize(stream, AsciiFontJsonContext.Default.AsciiFontResourceDefinition)
                ?? throw new InvalidOperationException($"Failed to deserialize embedded font '{resourceName}'.");

            var parsedChars = new Dictionary<char, FontSegment[][]>();
            foreach (var kvp in definition.Chars)
            {
                if (string.IsNullOrEmpty(kvp.Key))
                    continue;

                parsedChars[kvp.Key[0]] = kvp.Value.Select(ParseColorTags).ToArray();
            }

            fonts[fontName] = new ParsedAsciiFontDefinition(
                definition.Name,
                definition.Lines,
                definition.LetterspaceSize,
                definition.Colors ?? 1,
                parsedChars);
        }

        if (!fonts.ContainsKey(DefaultFont))
            throw new InvalidOperationException("The embedded ASCII font set is missing the required tiny font.");

        return fonts;
    }

    private static FontSegment[] ParseColorTags(string line)
    {
        var segments = new List<FontSegment>();
        int lastIndex = 0;

        foreach (Match match in s_colorTagRegex.Matches(line))
        {
            if (match.Index > lastIndex)
            {
                string plainText = line[lastIndex..match.Index];
                if (plainText.Length > 0)
                    segments.Add(new FontSegment(plainText, 0));
            }

            int colorIndex = Math.Max(0, int.Parse(match.Groups[1].Value) - 1);
            string taggedText = match.Groups[2].Value;
            if (taggedText.Length > 0)
                segments.Add(new FontSegment(taggedText, colorIndex));

            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < line.Length)
        {
            string remainingText = line[lastIndex..];
            if (remainingText.Length > 0)
                segments.Add(new FontSegment(remainingText, 0));
        }

        return [.. segments];
    }

    [GeneratedRegex("<c(\\d+)>(.*?)</c\\d+>", RegexOptions.Compiled)]
    private static partial Regex ColorTagRegex();

    private sealed record FontSegment(string Text, int ColorIndex);

    private sealed record ParsedAsciiFontDefinition(
        string Name,
        int Lines,
        int LetterspaceSize,
        int Colors,
        Dictionary<char, FontSegment[][]> Chars);

    internal sealed class AsciiFontResourceDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = "";

        [JsonPropertyName("lines")]
        public int Lines { get; init; }

        [JsonPropertyName("letterspace_size")]
        public int LetterspaceSize { get; init; }

        [JsonPropertyName("colors")]
        public int? Colors { get; init; }

        [JsonPropertyName("chars")]
        public Dictionary<string, string[]> Chars { get; init; } = [];
    }
}

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(AsciiFont.AsciiFontResourceDefinition))]
internal partial class AsciiFontJsonContext : JsonSerializerContext;

/// <summary>
/// Options for ASCIIFont renderable.
/// Matches TypeScript ASCIIFontOptions.
/// </summary>
public class ASCIIFontOptions : FrameBufferOptions
{
    /// <summary>
    /// Gets or sets the text.
    /// </summary>
    public string Text { get; init; } = "";
    /// <summary>
    /// Gets or sets the font.
    /// </summary>
    public string Font { get; init; } = "tiny";
    /// <summary>
    /// Gets or sets the color.
    /// </summary>
    public Rgba? Color { get; init; }
    /// <summary>
    /// Gets or sets the colors.
    /// </summary>
    public Rgba[]? Colors { get; init; }
    /// <summary>
    /// Gets or sets the selectable.
    /// </summary>
    public bool Selectable { get; init; } = true;
    /// <summary>
    /// Gets or sets the selection bg.
    /// </summary>
    public Rgba? SelectionBg { get; init; }
    /// <summary>
    /// Gets or sets the selection fg.
    /// </summary>
    public Rgba? SelectionFg { get; init; }
}

/// <summary>
/// ASCII art text renderable backed by the upstream cfonts JSON font catalog.
/// </summary>
public class ASCIIFontRenderable : FrameBufferRenderable
{
    private string _text;
    private string _font;
    private Rgba[] _colors;
    private bool _selectable;
    private Rgba? _selectionBg;
    private Rgba? _selectionFg;
    private LocalSelectionBounds? _lastLocalSelection;
    private readonly AsciiFontSelectionHelper _selectionHelper;

    /// <summary>
    /// Initializes a new instance of the ASCIIFontRenderable class.
    /// </summary>
    /// <param name="ctx">The render context.</param>
    /// <param name="options">The configuration options.</param>
    public ASCIIFontRenderable(IRenderContext ctx, ASCIIFontOptions? options = null)
        : base(ctx, options ?? new ASCIIFontOptions())
    {
        options ??= new ASCIIFontOptions();
        _text = options.Text;
        _font = options.Font;
        _colors = options.Colors is { Length: > 0 } ? options.Colors : [options.Color ?? Rgba.White];
        _selectable = options.Selectable;
        _selectionBg = options.SelectionBg;
        _selectionFg = options.SelectionFg;
        _selectionHelper = new AsciiFontSelectionHelper(() => _text, () => _font);

        base.Selectable = options.Selectable;
        FlexShrink = 0;
        UpdateDimensions();
    }

    /// <summary>
    /// Gets or sets the text.
    /// </summary>
    public string Text
    {
        get => _text;
        set
        {
            if (_text == value)
                return;

            _text = value;
            UpdateDimensions();
            RefreshSelection();
            RenderFontToBuffer();
            RequestRender();
        }
    }

    /// <summary>
    /// Gets or sets the font.
    /// </summary>
    public string Font
    {
        get => _font;
        set
        {
            if (_font == value)
                return;

            _font = value;
            UpdateDimensions();
            RefreshSelection();
            RenderFontToBuffer();
            RequestRender();
        }
    }

    /// <summary>
    /// Gets or sets the color.
    /// </summary>
    public Rgba Color
    {
        get => _colors[0];
        set
        {
            _colors = [value];
            RenderFontToBuffer();
            RequestRender();
        }
    }

    /// <summary>
    /// Gets or sets the colors.
    /// </summary>
    public Rgba[] Colors
    {
        get => _colors;
        set
        {
            _colors = value is { Length: > 0 } ? value : [Rgba.White];
            RenderFontToBuffer();
            RequestRender();
        }
    }

    /// <summary>
    /// Gets or sets the background color.
    /// </summary>
    public new Rgba BackgroundColor
    {
        get => base.BackgroundColor;
        set
        {
            if (base.BackgroundColor == value)
                return;

            base.BackgroundColor = value;
            RenderFontToBuffer();
            RequestRender();
        }
    }

    /// <summary>
    /// Gets or sets the selection bg.
    /// </summary>
    public Rgba? SelectionBg
    {
        get => _selectionBg;
        set
        {
            _selectionBg = value;
            RenderFontToBuffer();
            RequestRender();
        }
    }

    /// <summary>
    /// Gets or sets the selection fg.
    /// </summary>
    public Rgba? SelectionFg
    {
        get => _selectionFg;
        set
        {
            _selectionFg = value;
            RenderFontToBuffer();
            RequestRender();
        }
    }

    /// <summary>
    /// Gets or sets the selectable.
    /// </summary>
    public new bool Selectable
    {
        get => _selectable;
        set
        {
            _selectable = value;
            base.Selectable = value;
        }
    }

    /// <summary>
    /// Measures the text.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <param name="font">The font.</param>
    /// <returns>The result of measure text.</returns>
    public static (int Width, int Height) MeasureText(string text, string font) =>
        AsciiFont.MeasureText(text, font);

    /// <summary>
    /// Gets a character positions.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <param name="font">The font.</param>
    /// <returns>The character positions.</returns>
    public static int[] GetCharacterPositions(string text, string font) =>
        AsciiFont.GetCharacterPositions(text, font);

    /// <summary>
    /// Performs coordinate to character index.
    /// </summary>
    /// <param name="x">The horizontal position.</param>
    /// <param name="text">The text value.</param>
    /// <param name="font">The font.</param>
    /// <returns>The result of coordinate to character index.</returns>
    public static int CoordinateToCharacterIndex(int x, string text, string font) =>
        AsciiFont.CoordinateToCharacterIndex(x, text, font);

    /// <inheritdoc />
    public override bool ShouldStartSelection(int x, int y)
    {
        int localX = x - X;
        int localY = y - Y;
        return _selectionHelper.ShouldStartSelection(localX, localY, Width, Height);
    }

    /// <inheritdoc />
    public override bool OnSelectionChanged(Selection? selection)
    {
        LocalSelectionBounds? localSelection = SelectionHelpers.ConvertGlobalToLocalSelection(selection, X, Y);
        _lastLocalSelection = localSelection;
        bool changed = _selectionHelper.OnLocalSelectionChanged(localSelection, Width, Height);
        if (changed)
        {
            RenderFontToBuffer();
            RequestRender();
        }

        return HasSelection();
    }

    /// <inheritdoc />
    public override string GetSelectedText()
    {
        var selection = _selectionHelper.GetSelection();
        if (selection is null)
            return "";

        return _text[selection.Value.Start..selection.Value.End];
    }

    /// <inheritdoc />
    public override bool HasSelection() => _selectionHelper.HasSelection();

    /// <inheritdoc />
    protected override void OnResize(int width, int height)
    {
        base.OnResize(width, height);
        RenderFontToBuffer();
    }

    private void UpdateDimensions()
    {
        var measurements = AsciiFont.MeasureText(_text, _font);
        WidthDimension = DimensionValue.Point(Math.Max(1, measurements.Width));
        HeightDimension = DimensionValue.Point(Math.Max(1, measurements.Height));
    }

    private void RefreshSelection()
    {
        if (_lastLocalSelection.HasValue)
            _selectionHelper.OnLocalSelectionChanged(_lastLocalSelection, Width, Height);
    }

    private void RenderFontToBuffer()
    {
        if (IsDestroyed)
            return;

        var buffer = Buffer;
        if (buffer is null)
            return;

        buffer.Clear(base.BackgroundColor);
        AsciiFont.RenderToBuffer(buffer, new AsciiFontRenderOptions
        {
            Text = _text,
            Font = _font,
            Colors = _colors,
            BackgroundColor = base.BackgroundColor,
        });

        var selection = _selectionHelper.GetSelection();
        if (selection.HasValue && (_selectionBg.HasValue || _selectionFg.HasValue))
            RenderSelectionHighlight(buffer, selection.Value);
    }

    private void RenderSelectionHighlight(OptimizedBuffer buffer, (int Start, int End) selection)
    {
        string selectedText = _text[selection.Start..selection.End];
        if (selectedText.Length == 0)
            return;

        int[] positions = AsciiFont.GetCharacterPositions(_text, _font);
        int startX = positions[selection.Start];
        int endX = selection.End < positions.Length
            ? positions[selection.End]
            : AsciiFont.MeasureText(_text, _font).Width;

        if (_selectionBg.HasValue)
        {
            buffer.FillRect(
                (uint)startX,
                0,
                (uint)Math.Max(0, endX - startX),
                (uint)Math.Max(1, Height),
                _selectionBg.Value);
        }

        AsciiFont.RenderToBuffer(buffer, new AsciiFontRenderOptions
        {
            Text = selectedText,
            X = startX,
            Font = _font,
            Colors = _selectionFg.HasValue ? [_selectionFg.Value] : _colors,
            BackgroundColor = _selectionBg ?? base.BackgroundColor,
        });
    }

    private sealed class AsciiFontSelectionHelper(Func<string> getText, Func<string> getFont)
    {
        private (int Start, int End)? _localSelection;

        public bool HasSelection() => _localSelection.HasValue;

        public (int Start, int End)? GetSelection() => _localSelection;

        public bool ShouldStartSelection(int localX, int localY, int width, int height)
        {
            if (localX < 0 || localX >= width || localY < 0 || localY >= height)
                return false;

            int charIndex = AsciiFont.CoordinateToCharacterIndex(localX, getText(), getFont());
            return charIndex >= 0 && charIndex <= getText().Length;
        }

        public bool OnLocalSelectionChanged(LocalSelectionBounds? localSelection, int width, int height)
        {
            var previousSelection = _localSelection;

            if (localSelection is not { IsActive: true })
            {
                _localSelection = null;
                return previousSelection.HasValue;
            }

            string text = getText();
            string font = getFont();
            int startX = localSelection.Value.AnchorX;
            int startY = localSelection.Value.AnchorY;
            int endX = localSelection.Value.FocusX;
            int endY = localSelection.Value.FocusY;

            if (height - 1 < startY || 0 > endY)
            {
                _localSelection = null;
                return previousSelection.HasValue;
            }

            int startCharIndex = 0;
            int endCharIndex = text.Length;

            if (startY > height - 1)
            {
                _localSelection = null;
                return previousSelection.HasValue;
            }

            if (startY >= 0 && startY <= height - 1 && startX > 0)
                startCharIndex = AsciiFont.CoordinateToCharacterIndex(startX, text, font);

            if (endY < 0)
            {
                _localSelection = null;
                return previousSelection.HasValue;
            }

            if (endY >= 0 && endY <= height - 1)
                endCharIndex = endX >= 0 ? AsciiFont.CoordinateToCharacterIndex(endX, text, font) : 0;

            if (startCharIndex < endCharIndex && startCharIndex >= 0 && endCharIndex <= text.Length)
                _localSelection = (startCharIndex, endCharIndex);
            else
                _localSelection = null;

            return previousSelection?.Start != _localSelection?.Start || previousSelection?.End != _localSelection?.End;
        }
    }
}
