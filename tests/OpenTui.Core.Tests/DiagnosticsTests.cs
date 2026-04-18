using OpenTui.Core;
using Xunit;

namespace OpenTui.Core.Tests;

/// <summary>
/// Diagnostics API tests — exercises GetArenaAllocatedBytes, GetBuildOptions, GetAllocatorStats.
/// </summary>
public class DiagnosticsTests
{
    [Fact]
    public void GetArenaAllocatedBytes()
    {
        // Just verifying it doesn't crash — actual value depends on native state
        nuint bytes = Diagnostics.GetArenaAllocatedBytes();
        // Arena may have some pre-allocated memory
        _ = bytes;
    }

    [Fact]
    public void GetBuildOptions()
    {
        var opts = Diagnostics.GetBuildOptions();
        // Just verify the struct is readable without crash
        _ = opts.GpaSafeStats;
        _ = opts.GpaMemoryLimitTracking;
    }

    [Fact]
    public void GetAllocatorStats()
    {
        var stats = Diagnostics.GetAllocatorStats();
        _ = stats.TotalRequestedBytes;
        _ = stats.ActiveAllocations;
        _ = stats.SmallAllocations;
        _ = stats.LargeAllocations;
        _ = stats.RequestedBytesValid;
    }

    [Fact]
    public void GetAllocatorStatsAfterAllocation()
    {
        // Allocate something native, then check stats
        using var renderer = NativeRenderer.Create(80, 24, testing: true);
        var stats = Diagnostics.GetAllocatorStats();
        // After creating a renderer there should be some allocations
        Assert.True(stats.ActiveAllocations > 0 || !stats.RequestedBytesValid);
    }
}
