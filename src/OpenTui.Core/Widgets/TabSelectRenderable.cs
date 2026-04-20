namespace OpenTui.Core;

/// <summary>
/// Options for TabSelect renderable.
/// Matches TypeScript TabSelectRenderableOptions.
/// </summary>
public class TabSelectOptions : RenderableOptions
{
    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    public TabSelectOption[]? Options { get; init; }
    /// <summary>
    /// Gets or sets the tab width.
    /// </summary>
    public int TabWidth { get; init; } = 20;
    /// <summary>
    /// Gets or sets the background color.
    /// </summary>
    public Rgba? BackgroundColor { get; init; }
    /// <summary>
    /// Gets or sets the text color.
    /// </summary>
    public Rgba? TextColor { get; init; }
    /// <summary>
    /// Gets or sets the focused background color.
    /// </summary>
    public Rgba? FocusedBackgroundColor { get; init; }
    /// <summary>
    /// Gets or sets the focused text color.
    /// </summary>
    public Rgba? FocusedTextColor { get; init; }
    /// <summary>
    /// Gets or sets the selected background color.
    /// </summary>
    public Rgba? SelectedBackgroundColor { get; init; }
    /// <summary>
    /// Gets or sets the selected text color.
    /// </summary>
    public Rgba? SelectedTextColor { get; init; }
    /// <summary>
    /// Gets or sets the selected description color.
    /// </summary>
    public Rgba? SelectedDescriptionColor { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether show scroll arrows.
    /// </summary>
    public bool ShowScrollArrows { get; init; } = true;
    /// <summary>
    /// Gets or sets a value indicating whether show description.
    /// </summary>
    public bool ShowDescription { get; init; } = true;
    /// <summary>
    /// Gets or sets a value indicating whether show underline.
    /// </summary>
    public bool ShowUnderline { get; init; } = true;
    /// <summary>
    /// Gets or sets the wrap selection.
    /// </summary>
    public bool WrapSelection { get; init; }
}

/// <summary>A single option in a TabSelect.</summary>
public sealed class TabSelectOption
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public required string Name { get; init; }
    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string? Description { get; init; }
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public object? Value { get; init; }
}

/// <summary>
/// Horizontal tab bar with keyboard navigation, scroll arrows, and underline.
/// Uses buffered rendering. Matches TypeScript TabSelectRenderable from TabSelect.ts.
/// </summary>
public class TabSelectRenderable : Renderable
{
    #region Events

    /// <summary>
    /// Represents an Events.
    /// </summary>
    public static class Events
    {
        /// <summary>
        /// Stores the selection changed.
        /// </summary>
        public const string SelectionChanged = "selectionChanged";
        /// <summary>
        /// Stores the item selected.
        /// </summary>
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

    /// <summary>
    /// Initializes a new instance of the TabSelectRenderable class.
    /// </summary>
    /// <param name="ctx">The render context.</param>
    /// <param name="options">The configuration options.</param>
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

    /// <summary>
    /// Gets or sets the options.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the selected index.
    /// </summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set => SetSelectedIndex(value);
    }

    /// <summary>
    /// Gets or sets the tab width.
    /// </summary>
    public int TabWidth
    {
        get => _tabWidth;
        set { _tabWidth = value; RequestRender(); }
    }

    /// <summary>
    /// Gets or sets a value indicating whether show scroll arrows.
    /// </summary>
    public bool ShowScrollArrows
    {
        get => _showScrollArrows;
        set { _showScrollArrows = value; RequestRender(); }
    }

    /// <summary>
    /// Gets or sets a value indicating whether show description.
    /// </summary>
    public bool ShowDescription
    {
        get => _showDescription;
        set { _showDescription = value; UpdateDynamicHeight(); RequestRender(); }
    }

    /// <summary>
    /// Gets or sets a value indicating whether show underline.
    /// </summary>
    public bool ShowUnderline
    {
        get => _showUnderline;
        set { _showUnderline = value; UpdateDynamicHeight(); RequestRender(); }
    }

    /// <summary>
    /// Gets or sets the wrap selection.
    /// </summary>
    public bool WrapSelection
    {
        get => _wrapSelection;
        set => _wrapSelection = value;
    }

    #endregion

    #region Selection

    /// <summary>
    /// Gets a selected option.
    /// </summary>
    /// <returns>The selected option.</returns>
    public TabSelectOption? GetSelectedOption() =>
        _selectedIndex >= 0 && _selectedIndex < _options.Length ? _options[_selectedIndex] : null;

    /// <summary>
    /// Gets a selected index.
    /// </summary>
    /// <returns>The selected index.</returns>
    public int GetSelectedIndex() => _selectedIndex;

    /// <summary>
    /// Sets the selected index.
    /// </summary>
    /// <param name="index">The zero-based index.</param>
    public void SetSelectedIndex(int index)
    {
        if (_options.Length == 0) return;
        _selectedIndex = Math.Clamp(index, 0, _options.Length - 1);
        UpdateScrollOffset();
        Emit<(int Index, TabSelectOption? Option)>(Events.SelectionChanged,
            (_selectedIndex, GetSelectedOption()));
        RequestRender();
    }

    /// <summary>
    /// Performs move left.
    /// </summary>
    public void MoveLeft()
    {
        if (_options.Length == 0) return;
        var next = _selectedIndex - 1;
        if (_wrapSelection && next < 0)
            next = _options.Length - 1;
        SetSelectedIndex(next);
    }

    /// <summary>
    /// Performs move right.
    /// </summary>
    public void MoveRight()
    {
        if (_options.Length == 0) return;
        var next = _selectedIndex + 1;
        if (_wrapSelection && next >= _options.Length)
            next = 0;
        SetSelectedIndex(next);
    }

    /// <summary>
    /// Performs select current.
    /// </summary>
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

    /// <inheritdoc />
    protected override void OnResize(int width, int height)
    {
        UpdateScrollOffset();
        base.OnResize(width, height);
    }

    #endregion

    #region Keyboard

    /// <inheritdoc />
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

    /// <inheritdoc />
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
