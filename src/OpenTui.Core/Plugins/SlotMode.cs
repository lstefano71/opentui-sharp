namespace OpenTui.Core.Plugins;

/// <summary>
/// Controls how plugin contributions are composed within a slot.
/// </summary>
public enum SlotMode
{
    /// <summary>All plugin outputs are shown alongside the fallback content.</summary>
    Append,

    /// <summary>Plugin outputs replace the fallback content entirely.</summary>
    Replace,

    /// <summary>Only the highest-priority plugin renders; all others are hidden.</summary>
    SingleWinner,
}
