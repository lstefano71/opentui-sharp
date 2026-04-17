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

    public LayoutNode()
    {
        YogaNode = YGNodeNew();
    }

    // --- Flex container properties ---

    public FlexDirection FlexDirection
    {
        get => (FlexDirection)YGNodeStyleGetFlexDirection(YogaNode);
        set => YGNodeStyleSetFlexDirection(YogaNode, (YGFlexDirection)value);
    }

    public Justify JustifyContent
    {
        get => (Justify)YGNodeStyleGetJustifyContent(YogaNode);
        set => YGNodeStyleSetJustifyContent(YogaNode, (YGJustify)value);
    }

    public Align AlignItems
    {
        get => (Align)YGNodeStyleGetAlignItems(YogaNode);
        set => YGNodeStyleSetAlignItems(YogaNode, (YGAlign)value);
    }

    public Align AlignSelf
    {
        get => (Align)YGNodeStyleGetAlignSelf(YogaNode);
        set => YGNodeStyleSetAlignSelf(YogaNode, (YGAlign)value);
    }

    public Align AlignContent
    {
        get => (Align)YGNodeStyleGetAlignContent(YogaNode);
        set => YGNodeStyleSetAlignContent(YogaNode, (YGAlign)value);
    }

    public bool Wrap
    {
        get => YGNodeStyleGetFlexWrap(YogaNode) == YGWrap.Wrap;
        set => YGNodeStyleSetFlexWrap(YogaNode, value ? YGWrap.Wrap : YGWrap.NoWrap);
    }

    // --- Flex item properties ---

    public float FlexGrow
    {
        get => YGNodeStyleGetFlexGrow(YogaNode);
        set => YGNodeStyleSetFlexGrow(YogaNode, value);
    }

    public float FlexShrink
    {
        get => YGNodeStyleGetFlexShrink(YogaNode);
        set => YGNodeStyleSetFlexShrink(YogaNode, value);
    }

    // --- Dimensions ---

    public float Width
    {
        get => YGNodeStyleGetWidth(YogaNode).Value;
        set => YGNodeStyleSetWidth(YogaNode, value);
    }

    public float Height
    {
        get => YGNodeStyleGetHeight(YogaNode).Value;
        set => YGNodeStyleSetHeight(YogaNode, value);
    }

    public float MinWidth
    {
        get => YGNodeStyleGetMinWidth(YogaNode).Value;
        set => YGNodeStyleSetMinWidth(YogaNode, value);
    }

    public float MinHeight
    {
        get => YGNodeStyleGetMinHeight(YogaNode).Value;
        set => YGNodeStyleSetMinHeight(YogaNode, value);
    }

    public float MaxWidth
    {
        get => YGNodeStyleGetMaxWidth(YogaNode).Value;
        set => YGNodeStyleSetMaxWidth(YogaNode, value);
    }

    public float MaxHeight
    {
        get => YGNodeStyleGetMaxHeight(YogaNode).Value;
        set => YGNodeStyleSetMaxHeight(YogaNode, value);
    }

    // --- Spacing: margin ---

    public float MarginLeft { set => YGNodeStyleSetMargin(YogaNode, YGEdge.Left, value); }
    public float MarginTop { set => YGNodeStyleSetMargin(YogaNode, YGEdge.Top, value); }
    public float MarginRight { set => YGNodeStyleSetMargin(YogaNode, YGEdge.Right, value); }
    public float MarginBottom { set => YGNodeStyleSetMargin(YogaNode, YGEdge.Bottom, value); }
    public float Margin { set => YGNodeStyleSetMargin(YogaNode, YGEdge.All, value); }

    // --- Spacing: padding ---

    public float PaddingLeft { set => YGNodeStyleSetPadding(YogaNode, YGEdge.Left, value); }
    public float PaddingTop { set => YGNodeStyleSetPadding(YogaNode, YGEdge.Top, value); }
    public float PaddingRight { set => YGNodeStyleSetPadding(YogaNode, YGEdge.Right, value); }
    public float PaddingBottom { set => YGNodeStyleSetPadding(YogaNode, YGEdge.Bottom, value); }
    public float Padding { set => YGNodeStyleSetPadding(YogaNode, YGEdge.All, value); }

    // --- Spacing: border ---

    public float BorderLeft { set => YGNodeStyleSetBorder(YogaNode, YGEdge.Left, value); }
    public float BorderTop { set => YGNodeStyleSetBorder(YogaNode, YGEdge.Top, value); }
    public float BorderRight { set => YGNodeStyleSetBorder(YogaNode, YGEdge.Right, value); }
    public float BorderBottom { set => YGNodeStyleSetBorder(YogaNode, YGEdge.Bottom, value); }
    public float Border { set => YGNodeStyleSetBorder(YogaNode, YGEdge.All, value); }

    // --- Gap (row/column gaps) ---

    public float Gap { set => YGNodeStyleSetGap(YogaNode, YGGutter.All, value); }
    public float RowGap { set => YGNodeStyleSetGap(YogaNode, YGGutter.Row, value); }
    public float ColumnGap { set => YGNodeStyleSetGap(YogaNode, YGGutter.Column, value); }

    // --- Position ---

    public PositionType PositionType
    {
        get => (PositionType)YGNodeStyleGetPositionType(YogaNode);
        set => YGNodeStyleSetPositionType(YogaNode, (YGPositionType)value);
    }

    public Overflow Overflow
    {
        get => (Overflow)YGNodeStyleGetOverflow(YogaNode);
        set => YGNodeStyleSetOverflow(YogaNode, (YGOverflow)value);
    }

    // --- Display ---

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
