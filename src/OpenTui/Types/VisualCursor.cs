using System.Runtime.InteropServices;

namespace OpenTui;

/// <summary>Visual cursor position mapping visual rows/cols to logical position.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct VisualCursor(uint VisualRow, uint VisualCol, uint LogicalRow, uint LogicalCol, uint Offset);
