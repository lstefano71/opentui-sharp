namespace OpenTui;

/// <summary>Memory growth policy for NativeSpanFeed.</summary>
public enum GrowthPolicy : byte
{
    /// <summary>Allocate additional chunks as needed.</summary>
    Grow = 0,
    /// <summary>Block when capacity is exhausted.</summary>
    Block = 1,
}
