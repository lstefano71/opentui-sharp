using System.Runtime.InteropServices;

namespace OpenTui;

/// <summary>A text highlight range with a syntax style and priority.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Highlight
{
    /// <summary>Start byte offset of the highlight range.</summary>
    public uint Start;

    /// <summary>End byte offset of the highlight range.</summary>
    public uint End;

    /// <summary>Identifier of the style to apply.</summary>
    public uint StyleId;

    /// <summary>Priority for overlapping highlights (higher wins).</summary>
    public byte Priority;

    /// <summary>Reference identifier for the highlight source.</summary>
    public ushort HlRef;
}
