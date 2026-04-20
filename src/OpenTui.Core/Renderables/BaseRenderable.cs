namespace OpenTui.Core;

/// <summary>
/// Abstract base for all renderable nodes — provides shared identity, dirty tracking,
/// visibility, and destruction semantics. Matches TypeScript BaseRenderable.
///
/// Both <see cref="Renderable"/> (layout-backed visual nodes) and
/// <see cref="TextNodeRenderable"/> (inline text composition nodes) extend this class.
///
/// Child management is NOT defined here because the two hierarchies have
/// incompatible child types (Renderable vs string|TextNodeRenderable).
/// </summary>
public abstract class BaseRenderable : EventEmitter
{
    private static int s_nextBaseNum = 1;

    /// <summary>Backing field for <see cref="Id"/>.</summary>
    protected string _id;

    /// <summary>Backing field for <see cref="IsDirty"/>.</summary>
    protected bool _dirty;

    /// <summary>Backing field for <see cref="Visible"/>.</summary>
    protected bool _visible = true;

    /// <summary>Auto-incremented unique number for this renderable.</summary>
    public int Num { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseRenderable"/> class.
    /// </summary>
    protected BaseRenderable(string? id = null)
    {
        Num = Interlocked.Increment(ref s_nextBaseNum);
        _id = id ?? $"renderable-{Num}";
    }

    #region Id

    /// <summary>Gets or sets the identifier.</summary>
    public virtual string Id
    {
        get => _id;
        set => _id = value;
    }

    #endregion

    #region Dirty

    /// <summary>Gets a value indicating whether this renderable is dirty.</summary>
    public bool IsDirty => _dirty;

    /// <summary>Marks this renderable as clean.</summary>
    protected void MarkClean() => _dirty = false;

    /// <summary>Marks this renderable as dirty.</summary>
    protected void MarkDirty() => _dirty = true;

    #endregion

    #region Visible

    /// <summary>Gets or sets a value indicating whether this renderable is visible.</summary>
    public virtual bool Visible
    {
        get => _visible;
        set => _visible = value;
    }

    #endregion

    #region Destroy

    /// <summary>Destroys this renderable. Override for custom cleanup.</summary>
    public virtual void Destroy() { }

    /// <summary>Destroys this renderable and all descendants recursively.</summary>
    public virtual void DestroyRecursively() { }

    #endregion

    /// <summary>Requests a render pass. Subclasses propagate this to the renderer.</summary>
    public abstract void RequestRender();
}
