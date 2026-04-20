using Facebook.Yoga;
using static Facebook.Yoga.YGNodeStyleAPI;
using static Facebook.Yoga.YGNodeLayoutAPI;
using static Facebook.Yoga.YGNodeAPI;

namespace OpenTui;

/// <summary>
/// Wraps a Yoga layout node providing CSS Flexbox layout properties.
/// Each widget has an associated LayoutNode for computing position and size.
/// </summary>
public sealed class LayoutNode
{
    internal readonly Node YogaNode;

    /// <summary>
    /// Initializes a new instance of the LayoutNode class.
    /// </summary>
    public LayoutNode()
    {
        YogaNode = YGNodeNew();
    }

    // --- Flex container properties ---

    /// <summary>
    /// Gets or sets the flex direction.
    /// </summary>
    public FlexDirection FlexDirection
    {
        get => (FlexDirection)YGNodeStyleGetFlexDirection(YogaNode);
        set => YGNodeStyleSetFlexDirection(YogaNode, (YGFlexDirection)value);
    }

    /// <summary>
    /// Gets or sets the justify content.
    /// </summary>
    public Justify JustifyContent
    {
        get => (Justify)YGNodeStyleGetJustifyContent(YogaNode);
        set => YGNodeStyleSetJustifyContent(YogaNode, (YGJustify)value);
    }

    /// <summary>
    /// Gets or sets the align items.
    /// </summary>
    public Align AlignItems
    {
        get => (Align)YGNodeStyleGetAlignItems(YogaNode);
        set => YGNodeStyleSetAlignItems(YogaNode, (YGAlign)value);
    }

    /// <summary>
    /// Gets or sets the align self.
    /// </summary>
    public Align AlignSelf
    {
        get => (Align)YGNodeStyleGetAlignSelf(YogaNode);
        set => YGNodeStyleSetAlignSelf(YogaNode, (YGAlign)value);
    }

    /// <summary>
    /// Gets or sets the align content.
    /// </summary>
    public Align AlignContent
    {
        get => (Align)YGNodeStyleGetAlignContent(YogaNode);
        set => YGNodeStyleSetAlignContent(YogaNode, (YGAlign)value);
    }

    /// <summary>
    /// Gets or sets the wrap.
    /// </summary>
    public bool Wrap
    {
        get => YGNodeStyleGetFlexWrap(YogaNode) == YGWrap.Wrap;
        set => YGNodeStyleSetFlexWrap(YogaNode, value ? YGWrap.Wrap : YGWrap.NoWrap);
    }

    // --- Flex item properties ---

    /// <summary>
    /// Gets or sets the flex grow.
    /// </summary>
    public float FlexGrow
    {
        get => YGNodeStyleGetFlexGrow(YogaNode);
        set => YGNodeStyleSetFlexGrow(YogaNode, value);
    }

    /// <summary>
    /// Gets or sets the flex shrink.
    /// </summary>
    public float FlexShrink
    {
        get => YGNodeStyleGetFlexShrink(YogaNode);
        set => YGNodeStyleSetFlexShrink(YogaNode, value);
    }

    // --- Dimensions ---

    /// <summary>
    /// Gets or sets the width.
    /// </summary>
    public float Width
    {
        get => YGNodeStyleGetWidth(YogaNode).Value;
        set => YGNodeStyleSetWidth(YogaNode, value);
    }

    /// <summary>
    /// Gets or sets the height.
    /// </summary>
    public float Height
    {
        get => YGNodeStyleGetHeight(YogaNode).Value;
        set => YGNodeStyleSetHeight(YogaNode, value);
    }

    /// <summary>
    /// Gets or sets the min width.
    /// </summary>
    public float MinWidth
    {
        get => YGNodeStyleGetMinWidth(YogaNode).Value;
        set => YGNodeStyleSetMinWidth(YogaNode, value);
    }

    /// <summary>
    /// Gets or sets the min height.
    /// </summary>
    public float MinHeight
    {
        get => YGNodeStyleGetMinHeight(YogaNode).Value;
        set => YGNodeStyleSetMinHeight(YogaNode, value);
    }

    /// <summary>
    /// Gets or sets the max width.
    /// </summary>
    public float MaxWidth
    {
        get => YGNodeStyleGetMaxWidth(YogaNode).Value;
        set => YGNodeStyleSetMaxWidth(YogaNode, value);
    }

    /// <summary>
    /// Gets or sets the max height.
    /// </summary>
    public float MaxHeight
    {
        get => YGNodeStyleGetMaxHeight(YogaNode).Value;
        set => YGNodeStyleSetMaxHeight(YogaNode, value);
    }

    // --- Spacing: margin ---

    /// <summary>
    /// Gets or sets the margin left.
    /// </summary>
    public float MarginLeft { set => YGNodeStyleSetMargin(YogaNode, YGEdge.Left, value); }
    /// <summary>
    /// Gets or sets the margin top.
    /// </summary>
    public float MarginTop { set => YGNodeStyleSetMargin(YogaNode, YGEdge.Top, value); }
    /// <summary>
    /// Gets or sets the margin right.
    /// </summary>
    public float MarginRight { set => YGNodeStyleSetMargin(YogaNode, YGEdge.Right, value); }
    /// <summary>
    /// Gets or sets the margin bottom.
    /// </summary>
    public float MarginBottom { set => YGNodeStyleSetMargin(YogaNode, YGEdge.Bottom, value); }
    /// <summary>
    /// Gets or sets the margin.
    /// </summary>
    public float Margin { set => YGNodeStyleSetMargin(YogaNode, YGEdge.All, value); }

