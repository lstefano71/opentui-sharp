using System.Runtime.InteropServices;

namespace OpenTui;

/// <summary>Build-time configuration flags from the native library.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct BuildOptions
{
    /// <summary>Whether safe stats mode is enabled for the general-purpose allocator.</summary>
    [MarshalAs(UnmanagedType.U1)] public bool GpaSafeStats;

    /// <summary>Whether memory limit tracking is enabled for the general-purpose allocator.</summary>
    [MarshalAs(UnmanagedType.U1)] public bool GpaMemoryLimitTracking;
}
