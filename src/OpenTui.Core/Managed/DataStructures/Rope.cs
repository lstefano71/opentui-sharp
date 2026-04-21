using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace OpenTui.Core.Managed.DataStructures;

#region Interfaces

/// <summary>
/// Implement on custom metrics structs to support additive aggregation in the rope.
/// </summary>
public interface IAdditiveMetrics<TSelf> where TSelf : struct, IAdditiveMetrics<TSelf>
{
    void Add(in TSelf other);
    uint Weight();
}

/// <summary>
/// Implement on rope element types to provide custom metrics (e.g. byte counts, line widths).
/// </summary>
public interface IRopeMetrics<TMetrics> where TMetrics : struct, IAdditiveMetrics<TMetrics>
{
    TMetrics Measure();
}

/// <summary>
/// Implement on rope element types to provide a weight for weight-based indexing.
/// </summary>
public interface IRopeWeightable
{
    uint Weight { get; }
}

/// <summary>
/// Implement on rope element types to control how boundaries are rewritten on join.
/// (Deferred — included for forward-compatibility.)
/// </summary>
public interface IRopeBoundary<T>
{
    static abstract bool CanMerge(in T left, in T right);
    static abstract T Merge(in T left, in T right);
}

/// <summary>
/// Implement on rope element types to provide a default/empty sentinel value.
/// </summary>
public interface IRopeEmpty<TSelf>
{
    static abstract TSelf Empty();
}

#endregion

#region Metrics