    // --- Spacing: padding ---

    /// <summary>
    /// Gets or sets the padding left.
    /// </summary>
    public float PaddingLeft { set => YGNodeStyleSetPadding(YogaNode, YGEdge.Left, value); }
    /// <summary>
    /// Gets or sets the padding top.
    /// </summary>
    public float PaddingTop { set => YGNodeStyleSetPadding(YogaNode, YGEdge.Top, value); }
    /// <summary>
    /// Gets or sets the padding right.
    /// </summary>
    public float PaddingRight { set => YGNodeStyleSetPadding(YogaNode, YGEdge.Right, value); }
    /// <summary>
    /// Gets or sets the padding bottom.
    /// </summary>
    public float PaddingBottom { set => YGNodeStyleSetPadding(YogaNode, YGEdge.Bottom, value); }
    /// <summary>
    /// Gets or sets the padding.
    /// </summary>
    public float Padding { set => YGNodeStyleSetPadding(YogaNode, YGEdge.All, value); }

    // --- Spacing: border ---

    /// <summary>
    /// Gets or sets the border left.
    /// </summary>
    public float BorderLeft { set => YGNodeStyleSetBorder(YogaNode, YGEdge.Left, value); }
    /// <summary>
    /// Gets or sets the border top.
    /// </summary>
    public float BorderTop { set => YGNodeStyleSetBorder(YogaNode, YGEdge.Top, value); }
    /// <summary>
    /// Gets or sets the border right.
    /// </summary>
    public float BorderRight { set => YGNodeStyleSetBorder(YogaNode, YGEdge.Right, value); }
    /// <summary>
    /// Gets or sets the border bottom.
    /// </summary>
    public float BorderBottom { set => YGNodeStyleSetBorder(YogaNode, YGEdge.Bottom, value); }
    /// <summary>
    /// Gets or sets the border.
    /// </summary>
    public float Border { set => YGNodeStyleSetBorder(YogaNode, YGEdge.All, value); }

    // --- Gap (row/column gaps) ---

    /// <summary>
    /// Gets or sets the gap.
    /// </summary>
    public float Gap { set => YGNodeStyleSetGap(YogaNode, YGGutter.All, value); }
    /// <summary>
    /// Gets or sets the row gap.
    /// </summary>
    public float RowGap { set => YGNodeStyleSetGap(YogaNode, YGGutter.Row, value); }
    /// <summary>
    /// Gets or sets the column gap.
    /// </summary>
    public float ColumnGap { set => YGNodeStyleSetGap(YogaNode, YGGutter.Column, value); }

    // --- Position ---

    /// <summary>
    /// Gets or sets the position type.
    /// </summary>
    public PositionType PositionType
    {
        get => (PositionType)YGNodeStyleGetPositionType(YogaNode);
        set => YGNodeStyleSetPositionType(YogaNode, (YGPositionType)value);
    }

    /// <summary>
    /// Gets or sets the overflow.
    /// </summary>
    public Overflow Overflow
    {
        get => (Overflow)YGNodeStyleGetOverflow(YogaNode);
        set => YGNodeStyleSetOverflow(YogaNode, (YGOverflow)value);
    }

    // --- Display ---

    /// <summary>
    /// Gets or sets the visible.
    /// </summary>
    public bool Visible
    {
        get => YGNodeStyleGetDisplay(YogaNode) != YGDisplay.None;
        set => YGNodeStyleSetDisplay(YogaNode, value ? YGDisplay.Flex : YGDisplay.None);
    }

    // --- Computed layout results (read-only after CalculateLayout) ---

    /// <summary>Computed X position relative to parent.</summary>
    public float LayoutX => YGNodeLayoutGetLeft(YogaNode);

    /// <summary>Computed Y position relative to parent.</summary>
    public float LayoutY => YGNodeLayoutGetTop(YogaNode);

    /// <summary>Computed width.</summary>
    public float LayoutWidth => YGNodeLayoutGetWidth(YogaNode);

    /// <summary>Computed height.</summary>
    public float LayoutHeight => YGNodeLayoutGetHeight(YogaNode);

    /// <summary>Gets the computed layout bounds as a ViewportBounds.</summary>
    public ViewportBounds LayoutBounds => new(
        (int)LayoutX, (int)LayoutY,
        (int)LayoutWidth, (int)LayoutHeight);

    // --- Child management ---

    internal void InsertChild(LayoutNode child, int index) =>
        YGNodeInsertChild(YogaNode, child.YogaNode, (nuint)index);

    internal void RemoveChild(LayoutNode child) =>
        YGNodeRemoveChild(YogaNode, child.YogaNode);

    internal int ChildCount => (int)YGNodeGetChildCount(YogaNode);

    /// <summary>Calculates layout for the entire tree from this node.</summary>
    public void CalculateLayout(float availableWidth = float.NaN, float availableHeight = float.NaN) =>
        YGNodeCalculateLayout(YogaNode, availableWidth, availableHeight, YGDirection.LTR);

    /// <summary>Marks this node's layout as dirty, requiring recalculation.</summary>
    public void MarkDirty() => YogaNode.MarkDirtyAndPropagate();
}
