namespace OpenTui;

/// <summary>
/// Manages keyboard focus across widgets in a widget tree.
/// Supports Tab/Shift+Tab navigation and programmatic focus changes.
/// </summary>
public sealed class FocusManager
{
    private readonly List<Widget> _focusableWidgets = [];
    private int _focusIndex = -1;

    /// <summary>The currently focused widget, or null.</summary>
    public Widget? Focused => _focusIndex >= 0 && _focusIndex < _focusableWidgets.Count
        ? _focusableWidgets[_focusIndex]
        : null;

    /// <summary>Registers a widget as focusable.</summary>
    public void Register(Widget widget)
    {
        if (!_focusableWidgets.Contains(widget))
            _focusableWidgets.Add(widget);
    }

    /// <summary>Unregisters a widget from focus management.</summary>
    public void Unregister(Widget widget)
    {
        int idx = _focusableWidgets.IndexOf(widget);
        if (idx < 0) return;

        _focusableWidgets.RemoveAt(idx);
        if (_focusIndex >= _focusableWidgets.Count)
            _focusIndex = _focusableWidgets.Count - 1;
    }

    /// <summary>Moves focus to the next focusable widget.</summary>
    public void FocusNext()
    {
        if (_focusableWidgets.Count == 0) return;
        _focusIndex = (_focusIndex + 1) % _focusableWidgets.Count;
    }

    /// <summary>Moves focus to the previous focusable widget.</summary>
    public void FocusPrevious()
    {
        if (_focusableWidgets.Count == 0) return;
        _focusIndex = (_focusIndex - 1 + _focusableWidgets.Count) % _focusableWidgets.Count;
    }

    /// <summary>Programmatically focus a specific widget.</summary>
    public bool Focus(Widget widget)
    {
        int idx = _focusableWidgets.IndexOf(widget);
        if (idx < 0) return false;
        _focusIndex = idx;
        return true;
    }

    /// <summary>Clears focus from all widgets.</summary>
    public void ClearFocus() => _focusIndex = -1;
}
