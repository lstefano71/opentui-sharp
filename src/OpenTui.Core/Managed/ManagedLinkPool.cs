using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace OpenTui.Core.Managed;

/// <summary>
/// Pure C# reimplementation of the Zig <c>LinkPool</c> from <c>link.zig</c>.
/// Stores URL strings with reusable slot-based IDs and generation counters.
/// Thread-safe: no. Callers must synchronize externally if needed.
/// </summary>
public sealed class ManagedLinkPool
{
    // ── ID layout (24 bits total) ────────────────────────────────────
    //   [ generation (8 bits) | slot_index (16 bits) ]
    private const int GenBits = 8;
    private const int SlotBits = 16;
    private const uint GenMask = 0xFF;
    private const uint SlotMask = 0xFFFF;

    /// <summary>Maximum URL length in bytes (UTF-8).</summary>
    public const int MaxUrlLength = 512;

    // ── Slot storage ─────────────────────────────────────────────────
    private readonly List<Slot> _slots = [];
    private int _freeHead = -1; // index into _slots; -1 = empty free-list

    // ── URL interning ────────────────────────────────────────────────
    // Maps a live URL string → the slot index that owns it.
    private readonly Dictionary<string, int> _internMap = new(StringComparer.Ordinal);

    // ── Slot definition ──────────────────────────────────────────────
    private struct Slot
    {
        public string? Url;       // null when free
        public byte Generation;   // wraps at 255 → 1 (0 is never a valid gen for an occupied slot)
        public int RefCount;
        public int NextFree;      // next index in free-list (-1 = end)
    }

    // ── Public API ───────────────────────────────────────────────────

    /// <summary>
    /// Allocate (or intern) a link ID for <paramref name="url"/>.
    /// If the same URL is already live, its reference count is incremented
    /// and the existing ID is returned.
    /// </summary>
    /// <returns>A 24-bit link ID encoding generation and slot index.</returns>
    /// <exception cref="ArgumentException">URL is null, empty, or exceeds <see cref="MaxUrlLength"/> UTF-8 bytes.</exception>
    /// <exception cref="InvalidOperationException">All 65 536 slots are exhausted.</exception>
    public uint Alloc(string url)
    {
        ArgumentException.ThrowIfNullOrEmpty(url);
        if (System.Text.Encoding.UTF8.GetByteCount(url) > MaxUrlLength)
            throw new ArgumentException($"URL exceeds maximum length of {MaxUrlLength} bytes.", nameof(url));

        // ── Intern check ─────────────────────────────────────────
        if (_internMap.TryGetValue(url, out int existingIndex))
        {
            ref Slot existing = ref CollectionsMarshal.AsSpan(_slots)[existingIndex];
            existing.RefCount++;
            return MakeId(existing.Generation, (uint)existingIndex);
        }

        // ── Acquire a slot ───────────────────────────────────────
        int slotIndex;
        if (_freeHead >= 0)
        {
            // Reuse from free-list
            slotIndex = _freeHead;
            ref Slot reused = ref CollectionsMarshal.AsSpan(_slots)[slotIndex];
            _freeHead = reused.NextFree;
            reused.Url = url;
            reused.RefCount = 1;
            reused.NextFree = -1;
            // Generation was already bumped on Release; keep it.
        }
        else
        {
            // Grow
            if (_slots.Count >= (int)SlotMask + 1)
                throw new InvalidOperationException("ManagedLinkPool: all 65 536 slots are exhausted.");

            slotIndex = _slots.Count;
            _slots.Add(new Slot
            {
                Url = url,
                Generation = 1,  // first valid generation
                RefCount = 1,
                NextFree = -1,
            });
        }

        ref Slot slot = ref CollectionsMarshal.AsSpan(_slots)[slotIndex];
        _internMap[url] = slotIndex;
        return MakeId(slot.Generation, (uint)slotIndex);
    }

    /// <summary>
    /// Retrieve the URL associated with <paramref name="linkId"/>.
    /// Returns <see langword="null"/> if the ID is stale (generation mismatch) or the slot is free.
    /// </summary>
    public string? GetUrl(uint linkId)
    {
        (byte gen, uint index) = SplitId(linkId);
        if (index >= (uint)_slots.Count)
            return null;

        ref readonly Slot slot = ref CollectionsMarshal.AsSpan(_slots)[(int)index];
        if (slot.Generation != gen || slot.Url is null)
            return null;

        return slot.Url;
    }

    /// <summary>
    /// Decrement the reference count for <paramref name="linkId"/>.
    /// When the count reaches zero the slot is freed and returned to the free-list.
    /// Stale or invalid IDs are silently ignored.
    /// </summary>
    public void Release(uint linkId)
    {
        (byte gen, uint index) = SplitId(linkId);
        if (index >= (uint)_slots.Count)
            return;

        ref Slot slot = ref CollectionsMarshal.AsSpan(_slots)[(int)index];
        if (slot.Generation != gen || slot.Url is null)
            return;

        slot.RefCount--;
        if (slot.RefCount > 0)
            return;

        // Free the slot
        _internMap.Remove(slot.Url);
        slot.Url = null;

        // Bump generation (wrap 255 → 1; 0 is reserved / never-valid)
        slot.Generation = (byte)(slot.Generation == GenMask ? 1 : slot.Generation + 1);

        // Push onto free-list
        slot.NextFree = _freeHead;
        _freeHead = (int)index;
    }

    // ── Attribute helpers (static, pure bit-manipulation) ────────────

    /// <summary>
    /// Embed a 24-bit <paramref name="linkId"/> into the upper 24 bits (bits 8-31)
    /// of an attribute value, preserving the lower 8 bits of <paramref name="attrs"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint AttributesWithLink(uint attrs, uint linkId)
        => (attrs & 0xFF) | (linkId << 8);

    /// <summary>
    /// Extract the 24-bit link ID from bits 8-31 of <paramref name="attrs"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetLinkId(uint attrs)
        => attrs >> 8;

    // ── Private helpers ──────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint MakeId(byte generation, uint slotIndex)
        => ((uint)generation << SlotBits) | (slotIndex & SlotMask);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (byte Generation, uint SlotIndex) SplitId(uint id)
        => ((byte)((id >> SlotBits) & GenMask), id & SlotMask);
}
