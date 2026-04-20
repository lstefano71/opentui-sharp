namespace OpenTui.Core;

/// <summary>
/// Options for TabSelect renderable.
/// Matches TypeScript TabSelectRenderableOptions.
/// </summary>
public class TabSelectOptions : RenderableOptions
{
    public TabSelectOption[]? Options { get; init; }
    public int TabWidth { get; init; } = 20;
    public Rgba? BackgroundColor { get; init; }
    public Rgba? TextColor { get; init; }
    public Rgba? FocusedBackgroundColor { get; init; }
    public Rgba? FocusedTextColor { get; init; }
    public Rgba? SelectedBackgroundColor { get; init; }
    public Rgba? SelectedTextColor { get; init; }
    public Rgba? SelectedDescriptionColor { get; init; }
    public bool ShowScrollArrows { get; init; } = true;
    public bool ShowDescription { get; init; } = true;
    public bool ShowUnderline { get; init; } = true;
    public bool WrapSelection { get; init; }
}

/// <summary>A single option in a TabSelect.</summary>
public sealed class TabSelectOption
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public object? Value { get; init; }
}

/// <summary>
/// Horizontal tab bar with keyboard navigation, scroll arrows, and underline.
/// Uses buffered rendering. Matches TypeScript TabSelectRenderable from TabSelect.ts.
/// </summary>
public class TabSelectRenderable : Renderable
{
    #region Events

    public static class Events
    {
        public const string SelectionChanged = "selectionChanged";
        public const string ItemSelected = "itemSelected";
    }

    #endregion

    private TabSelectOption[] _options;
    private int _selectedIndex;
    private int _scrollOffset;
    private int _tabWidth;
    private bool _showScrollArrows;
    private bool _showDescription;
    private bool _showUnderline;
    private bool _wrapSelection;

    private Rgba _backgroundColor;
    private Rgba _textColor;
    private Rgba _focusedBackgroundColor;
    private Rgba _focusedTextColor;
    private Rgba _selectedBackgroundColor;
    private Rgba _selectedTextColor;
    private Rgba _selectedDescriptionColor;

    public TabSelectRenderable(IRenderContext ctx, TabSelectOptions? options = null)
        : base(ctx, options ?? new TabSelectOptions { Buffered = true })
    {
        options ??= new TabSelectOptions();

        _options = options.Options ?? [];
        _tabWidth = options.TabWidth;
        _showScrollArrows = options.ShowScrollArrows;
        _showDescription = options.ShowDescription;
        _showUnderline = options.ShowUnderline;
        _wrapSelection = options.WrapSelection;

        _backgroundColor = options.BackgroundColor ?? Rgba.Transparent;
        _textColor = options.TextColor ?? Rgba.FromInts(255, 255, 255);
        _focusedBackgroundColor = options.FocusedBackgroundColor ?? _backgroundColor;
        _focusedTextColor = options.FocusedTextColor ?? _textColor;
        _selectedBackgroundColor = options.SelectedBackgroundColor ?? Rgba.FromHex("#334455");
        _selectedTextColor = options.SelectedTextColor ?? Rgba.FromHex("#FFFF00");
        _selectedDescriptionColor = options.SelectedDescriptionColor ?? Rgba.FromHex("#CCCCCC");

        Focusable = true;

        // Dynamic height: 1 for tabs + underline line + description line
        UpdateDynamicHeight();
    }

    #region Properties

    public TabSelectOption[] Options
    {
        get => _options;
        set
        {
            _options = value;
            _selectedIndex = Math.Clamp(_selectedIndex, 0, Math.Max(0, value.Length - 1));
            _scrollOffset = 0;
            RequestRender();
        }
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set => SetSelectedIndex(value);
    }

    public int TabWidth
    {
        get => _tabWidth;
        set { _tabWidth = value; RequestRender(); }
    }

    public bool ShowScrollArrows
    {
        get => _showScrollArrows;
        set { _showScrollArrows = value; RequestRender(); }
    }

    public bool ShowDescription
    {
        get => _showDescription;
        set { _showDescription = value; UpdateDynamicHeight(); RequestRender(); }
    }

    public bool ShowUnderline
    {
        get => _showUnderline;
        set { _showUnderline = value; UpdateDynamicHeight(); RequestRender(); }
    }

    public bool WrapSelection
    {
        get => _wrapSelection;
        set => _wrapSelection = value;
    }

    #endregion

    #region Selection

    public TabSelectOption? GetSelectedOption() =>
        _selectedIndex >= 0 && _selectedIndex < _options.Length ? _options[_selectedIndex] : null;

    public int GetSelectedIndex() => _selectedIndex;

    public void SetSelectedIndex(int index)
    {
        if (_options.Length == 0) return;
        _selectedIndex = Math.Clamp(index, 0, _options.Length - 1);
        UpdateScrollOffset();
        Emit<(int Index, TabSelectOption? Option)>(Events.SelectionChanged,
            (_selectedIndex, GetSelectedOption()));
        RequestRender();
    }

