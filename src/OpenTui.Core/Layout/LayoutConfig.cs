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
    /// <summary>
    /// Represents the Column option.
    /// </summary>
    Column,
    /// <summary>
    /// Represents the Column Reverse option.
    /// </summary>
    ColumnReverse,
    /// <summary>
    /// Represents the Row option.
    /// </summary>
    Row,
    /// <summary>
    /// Represents the Row Reverse option.
    /// </summary>
    RowReverse,
}

/// <summary>String-based flex wrap values.</summary>
public enum WrapValue : byte
{
    /// <summary>
    /// Represents the No Wrap option.
    /// </summary>
    NoWrap,
    /// <summary>
    /// Represents the Wrap option.
    /// </summary>
    Wrap,
    /// <summary>
    /// Represents the Wrap Reverse option.
    /// </summary>
    WrapReverse,
}

/// <summary>String-based alignment values for alignItems/alignSelf.</summary>
public enum AlignValue : byte
{
    /// <summary>
    /// Represents the Auto option.
    /// </summary>
    Auto,
    /// <summary>
    /// Represents the Flex Start option.
    /// </summary>
    FlexStart,
    /// <summary>
    /// Represents the Center option.
    /// </summary>
    Center,
    /// <summary>
    /// Represents the Flex End option.
    /// </summary>
    FlexEnd,
    /// <summary>
    /// Represents the Stretch option.
    /// </summary>
    Stretch,
    /// <summary>
    /// Represents the Baseline option.
    /// </summary>
    Baseline,
    /// <summary>
    /// Represents the Space Between option.
    /// </summary>
    SpaceBetween,
    /// <summary>
    /// Represents the Space Around option.
    /// </summary>
    SpaceAround,
    /// <summary>
    /// Represents the Space Evenly option.
    /// </summary>
    SpaceEvenly,
}

/// <summary>String-based justify content values.</summary>
public enum JustifyValue : byte
{
    /// <summary>
    /// Represents the Flex Start option.
    /// </summary>
    FlexStart,
    /// <summary>
    /// Represents the Center option.
    /// </summary>
    Center,
    /// <summary>
    /// Represents the Flex End option.
    /// </summary>
    FlexEnd,
    /// <summary>
    /// Represents the Space Between option.
    /// </summary>
    SpaceBetween,
    /// <summary>
    /// Represents the Space Around option.
    /// </summary>
    SpaceAround,
    /// <summary>
    /// Represents the Space Evenly option.
    /// </summary>
    SpaceEvenly,
    /// <summary>
    /// Represents the Stretch option.
    /// </summary>
    Stretch,
}

/// <summary>Position type values.</summary>
public enum PositionValue : byte
{
    /// <summary>
    /// Represents the Static option.
    /// </summary>
    Static,
    /// <summary>
    /// Represents the Relative option.
    /// </summary>
    Relative,
    /// <summary>
    /// Represents the Absolute option.
    /// </summary>
    Absolute,
}

/// <summary>Overflow values.</summary>
public enum OverflowValue : byte
{
    /// <summary>
    /// Represents the Visible option.
    /// </summary>
    Visible,
    /// <summary>
    /// Represents the Hidden option.
    /// </summary>
    Hidden,
    /// <summary>
    /// Represents the Scroll option.
    /// </summary>
    Scroll,
}

/// <summary>
/// Maps OpenTUI enum values to Yoga.Net enum values.
/// Port of TypeScript yoga.options.ts parser functions.
/// </summary>
public static class YogaEnumMapper
{
    /// <summary>
    /// Performs to yoga.
    /// </summary>
    /// <param name="value">The value to set.</param>
    /// <returns>The result of to yoga.</returns>
    public static YGFlexDirection ToYoga(this FlexDirectionValue value) => value switch
    {
        FlexDirectionValue.Column => YGFlexDirection.Column,
        FlexDirectionValue.ColumnReverse => YGFlexDirection.ColumnReverse,
        FlexDirectionValue.Row => YGFlexDirection.Row,
        FlexDirectionValue.RowReverse => YGFlexDirection.RowReverse,
        _ => YGFlexDirection.Column,
    };

    /// <summary>
    /// Performs to yoga.
    /// </summary>
    /// <param name="value">The value to set.</param>
    /// <returns>The result of to yoga.</returns>
    public static YGWrap ToYoga(this WrapValue value) => value switch
    {
        WrapValue.NoWrap => YGWrap.NoWrap,
        WrapValue.Wrap => YGWrap.Wrap,
        WrapValue.WrapReverse => YGWrap.WrapReverse,
        _ => YGWrap.NoWrap,
    };

    /// <summary>
    /// Performs to yoga align.
    /// </summary>
    /// <param name="value">The value to set.</param>
    /// <returns>The result of to yoga align.</returns>
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

    /// <summary>
    /// Performs to yoga.
    /// </summary>
    /// <param name="value">The value to set.</param>
    /// <returns>The result of to yoga.</returns>
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

    /// <summary>
    /// Performs to yoga.
    /// </summary>
    /// <param name="value">The value to set.</param>
    /// <returns>The result of to yoga.</returns>
    public static YGPositionType ToYoga(this PositionValue value) => value switch
    {
        PositionValue.Static => YGPositionType.Static,
        PositionValue.Relative => YGPositionType.Relative,
        PositionValue.Absolute => YGPositionType.Absolute,
        _ => YGPositionType.Relative,
    };

    /// <summary>
    /// Performs to yoga.
    /// </summary>
    /// <param name="value">The value to set.</param>
    /// <returns>The result of to yoga.</returns>
    public static YGOverflow ToYoga(this OverflowValue value) => value switch
    {
        OverflowValue.Visible => YGOverflow.Visible,
        OverflowValue.Hidden => YGOverflow.Hidden,
        OverflowValue.Scroll => YGOverflow.Scroll,
        _ => YGOverflow.Visible,
    };
}
