using System.Runtime.InteropServices;

namespace OpenTui;

/// <summary>Result of measuring text dimensions.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct MeasureResult(uint LineCount, uint WidthColsMax);
