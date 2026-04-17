namespace OpenTui;

/// <summary>
/// A container widget that can draw a border and hold child widgets.
/// The primary building block for TUI layouts.
/// </summary>
public class Box : Widget
{
    private BorderStyle? _border;

    /// <summary>Border style. Null means no border.</summary>
    public BorderStyle? Border
    {
        get => _border;
        set
        {
            _border = value;
            ApplyYogaBorders();
        }
    }

    /// <summary>Optional title displayed in the top border.</summary>
    public string? Title { get; set; }

    /// <summary>Title alignment within the border.</summary>
    public TitleAlignment TitleAlignment { get; set; } = TitleAlignment.Left;

    /// <summary>Border color override. Uses resolved foreground if null.</summary>
    public Rgba? BorderColor { get; set; }

    /// <summary>Whether to fill the box interior with the background color.</summary>
    public bool ShouldFill { get; set; } = true;

    private void ApplyYogaBorders()
    {
        float b = _border is not null ? 1 : 0;
        Layout.BorderLeft = b;
        Layout.BorderRight = b;
        Layout.BorderTop = b;
        Layout.BorderBottom = b;
    }

    /// <inheritdoc />
    protected internal override void Draw(NativeBuffer buffer, int offsetX, int offsetY)
    {
        int w = (int)Layout.LayoutWidth;
        int h = (int)Layout.LayoutHeight;

        if (w <= 0 || h <= 0) return;

        bool hasBorder = _border is not null;
        bool hasFill = ShouldFill && Bg is { A: > 0 };

        if (!hasBorder && !hasFill) return;

        var chars = hasBorder ? BorderCharacters.ForStyle(_border!.Value) : null;

        buffer.DrawBox(offsetX, offsetY, (uint)w, (uint)h,
            borderChars: chars,
            borderTop: hasBorder, borderRight: hasBorder,
            borderBottom: hasBorder, borderLeft: hasBorder,
            shouldFill: hasFill,
            borderColor: BorderColor ?? ResolvedFg,
            backgroundColor: Bg ?? Rgba.Transparent,
            title: Title,
            titleAlignment: TitleAlignment);
    }
}
