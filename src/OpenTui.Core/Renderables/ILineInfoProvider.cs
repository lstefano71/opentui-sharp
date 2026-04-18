namespace OpenTui.Core;

/// <summary>
/// Interface for renderables that provide line info (used by LineNumberRenderable).
/// Matches TypeScript LineInfoProvider pattern.
/// </summary>
public interface ILineInfoProvider
{
    /// <summary>Total number of lines in the content.</summary>
    int LineCount { get; }

    /// <summary>Current vertical scroll offset in lines.</summary>
    int ScrollY { get; }

    /// <summary>Event name emitted when line info changes.</summary>
    static readonly string LineInfoChangeEvent = "line-info-change";
}
