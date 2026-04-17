namespace OpenTui;

/// <summary>Character width calculation method for Unicode text.</summary>
public enum WidthMethod : byte
{
    /// <summary>Use wcwidth for character width calculation.</summary>
    WcWidth = 0,
    /// <summary>Use Unicode standard width tables.</summary>
    Unicode = 1,
}
