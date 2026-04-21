using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace OpenTui.Core.Managed.Unicode;

/// <summary>
/// Slab-allocated pool for storing grapheme cluster byte sequences with reusable IDs.
/// Port of the Zig <c>GraphemePool</c> from <c>grapheme.zig</c>.
/// Thread-safe: no. Callers must synchronize externally if needed.
/// </summary>
/// <remarks>
/// <para>
/// ID layout (26-bit payload packed into the lower 26 bits of a u32):
/// <c>[ class (3 bits) | generation (7 bits) | slot_index (16 bits) ]</c>
/// </para>
/// <para>
/// Size classes: 8, 16, 32, 64, 128 bytes.
/// Generation-based slot reuse prevents stale IDs from silently returning wrong data.
/// Interning: identical byte sequences yield the same ID with reference counting.
/// </para>
/// </remarks>
public sealed class ManagedGraphemePool
{
    #region Public constants matching Zig grapheme.zig

    /// <summary>Bit 31 set → grapheme start cell.</summary>
    public const uint CharFlagGrapheme = 0x8000_0000;

    /// <summary>Bits 31+30 set → continuation cell.</summary>
    public const uint CharFlagContinuation = 0xC000_0000;

    /// <summary>Right extent field: bits 29..28.</summary>
    public const int CharExtRightShift = 28;

    /// <summary>Left extent field: bits 27..26.</summary>
    public const int CharExtLeftShift = 26;

    /// <summary>Mask for 2-bit extent fields.</summary>
    public const uint CharExtMask = 0x3;

    /// <summary>Lower 26 bits carry the grapheme pool ID.</summary>
    public const uint GraphemeIdMask = 0x03FF_FFFF;

    #endregion

    #region Internal constants

    private const int ClassBits = 3;
    private const int GenerationBits = 7;
    private const int SlotBits = 16;
    private const uint ClassMask = (1u << ClassBits) - 1;       // 0b111
    private const uint GenerationMask = (1u << GenerationBits) - 1; // 0x7F
    private const uint SlotMask = (1u << SlotBits) - 1;         // 0xFFFF
    private const int MaxClasses = 5;

    private static ReadOnlySpan<int> ClassSizes => [8, 16, 32, 64, 128];
    private static ReadOnlySpan<int> DefaultSlotsPerPage => [256, 128, 64, 16, 8];

    #endregion

    #region State

    private readonly ClassPool[] _classes;

    // Interning: maps byte content → packed ID for deduplication
    private readonly Dictionary<ByteKey, uint> _internMap = new();

    #endregion

    /// <summary>Create a new grapheme pool with default capacity.</summary>
    public ManagedGraphemePool() : this(null) { }

    /// <summary>
    /// Create a new grapheme pool.
    /// </summary>
    /// <param name="slotsPerPage">
    /// Slots per page for each of the 5 size classes.
    /// Null uses defaults: [256, 128, 64, 16, 8].
    /// </param>
    public ManagedGraphemePool(int[]? slotsPerPage)
    {
        _classes = new ClassPool[MaxClasses];
        for (int i = 0; i < MaxClasses; i++)
        {
            int capacity = ClassSizes[i];
            int slots = (slotsPerPage is not null && i < slotsPerPage.Length) ? slotsPerPage[i] : DefaultSlotsPerPage[i];
            _classes[i] = new ClassPool(capacity, slots);
        }
    }

    /// <summary>
    /// Allocate a slot for grapheme bytes, returning a packed 26-bit ID.
    /// If identical bytes are already live, the existing ID is returned with an incremented refcount.
    /// </summary>
    /// <exception cref="InvalidOperationException">Pool slots exhausted.</exception>
    public uint Alloc(ReadOnlySpan<byte> graphemeBytes)
    {
        // Intern check
        var key = new ByteKey(graphemeBytes);
        if (_internMap.TryGetValue(key, out uint existingId))
        {
            // Validate the existing ID is still live
            if (TryGetClassAndSlot(existingId, out int cls, out int slot, out uint gen))
            {
                ref ClassPool pool = ref _classes[cls];
                if (pool.IsLive(slot, gen))
                {
                    pool.IncRef(slot, gen);
                    return existingId;
                }
            }
            // Stale entry — remove and fall through
            _internMap.Remove(key);
        }

        int classId = ClassForSize(graphemeBytes.Length);
        ref ClassPool classPool = ref _classes[classId];
        int slotIndex = classPool.AllocSlot(graphemeBytes);
        uint generation = classPool.GetGeneration(slotIndex);
        uint id = PackId((uint)classId, (uint)slotIndex, generation);

        // Intern the new allocation
        _internMap[new ByteKey(classPool.GetBytes(slotIndex, generation))] = id;

        return id;
    }

    /// <summary>
    /// Get the grapheme bytes for a packed ID.
    /// </summary>
    /// <exception cref="InvalidOperationException">ID is invalid or stale.</exception>
    public ReadOnlySpan<byte> Get(uint id)
    {
        Unpack(id, out int classId, out int slotIndex, out uint generation);
        return _classes[classId].GetBytes(slotIndex, generation);
    }

