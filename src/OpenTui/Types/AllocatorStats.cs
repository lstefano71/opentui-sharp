using System.Runtime.InteropServices;

namespace OpenTui;

/// <summary>Memory allocator statistics from the native library.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct AllocatorStats
{
    /// <summary>Total bytes requested from the allocator.</summary>
    public ulong TotalRequestedBytes;

    /// <summary>Number of currently active allocations.</summary>
    public ulong ActiveAllocations;

    /// <summary>Number of small allocations made.</summary>
    public ulong SmallAllocations;

    /// <summary>Number of large allocations made.</summary>
    public ulong LargeAllocations;

    /// <summary>Whether the requested bytes counter is valid.</summary>
    [MarshalAs(UnmanagedType.U1)] public bool RequestedBytesValid;
}
