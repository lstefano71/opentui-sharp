namespace OpenTui.Core;

/// <summary>
/// Represents configuration options for Extmark.
/// </summary>
public sealed class ExtmarkOptions
{
    /// <summary>
    /// Gets or sets the start.
    /// </summary>
    public required uint Start { get; init; }
    /// <summary>
    /// Gets or sets the end.
    /// </summary>
    public required uint End { get; init; }
    /// <summary>
    /// Gets or sets the virtual.
    /// </summary>
    public bool Virtual { get; init; }
    /// <summary>
    /// Gets or sets the style id.
    /// </summary>
    public uint? StyleId { get; init; }
    /// <summary>
    /// Gets or sets the priority.
    /// </summary>
    public byte? Priority { get; init; }
    /// <summary>
    /// Gets or sets the data.
    /// </summary>
    public object? Data { get; init; }
    /// <summary>
    /// Gets or sets the type id.
    /// </summary>
    public int TypeId { get; init; }
    /// <summary>
    /// Gets or sets the metadata.
    /// </summary>
    public object? Metadata { get; init; }
}

/// <summary>
/// Represents an Extmark.
/// </summary>
public sealed class Extmark
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public required int Id { get; init; }
    /// <summary>
    /// Gets or sets the start.
    /// </summary>
    public uint Start { get; internal set; }
    /// <summary>
    /// Gets or sets the end.
    /// </summary>
    public uint End { get; internal set; }
    /// <summary>
    /// Gets or sets the virtual.
    /// </summary>
    public bool Virtual { get; internal set; }
    /// <summary>
    /// Gets or sets the style id.
    /// </summary>
    public uint? StyleId { get; internal set; }
    /// <summary>
    /// Gets or sets the priority.
    /// </summary>
    public byte? Priority { get; internal set; }
    /// <summary>
    /// Gets or sets the data.
    /// </summary>
    public object? Data { get; internal set; }
    /// <summary>
    /// Gets or sets the type id.
    /// </summary>
    public int TypeId { get; internal set; }

    internal Extmark Clone() =>
        new()
        {
            Id = Id,
            Start = Start,
            End = End,
            Virtual = Virtual,
            StyleId = StyleId,
            Priority = Priority,
            Data = Data,
            TypeId = TypeId
        };
}

/// <summary>
/// Represents an Extmarks Controller.
/// </summary>
public sealed class ExtmarksController
{
    private readonly EditBuffer _editBuffer;
    private readonly EditorView _editorView;
    private readonly TextBuffer _textBuffer;
    private readonly Dictionary<int, Extmark> _extmarks = [];
    private readonly Dictionary<int, HashSet<int>> _extmarksByTypeId = [];
    private readonly Dictionary<int, object?> _metadata = [];
    private readonly ExtmarksHistory _history = new();
    private readonly Dictionary<string, int> _typeNameToId = new(StringComparer.Ordinal);
    private readonly Dictionary<int, string> _typeIdToName = [];

    private bool _destroyed;
    private int _nextId = 1;
    private int _nextTypeId = 1;

    internal ExtmarksController(EditBuffer editBuffer, EditorView editorView, TextBuffer textBuffer)
    {
        _editBuffer = editBuffer;
        _editorView = editorView;
        _textBuffer = textBuffer;
    }

    /// <summary>
    /// Performs create.
    /// </summary>
    /// <param name="options">The configuration options.</param>
    /// <returns>The result of create.</returns>
    public int Create(ExtmarkOptions options)
    {
        EnsureNotDestroyed();

        int id = _nextId++;
        var extmark = new Extmark
        {
            Id = id,
            Start = options.Start,
            End = options.End,
            Virtual = options.Virtual,
            StyleId = options.StyleId,
            Priority = options.Priority,
            Data = options.Data,
            TypeId = options.TypeId
        };

        _extmarks[id] = extmark;
        if (!_extmarksByTypeId.TryGetValue(extmark.TypeId, out var ids))
        {
            ids = [];
            _extmarksByTypeId[extmark.TypeId] = ids;
        }

        ids.Add(id);
        if (options.Metadata is not null)
            _metadata[id] = options.Metadata;

        UpdateHighlights();
        return id;
    }

    /// <summary>
    /// Performs delete.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>true if delete; otherwise, false.</returns>
    public bool Delete(int id)
    {
        EnsureNotDestroyed();
        if (!_extmarks.ContainsKey(id))
            return false;

        DeleteExtmarkById(id);
        UpdateHighlights();
        return true;
    }

    /// <summary>
    /// Performs get.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The result of get.</returns>
    public Extmark? Get(int id) => _destroyed || !_extmarks.TryGetValue(id, out var extmark) ? null : extmark.Clone();

