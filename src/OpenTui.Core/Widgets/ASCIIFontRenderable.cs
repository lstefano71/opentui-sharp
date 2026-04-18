namespace OpenTui.Core;

/// <summary>
/// Options for ASCIIFont renderable.
/// Matches TypeScript ASCIIFontOptions.
/// </summary>
public class ASCIIFontOptions : FrameBufferOptions
{
    public string Text { get; init; } = "";
    public string Font { get; init; } = "tiny";
    public Rgba? Color { get; init; }
    public Rgba[]? Colors { get; init; }
    public Rgba? BackgroundColor { get; init; }
    public bool Selectable { get; init; } = true;
    public Rgba? SelectionBg { get; init; }
    public Rgba? SelectionFg { get; init; }
}

/// <summary>
/// Large ASCII art text rendering using block characters.
/// Extends FrameBufferRenderable to draw bitmap-style glyphs.
/// Matches TypeScript ASCIIFontRenderable from ASCIIFont.ts.
///
/// Font rendering uses a lookup table of block-character bitmaps
/// for each ASCII character. Each character maps to a grid of
/// Unicode block elements that approximate the glyph shape.
/// </summary>
public class ASCIIFontRenderable : FrameBufferRenderable
{
    private string _text;
    private string _font;
    private Rgba _color;
    private Rgba[] _colors;
    private bool _selectable;

    // ASCII block font definitions
    // Each font maps characters to 2D arrays of codepoints
    private static readonly Dictionary<string, Dictionary<char, string[]>> _fonts = new()
    {
        ["tiny"] = BuildTinyFont(),
        ["small"] = BuildSmallFont(),
    };

    public ASCIIFontRenderable(IRenderContext ctx, ASCIIFontOptions? options = null)
        : base(ctx, options ?? new ASCIIFontOptions())
    {
        options ??= new ASCIIFontOptions();
        _text = options.Text;
        _font = options.Font;
        _color = options.Color ?? Rgba.FromInts(255, 255, 255);
        _colors = options.Colors ?? [_color];
        _selectable = options.Selectable;

        var (w, h) = MeasureText(_text, _font);
        WidthDimension = DimensionValue.Point(w);
        HeightDimension = DimensionValue.Point(h);

        RenderFontToBuffer();
    }

    #region Properties