    /// <summary>
    /// Decrement the reference count for an ID. When it reaches zero the slot becomes reusable.
    /// Stale IDs are silently ignored.
    /// </summary>
    public void Release(uint id)
    {
        if (!TryGetClassAndSlot(id, out int classId, out int slotIndex, out uint generation))
            return;

        ref ClassPool pool = ref _classes[classId];
        if (!pool.IsLive(slotIndex, generation))
            return;

        int oldRef = pool.GetRefCount(slotIndex, generation);
        if (oldRef <= 1)
        {
            // Transition from 1→0: remove intern entry
            var bytes = pool.GetBytes(slotIndex, generation);
            _internMap.Remove(new ByteKey(bytes));
        }

        pool.DecRef(slotIndex, generation);
    }

    /// <summary>Check if an ID is still valid (correct generation and refcount &gt; 0).</summary>
    public bool IsValid(uint id)
    {
        if (!TryGetClassAndSlot(id, out int classId, out int slotIndex, out uint generation))
            return false;

        return _classes[classId].IsLive(slotIndex, generation);
    }

    #region Private helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ClassForSize(int size)
    {
        if (size <= 8) return 0;
        if (size <= 16) return 1;
        if (size <= 32) return 2;
        if (size <= 64) return 3;
        return 4; // up to 128
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint PackId(uint classId, uint slotIndex, uint generation)
        => (classId << (GenerationBits + SlotBits))
         | ((generation & GenerationMask) << SlotBits)
         | (slotIndex & SlotMask);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Unpack(uint id, out int classId, out int slotIndex, out uint generation)
    {
        classId = (int)((id >> (GenerationBits + SlotBits)) & ClassMask);
        generation = (id >> SlotBits) & GenerationMask;
        slotIndex = (int)(id & SlotMask);

        if (classId >= MaxClasses)
            ThrowInvalidId();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetClassAndSlot(uint id, out int classId, out int slotIndex, out uint generation)
    {
        classId = (int)((id >> (GenerationBits + SlotBits)) & ClassMask);
        generation = (id >> SlotBits) & GenerationMask;
        slotIndex = (int)(id & SlotMask);
        return classId < MaxClasses;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInvalidId()
        => throw new InvalidOperationException("ManagedGraphemePool: invalid ID (class out of range).");

    #endregion

    #region ClassPool — per-size-class slab allocator

    private struct ClassPool
    {
        private readonly int _slotCapacity;
        private readonly int _slotsPerPage;

        // Flat byte array: each slot is [SlotHeader | data bytes].
        // SlotHeader is stored inline as fields at fixed offsets.
        private byte[] _data;
        private int _slotSizeBytes; // sizeof(header) + _slotCapacity, aligned
        private int _numSlots;
        private int _freeHead; // -1 = empty free list

        // Header layout within each slot (stored as raw bytes):
        //   offset 0: ushort len        (2 bytes)
        //   offset 2: ushort _pad       (2 bytes)
        //   offset 4: int    refcount   (4 bytes)
        //   offset 8: uint   generation (4 bytes)
        //   offset 12: int   nextFree   (4 bytes)
        // Total: 16 bytes header
        private const int HeaderSize = 16;
        private const int OffsetLen = 0;
        private const int OffsetRefCount = 4;
        private const int OffsetGeneration = 8;
        private const int OffsetNextFree = 12;

        public ClassPool(int slotCapacity, int slotsPerPage)
        {
            _slotCapacity = slotCapacity;
            _slotsPerPage = slotsPerPage;
            _slotSizeBytes = HeaderSize + slotCapacity;
            _numSlots = 0;
            _freeHead = -1;
            _data = [];
        }

        /// <summary>Allocate a slot, copy bytes in, set refcount to 1. Return slot index.</summary>
        public int AllocSlot(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length > _slotCapacity)
                throw new ArgumentException($"Grapheme bytes ({bytes.Length}) exceed class capacity ({_slotCapacity}).");

            int slotIndex;

            if (_freeHead >= 0)
            {
                slotIndex = _freeHead;
                int baseOff = slotIndex * _slotSizeBytes;
                _freeHead = ReadInt(_data, baseOff + OffsetNextFree);
                // Generation was already bumped on free; keep it
            }
            else
            {
                // Grow
                if (_numSlots >= (int)SlotMask + 1)
                    throw new InvalidOperationException("ManagedGraphemePool: slot limit exhausted for size class.");

                slotIndex = _numSlots;
                _numSlots++;
                EnsureCapacity();

                int baseOff = slotIndex * _slotSizeBytes;
                WriteUInt(_data, baseOff + OffsetGeneration, 1); // first valid generation
            }

            {
                int baseOff = slotIndex * _slotSizeBytes;
                WriteUShort(_data, baseOff + OffsetLen, (ushort)bytes.Length);
                WriteInt(_data, baseOff + OffsetRefCount, 1);
                WriteInt(_data, baseOff + OffsetNextFree, -1);

                // Copy grapheme bytes
                bytes.CopyTo(_data.AsSpan(baseOff + HeaderSize, _slotCapacity));
            }

            return slotIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly uint GetGeneration(int slotIndex)
        {
            int baseOff = slotIndex * _slotSizeBytes;
            return ReadUInt(_data, baseOff + OffsetGeneration);
        }

        public readonly ReadOnlySpan<byte> GetBytes(int slotIndex, uint generation)
        {
            ValidateSlot(slotIndex, generation);
            int baseOff = slotIndex * _slotSizeBytes;
            int len = ReadUShort(_data, baseOff + OffsetLen);
            return _data.AsSpan(baseOff + HeaderSize, len);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int GetRefCount(int slotIndex, uint generation)
        {
            ValidateSlot(slotIndex, generation);
            int baseOff = slotIndex * _slotSizeBytes;
            return ReadInt(_data, baseOff + OffsetRefCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsLive(int slotIndex, uint generation)
        {
            if (slotIndex < 0 || slotIndex >= _numSlots) return false;
            int baseOff = slotIndex * _slotSizeBytes;
            return ReadUInt(_data, baseOff + OffsetGeneration) == generation
                && ReadInt(_data, baseOff + OffsetRefCount) > 0;
        }

        public void IncRef(int slotIndex, uint generation)
        {
            ValidateSlot(slotIndex, generation);
            int baseOff = slotIndex * _slotSizeBytes;
            int rc = ReadInt(_data, baseOff + OffsetRefCount);
            WriteInt(_data, baseOff + OffsetRefCount, rc + 1);
        }

        public void DecRef(int slotIndex, uint generation)
        {
            ValidateSlot(slotIndex, generation);
            int baseOff = slotIndex * _slotSizeBytes;
            int rc = ReadInt(_data, baseOff + OffsetRefCount);
            if (rc <= 0) return;

            rc--;
            WriteInt(_data, baseOff + OffsetRefCount, rc);

            if (rc == 0)
            {
                // Bump generation (wrap GenerationMask → 1; 0 is never-valid for fresh slots)
                uint gen = ReadUInt(_data, baseOff + OffsetGeneration);
                gen = gen >= GenerationMask ? 1 : gen + 1;
                WriteUInt(_data, baseOff + OffsetGeneration, gen);

                // Push onto free list
                WriteInt(_data, baseOff + OffsetNextFree, _freeHead);
                _freeHead = slotIndex;
            }
        }

        private readonly void ValidateSlot(int slotIndex, uint generation)
        {
            if (slotIndex < 0 || slotIndex >= _numSlots)
                throw new InvalidOperationException("ManagedGraphemePool: slot index out of range.");

            int baseOff = slotIndex * _slotSizeBytes;
            if (ReadUInt(_data, baseOff + OffsetGeneration) != generation)
                throw new InvalidOperationException("ManagedGraphemePool: stale generation (wrong generation).");
        }

        private void EnsureCapacity()
        {
            int required = _numSlots * _slotSizeBytes;
            if (required <= _data.Length) return;

            // Grow by slotsPerPage at a time
            int newSlotCount = _numSlots + _slotsPerPage - 1;
            newSlotCount -= newSlotCount % _slotsPerPage;
            int newSize = newSlotCount * _slotSizeBytes;

            byte[] newData = new byte[newSize];
            if (_data.Length > 0)
                _data.AsSpan().CopyTo(newData);
            _data = newData;
        }

        // Little-endian raw read/write helpers
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort ReadUShort(byte[] data, int offset)
            => Unsafe.ReadUnaligned<ushort>(ref data[offset]);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUShort(byte[] data, int offset, ushort value)
            => Unsafe.WriteUnaligned(ref data[offset], value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ReadInt(byte[] data, int offset)
            => Unsafe.ReadUnaligned<int>(ref data[offset]);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteInt(byte[] data, int offset, int value)
            => Unsafe.WriteUnaligned(ref data[offset], value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReadUInt(byte[] data, int offset)
            => Unsafe.ReadUnaligned<uint>(ref data[offset]);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUInt(byte[] data, int offset, uint value)
            => Unsafe.WriteUnaligned(ref data[offset], value);
    }

    #endregion

    #region ByteKey — heap-allocated key for interning dictionary

    /// <summary>
    /// Wrapper around a byte array for use as a dictionary key with value equality.
    /// </summary>
    private readonly struct ByteKey : IEquatable<ByteKey>
    {
        private readonly byte[] _data;

        public ByteKey(ReadOnlySpan<byte> bytes)
        {
            _data = bytes.ToArray();
        }

        public bool Equals(ByteKey other)
            => _data.AsSpan().SequenceEqual(other._data);

        public override bool Equals(object? obj) => obj is ByteKey other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.AddBytes(_data);
            return hash.ToHashCode();
        }
    }

    #endregion
}
