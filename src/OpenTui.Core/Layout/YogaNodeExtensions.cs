using Facebook.Yoga;

namespace OpenTui.Core;

/// <summary>
/// Helpers for applying LayoutOptions to a Yoga Node.
/// Port of TypeScript setupYogaProperties and individual property setters.
/// </summary>
public static class YogaNodeExtensions
{
    /// <summary>Creates a new Yoga node with the shared config.</summary>
    public static Node CreateLayoutNode()
    {
        return new Node(LayoutConfig.Shared);
    }

    /// <summary>Applies all layout options to a Yoga node (initial setup).</summary>
    public static void ApplyLayoutOptions(this Node node, LayoutOptions options)
    {
        if (options.FlexBasis is { } basis)
            node.SetDimension(YGNodeStyleAPI.YGNodeStyleSetFlexBasis, YGNodeStyleAPI.YGNodeStyleSetFlexBasisPercent, YGNodeStyleAPI.YGNodeStyleSetFlexBasisAuto, basis);

        if (options.MinWidth is { } minW)
            node.SetSizeConstraint(YGNodeStyleAPI.YGNodeStyleSetMinWidth, YGNodeStyleAPI.YGNodeStyleSetMinWidthPercent, minW);
        if (options.MinHeight is { } minH)
            node.SetSizeConstraint(YGNodeStyleAPI.YGNodeStyleSetMinHeight, YGNodeStyleAPI.YGNodeStyleSetMinHeightPercent, minH);
        if (options.MaxWidth is { } maxW)
            node.SetSizeConstraint(YGNodeStyleAPI.YGNodeStyleSetMaxWidth, YGNodeStyleAPI.YGNodeStyleSetMaxWidthPercent, maxW);
        if (options.MaxHeight is { } maxH)
            node.SetSizeConstraint(YGNodeStyleAPI.YGNodeStyleSetMaxHeight, YGNodeStyleAPI.YGNodeStyleSetMaxHeightPercent, maxH);

        // FlexGrow defaults to 0
        YGNodeStyleAPI.YGNodeStyleSetFlexGrow(node, options.FlexGrow ?? 0f);

        // FlexShrink: 0 if explicit width/height, else 1
        bool hasExplicitSize = (options.Width is { IsPoint: true }) || (options.Height is { IsPoint: true });
        YGNodeStyleAPI.YGNodeStyleSetFlexShrink(node, options.FlexShrink ?? (hasExplicitSize ? 0f : 1f));

        YGNodeStyleAPI.YGNodeStyleSetFlexDirection(node, (options.FlexDirection ?? FlexDirectionValue.Column).ToYoga());
        YGNodeStyleAPI.YGNodeStyleSetFlexWrap(node, (options.FlexWrap ?? WrapValue.NoWrap).ToYoga());
        YGNodeStyleAPI.YGNodeStyleSetAlignItems(node, (options.AlignItems ?? AlignValue.Stretch).ToYogaAlign());
        YGNodeStyleAPI.YGNodeStyleSetJustifyContent(node, (options.JustifyContent ?? JustifyValue.FlexStart).ToYoga());
        YGNodeStyleAPI.YGNodeStyleSetAlignSelf(node, (options.AlignSelf ?? AlignValue.Auto).ToYogaAlign());

        if (options.Width is { } w)
            node.SetDimension(YGNodeStyleAPI.YGNodeStyleSetWidth, YGNodeStyleAPI.YGNodeStyleSetWidthPercent, YGNodeStyleAPI.YGNodeStyleSetWidthAuto, w);
        if (options.Height is { } h)
            node.SetDimension(YGNodeStyleAPI.YGNodeStyleSetHeight, YGNodeStyleAPI.YGNodeStyleSetHeightPercent, YGNodeStyleAPI.YGNodeStyleSetHeightAuto, h);

        if (options.Position is { } pos)
            YGNodeStyleAPI.YGNodeStyleSetPositionType(node, pos.ToYoga());
        if (options.Overflow is { } ovf)
            YGNodeStyleAPI.YGNodeStyleSetOverflow(node, ovf.ToYoga());

        // Position edges
        if (options.Top is { } top) node.SetPosition(YGEdge.Top, top);
        if (options.Right is { } right) node.SetPosition(YGEdge.Right, right);
        if (options.Bottom is { } bottom) node.SetPosition(YGEdge.Bottom, bottom);
        if (options.Left is { } left) node.SetPosition(YGEdge.Left, left);

        // Margin
        ApplyEdgeValues(node, options.Margin, options.MarginX, options.MarginY,
            options.MarginTop, options.MarginRight, options.MarginBottom, options.MarginLeft,
            SetMargin);

        // Padding
        ApplyEdgeValues(node, options.Padding, options.PaddingX, options.PaddingY,
            options.PaddingTop, options.PaddingRight, options.PaddingBottom, options.PaddingLeft,
            SetPadding);

        // Gap
        if (options.Gap is { } gap) YGNodeStyleAPI.YGNodeStyleSetGap(node, YGGutter.All, gap);
        if (options.RowGap is { } rowGap) YGNodeStyleAPI.YGNodeStyleSetGap(node, YGGutter.Row, rowGap);
        if (options.ColumnGap is { } colGap) YGNodeStyleAPI.YGNodeStyleSetGap(node, YGGutter.Column, colGap);
    }

