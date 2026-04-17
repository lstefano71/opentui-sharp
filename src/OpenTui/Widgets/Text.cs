namespace OpenTui;

/// <summary>
/// A widget that displays styled text content.
/// </summary>
public class Text : Widget
{
    private StyledText _content;

    /// <summary>Creates a Text widget from a plain string.</summary>
    public Text(string text) => _content = text;

    /// <summary>Creates a Text widget from styled text.</summary>
    public Text(StyledText content) => _content = content;

    /// <summary>The text content to display.</summary>
    public StyledText Content
    {
        get => _content;
        set => _content = value;
    }

    /// <summary>Text attributes (bold, italic, etc.).</summary>
    public TextAttribute Attributes { get; set; } = TextAttribute.None;

    /// <inheritdoc />
    protected internal override void Draw(NativeBuffer buffer, int offsetX, int offsetY)
    {
        var fg = ResolvedFg;
        var bg = ResolvedBg;
        int maxW = (int)Layout.LayoutWidth;
        int maxH = (int)Layout.LayoutHeight;
        if (maxW <= 0 || maxH <= 0) return;

        int y = 0;
        foreach (var chunk in _content.Chunks)
        {
            var chunkFg = chunk.Fg ?? fg;
            var chunkBg = chunk.Bg ?? bg;
            var chunkAttrs = chunk.Attributes | Attributes;

            var lines = chunk.Text.Split('\n');
            for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++)
            {
                if (lineIdx > 0) y++;
                if (y >= maxH) break;

                var line = lines[lineIdx];
                if (line.Length > 0)
                {
                    buffer.DrawText(line, (uint)offsetX, (uint)(offsetY + y), chunkFg, chunkBg, chunkAttrs);
                }
            }
            if (y >= maxH) break;
        }
    }
}
