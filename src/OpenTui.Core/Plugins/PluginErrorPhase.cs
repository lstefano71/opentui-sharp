namespace OpenTui.Core.Plugins;

/// <summary>
/// Indicates during which phase a plugin error occurred.
/// </summary>
public enum PluginErrorPhase
{
    /// <summary>Error occurred during plugin setup/registration.</summary>
    Setup,

    /// <summary>Error occurred during plugin rendering.</summary>
    Render,

    /// <summary>Error occurred during plugin disposal.</summary>
    Dispose,

    /// <summary>Error occurred while rendering the error placeholder.</summary>
    ErrorPlaceholder,
}