    public string Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value;
            var (w, h) = MeasureText(_text, _font);
            WidthDimension = DimensionValue.Point(w);
            HeightDimension = DimensionValue.Point(h);
            RenderFontToBuffer();
            RequestRender();
        }
    }

    public string Font
    {
        get => _font;
        set
        {
            if (_font == value) return;
            _font = value;
            var (w, h) = MeasureText(_text, _font);
            WidthDimension = DimensionValue.Point(w);
            HeightDimension = DimensionValue.Point(h);
            RenderFontToBuffer();
            RequestRender();
        }
    }

    public Rgba Color
    {
        get => _color;
        set
        {
            _color = value;
            _colors = [value];
            RenderFontToBuffer();
            RequestRender();
        }
    }

    #endregion

    #region Rendering

    private void RenderFontToBuffer()
    {
        var buf = Buffer;
        if (buf == null) return;

        Clear();

        if (!_fonts.TryGetValue(_font, out var fontDef))
            fontDef = _fonts["tiny"];

        int x = 0;
        for (int i = 0; i < _text.Length; i++)
        {
            char ch = _text[i];
            if (!fontDef.TryGetValue(ch, out var glyph))
            {
                // Unknown char — skip with 1 col gap
                x++;
                continue;
            }

            var color = _colors[i % _colors.Length];

            for (int row = 0; row < glyph.Length; row++)
            {
                var line = glyph[row];
                for (int col = 0; col < line.Length; col++)
                {
                    if (line[col] != ' ')
                    {
                        buf.SetCell((uint)(x + col), (uint)row,
                            line[col], color, Rgba.Transparent);
                    }
                }
            }

            x += (glyph.Length > 0 ? glyph[0].Length : 0) + 1; // +1 for spacing
        }
    }

    #endregion

    #region Font Metrics

    /// <summary>
    /// Measures the dimensions needed for the given text in the given font.
    /// </summary>
    public static (int Width, int Height) MeasureText(string text, string font)
    {
        if (string.IsNullOrEmpty(text)) return (0, 0);

        if (!_fonts.TryGetValue(font, out var fontDef))
            fontDef = _fonts["tiny"];

        int width = 0;
        int height = 0;

        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (fontDef.TryGetValue(ch, out var glyph) && glyph.Length > 0)
            {
                width += glyph[0].Length + (i < text.Length - 1 ? 1 : 0);
                height = Math.Max(height, glyph.Length);
            }
            else
            {
                width += 1 + (i < text.Length - 1 ? 1 : 0);
            }
        }

        return (width, height);
    }

    #endregion

    #region Font Definitions

    private static Dictionary<char, string[]> BuildTinyFont()
    {
        // Tiny 3-high font using Unicode block characters
        var font = new Dictionary<char, string[]>();
        font['A'] = ["▄▀▄", "█▀█", "▀ ▀"];
        font['B'] = ["█▀▄", "█▀▄", "▀▀ "];
        font['C'] = ["▄▀▀", "█  ", "▀▀▀"];
        font['D'] = ["█▀▄", "█ █", "▀▀ "];
        font['E'] = ["█▀▀", "█▀ ", "▀▀▀"];
        font['F'] = ["█▀▀", "█▀ ", "▀  "];
        font['G'] = ["▄▀▀", "█ █", "▀▀▀"];
        font['H'] = ["█ █", "█▀█", "▀ ▀"];
        font['I'] = ["▀█▀", " █ ", "▀█▀"];
        font['J'] = ["  █", "  █", "▀▀ "];
        font['K'] = ["█ █", "█▀▄", "▀ ▀"];
        font['L'] = ["█  ", "█  ", "▀▀▀"];
        font['M'] = ["█▄█", "█ █", "▀ ▀"];
        font['N'] = ["█▄█", "█ █", "▀ ▀"];
        font['O'] = ["▄▀▄", "█ █", "▀▀▀"];
        font['P'] = ["█▀▄", "█▀ ", "▀  "];
        font['Q'] = ["▄▀▄", "█ █", "▀▀▄"];
        font['R'] = ["█▀▄", "█▀▄", "▀ ▀"];
        font['S'] = ["▄▀▀", "▀▀▄", "▀▀ "];
        font['T'] = ["▀█▀", " █ ", " ▀ "];
        font['U'] = ["█ █", "█ █", "▀▀▀"];
        font['V'] = ["█ █", "█ █", " ▀ "];
        font['W'] = ["█ █", "█ █", "▀▄▀"];
        font['X'] = ["█ █", " ▀ ", "█ █"];
        font['Y'] = ["█ █", " ▀ ", " ▀ "];
        font['Z'] = ["▀▀█", " ▀ ", "█▀▀"];

        // Lowercase maps to uppercase
        for (char c = 'a'; c <= 'z'; c++)
        {
            if (font.TryGetValue(char.ToUpperInvariant(c), out var g))
                font[c] = g;
        }

        font['0'] = ["▄▀▄", "█ █", "▀▀▀"];
        font['1'] = ["▄█ ", " █ ", "▀█▀"];
        font['2'] = ["▀▀▄", " ▀ ", "▀▀▀"];
        font['3'] = ["▀▀▄", " ▀▄", "▀▀ "];
        font['4'] = ["█ █", "▀▀█", "  ▀"];
        font['5'] = ["█▀▀", "▀▀▄", "▀▀ "];
        font['6'] = ["▄▀▀", "█▀▄", "▀▀ "];
        font['7'] = ["▀▀█", "  █", "  ▀"];
        font['8'] = ["▄▀▄", "▄▀▄", "▀▀▀"];
        font['9'] = ["▄▀▄", "▀▀█", "▀▀ "];
        font[' '] = ["   ", "   ", "   "];
        font['!'] = [" █ ", " █ ", " ▀ "];
        font['?'] = ["▀▀▄", " ▀ ", " ▀ "];
        font['.'] = ["   ", "   ", " ▀ "];
        font[','] = ["   ", "   ", " ▄ "];
        font['-'] = ["   ", "▀▀▀", "   "];
        font[':'] = [" ▀ ", "   ", " ▀ "];
        font['/'] = ["  █", " █ ", "█  "];

        return font;
    }

    private static Dictionary<char, string[]> BuildSmallFont()
    {
        // Small 5-high font
        var font = new Dictionary<char, string[]>();
        font['A'] = [" ▄▄ ", "█  █", "████", "█  █", "▀  ▀"];
        font['B'] = ["███ ", "█  █", "███ ", "█  █", "███ "];
        font['C'] = [" ▄▄▄", "█   ", "█   ", "█   ", " ▀▀▀"];
        font['D'] = ["███ ", "█  █", "█  █", "█  █", "███ "];
        font['E'] = ["████", "█   ", "███ ", "█   ", "████"];
        font['F'] = ["████", "█   ", "███ ", "█   ", "▀   "];
        font['G'] = [" ▄▄▄", "█   ", "█ ██", "█  █", " ▀▀▀"];
        font['H'] = ["█  █", "█  █", "████", "█  █", "▀  ▀"];
        font['I'] = ["███", " █ ", " █ ", " █ ", "███"];
        font['J'] = ["   █", "   █", "   █", "█  █", " ▀▀ "];
        font['K'] = ["█  █", "█ █ ", "██  ", "█ █ ", "▀  ▀"];
        font['L'] = ["█   ", "█   ", "█   ", "█   ", "████"];
        font['M'] = ["█   █", "██ ██", "█ █ █", "█   █", "▀   ▀"];
        font['N'] = ["█   █", "██  █", "█ █ █", "█  ██", "▀   ▀"];
        font['O'] = [" ▄▄ ", "█  █", "█  █", "█  █", " ▀▀ "];
        font['P'] = ["███ ", "█  █", "███ ", "█   ", "▀   "];
        font['Q'] = [" ▄▄ ", "█  █", "█ ██", "█ █ ", " ▀▀▄"];
        font['R'] = ["███ ", "█  █", "███ ", "█ █ ", "▀  ▀"];
        font['S'] = [" ▄▄▄", "█   ", " ▀▀▄", "   █", "▀▀▀ "];
        font['T'] = ["█████", "  █  ", "  █  ", "  █  ", "  ▀  "];
        font['U'] = ["█  █", "█  █", "█  █", "█  █", " ▀▀ "];
        font['V'] = ["█   █", "█   █", " █ █ ", " █ █ ", "  ▀  "];
        font['W'] = ["█   █", "█   █", "█ █ █", "██ ██", "▀   ▀"];
        font['X'] = ["█   █", " █ █ ", "  █  ", " █ █ ", "▀   ▀"];
        font['Y'] = ["█   █", " █ █ ", "  █  ", "  █  ", "  ▀  "];
        font['Z'] = ["█████", "   █ ", "  █  ", " █   ", "█████"];

        for (char c = 'a'; c <= 'z'; c++)
        {
            if (font.TryGetValue(char.ToUpperInvariant(c), out var g))
                font[c] = g;
        }

        font['0'] = [" ▄▄ ", "█  █", "█  █", "█  █", " ▀▀ "];
        font['1'] = [" ▄█ ", "  █ ", "  █ ", "  █ ", " ▀█▀"];
        font['2'] = [" ▄▄ ", "   █", " ▄▄ ", "█   ", "▀▀▀▀"];
        font['3'] = ["▀▀▄ ", "   █", " ▀▄ ", "   █", "▀▀  "];
        font['4'] = ["█  █", "█  █", "▀▀▀█", "   █", "   ▀"];
        font['5'] = ["████", "█   ", "▀▀▄ ", "   █", "▀▀  "];
        font['6'] = [" ▄▄ ", "█   ", "█▀▄ ", "█  █", " ▀▀ "];
        font['7'] = ["▀▀▀█", "   █", "  █ ", "  █ ", "  ▀ "];
        font['8'] = [" ▄▄ ", "█  █", " ▄▄ ", "█  █", " ▀▀ "];
        font['9'] = [" ▄▄ ", "█  █", " ▀▀█", "   █", " ▀▀ "];
        font[' '] = ["    ", "    ", "    ", "    ", "    "];
        font['!'] = [" █ ", " █ ", " █ ", "   ", " ▀ "];
        font['?'] = [" ▄▄ ", "   █", " ▄▀ ", "    ", " ▀  "];
        font['.'] = ["  ", "  ", "  ", "  ", "▀ "];
        font['-'] = ["    ", "    ", "▀▀▀▀", "    ", "    "];
        font[':'] = [" ▀ ", "   ", " ▀ ", "   ", " ▀ "];

        return font;
    }

    #endregion
}