    /// <summary>
    /// Gets an all.
    /// </summary>
    /// <returns>The all.</returns>
    public IReadOnlyList<Extmark> GetAll() => _destroyed ? [] : _extmarks.Values.Select(extmark => extmark.Clone()).ToArray();

    /// <summary>
    /// Gets a virtual.
    /// </summary>
    /// <returns>The virtual.</returns>
    public IReadOnlyList<Extmark> GetVirtual() =>
        _destroyed ? [] : _extmarks.Values.Where(extmark => extmark.Virtual).Select(extmark => extmark.Clone()).ToArray();

    /// <summary>
    /// Gets an at offset.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <returns>The at offset.</returns>
    public IReadOnlyList<Extmark> GetAtOffset(uint offset) =>
        _destroyed ? [] : _extmarks.Values.Where(extmark => offset >= extmark.Start && offset < extmark.End).Select(extmark => extmark.Clone()).ToArray();

    /// <summary>
    /// Gets an all for type id.
    /// </summary>
    /// <param name="typeId">The type id.</param>
    /// <returns>The all for type id.</returns>
    public IReadOnlyList<Extmark> GetAllForTypeId(int typeId)
    {
        if (_destroyed || !_extmarksByTypeId.TryGetValue(typeId, out var ids))
            return [];

        return ids.Select(id => _extmarks[id].Clone()).ToArray();
    }

    /// <summary>
    /// Performs clear.
    /// </summary>
    public void Clear()
    {
        if (_destroyed)
            return;

        ClearCore();
        UpdateHighlights();
    }

    /// <summary>
    /// Performs register type.
    /// </summary>
    /// <param name="typeName">The type name.</param>
    /// <returns>The result of register type.</returns>
    public int RegisterType(string typeName)
    {
        EnsureNotDestroyed();

        if (_typeNameToId.TryGetValue(typeName, out var existing))
            return existing;

        int typeId = _nextTypeId++;
        _typeNameToId[typeName] = typeId;
        _typeIdToName[typeId] = typeName;
        return typeId;
    }

    /// <summary>
    /// Gets a type id.
    /// </summary>
    /// <param name="typeName">The type name.</param>
    /// <returns>The type id.</returns>
    public int? GetTypeId(string typeName) =>
        _destroyed || !_typeNameToId.TryGetValue(typeName, out var typeId) ? null : typeId;

    /// <summary>
    /// Gets a type name.
    /// </summary>
    /// <param name="typeId">The type id.</param>
    /// <returns>The type name.</returns>
    public string? GetTypeName(int typeId) =>
        _destroyed || !_typeIdToName.TryGetValue(typeId, out var typeName) ? null : typeName;

    /// <summary>
    /// Gets a metadata for.
    /// </summary>
    /// <param name="extmarkId">The extmark id.</param>
    /// <returns>The metadata for.</returns>
    public object? GetMetadataFor(int extmarkId) =>
        _destroyed || !_metadata.TryGetValue(extmarkId, out var metadata) ? null : metadata;

    /// <summary>
    /// Performs destroy.
    /// </summary>
    public void Destroy()
    {
        if (_destroyed)
            return;

        ClearCore();
        _typeNameToId.Clear();
        _typeIdToName.Clear();
        _history.Clear();
        UpdateHighlights();
        _destroyed = true;
    }

    internal bool TryMoveCursorLeft(bool hasSelection)
    {
        if (_destroyed || hasSelection)
            return false;

        uint currentOffset = _editorView.GetVisualCursor().Offset;
        if (currentOffset == 0)
            return false;

        uint targetOffset = currentOffset - 1;
        var virtualExtmark = FindVirtualExtmarkContaining(targetOffset);
        if (virtualExtmark is null || currentOffset < virtualExtmark.End)
            return false;

        SetCursorByOffsetRaw(virtualExtmark.Start == 0 ? 0 : virtualExtmark.Start - 1);
        return true;
    }

    internal bool TryMoveCursorRight(bool hasSelection)
    {
        if (_destroyed || hasSelection)
            return false;

        uint currentOffset = _editorView.GetVisualCursor().Offset;
        uint targetOffset = currentOffset + 1;
        uint textLength = (uint)_editBuffer.GetText().Length;
        if (targetOffset > textLength)
            return false;

        var virtualExtmark = FindVirtualExtmarkContaining(targetOffset);
        if (virtualExtmark is null || currentOffset > virtualExtmark.Start)
            return false;

        SetCursorByOffsetRaw(virtualExtmark.End);
        return true;
    }

