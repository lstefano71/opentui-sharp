namespace OpenTui.Core.Plugins;

/// <summary>
/// Describes a plugin error event including context about where and when the error occurred.
/// </summary>
public sealed record PluginErrorEvent
{
    public required string PluginId { get; init; }
    public string? Slot { get; init; }
    public required PluginErrorPhase Phase { get; init; }
    public required string Source { get; init; }
    public required Exception Error { get; init; }
    public required long Timestamp { get; init; }
}