    /// <summary>Reads the computed layout from a Yoga node after calculateLayout.</summary>
    public static ComputedLayout GetComputedLayout(this Node node)
    {
        return new ComputedLayout(
            YGNodeLayoutAPI.YGNodeLayoutGetLeft(node),
            YGNodeLayoutAPI.YGNodeLayoutGetTop(node),
            YGNodeLayoutAPI.YGNodeLayoutGetWidth(node),
            YGNodeLayoutAPI.YGNodeLayoutGetHeight(node));
    }

    private static void SetDimension(this Node node,
        Action<Node, float> setPoint,
        Action<Node, float> setPercent,
        Action<Node> setAuto,
        DimensionValue dim)
    {
        if (dim.IsAuto) setAuto(node);
        else if (dim.IsPercent) setPercent(node, dim.Value);
        else if (dim.IsPoint) setPoint(node, dim.Value);
    }

    private static void SetSizeConstraint(this Node node,
        Action<Node, float> setPoint,
        Action<Node, float> setPercent,
        DimensionValue dim)
    {
        if (dim.IsAuto || dim.IsUndefined) setPoint(node, float.NaN);
        else if (dim.IsPercent) setPercent(node, dim.Value);
        else if (dim.IsPoint) setPoint(node, dim.Value);
    }

    private static void SetPosition(this Node node, YGEdge edge, DimensionValue dim)
    {
        if (dim.IsAuto) YGNodeStyleAPI.YGNodeStyleSetPositionAuto(node, edge);
        else if (dim.IsPercent) YGNodeStyleAPI.YGNodeStyleSetPositionPercent(node, edge, dim.Value);
        else if (dim.IsPoint) YGNodeStyleAPI.YGNodeStyleSetPosition(node, edge, dim.Value);
    }

    private static void SetMargin(Node node, YGEdge edge, DimensionValue dim)
    {
        if (dim.IsAuto) YGNodeStyleAPI.YGNodeStyleSetMarginAuto(node, edge);
        else if (dim.IsPercent) YGNodeStyleAPI.YGNodeStyleSetMarginPercent(node, edge, dim.Value);
        else if (dim.IsPoint) YGNodeStyleAPI.YGNodeStyleSetMargin(node, edge, dim.Value);
    }

    private static void SetPadding(Node node, YGEdge edge, DimensionValue dim)
    {
        if (dim.IsPercent) YGNodeStyleAPI.YGNodeStyleSetPaddingPercent(node, edge, dim.Value);
        else if (dim.IsPoint) YGNodeStyleAPI.YGNodeStyleSetPadding(node, edge, dim.Value);
    }

    private delegate void EdgeSetter(Node node, YGEdge edge, DimensionValue dim);

    private static void ApplyEdgeValues(Node node,
        DimensionValue? all, DimensionValue? x, DimensionValue? y,
        DimensionValue? top, DimensionValue? right, DimensionValue? bottom, DimensionValue? left,
        EdgeSetter setter)
    {
        if (all is { } a) setter(node, YGEdge.All, a);
        if (x is { } xv) { setter(node, YGEdge.Horizontal, xv); }
        if (y is { } yv) { setter(node, YGEdge.Vertical, yv); }
        if (top is { } t) setter(node, YGEdge.Top, t);
        if (right is { } r) setter(node, YGEdge.Right, r);
        if (bottom is { } b) setter(node, YGEdge.Bottom, b);
        if (left is { } l) setter(node, YGEdge.Left, l);
    }
}