    internal void AdjustCursorAfterVerticalMove(uint previousOffset)
    {
        if (_destroyed)
            return;

        uint newOffset = _editorView.GetVisualCursor().Offset;
        var virtualExtmark = FindVirtualExtmarkContaining(newOffset);
        if (virtualExtmark is null)
            return;

        uint distanceToStart = newOffset - virtualExtmark.Start;
        uint distanceToEnd = virtualExtmark.End - newOffset;

        if (distanceToStart < distanceToEnd)
        {
            uint adjustedOffset = virtualExtmark.Start == 0 ? 0 : virtualExtmark.Start - 1;
            uint targetOffset = adjustedOffset <= previousOffset ? virtualExtmark.End : adjustedOffset;
            _editorView.SetCursorByOffset(targetOffset);
            return;
        }

        _editorView.SetCursorByOffset(virtualExtmark.End);
    }

    internal void AdjustCursorAfterSetOffset(uint requestedOffset, uint previousOffset)
    {
        if (_destroyed)
            return;

        bool movingForward = requestedOffset > previousOffset;
        if (movingForward)
        {
            var virtualExtmark = FindVirtualExtmarkContaining(requestedOffset);
            if (virtualExtmark is not null && previousOffset <= virtualExtmark.Start)
                SetCursorByOffsetRaw(virtualExtmark.End);
            return;
        }

        foreach (var extmark in _extmarks.Values)
        {
            if (!extmark.Virtual)
                continue;

            if (previousOffset >= extmark.End && requestedOffset < extmark.End && requestedOffset >= extmark.Start)
            {
                SetCursorByOffsetRaw(extmark.Start == 0 ? 0 : extmark.Start - 1);
                return;
            }
        }
    }

    internal bool TryDeleteCharBackward(bool hadSelection)
    {
        if (_destroyed)
            return false;

        SaveSnapshot();

        uint currentOffset = _editorView.GetVisualCursor().Offset;
        if (currentOffset == 0 || hadSelection)
            return false;

        uint targetOffset = currentOffset - 1;
        var virtualExtmark = FindVirtualExtmarkContaining(targetOffset);
        if (virtualExtmark is null || currentOffset != virtualExtmark.End)
            return false;

        DeleteExtmarkRange(virtualExtmark);
        return true;
    }

    internal bool TryDeleteChar(bool hadSelection)
    {
        if (_destroyed)
            return false;

        SaveSnapshot();

        uint currentOffset = _editorView.GetVisualCursor().Offset;
        uint textLength = (uint)_editBuffer.GetText().Length;
        if (currentOffset >= textLength || hadSelection)
            return false;

        var virtualExtmark = FindVirtualExtmarkContaining(currentOffset);
        if (virtualExtmark is null || currentOffset != virtualExtmark.Start)
            return false;

        DeleteExtmarkRange(virtualExtmark);
        return true;
    }

    internal void HandleDeletion(uint deleteOffset, uint length)
    {
        if (_destroyed)
            return;

        AdjustExtmarksAfterDeletion(deleteOffset, length);
    }

    internal void HandleInsertion(uint insertOffset, uint length)
    {
        if (_destroyed)
            return;

        AdjustExtmarksAfterInsertion(insertOffset, length);
    }

    internal void HandleSetText()
    {
        if (_destroyed)
            return;

        ClearCore();
        UpdateHighlights();
    }

    internal void HandleReplaceText()
    {
        if (_destroyed)
            return;

        SaveSnapshot();
        ClearCore();
        UpdateHighlights();
    }

    internal void HandleClear()
    {
        if (_destroyed)
            return;

        SaveSnapshot();
        ClearCore();
        UpdateHighlights();
    }

    internal void HandleSelectionDeletion(uint deleteOffset, uint deleteLength)
    {
        if (_destroyed)
            return;

        SaveSnapshot();
        if (deleteLength > 0)
            AdjustExtmarksAfterDeletion(deleteOffset, deleteLength);
    }

    internal void SaveSnapshot()
    {
        if (_destroyed)
            return;

        _history.SaveSnapshot(_extmarks, _nextId);
    }

    internal void RestoreUndoState()
    {
        if (_destroyed || !_history.CanUndo())
            return;

        _history.PushRedo(new ExtmarksSnapshot(CloneExtmarks(), _nextId));
        var snapshot = _history.Undo();
        if (snapshot is { } value)
            RestoreSnapshot(value);
    }

    internal void RestoreRedoState()
    {
        if (_destroyed || !_history.CanRedo())
            return;

        _history.PushUndo(new ExtmarksSnapshot(CloneExtmarks(), _nextId));
        var snapshot = _history.Redo();
        if (snapshot is { } value)
            RestoreSnapshot(value);
    }

    private void RestoreSnapshot(ExtmarksSnapshot snapshot)
    {
        _extmarks.Clear();
        foreach (var (id, extmark) in snapshot.Extmarks)
            _extmarks[id] = extmark.Clone();

        _nextId = snapshot.NextId;
        RebuildTypeIndex();
        UpdateHighlights();
    }

