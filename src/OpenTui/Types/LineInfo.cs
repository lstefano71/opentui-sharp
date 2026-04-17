namespace OpenTui;

/// <summary>Information about visual line layout from a text buffer view.</summary>
public sealed class LineInfo
{
    /// <summary>Display-column offset for each visual line start.</summary>
    public required uint[] LineStartCols { get; init; }

    /// <summary>Display-column width for each visual line.</summary>
    public required uint[] LineWidthCols { get; init; }

    /// <summary>Maximum display-column width across all visual lines.</summary>
    public required uint LineWidthColsMax { get; init; }

    /// <summary>Source logical line index for each visual line.</summary>
    public required uint[] LineSources { get; init; }

    /// <summary>Wrap index within each source logical line.</summary>
    public required uint[] LineWraps { get; init; }
}
