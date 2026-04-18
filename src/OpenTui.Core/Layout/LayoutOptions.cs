namespace OpenTui.Core;

/// <summary>
/// A dimension value that can be a fixed number, "auto", or a percentage.
/// Matches TypeScript's `number | "auto" | `${number}%`` union type.
/// </summary>
public readonly struct DimensionValue
{
    public float Value { get; }
    public DimensionUnit Unit { get; }

    private DimensionValue(float value, DimensionUnit unit)
    {
        Value = value;
        Unit = unit;
    }

    public static DimensionValue Point(float value) => new(value, DimensionUnit.Point);
    public static DimensionValue Percent(float value) => new(value, DimensionUnit.Percent);
    public static DimensionValue Auto => new(0, DimensionUnit.Auto);
    public static DimensionValue Undefined => new(float.NaN, DimensionUnit.Undefined);

    public bool IsUndefined => Unit == DimensionUnit.Undefined;
    public bool IsAuto => Unit == DimensionUnit.Auto;
    public bool IsPoint => Unit == DimensionUnit.Point;
    public bool IsPercent => Unit == DimensionUnit.Percent;

    public static implicit operator DimensionValue(float value) => Point(value);
    public static implicit operator DimensionValue(int value) => Point(value);
}

/// <summary>Unit type for dimension values.</summary>
public enum DimensionUnit : byte
{
    Undefined,
    Point,
    Percent,
    Auto,
}

/// <summary>
/// Layout options for a renderable, matching TypeScript LayoutOptions interface.
/// All properties are nullable — null means "use default / don't set".
/// </summary>
public class LayoutOptions
{
    public DimensionValue? Width { get; init; }
    public DimensionValue? Height { get; init; }
    public DimensionValue? MinWidth { get; init; }
    public DimensionValue? MinHeight { get; init; }
    public DimensionValue? MaxWidth { get; init; }
    public DimensionValue? MaxHeight { get; init; }

    public float? FlexGrow { get; init; }
    public float? FlexShrink { get; init; }
    public DimensionValue? FlexBasis { get; init; }

    public FlexDirectionValue? FlexDirection { get; init; }
    public WrapValue? FlexWrap { get; init; }
    public AlignValue? AlignItems { get; init; }
    public JustifyValue? JustifyContent { get; init; }
    public AlignValue? AlignSelf { get; init; }

    public PositionValue? Position { get; init; }
    public OverflowValue? Overflow { get; init; }

    public DimensionValue? Top { get; init; }
    public DimensionValue? Right { get; init; }
    public DimensionValue? Bottom { get; init; }
    public DimensionValue? Left { get; init; }

    public DimensionValue? Margin { get; init; }
    public DimensionValue? MarginX { get; init; }
    public DimensionValue? MarginY { get; init; }
    public DimensionValue? MarginTop { get; init; }
    public DimensionValue? MarginRight { get; init; }
    public DimensionValue? MarginBottom { get; init; }
    public DimensionValue? MarginLeft { get; init; }

    public DimensionValue? Padding { get; init; }
    public DimensionValue? PaddingX { get; init; }
    public DimensionValue? PaddingY { get; init; }
    public DimensionValue? PaddingTop { get; init; }
    public DimensionValue? PaddingRight { get; init; }
    public DimensionValue? PaddingBottom { get; init; }
    public DimensionValue? PaddingLeft { get; init; }

    public float? Gap { get; init; }
    public float? RowGap { get; init; }
    public float? ColumnGap { get; init; }
}
