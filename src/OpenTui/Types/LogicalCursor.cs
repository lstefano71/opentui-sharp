using System.Runtime.InteropServices;

namespace OpenTui;

/// <summary>Logical cursor position in a text buffer (row, column, byte offset).</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct LogicalCursor(uint Row, uint Col, uint Offset);
