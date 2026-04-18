using OpenTui.Native;

namespace OpenTui.Core;

/// <summary>
/// Wrappers for the native diagnostics API (arena stats, build options, allocator stats).
/// </summary>
public static class Diagnostics
{
    /// <summary>Gets the total bytes allocated by the arena allocator.</summary>
    public static nuint GetArenaAllocatedBytes() =>
        OpenTuiNative.GetArenaAllocatedBytes();

    /// <summary>Gets the native library build options.</summary>
    public static unsafe BuildOptions GetBuildOptions()
    {
        BuildOptions opts;
        OpenTuiNative.GetBuildOptions((nint)(&opts));
        return opts;
    }

    /// <summary>Gets the allocator statistics.</summary>
    public static unsafe AllocatorStats GetAllocatorStats()
    {
        AllocatorStats stats;
        OpenTuiNative.GetAllocatorStats((nint)(&stats));
        return stats;
    }
}
