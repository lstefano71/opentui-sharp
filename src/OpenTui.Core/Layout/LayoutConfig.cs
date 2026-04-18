using Facebook.Yoga;

namespace OpenTui.Core;

/// <summary>
/// Global Yoga configuration shared across all layout nodes.
/// Mirrors TypeScript: Yoga.Config.create() with useWebDefaults=false, pointScaleFactor=1.
/// </summary>
public static class LayoutConfig
{
    private static readonly Config s_config = CreateConfig();

    /// <summary>Gets the shared Yoga config instance.</summary>
    public static Config Shared => s_config;

    private static Config CreateConfig()
    {
        var config = new Config();
        config.SetUseWebDefaults(false);
        config.SetPointScaleFactor(1.0f);
        return config;
    }
}

/// <summary>
/// Computed layout result read from Yoga after calculateLayout().
/// </summary>
public readonly record struct ComputedLayout(float Left, float Top, float Width, float Height);

/// <summary>String-based flex direction values matching CSS/TypeScript API.</summary>
public enum FlexDirectionValue : byte
{
    Column,
    ColumnReverse,
    Row,
    RowReverse,
}

/// <summary>String-based flex wrap values.</summary>
public enum WrapValue : byte
{
    NoWrap,
    Wrap,
    WrapReverse,
}

/// <summary>String-based alignment values for alignItems/alignSelf.</summary>
public enum AlignValue : byte
{
    Auto,
    FlexStart,
    Center,
    FlexEnd,
    Stretch,
    Baseline,
    SpaceBetween,
    SpaceAround,
    SpaceEvenly,
}

/// <summary>String-based justify content values.</summary>
public enum JustifyValue : byte
{
    FlexStart,
    Center,
    FlexEnd,
    SpaceBetween,
    SpaceAround,
    SpaceEvenly,
    Stretch,
}

/// <summary>Position type values.</summary>
public enum PositionValue : byte
{
    Static,
    Relative,
    Absolute,
}

/// <summary>Overflow values.</summary>
public enum OverflowValue : byte
{
    Visible,
    Hidden,
    Scroll,
}

/// <summary>
/// Maps OpenTUI enum values to Yoga.Net enum values.
/// Port of TypeScript yoga.options.ts parser functions.
/// </summary>
public static class YogaEnumMapper
{
    public static YGFlexDirection ToYoga(this FlexDirectionValue value) => value switch
    {
        FlexDirectionValue.Column => YGFlexDirection.Column,
        FlexDirectionValue.ColumnReverse => YGFlexDirection.ColumnReverse,
        FlexDirectionValue.Row => YGFlexDirection.Row,
        FlexDirectionValue.RowReverse => YGFlexDirection.RowReverse,
        _ => YGFlexDirection.Column,
    };

    public static YGWrap ToYoga(this WrapValue value) => value switch
    {
        WrapValue.NoWrap => YGWrap.NoWrap,
        WrapValue.Wrap => YGWrap.Wrap,
        WrapValue.WrapReverse => YGWrap.WrapReverse,
        _ => YGWrap.NoWrap,
    };

    public static YGAlign ToYogaAlign(this AlignValue value) => value switch
    {
        AlignValue.Auto => YGAlign.Auto,
        AlignValue.FlexStart => YGAlign.FlexStart,
        AlignValue.Center => YGAlign.Center,
        AlignValue.FlexEnd => YGAlign.FlexEnd,
        AlignValue.Stretch => YGAlign.Stretch,
        AlignValue.Baseline => YGAlign.Baseline,
        AlignValue.SpaceBetween => YGAlign.SpaceBetween,
        AlignValue.SpaceAround => YGAlign.SpaceAround,
        AlignValue.SpaceEvenly => YGAlign.SpaceEvenly,
        _ => YGAlign.Auto,
    };

    public static YGJustify ToYoga(this JustifyValue value) => value switch
    {
        JustifyValue.FlexStart => YGJustify.FlexStart,
        JustifyValue.Center => YGJustify.Center,
        JustifyValue.FlexEnd => YGJustify.FlexEnd,
        JustifyValue.SpaceBetween => YGJustify.SpaceBetween,
        JustifyValue.SpaceAround => YGJustify.SpaceAround,
        JustifyValue.SpaceEvenly => YGJustify.SpaceEvenly,
        JustifyValue.Stretch => YGJustify.Stretch,
        _ => YGJustify.FlexStart,
    };

    public static YGPositionType ToYoga(this PositionValue value) => value switch
    {
        PositionValue.Static => YGPositionType.Static,
        PositionValue.Relative => YGPositionType.Relative,
        PositionValue.Absolute => YGPositionType.Absolute,
        _ => YGPositionType.Relative,
    };

    public static YGOverflow ToYoga(this OverflowValue value) => value switch
    {
        OverflowValue.Visible => YGOverflow.Visible,
        OverflowValue.Hidden => YGOverflow.Hidden,
        OverflowValue.Scroll => YGOverflow.Scroll,
        _ => YGOverflow.Visible,
    };
}
