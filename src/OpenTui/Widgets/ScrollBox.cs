namespace OpenTui;

/// <summary>
/// A scrollable container that clips its children to a viewport and
/// supports vertical and horizontal scrolling.
/// </summary>
public class ScrollBox : Widget
{
    /// <summary>Current vertical scroll offset in rows.</summary>
    public int ScrollY { get; set; }

    /// <summary>Current horizontal scroll offset in columns.</summary>
    public int ScrollX { get; set; }

    /// <summary>Whether to display the vertical scroll bar.</summary>
    public bool ShowVerticalBar { get; set; } = true;

    /// <summary>Whether to display the horizontal scroll bar.</summary>
    public bool ShowHorizontalBar { get; set; }

    /// <summary>
    /// The total height of the scrollable content, computed from children.
    /// </summary>
    public int ContentHeight
    {
        get
        {
            int max = 0;
            foreach (var child in Children)
            {
                int bottom = (int)(child.Layout.LayoutY + child.Layout.LayoutHeight);
                if (bottom > max) max = bottom;
            }
            return max;
        }
    }

    /// <summary>Visible viewport height based on this widget's layout.</summary>
    public int ViewportHeight => (int)Layout.LayoutHeight;

    /// <summary>Scrolls to an absolute vertical position, clamped to valid range.</summary>
    public void ScrollTo(int y)
    {
        int maxScroll = Math.Max(0, ContentHeight - ViewportHeight);
        ScrollY = Math.Clamp(y, 0, maxScroll);
    }

    /// <summary>Scrolls by a relative delta, clamped to valid range.</summary>
    public void ScrollBy(int delta) => ScrollTo(ScrollY + delta);

    /// <inheritdoc />
    protected internal override void Draw(nint buffer, int offsetX, int offsetY)
    {
        // TODO: wire to native buffer
    }
}