    private void DeleteExtmarkRange(Extmark extmark)
    {
        var startCursor = OffsetToPosition(extmark.Start);
        var endCursor = OffsetToPosition(extmark.End);
        uint deleteOffset = extmark.Start;
        uint deleteLength = extmark.End - extmark.Start;

        DeleteExtmarkById(extmark.Id);
        _editBuffer.DeleteRange(startCursor.Row, startCursor.Col, endCursor.Row, endCursor.Col);
        AdjustExtmarksAfterDeletion(deleteOffset, deleteLength);
    }

    private void DeleteExtmarkById(int id)
    {
        if (!_extmarks.Remove(id, out var extmark))
            return;

        if (_extmarksByTypeId.TryGetValue(extmark.TypeId, out var ids))
        {
            ids.Remove(id);
            if (ids.Count == 0)
                _extmarksByTypeId.Remove(extmark.TypeId);
        }

        _metadata.Remove(id);
    }

    private Extmark? FindVirtualExtmarkContaining(uint offset)
    {
        foreach (var extmark in _extmarks.Values)
        {
            if (extmark.Virtual && offset >= extmark.Start && offset < extmark.End)
                return extmark;
        }

        return null;
    }

    private void AdjustExtmarksAfterInsertion(uint insertOffset, uint length)
    {
        foreach (var extmark in _extmarks.Values)
        {
            if (extmark.Start >= insertOffset)
            {
                extmark.Start += length;
                extmark.End += length;
            }
            else if (extmark.End > insertOffset)
            {
                extmark.End += length;
            }
        }

        UpdateHighlights();
    }

    private void AdjustExtmarksAfterDeletion(uint deleteOffset, uint length)
    {
        var toDelete = new List<int>();

        foreach (var extmark in _extmarks.Values)
        {
            if (extmark.End <= deleteOffset)
                continue;

            if (extmark.Start >= deleteOffset + length)
            {
                extmark.Start -= length;
                extmark.End -= length;
            }
            else if (extmark.Start >= deleteOffset && extmark.End <= deleteOffset + length)
            {
                toDelete.Add(extmark.Id);
            }
            else if (extmark.Start < deleteOffset && extmark.End > deleteOffset + length)
            {
                extmark.End -= length;
            }
            else if (extmark.Start < deleteOffset && extmark.End > deleteOffset)
            {
                extmark.End -= Math.Min(extmark.End, deleteOffset + length) - deleteOffset;
            }
            else if (extmark.Start < deleteOffset + length && extmark.End > deleteOffset + length)
            {
                extmark.Start = deleteOffset;
                extmark.End -= length;
            }
        }

        foreach (int id in toDelete)
            DeleteExtmarkById(id);

        UpdateHighlights();
    }

    private LogicalCursor OffsetToPosition(uint offset) =>
        _editBuffer.OffsetToPosition(offset, out var cursor) ? cursor : default;

    private void UpdateHighlights()
    {
        _textBuffer.ClearHighlights();

        foreach (var extmark in _extmarks.Values)
        {
            if (extmark.StyleId is not { } styleId)
                continue;

            _textBuffer.AddHighlightByCharRange(new Highlight
            {
                Start = OffsetExcludingNewlines(extmark.Start),
                End = OffsetExcludingNewlines(extmark.End),
                StyleId = styleId,
                Priority = extmark.Priority ?? 0,
                HlRef = unchecked((ushort)extmark.Id)
            });
        }
    }

    private uint OffsetExcludingNewlines(uint offset)
    {
        return _editBuffer.OffsetToPosition(offset, out var cursor)
            ? offset - cursor.Row
            : offset;
    }

    private void ClearCore()
    {
        _extmarks.Clear();
        _extmarksByTypeId.Clear();
        _metadata.Clear();
    }

    private Dictionary<int, Extmark> CloneExtmarks()
    {
        var snapshot = new Dictionary<int, Extmark>(_extmarks.Count);
        foreach (var (id, extmark) in _extmarks)
            snapshot[id] = extmark.Clone();

        return snapshot;
    }

    private void RebuildTypeIndex()
    {
        _extmarksByTypeId.Clear();
        foreach (var (id, extmark) in _extmarks)
        {
            if (!_extmarksByTypeId.TryGetValue(extmark.TypeId, out var ids))
            {
                ids = [];
                _extmarksByTypeId[extmark.TypeId] = ids;
            }

            ids.Add(id);
        }
    }

    private void SetCursorByOffsetRaw(uint offset)
    {
        _editBuffer.SetCursorByOffset(offset);
        _editorView.SetCursorByOffset(offset);
    }

    private void EnsureNotDestroyed()
    {
        if (_destroyed)
            throw new InvalidOperationException("ExtmarksController is destroyed.");
    }
}
