namespace OpenTui;

/// <summary>An option in a select list or tab bar.</summary>
/// <typeparam name="T">The type of the value associated with this option.</typeparam>
public record SelectOption<T>(string Label, T Value, string? Description = null);
