namespace OpenTui.Core;

internal sealed class ExtmarksHistory
{
    private readonly Stack<ExtmarksSnapshot> _undoStack = [];
    private readonly Stack<ExtmarksSnapshot> _redoStack = [];

    public void SaveSnapshot(Dictionary<int, Extmark> extmarks, int nextId)
    {
        _undoStack.Push(new ExtmarksSnapshot(CloneExtmarks(extmarks), nextId));
        _redoStack.Clear();
    }

    public ExtmarksSnapshot? Undo() => _undoStack.Count == 0 ? null : _undoStack.Pop();

    public ExtmarksSnapshot? Redo() => _redoStack.Count == 0 ? null : _redoStack.Pop();

    public void PushRedo(ExtmarksSnapshot snapshot) => _redoStack.Push(snapshot);

    public void PushUndo(ExtmarksSnapshot snapshot) => _undoStack.Push(snapshot);

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }

    public bool CanUndo() => _undoStack.Count > 0;

    public bool CanRedo() => _redoStack.Count > 0;

    private static Dictionary<int, Extmark> CloneExtmarks(Dictionary<int, Extmark> extmarks)
    {
        var clone = new Dictionary<int, Extmark>(extmarks.Count);
        foreach (var (id, extmark) in extmarks)
            clone[id] = extmark.Clone();

        return clone;
    }
}

internal readonly record struct ExtmarksSnapshot(Dictionary<int, Extmark> Extmarks, int NextId);
