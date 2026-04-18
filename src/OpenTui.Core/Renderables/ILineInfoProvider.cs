namespace OpenTui.Core;

/// <summary>
/// Interface for renderables that provide line info (used by LineNumberRenderable).
/// Matches TypeScript LineInfoProvider pattern.
/// </summary>
public interface ILineInfoProvider
{
    /// <summary>Total number of logical lines in the content.</summary>
    int LineCount { get; }

    /// <summary>Current vertical scroll offset in visual lines.</summary>
    int ScrollY { get; }

    /// <summary>
    /// Visual-line layout info from the native TextBufferView.
    /// <see cref="LineInfo.LineSources"/> maps each visual line to its logical source line.
    /// May be cached; callers should not mutate.
    /// </summary>
    LineInfo? GetCachedLineInfo();

    /// <summary>Event name emitted when line info changes.</summary>
    static readonly string LineInfoChangeEvent = "line-info-change";
}