    public void MoveLeft()
    {
        if (_options.Length == 0) return;
        var next = _selectedIndex - 1;
        if (_wrapSelection && next < 0)
            next = _options.Length - 1;
        SetSelectedIndex(next);
    }

    public void MoveRight()
    {
        if (_options.Length == 0) return;
        var next = _selectedIndex + 1;
        if (_wrapSelection && next >= _options.Length)
            next = 0;
        SetSelectedIndex(next);
    }

    public void SelectCurrent()
    {
        Emit<(int Index, TabSelectOption? Option)>(Events.ItemSelected,
            (_selectedIndex, GetSelectedOption()));
    }

    private int MaxVisibleTabs => _widthValue > 0 && _tabWidth > 0 ? _widthValue / _tabWidth : 0;

    private void UpdateScrollOffset()
    {
        int maxVisible = MaxVisibleTabs;
        if (maxVisible <= 0) return;
        int halfVisible = maxVisible / 2;
        _scrollOffset = Math.Clamp(
            _selectedIndex - halfVisible,
            0,
            Math.Max(0, _options.Length - maxVisible));
    }

    private void UpdateDynamicHeight()
    {
        int h = 1 + (_showUnderline ? 1 : 0) + (_showDescription ? 1 : 0);
        HeightDimension = DimensionValue.Point(h);
    }

    protected override void OnResize(int width, int height)
    {
        UpdateScrollOffset();
        base.OnResize(width, height);
    }

    #endregion

    #region Keyboard

    protected override void HandleKeyPress(KeyEvent key)
    {
        switch (key.Name)
        {
            case "left" or "[":
                MoveLeft();
                key.StopPropagation();
                break;
            case "right" or "]":
                MoveRight();
                key.StopPropagation();
                break;
            case "return" or "linefeed":
                SelectCurrent();
                key.StopPropagation();
                break;
        }

        base.HandleKeyPress(key);
    }

    #endregion

    #region Rendering

    protected override void RenderSelf(OptimizedBuffer buffer, float deltaTime)
    {
        if (_widthValue == 0 || _heightValue == 0) return;

        int startX = (int)_screenX;
        int startY = (int)_screenY;
        var bgColor = Focused ? _focusedBackgroundColor : _backgroundColor;

        // Clear background
        buffer.FillRect((uint)startX, (uint)startY, (uint)_widthValue, (uint)_heightValue, bgColor);

        if (_options.Length == 0) return;

        int maxVisible = MaxVisibleTabs;
        int endIdx = Math.Min(_scrollOffset + maxVisible, _options.Length);

        for (int i = _scrollOffset; i < endIdx; i++)
        {
            var option = _options[i];
            bool isSelected = i == _selectedIndex;
            int relativeIdx = i - _scrollOffset;
            int tabX = startX + relativeIdx * _tabWidth;
            int actualTabWidth = Math.Min(_tabWidth, _widthValue - relativeIdx * _tabWidth);
            if (actualTabWidth <= 0) break;

            // Selection highlight
            if (isSelected)
            {
                buffer.FillRect((uint)tabX, (uint)startY, (uint)actualTabWidth, 1,
                    _selectedBackgroundColor);
            }

            // Tab name (truncated)
            var nameColor = isSelected ? _selectedTextColor : (Focused ? _focusedTextColor : _textColor);
            string name = TruncateText(option.Name, actualTabWidth - 2);
            buffer.DrawText(name, (uint)(tabX + 1), (uint)startY, nameColor);

            // Underline
            if (isSelected && _showUnderline && _heightValue >= 2)
            {
                string underline = new('▬', actualTabWidth);
                buffer.DrawText(underline, (uint)tabX, (uint)(startY + 1), _selectedTextColor);
            }
        }

        // Description
        if (_showDescription)
        {
            int descY = startY + (_showUnderline ? 2 : 1);
            if (descY < startY + _heightValue)
            {
                var selected = GetSelectedOption();
                if (selected?.Description is { } desc)
                {
                    string truncDesc = TruncateText(desc, _widthValue - 2);
                    buffer.DrawText(truncDesc, (uint)(startX + 1), (uint)descY,
                        _selectedDescriptionColor);
                }
            }
        }

        // Scroll arrows
        if (_showScrollArrows && _options.Length > maxVisible && _widthValue > 0)
        {
            var arrowColor = Rgba.FromHex("#AAAAAA");
            if (_scrollOffset > 0)
                buffer.DrawText("‹", (uint)startX, (uint)startY, arrowColor);
            if (_scrollOffset + maxVisible < _options.Length)
                buffer.DrawText("›", (uint)(startX + _widthValue - 1), (uint)startY, arrowColor);
        }
    }

    private static string TruncateText(string text, int maxWidth)
    {
        if (maxWidth <= 0) return "";
        if (text.Length <= maxWidth) return text;
        return text[..(maxWidth - 1)] + "…";
    }

    #endregion
}
