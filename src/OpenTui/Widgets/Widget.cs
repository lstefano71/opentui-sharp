using System.Collections;
using System.Collections.ObjectModel;

namespace OpenTui;

/// <summary>
/// Base class for all OpenTUI widgets. Provides layout, child management,
/// and rendering infrastructure.
/// </summary>
public abstract class Widget : IEnumerable<Widget>
{
    private readonly List<Widget> _children = [];
    private Widget? _parent;

    /// <summary>Layout node controlling this widget's position and size.</summary>
    public LayoutNode Layout { get; } = new();

    /// <summary>The parent widget, or null if this is a root widget.</summary>
    public Widget? Parent
    {
        get => _parent;
        internal set => _parent = value;
    }

    /// <summary>Read-only collection of child widgets.</summary>
    public ReadOnlyCollection<Widget> Children => _children.AsReadOnly();

    /// <summary>Foreground color. Inherited from parent if null.</summary>
    public Rgba? Fg { get; set; }

    /// <summary>Background color. Inherited from parent if null.</summary>
    public Rgba? Bg { get; set; }

    /// <summary>Whether this widget is visible.</summary>
    public bool Visible
    {
        get => Layout.Visible;
        set => Layout.Visible = value;
    }

    // Layout shortcut properties

    /// <summary>
    /// Gets or sets the flex direction.
    /// </summary>
    public FlexDirection FlexDirection { get => Layout.FlexDirection; set => Layout.FlexDirection = value; }
    /// <summary>
    /// Gets or sets the justify content.
    /// </summary>
    public Justify JustifyContent { get => Layout.JustifyContent; set => Layout.JustifyContent = value; }
    /// <summary>
    /// Gets or sets the align items.
    /// </summary>
    public Align AlignItems { get => Layout.AlignItems; set => Layout.AlignItems = value; }
    /// <summary>
    /// Gets or sets the align self.
    /// </summary>
    public Align AlignSelf { get => Layout.AlignSelf; set => Layout.AlignSelf = value; }
    /// <summary>
    /// Gets or sets the flex grow.
    /// </summary>
    public float FlexGrow { get => Layout.FlexGrow; set => Layout.FlexGrow = value; }
    /// <summary>
    /// Gets or sets the flex shrink.
    /// </summary>
    public float FlexShrink { get => Layout.FlexShrink; set => Layout.FlexShrink = value; }
    /// <summary>
    /// Gets or sets the width.
    /// </summary>
    public float Width { set => Layout.Width = value; }
    /// <summary>
    /// Gets or sets the height.
    /// </summary>
    public float Height { set => Layout.Height = value; }
    /// <summary>
    /// Gets or sets the min width.
    /// </summary>
    public float MinWidth { set => Layout.MinWidth = value; }
    /// <summary>
    /// Gets or sets the min height.
    /// </summary>
    public float MinHeight { set => Layout.MinHeight = value; }
    /// <summary>
    /// Gets or sets the max width.
    /// </summary>
    public float MaxWidth { set => Layout.MaxWidth = value; }
    /// <summary>
    /// Gets or sets the max height.
    /// </summary>
    public float MaxHeight { set => Layout.MaxHeight = value; }
    /// <summary>
    /// Gets or sets the margin.
    /// </summary>
    public float Margin { set => Layout.Margin = value; }
    /// <summary>
    /// Gets or sets the padding.
    /// </summary>
    public float Padding { set => Layout.Padding = value; }
    /// <summary>
    /// Gets or sets the gap.
    /// </summary>
    public float Gap { set => Layout.Gap = value; }

    /// <summary>Adds a child widget.</summary>
    public void Add(Widget child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (child._parent is not null)
            throw new InvalidOperationException("Widget already has a parent. Remove it first.");

        child._parent = this;
        _children.Add(child);
        Layout.InsertChild(child.Layout, _children.Count - 1);
    }

    /// <summary>Removes a child widget.</summary>
    public bool Remove(Widget child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (!_children.Remove(child))
            return false;

        child._parent = null;
        Layout.RemoveChild(child.Layout);
        return true;
    }

    /// <summary>Removes all children.</summary>
    public void Clear()
    {
        foreach (var child in _children)
        {
            child._parent = null;
            Layout.RemoveChild(child.Layout);
        }
        _children.Clear();
    }

    /// <summary>
    /// Renders this widget to the given buffer at its computed layout position.
    /// Called by the rendering system after layout has been calculated.
    /// </summary>
    /// <param name="buffer">The native buffer to draw into.</param>
    /// <param name="offsetX">X offset from parent's absolute position.</param>
    /// <param name="offsetY">Y offset from parent's absolute position.</param>
    protected internal abstract void Draw(NativeBuffer buffer, int offsetX, int offsetY);

    /// <summary>
    /// Renders this widget and all its children recursively.
    /// </summary>
    internal void Render(NativeBuffer buffer, int parentX, int parentY)
    {
        if (!Visible) return;

        int absX = parentX + (int)Layout.LayoutX;
        int absY = parentY + (int)Layout.LayoutY;
        int w = (int)Layout.LayoutWidth;
        int h = (int)Layout.LayoutHeight;

        Draw(buffer, absX, absY);

        if (w > 0 && h > 0)
        {
            buffer.PushScissor(absX, absY, (uint)w, (uint)h);
            foreach (var child in _children)
                child.Render(buffer, absX, absY);
            buffer.PopScissor();
        }
    }

    /// <summary>Gets the resolved foreground color, inheriting from parent chain.</summary>
    public Rgba ResolvedFg => Fg ?? Parent?.ResolvedFg ?? Rgba.White;

    /// <summary>Gets the resolved background color, inheriting from parent chain.</summary>
    public Rgba ResolvedBg => Bg ?? Parent?.ResolvedBg ?? Rgba.Transparent;

    /// <inheritdoc />
    public IEnumerator<Widget> GetEnumerator() => _children.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
