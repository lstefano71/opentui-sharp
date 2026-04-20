namespace OpenTui.Core.Plugins;

/// <summary>
/// Input for reporting a plugin error. The registry will normalize this into a <see cref="PluginErrorEvent"/>.
/// </summary>
public sealed record PluginErrorReport
{
    public required string PluginId { get; init; }
    public string? Slot { get; init; }
    public required PluginErrorPhase Phase { get; init; }
    public string? Source { get; init; }
    public required Exception Error { get; init; }
}