/// <summary>
/// Aggregated metrics cached at every node in the rope tree.
/// </summary>
public readonly struct RopeMetrics : IEquatable<RopeMetrics>
{
    public readonly uint Count;
    public readonly uint Depth;

    public RopeMetrics(uint count, uint depth)
    {
        Count = count;
        Depth = depth;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RopeMetrics Add(in RopeMetrics other) =>
        new(Count + other.Count, Math.Max(Depth, other.Depth));

    public bool Equals(RopeMetrics other) => Count == other.Count && Depth == other.Depth;
    public override bool Equals(object? obj) => obj is RopeMetrics m && Equals(m);
    public override int GetHashCode() => HashCode.Combine(Count, Depth);
}

/// <summary>
/// Extended metrics including optional custom metrics from <typeparamref name="TCustom"/>.
/// </summary>
public readonly struct RopeMetrics<TCustom> : IEquatable<RopeMetrics<TCustom>>
    where TCustom : struct, IAdditiveMetrics<TCustom>
{
    public readonly uint Count;
    public readonly uint Depth;
    public readonly TCustom Custom;

    public RopeMetrics(uint count, uint depth, TCustom custom)
    {
        Count = count;
        Depth = depth;
        Custom = custom;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint Weight() => Custom.Weight();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RopeMetrics<TCustom> Add(in RopeMetrics<TCustom> other)
    {
        var c = Custom;
        c.Add(in other.Custom);
        return new(Count + other.Count, Math.Max(Depth, other.Depth), c);
    }

    public bool Equals(RopeMetrics<TCustom> other) =>
        Count == other.Count && Depth == other.Depth && Custom.Equals(other.Custom);
    public override bool Equals(object? obj) => obj is RopeMetrics<TCustom> m && Equals(m);
    public override int GetHashCode() => HashCode.Combine(Count, Depth, Custom);
}

#endregion

#region Result types

/// <summary>Result of a weight-based find operation.</summary>
public readonly record struct WeightFindResult<T>(T Leaf, uint StartWeight);

#endregion

#region Node hierarchy

/// <summary>
/// Abstract base for rope tree nodes. Nodes are immutable reference types shared
/// across persistent rope versions (critical for undo/redo).
/// </summary>
internal abstract class RopeNode<T>
{
    public abstract uint Count { get; }
    public abstract uint Depth { get; }
    public abstract uint GetWeight();
    public abstract bool IsBalanced { get; }

    public abstract T? Get(uint index);
    public abstract bool Walk(Func<T, uint, bool> visitor, ref uint currentIndex);
    public abstract bool WalkFrom(uint startIndex, Func<T, uint, bool> visitor, ref uint currentIndex);
    public abstract void Collect(List<RopeNode<T>> leaves);
}

internal sealed class BranchNode<T> : RopeNode<T>
{
    public readonly RopeNode<T> Left;
    public readonly RopeNode<T> Right;
    public readonly uint LeftCount;
    public readonly uint LeftWeight;
    private readonly uint _count;
    private readonly uint _depth;
    private readonly uint _weight;

    public BranchNode(RopeNode<T> left, RopeNode<T> right)
    {
        Left = left;
        Right = right;
        LeftCount = left.Count;
        LeftWeight = left.GetWeight();
        _count = left.Count + right.Count;
        _depth = Math.Max(left.Depth, right.Depth) + 1;
        _weight = left.GetWeight() + right.GetWeight();
    }

    public override uint Count => _count;
    public override uint Depth => _depth;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override uint GetWeight() => _weight;

    public override bool IsBalanced
    {
        get
        {
            if (_weight == 0) return true;
            uint maxSide = (_weight * 3) / 4;
            return LeftWeight <= maxSide && (_weight - LeftWeight) <= maxSide;
        }
    }

    public override T? Get(uint index)
    {
        if (index < LeftCount)
            return Left.Get(index);
        return Right.Get(index - LeftCount);
    }

    public override bool Walk(Func<T, uint, bool> visitor, ref uint currentIndex)
    {
        if (!Left.Walk(visitor, ref currentIndex)) return false;
        return Right.Walk(visitor, ref currentIndex);
    }

    public override bool WalkFrom(uint startIndex, Func<T, uint, bool> visitor, ref uint currentIndex)
    {
        if (startIndex >= LeftCount)
        {
            return Right.WalkFrom(startIndex - LeftCount, visitor, ref currentIndex);
        }

        if (!Left.WalkFrom(startIndex, visitor, ref currentIndex)) return false;
        return Right.Walk(visitor, ref currentIndex);
    }

    public override void Collect(List<RopeNode<T>> leaves)
    {
        Left.Collect(leaves);
        Right.Collect(leaves);
    }
}

internal sealed class LeafNode<T> : RopeNode<T>
{
    public readonly T Data;
    public readonly bool IsSentinel;
    private readonly uint _weight;

    public LeafNode(T data, bool isSentinel, uint weight)
    {
        Data = data;
        IsSentinel = isSentinel;
        _weight = weight;
    }

    public override uint Count => IsSentinel ? 0u : 1u;
    public override uint Depth => 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override uint GetWeight() => IsSentinel ? 0u : _weight;

    public override bool IsBalanced => true;

    public override T? Get(uint index) => index == 0 && !IsSentinel ? Data : default;

    public override bool Walk(Func<T, uint, bool> visitor, ref uint currentIndex)
    {
        if (IsSentinel) return true;
        bool result = visitor(Data, currentIndex);
        currentIndex++;
        return result;
    }

    public override bool WalkFrom(uint startIndex, Func<T, uint, bool> visitor, ref uint currentIndex)
    {
        if (IsSentinel) return true;
        if (startIndex == 0)
        {
            bool result = visitor(Data, currentIndex);
            currentIndex++;
            return result;
        }
        return true;
    }

    public override void Collect(List<RopeNode<T>> leaves)
    {
        leaves.Add(this);
    }
}

#endregion

#region Undo types

internal sealed class UndoEntry<T>
{
    public readonly RopeNode<T> Root;
    public readonly string Meta;
    public UndoEntry<T>? Next;
    public UndoBranch<T>? Branches;

    public UndoEntry(RopeNode<T> root, string meta)
    {
        Root = root;
        Meta = meta;
    }
}

internal sealed class UndoBranch<T>
{
    public readonly UndoEntry<T> Redo;
    public readonly UndoBranch<T>? Next;

    public UndoBranch(UndoEntry<T> redo, UndoBranch<T>? next)
    {
        Redo = redo;
        Next = next;
    }
}

#endregion

/// <summary>
/// Runtime helper to resolve the empty/sentinel value for a rope element type.
/// Caches the result in a static field for zero-cost subsequent access.
/// </summary>
internal static class RopeEmptyHelper<T>
{
    // For types that don't implement IRopeEmpty<T>, this will be default(T).
    // For types that do, call SetEmpty() from a static constructor or initialization path
    // before first use of the rope, or use the Rope<T> overload that accepts a sentinel value.
    private static T _empty = default!;
    private static bool _initialized;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T GetEmpty() => _empty;

    /// <summary>
    /// Registers a custom empty/sentinel value for type T. Call once at startup for
    /// types that implement IRopeEmpty&lt;T&gt;. This avoids reflection and is AOT-safe.
    /// </summary>
    public static void SetEmpty(T value)
    {
        _empty = value;
        _initialized = true;
    }

    internal static bool IsInitialized => _initialized;
}

/// <summary>
/// Helper to register the empty value for types implementing <see cref="IRopeEmpty{TSelf}"/>.
/// Call <c>RopeEmpty&lt;T&gt;.Register()</c> once before using <c>Rope&lt;T&gt;</c>.
/// </summary>
public static class RopeEmpty<T> where T : IRopeEmpty<T>
{
    private static bool _registered;

    /// <summary>Registers T.Empty() as the sentinel value. Safe to call multiple times.</summary>
    public static void Register()
    {
        if (_registered) return;
        RopeEmptyHelper<T>.SetEmpty(T.Empty());
        _registered = true;
    }
}

/// <summary>
/// A persistent/immutable rope data structure — a balanced binary tree of elements.
/// All mutation operations return new rope states without modifying existing nodes,
/// enabling efficient undo/redo via root-pointer swapping.
/// <para>
/// Port of the Zig rope from opentui/packages/core/src/zig/rope.zig.
/// </para>
/// </summary>
public sealed class Rope<T>
{
    private RopeNode<T> _root;
    private readonly LeafNode<T> _emptyLeaf;
    private UndoEntry<T>? _undoHistory;
    private UndoEntry<T>? _redoHistory;
    private UndoEntry<T>? _currHistory;
    private uint _undoDepth;
    private ulong _version;

    /// <summary>Maximum undo depth. Null means unlimited.</summary>
    public uint? MaxUndoDepth { get; set; }

    #region Construction

    private Rope(RopeNode<T> root, LeafNode<T> emptyLeaf, uint? maxUndoDepth = null)
    {
        _root = root;
        _emptyLeaf = emptyLeaf;
        MaxUndoDepth = maxUndoDepth;
    }

    /// <summary>Creates an empty rope.</summary>
    public static Rope<T> Create(uint? maxUndoDepth = null)
    {
        var emptyLeaf = CreateSentinelLeaf();
        return new Rope<T>(emptyLeaf, emptyLeaf, maxUndoDepth);
    }

    /// <summary>Gets a new empty rope instance.</summary>
    public static Rope<T> Empty => Create();

    /// <summary>Creates a rope containing a single item.</summary>
    public static Rope<T> FromItem(T item, uint? maxUndoDepth = null)
    {
        var emptyLeaf = CreateSentinelLeaf();
        var root = CreateLeaf(item);
        return new Rope<T>(root, emptyLeaf, maxUndoDepth);
    }

    /// <summary>Creates a rope from a span of items via balanced merge.</summary>
    public static Rope<T> FromSlice(ReadOnlySpan<T> items, uint? maxUndoDepth = null)
    {
        var emptyLeaf = CreateSentinelLeaf();
        if (items.Length == 0)
            return new Rope<T>(emptyLeaf, emptyLeaf, maxUndoDepth);

        var leaves = new RopeNode<T>[items.Length];
        for (int i = 0; i < items.Length; i++)
            leaves[i] = CreateLeaf(items[i]);

        var root = MergeLeaves(leaves, 0, leaves.Length);
        return new Rope<T>(root, emptyLeaf, maxUndoDepth);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static LeafNode<T> CreateSentinelLeaf()
    {
        T emptyData = RopeEmptyHelper<T>.GetEmpty();
        return new LeafNode<T>(emptyData, isSentinel: true, weight: 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static LeafNode<T> CreateLeaf(T data)
    {
        uint weight = data is IRopeWeightable w ? w.Weight : 1;
        return new LeafNode<T>(data, isSentinel: false, weight: weight);
    }

    #endregion

    #region Properties

    /// <summary>Number of non-sentinel elements in the rope.</summary>
    public uint Count => _root.Count;

    /// <summary>Tree depth.</summary>
    public uint TreeDepth => _root.Depth;

    /// <summary>Total weight (custom weight if T : IRopeWeightable, otherwise same as Count).</summary>
    public uint TotalWeight => _root.GetWeight();

    /// <summary>Whether the rope is empty.</summary>
    public bool IsEmpty => _root.Count == 0;

    /// <summary>Monotonically-increasing version counter, bumped on every mutation.</summary>
    public ulong Version => _version;

    #endregion

    #region Access

    /// <summary>Gets the element at the given index, or default if out of range.</summary>
    public T? Get(uint index) => index < Count ? _root.Get(index) : default;

    #endregion

    #region Mutation (persistent — returns new state via root swap)

    /// <summary>Inserts a single item at the given index.</summary>
    public Rope<T> Insert(uint index, T item)
    {
        InsertSliceInternal(index, [item]);
        return this;
    }

    /// <summary>Inserts a span of items at the given index.</summary>
    public Rope<T> InsertSlice(uint index, ReadOnlySpan<T> items)
    {
        InsertSliceInternal(index, items);
        return this;
    }

    /// <summary>Deletes the element at the given index.</summary>
    public Rope<T> Delete(uint index)
    {
        DeleteRange(index, index + 1);
        return this;
    }

    /// <summary>Deletes elements in the range [start, end).</summary>
    public Rope<T> DeleteRange(uint start, uint end)
    {
        if (start >= end) return this;

        var (firstLeft, firstRight) = SplitAt(_root, start);
        var (_, secondRight) = SplitAt(firstRight, end - start);
        _root = JoinBalanced(firstLeft, secondRight);
        _version++;
        return this;
    }

    /// <summary>Replaces the element at the given index with a new value.</summary>
    public Rope<T> Replace(uint index, T item)
    {
        if (index >= Count) return this;
        DeleteRange(index, index + 1);
        InsertSliceInternal(index, [item]);
        return this;
    }

    /// <summary>Appends a single item to the end.</summary>
    public Rope<T> Append(T item) => Insert(Count, item);

    /// <summary>Prepends a single item to the beginning.</summary>
    public Rope<T> Prepend(T item) => Insert(0, item);

    /// <summary>Concatenates another rope onto the end of this one.</summary>
    public Rope<T> Concat(Rope<T> other)
    {
        _root = JoinBalanced(_root, other._root);
        _version++;
        return this;
    }

    /// <summary>
    /// Splits the rope at the given index.
    /// This rope becomes the left half [0, index); the returned rope is the right half [index, Count).
    /// </summary>
    public (Rope<T> Left, Rope<T> Right) Split(uint index)
    {
        var (left, right) = SplitAt(_root, index);
        _root = left;
        _version++;
        var rightRope = new Rope<T>(right, _emptyLeaf, MaxUndoDepth);
        return (this, rightRope);
    }

    /// <summary>Replaces all content with the given items.</summary>
    public Rope<T> SetSegments(ReadOnlySpan<T> items)
    {
        if (items.Length == 0)
        {
            _root = _emptyLeaf;
            _version++;
            return this;
        }

        var leaves = new RopeNode<T>[items.Length];
        for (int i = 0; i < items.Length; i++)
            leaves[i] = CreateLeaf(items[i]);

        _root = MergeLeaves(leaves, 0, leaves.Length);
        _version++;
        return this;
    }

    /// <summary>Clears the rope to empty.</summary>
    public Rope<T> Clear()
    {
        _root = _emptyLeaf;
        _version++;
        return this;
    }

    private void InsertSliceInternal(uint index, ReadOnlySpan<T> items)
    {
        if (items.Length == 0) return;

        var leaves = new RopeNode<T>[items.Length];
        for (int i = 0; i < items.Length; i++)
            leaves[i] = CreateLeaf(items[i]);

        var insertRoot = MergeLeaves(leaves, 0, leaves.Length);
        var (left, right) = SplitAt(_root, index);
        var leftJoined = JoinBalanced(left, insertRoot);
        _root = JoinBalanced(leftJoined, right);
        _version++;
    }

    #endregion

    #region Traversal

    /// <summary>
    /// Walks all non-sentinel leaves in order. The visitor receives (data, index).
    /// </summary>
    public void Walk(Action<T, uint> visitor)
    {
        uint idx = 0;
        _root.Walk((data, index) =>
        {
            visitor(data, index);
            return true;
        }, ref idx);
    }

    /// <summary>
    /// Walks all non-sentinel leaves in order. The visitor receives (data, index)
    /// and returns true to continue or false to stop.
    /// </summary>
    public void Walk(Func<T, uint, bool> visitor)
    {
        uint idx = 0;
        _root.Walk(visitor, ref idx);
    }

    /// <summary>
    /// Walks non-sentinel leaves starting from startIndex. The visitor receives (data, index).
    /// </summary>
    public void WalkFrom(uint startIndex, Action<T, uint> visitor)
    {
        uint idx = 0;
        _root.WalkFrom(startIndex, (data, index) =>
        {
            visitor(data, index);
            return true;
        }, ref idx);
    }

    /// <summary>
    /// Walks non-sentinel leaves starting from startIndex. The visitor receives (data, index)
    /// and returns true to continue or false to stop.
    /// </summary>
    public void WalkFrom(uint startIndex, Func<T, uint, bool> visitor)
    {
        uint idx = 0;
        _root.WalkFrom(startIndex, visitor, ref idx);
    }

    /// <summary>Collects all non-sentinel elements into an array.</summary>
    public T[] ToArray()
    {
        if (Count == 0) return [];
        return ToArrayCore();
    }

    private T[] ToArrayCore()
    {
        var result = new T[Count];
        int writeIdx = 0;
        uint walkIdx = 0;
        _root.Walk((data, _) =>
        {
            result[writeIdx++] = data;
            return true;
        }, ref walkIdx);
        return result;
    }

    /// <summary>Extracts a slice [start, end) into an array.</summary>
    public T[] Slice(uint start, uint end)
    {
        if (start >= end || start >= Count) return [];
        end = Math.Min(end, Count);

        var result = new List<T>((int)(end - start));
        uint currentIndex = 0;
        uint walkIdx = 0;
        _root.Walk((data, _) =>
        {
            if (currentIndex >= start && currentIndex < end)
                result.Add(data);
            currentIndex++;
            return currentIndex < end;
        }, ref walkIdx);
        return [.. result];
    }

    #endregion

    #region Weight-based operations

    /// <summary>
    /// Splits the rope at the given weight boundary.
    /// This rope becomes the left half; the returned rope is the right half.
    /// The splitLeaf function is called if a leaf must be split at a weight offset within it.
    /// </summary>
    public (Rope<T> Left, Rope<T> Right) SplitByWeight(uint weight, Func<T, uint, (T Left, T Right)> splitLeaf)
    {
        var (left, right) = SplitAtWeight(_root, weight, splitLeaf);
        _root = left;
        _version++;
        var rightRope = new Rope<T>(right, _emptyLeaf, MaxUndoDepth);
        return (this, rightRope);
    }

    /// <summary>Finds the leaf containing the given weight offset.</summary>
    public WeightFindResult<T>? FindByWeight(uint weight)
    {
        return FindByWeightInNode(_root, weight, 0);
    }

    /// <summary>Deletes elements in the weight range [start, end).</summary>
    public Rope<T> DeleteRangeByWeight(uint start, uint end, Func<T, uint, (T Left, T Right)> splitLeaf)
    {
        if (start >= end) return this;

        var (firstLeft, firstRight) = SplitAtWeight(_root, start, splitLeaf);
        var (_, secondRight) = SplitAtWeight(firstRight, end - start, splitLeaf);
        _root = JoinBalanced(firstLeft, secondRight);
        _version++;
        return this;
    }

    /// <summary>Inserts items at a weight-based position.</summary>
    public Rope<T> InsertSliceByWeight(uint weight, ReadOnlySpan<T> items, Func<T, uint, (T Left, T Right)> splitLeaf)
    {
        if (items.Length == 0) return this;

        var leaves = new RopeNode<T>[items.Length];
        for (int i = 0; i < items.Length; i++)
            leaves[i] = CreateLeaf(items[i]);

        var insertRoot = MergeLeaves(leaves, 0, leaves.Length);
        var (left, right) = SplitAtWeight(_root, weight, splitLeaf);
        var leftJoined = JoinBalanced(left, insertRoot);
        _root = JoinBalanced(leftJoined, right);
        _version++;
        return this;
    }

    private static WeightFindResult<T>? FindByWeightInNode(RopeNode<T> node, uint targetWeight, uint currentWeight)
    {
        if (node is BranchNode<T> branch)
        {
            uint leftWeight = branch.LeftWeight;
            if (targetWeight < currentWeight + leftWeight)
                return FindByWeightInNode(branch.Left, targetWeight, currentWeight);
            return FindByWeightInNode(branch.Right, targetWeight, currentWeight + leftWeight);
        }

        if (node is LeafNode<T> leaf)
        {
            uint leafWeight = node.GetWeight();
            if (targetWeight < currentWeight + leafWeight)
                return new WeightFindResult<T>(leaf.Data, currentWeight);
            return null;
        }

        return null;
    }

    private (RopeNode<T> Left, RopeNode<T> Right) SplitAtWeight(
        RopeNode<T> node, uint targetWeight, Func<T, uint, (T Left, T Right)> splitLeaf)
    {
        if (node is LeafNode<T> leaf)
        {
            uint leafWeight = node.GetWeight();

            if (targetWeight == 0)
                return (_emptyLeaf, node);
            if (targetWeight >= leafWeight)
                return (node, _emptyLeaf);

            var (leftData, rightData) = splitLeaf(leaf.Data, targetWeight);
            return (CreateLeaf(leftData), CreateLeaf(rightData));
        }

        if (node is BranchNode<T> branch)
        {
            uint leftWeight = branch.LeftWeight;

            if (targetWeight < leftWeight)
            {
                var (sl, sr) = SplitAtWeight(branch.Left, targetWeight, splitLeaf);
                var newRight = JoinBalanced(sr, branch.Right);
                return (sl, newRight);
            }
            if (targetWeight > leftWeight)
            {
                var (sl, sr) = SplitAtWeight(branch.Right, targetWeight - leftWeight, splitLeaf);
                var newLeft = JoinBalanced(branch.Left, sl);
                return (newLeft, sr);
            }

            return (branch.Left, branch.Right);
        }

        return (_emptyLeaf, _emptyLeaf);
    }

    #endregion

    #region Rebalancing

    /// <summary>Rebalances the tree if it has become unbalanced.</summary>
    public Rope<T> Rebalance()
    {
        if (_root.IsBalanced) return this;

        var leaves = new List<RopeNode<T>>((int)Count);
        _root.Collect(leaves);

        if (leaves.Count == 0)
        {
            _root = _emptyLeaf;
            return this;
        }

        _root = MergeLeaves(leaves, 0, leaves.Count);
        return this;
    }

    #endregion

    #region Undo / Redo

    /// <summary>Whether there is an undo entry available.</summary>
    public bool CanUndo => _undoHistory is not null;

    /// <summary>Whether there is a redo entry available.</summary>
    public bool CanRedo => _redoHistory is not null && _currHistory is not null;

    /// <summary>
    /// Stores the current state as an undo checkpoint with the given metadata string.
    /// Call this before making a batch of changes that should be undoable as one unit.
    /// </summary>
    public void StoreUndo(string meta)
    {
        var undoEntry = new UndoEntry<T>(_root, meta);
        PushUndo(undoEntry);
        _currHistory = null;
        PushRedoBranch();
    }

    /// <summary>
    /// Undoes the last change. Returns the metadata string from the undo checkpoint, or null if nothing to undo.
    /// The <paramref name="currentMeta"/> is stored with the current state so it can be restored on redo.
    /// </summary>
    public string? Undo(string currentMeta = "")
    {
        var r = _currHistory ?? new UndoEntry<T>(_root, currentMeta);
        var h = _undoHistory;
        if (h is null) return null;

        _undoHistory = h.Next;
        _currHistory = h;
        _root = h.Root;
        _version++;
        PushRedo(r);
        if (_undoDepth > 0) _undoDepth--;
        return h.Meta;
    }

    /// <summary>
    /// Redoes the last undone change. Returns the metadata string, or null if nothing to redo.
    /// </summary>
    public string? Redo()
    {
        var u = _currHistory;
        var h = _redoHistory;
        if (u is null || h is null) return null;
        if (u.Root != _root) return null;

        _redoHistory = h.Next;
        _currHistory = h;
        _root = h.Root;
        _version++;
        PushUndo(u);
        return h.Meta;
    }

    /// <summary>Clears all undo/redo history.</summary>
    public void ClearHistory()
    {
        _undoHistory = null;
        _redoHistory = null;
        _currHistory = null;
        _undoDepth = 0;
    }

    private void PushUndo(UndoEntry<T> entry)
    {
        entry.Next = _undoHistory;
        _undoHistory = entry;
        _undoDepth++;

        if (MaxUndoDepth is { } maxDepth && _undoDepth > maxDepth)
            TrimUndoHistory(maxDepth);
    }

    private void PushRedo(UndoEntry<T> entry)
    {
        entry.Next = _redoHistory;
        _redoHistory = entry;
    }

    private void PushRedoBranch()
    {
        var r = _redoHistory;
        var u = _undoHistory;
        if (r is null || u is null) return;

        u.Branches = new UndoBranch<T>(r, u.Branches);
        _redoHistory = null;
    }

    private void TrimUndoHistory(uint maxDepth)
    {
        var current = _undoHistory;
        uint depthCount = 0;
        UndoEntry<T>? prev = null;

        while (current is not null)
        {
            depthCount++;
            if (depthCount >= maxDepth)
            {
                if (prev is not null)
                    prev.Next = null;
                _undoDepth = maxDepth;
                return;
            }
            prev = current;
            current = current.Next;
        }
    }

    #endregion

    #region Tree operations (static helpers)

    /// <summary>
    /// Splits a node at the given count-based index.
    /// Returns (left, right) where left contains indices [0, index) and right contains [index, count).
    /// </summary>
    private (RopeNode<T> Left, RopeNode<T> Right) SplitAt(RopeNode<T> node, uint index)
    {
        if (node is LeafNode<T>)
        {
            return index == 0
                ? (_emptyLeaf, node)
                : (node, _emptyLeaf);
        }

        if (node is BranchNode<T> branch)
        {
            uint leftCount = branch.LeftCount;
            if (index < leftCount)
            {
                var (sl, sr) = SplitAt(branch.Left, index);
                var newRight = JoinBalanced(sr, branch.Right);
                return (sl, newRight);
            }
            if (index > leftCount)
            {
                var (sl, sr) = SplitAt(branch.Right, index - leftCount);
                var newLeft = JoinBalanced(branch.Left, sl);
                return (newLeft, sr);
            }

            return (branch.Left, branch.Right);
        }

        return (_emptyLeaf, _emptyLeaf);
    }

    /// <summary>
    /// Joins two subtrees while maintaining balance.
    /// If one side exceeds 3/4 of total weight, rotates to rebalance.
    /// </summary>
    private static RopeNode<T> JoinBalanced(RopeNode<T> left, RopeNode<T> right)
    {
        uint leftCount = left.Count;
        uint rightCount = right.Count;

        if (leftCount == 0) return right;
        if (rightCount == 0) return left;

        uint leftWeight = left.GetWeight();
        uint rightWeight = right.GetWeight();
        uint totalWeight = leftWeight + rightWeight;

        if (totalWeight > 0)
        {
            uint maxSide = (totalWeight * 3) / 4;
            if (leftWeight <= maxSide && rightWeight <= maxSide)
                return new BranchNode<T>(left, right);
        }

        // Left-heavy: rotate right child up
        if (leftWeight > rightWeight * 3)
        {
            if (left is BranchNode<T> lb)
            {
                var newRight = JoinBalanced(lb.Right, right);
                return new BranchNode<T>(lb.Left, newRight);
            }
            return new BranchNode<T>(left, right);
        }

        // Right-heavy: rotate left child up
        if (right is BranchNode<T> rb)
        {
            var newLeft = JoinBalanced(left, rb.Left);
            return new BranchNode<T>(newLeft, rb.Right);
        }
        return new BranchNode<T>(left, right);
    }

    /// <summary>
    /// Builds a balanced tree from a contiguous range of leaf nodes via midpoint splitting.
    /// </summary>
    private static RopeNode<T> MergeLeaves(IReadOnlyList<RopeNode<T>> leaves, int start, int end)
    {
        int len = end - start;
        Debug.Assert(len > 0, "MergeLeaves called with empty range");

        if (len == 1) return leaves[start];
        if (len == 2) return new BranchNode<T>(leaves[start], leaves[start + 1]);

        int mid = start + len / 2;
        return new BranchNode<T>(
            MergeLeaves(leaves, start, mid),
            MergeLeaves(leaves, mid, end));
    }

    #endregion
}
