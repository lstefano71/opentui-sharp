namespace OpenTui.Core;

/// <summary>
/// A dimension value that can be a fixed number, "auto", or a percentage.
/// Matches TypeScript's `number | "auto" | `${number}%`` union type.
/// </summary>
public readonly struct DimensionValue
{
    /// <summary>
    /// Gets the value.
    /// </summary>
    public float Value { get; }
    /// <summary>
    /// Gets the unit.
    /// </summary>
    public DimensionUnit Unit { get; }

    private DimensionValue(float value, DimensionUnit unit)
    {
        Value = value;
        Unit = unit;
    }

    /// <summary>
    /// Performs point.
    /// </summary>
    /// <param name="value">The value to set.</param>
    /// <returns>The result of point.</returns>
    public static DimensionValue Point(float value) => new(value, DimensionUnit.Point);
    /// <summary>
    /// Performs percent.
    /// </summary>
    /// <param name="value">The value to set.</param>
    /// <returns>The result of percent.</returns>
    public static DimensionValue Percent(float value) => new(value, DimensionUnit.Percent);
    /// <summary>
    /// Gets the auto.
    /// </summary>
    public static DimensionValue Auto => new(0, DimensionUnit.Auto);
    /// <summary>
    /// Gets the undefined.
    /// </summary>
    public static DimensionValue Undefined => new(float.NaN, DimensionUnit.Undefined);

    /// <summary>
    /// Gets a value indicating whether is undefined.
    /// </summary>
    public bool IsUndefined => Unit == DimensionUnit.Undefined;
    /// <summary>
    /// Gets a value indicating whether is auto.
    /// </summary>
    public bool IsAuto => Unit == DimensionUnit.Auto;
    /// <summary>
    /// Gets a value indicating whether is point.
    /// </summary>
    public bool IsPoint => Unit == DimensionUnit.Point;
    /// <summary>
    /// Gets a value indicating whether is percent.
    /// </summary>
    public bool IsPercent => Unit == DimensionUnit.Percent;

    /// <summary>
    /// Implicitly converts a value to DimensionValue.
    /// </summary>
    /// <param name="value">The value to set.</param>
    /// <returns>The converted DimensionValue value.</returns>
    public static implicit operator DimensionValue(float value) => Point(value);
    /// <summary>
    /// Implicitly converts a value to DimensionValue.
    /// </summary>
    /// <param name="value">The value to set.</param>
    /// <returns>The converted DimensionValue value.</returns>
    public static implicit operator DimensionValue(int value) => Point(value);
}

/// <summary>Unit type for dimension values.</summary>
public enum DimensionUnit : byte
{
    /// <summary>
    /// Represents the Undefined option.
    /// </summary>
    Undefined,
    /// <summary>
    /// Represents the Point option.
    /// </summary>
    Point,
    /// <summary>
    /// Represents the Percent option.
    /// </summary>
    Percent,
    /// <summary>
    /// Represents the Auto option.
    /// </summary>
    Auto,
}

/// <summary>
/// Layout options for a renderable, matching TypeScript LayoutOptions interface.
/// All properties are nullable — null means "use default / don't set".
/// </summary>
public class LayoutOptions
{
    /// <summary>
    /// Gets or sets the width.
    /// </summary>
    public DimensionValue? Width { get; init; }
    /// <summary>
    /// Gets or sets the height.
    /// </summary>
    public DimensionValue? Height { get; init; }
    /// <summary>
    /// Gets or sets the min width.
    /// </summary>
    public DimensionValue? MinWidth { get; init; }
    /// <summary>
    /// Gets or sets the min height.
    /// </summary>
    public DimensionValue? MinHeight { get; init; }
    /// <summary>
    /// Gets or sets the max width.
    /// </summary>
    public DimensionValue? MaxWidth { get; init; }
    /// <summary>
    /// Gets or sets the max height.
    /// </summary>
    public DimensionValue? MaxHeight { get; init; }

