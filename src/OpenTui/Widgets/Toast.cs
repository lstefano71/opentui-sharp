namespace OpenTui;

/// <summary>Visual variant for a <see cref="Toast"/> notification.</summary>
public enum ToastVariant : byte
{
    /// <summary>Informational toast.</summary>
    Info = 0,

    /// <summary>Success toast.</summary>
    Success = 1,

    /// <summary>Warning toast.</summary>
    Warning = 2,

    /// <summary>Error toast.</summary>
    Error = 3,
}

/// <summary>
/// A notification popup widget that displays a brief message and
/// automatically dismisses after a configurable duration.
/// </summary>
public class Toast : Widget
{
    /// <summary>The message text to display.</summary>
    public string Message { get; set; } = "";

    /// <summary>Visual variant of the toast.</summary>
    public ToastVariant Variant { get; set; } = ToastVariant.Info;

    /// <summary>How long the toast stays visible before auto-dismissing.</summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>Whether the toast is currently visible.</summary>
    public bool IsVisible { get; set; }

    /// <summary>Raised when the toast is dismissed.</summary>
    public event Action? OnDismissed;

    /// <summary>Invokes <see cref="OnDismissed"/> from within the assembly.</summary>
    internal void RaiseDismissed() => OnDismissed?.Invoke();

    /// <inheritdoc />
    protected internal override void Draw(NativeBuffer buffer, int offsetX, int offsetY)
    {
        // TODO: wire to native buffer
    }
}

/// <summary>
/// Manages a stack of <see cref="Toast"/> notifications, providing
/// convenience methods to show and dismiss toasts.
/// </summary>
public class ToastManager
{
    private readonly List<Toast> _toasts = [];

    /// <summary>The currently active toasts (most recent last).</summary>
    public IReadOnlyList<Toast> ActiveToasts => _toasts.AsReadOnly();

    /// <summary>
    /// Creates a new toast, adds it to the active stack, and returns it.
    /// </summary>
    /// <param name="message">Message to display.</param>
    /// <param name="variant">Visual variant.</param>
    public Toast Show(string message, ToastVariant variant = ToastVariant.Info)
    {
        var toast = new Toast
        {
            Message = message,
            Variant = variant,
            IsVisible = true,
        };
        _toasts.Add(toast);
        return toast;
    }

    /// <summary>Dismisses a toast and removes it from the active stack.</summary>
    public void Dismiss(Toast toast)
    {
        toast.IsVisible = false;
        toast.RaiseDismissed();
        _toasts.Remove(toast);
    }
}