    /// <summary>
    /// Gets or sets the flex grow.
    /// </summary>
    public float? FlexGrow { get; init; }
    /// <summary>
    /// Gets or sets the flex shrink.
    /// </summary>
    public float? FlexShrink { get; init; }
    /// <summary>
    /// Gets or sets the flex basis.
    /// </summary>
    public DimensionValue? FlexBasis { get; init; }

    /// <summary>
    /// Gets or sets the flex direction.
    /// </summary>
    public FlexDirectionValue? FlexDirection { get; init; }
    /// <summary>
    /// Gets or sets the flex wrap.
    /// </summary>
    public WrapValue? FlexWrap { get; init; }
    /// <summary>
    /// Gets or sets the align items.
    /// </summary>
    public AlignValue? AlignItems { get; init; }
    /// <summary>
    /// Gets or sets the justify content.
    /// </summary>
    public JustifyValue? JustifyContent { get; init; }
    /// <summary>
    /// Gets or sets the align self.
    /// </summary>
    public AlignValue? AlignSelf { get; init; }

    /// <summary>
    /// Gets or sets the position.
    /// </summary>
    public PositionValue? Position { get; init; }
    /// <summary>
    /// Gets or sets the overflow.
    /// </summary>
    public OverflowValue? Overflow { get; init; }

    /// <summary>
    /// Gets or sets the top.
    /// </summary>
    public DimensionValue? Top { get; init; }
    /// <summary>
    /// Gets or sets the right.
    /// </summary>
    public DimensionValue? Right { get; init; }
    /// <summary>
    /// Gets or sets the bottom.
    /// </summary>
    public DimensionValue? Bottom { get; init; }
    /// <summary>
    /// Gets or sets the left.
    /// </summary>
    public DimensionValue? Left { get; init; }

    /// <summary>
    /// Gets or sets the margin.
    /// </summary>
    public DimensionValue? Margin { get; init; }
    /// <summary>
    /// Gets or sets the margin x.
    /// </summary>
    public DimensionValue? MarginX { get; init; }
    /// <summary>
    /// Gets or sets the margin y.
    /// </summary>
    public DimensionValue? MarginY { get; init; }
    /// <summary>
    /// Gets or sets the margin top.
    /// </summary>
    public DimensionValue? MarginTop { get; init; }
    /// <summary>
    /// Gets or sets the margin right.
    /// </summary>
    public DimensionValue? MarginRight { get; init; }
    /// <summary>
    /// Gets or sets the margin bottom.
    /// </summary>
    public DimensionValue? MarginBottom { get; init; }
    /// <summary>
    /// Gets or sets the margin left.
    /// </summary>
    public DimensionValue? MarginLeft { get; init; }

    /// <summary>
    /// Gets or sets the padding.
    /// </summary>
    public DimensionValue? Padding { get; init; }
    /// <summary>
    /// Gets or sets the padding x.
    /// </summary>
    public DimensionValue? PaddingX { get; init; }
    /// <summary>
    /// Gets or sets the padding y.
    /// </summary>
    public DimensionValue? PaddingY { get; init; }
    /// <summary>
    /// Gets or sets the padding top.
    /// </summary>
    public DimensionValue? PaddingTop { get; init; }
    /// <summary>
    /// Gets or sets the padding right.
    /// </summary>
    public DimensionValue? PaddingRight { get; init; }
    /// <summary>
    /// Gets or sets the padding bottom.
    /// </summary>
    public DimensionValue? PaddingBottom { get; init; }
    /// <summary>
    /// Gets or sets the padding left.
    /// </summary>
    public DimensionValue? PaddingLeft { get; init; }

    /// <summary>
    /// Gets or sets the gap.
    /// </summary>
    public float? Gap { get; init; }
    /// <summary>
    /// Gets or sets the row gap.
    /// </summary>
    public float? RowGap { get; init; }
    /// <summary>
    /// Gets or sets the column gap.
    /// </summary>
    public float? ColumnGap { get; init; }
}
