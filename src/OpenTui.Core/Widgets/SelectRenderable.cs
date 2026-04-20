namespace OpenTui.Core;

/// <summary>
/// Vertical list picker with keyboard navigation, scroll indicator, and descriptions.
/// Matches TypeScript SelectRenderable from Select.ts.
/// Uses buffered rendering (framebuffer) for efficient redraw.
/// </summary>
public class SelectRenderable : BoxRenderable
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

    private SelectOption[] _options;
    private int _selectedIndex;
    private int _scrollOffset;
    private bool _showScrollIndicator;
    private bool _showDescription;
    private bool _wrapSelection;
    private int _itemSpacing;
    private int _fastScrollStep;

    private Rgba _textColor;
    private Rgba _focusedBackgroundColor;
    private Rgba _focusedTextColor;
    private Rgba _selectedBackgroundColor;
    private Rgba _selectedTextColor;
    private Rgba _descriptionColor;
    private Rgba _selectedDescriptionColor;

    // Double-click tracking
    private long _lastMouseClickTick;
    private int _lastMouseClickIndex = -1;
    private const int DoubleClickThresholdMs = 400;

    /// <summary>
    /// Initializes a new instance of the SelectRenderable class.
    /// </summary>
    /// <param name="ctx">The render context.</param>
    /// <param name="options">The configuration options.</param>
    public SelectRenderable(IRenderContext ctx, SelectOptions? options = null)
        : base(ctx, options ?? new SelectOptions { Buffered = true })
    {
        options ??= new SelectOptions();

        _options = options.Options ?? [];
        _selectedIndex = Math.Clamp(options.SelectedIndex, 0, Math.Max(0, _options.Length - 1));
        _showScrollIndicator = options.ShowScrollIndicator;
        _showDescription = options.ShowDescription;
        _wrapSelection = options.WrapSelection;
        _itemSpacing = options.ItemSpacing;
        _fastScrollStep = options.FastScrollStep;

        _textColor = options.TextColor ?? Rgba.FromInts(255, 255, 255);
        _focusedBackgroundColor = options.FocusedBackgroundColor ?? Rgba.FromHex("#1a1a1a");
        _focusedTextColor = options.FocusedTextColor ?? Rgba.FromInts(255, 255, 255);
        _selectedBackgroundColor = options.SelectedBackgroundColor ?? Rgba.FromHex("#334455");
        _selectedTextColor = options.SelectedTextColor ?? Rgba.FromHex("#FFFF00");
        _descriptionColor = options.DescriptionColor ?? Rgba.FromHex("#888888");
        _selectedDescriptionColor = options.SelectedDescriptionColor ?? Rgba.FromHex("#CCCCCC");

        Focusable = true;
    }

    #region Properties

    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    public SelectOption[] Options
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
    /// Gets or sets a value indicating whether show scroll indicator.
    /// </summary>
    public bool ShowScrollIndicator
    {
        get => _showScrollIndicator;
        set { _showScrollIndicator = value; RequestRender(); }
    }

    /// <summary>
    /// Gets or sets a value indicating whether show description.
    /// </summary>
    public bool ShowDescription
    {
        get => _showDescription;
        set { _showDescription = value; RequestRender(); }
    }

    /// <summary>
    /// Gets or sets the wrap selection.
    /// </summary>
    public bool WrapSelection
    {
        get => _wrapSelection;
        set => _wrapSelection = value;
    }

    /// <summary>
    /// Gets or sets the item spacing.
    /// </summary>
    public int ItemSpacing
    {
        get => _itemSpacing;
        set { _itemSpacing = value; RequestRender(); }
    }

    /// <summary>
    /// Gets or sets the fast scroll step.
    /// </summary>
    public int FastScrollStep
    {
        get => _fastScrollStep;
        set => _fastScrollStep = value;
    }

    #endregion

    #region Selection

    /// <summary>
    /// Gets a selected option.
    /// </summary>
    /// <returns>The selected option.</returns>
    public SelectOption? GetSelectedOption() =>
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
        Emit<(int Index, SelectOption? Option)>(Events.SelectionChanged,
            (_selectedIndex, GetSelectedOption()));
        RequestRender();
    }

    /// <summary>
    /// Performs move up.
    /// </summary>
    /// <param name="steps">The steps.</param>
    public void MoveUp(int steps = 1)
    {
        if (_options.Length == 0) return;
        var next = _selectedIndex - steps;
        if (_wrapSelection && next < 0)
            next = _options.Length + (next % _options.Length);
        _selectedIndex = Math.Clamp(next, 0, _options.Length - 1);
        UpdateScrollOffset();
        Emit<(int Index, SelectOption? Option)>(Events.SelectionChanged,
            (_selectedIndex, GetSelectedOption()));
        RequestRender();
    }

    /// <summary>
    /// Performs move down.
    /// </summary>
    /// <param name="steps">The steps.</param>
    public void MoveDown(int steps = 1)
    {
        if (_options.Length == 0) return;
        var next = _selectedIndex + steps;
        if (_wrapSelection && next >= _options.Length)
            next %= _options.Length;
        _selectedIndex = Math.Clamp(next, 0, _options.Length - 1);
        UpdateScrollOffset();
        Emit<(int Index, SelectOption? Option)>(Events.SelectionChanged,
            (_selectedIndex, GetSelectedOption()));
        RequestRender();
    }

    /// <summary>
    /// Performs select current.
    /// </summary>
    public void SelectCurrent()
    {
        Emit<(int Index, SelectOption? Option)>(Events.ItemSelected,
            (_selectedIndex, GetSelectedOption()));
    }

    private int LinesPerItem => (_showDescription ? 2 : 1) + _itemSpacing;

    private int MaxVisibleItems
    {
        get
        {
            var (_, _, _, height) = GetContentBounds();
            return height > 0 ? Math.Max(1, height / LinesPerItem) : 0;
        }
    }

    private void UpdateScrollOffset()
    {
        int maxVisible = MaxVisibleItems;
        if (maxVisible <= 0) return;

        int halfVisible = maxVisible / 2;
        _scrollOffset = Math.Clamp(
            _selectedIndex - halfVisible,
            0,
            Math.Max(0, _options.Length - maxVisible));
    }

    #endregion

    #region Keyboard

    /// <inheritdoc />
    protected override void HandleKeyPress(KeyEvent key)
    {
        switch (key.Name)
        {
            case "up" or "k":
                MoveUp(key.Shift ? _fastScrollStep : 1);
                key.StopPropagation();
                break;
            case "down" or "j":
                MoveDown(key.Shift ? _fastScrollStep : 1);
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

    #region Mouse

    /// <inheritdoc />
    protected override void OnMouseEvent(UiMouseEvent evt)
    {
        if (evt.Type == MouseEventType.Scroll)
        {
            if (_options.Length == 0) return;
            var direction = evt.Scroll?.Direction;
            if (direction == "up")
                MoveUp(1);
            else if (direction == "down")
                MoveDown(1);
            evt.StopPropagation();
            return;
        }

        if (evt.Type != MouseEventType.Down || evt.Button != (int)MouseButton.Left)
            return;
        if (_options.Length == 0) return;

        int topInset = (ActiveBorderSides & BorderSides.Top) != 0 ? 1 : 0;
        int localY = evt.Y - (int)_screenY - topInset;
        if (localY < 0) return;

        int linesPerItem = LinesPerItem;
        int itemVisualIndex = localY / linesPerItem;
        int itemIndex = _scrollOffset + itemVisualIndex;
        if (itemIndex < 0 || itemIndex >= _options.Length) return;

        long now = Environment.TickCount64;
        bool isDoubleClick = itemIndex == _lastMouseClickIndex
            && (now - _lastMouseClickTick) <= DoubleClickThresholdMs;

        _lastMouseClickTick = now;
        _lastMouseClickIndex = itemIndex;

        SetSelectedIndex(itemIndex);

        if (isDoubleClick)
            SelectCurrent();
    }

    #endregion

    #region Rendering

    /// <inheritdoc />
    protected override void RenderSelf(OptimizedBuffer buffer, float deltaTime)
    {
        // Draw background
        base.RenderSelf(buffer, deltaTime);

        var (startX, startY, contentWidth, contentHeight) = GetContentBounds();
        if (_options.Length == 0 || contentWidth <= 0 || contentHeight <= 0) return;

        var bgColor = Focused ? _focusedBackgroundColor : BackgroundColor;
        var textColor = Focused ? _focusedTextColor : _textColor;

        if (bgColor.A > 0)
        {
            buffer.FillRect((uint)startX, (uint)startY,
                (uint)contentWidth, (uint)contentHeight, bgColor);
        }

        int linesPerItem = LinesPerItem;
        int maxVisible = MaxVisibleItems;

        int endIdx = Math.Min(_scrollOffset + maxVisible, _options.Length);
        for (int i = _scrollOffset; i < endIdx; i++)
        {
            var option = _options[i];
            bool isSelected = i == _selectedIndex;
            int relativeIdx = i - _scrollOffset;
            int y = startY + relativeIdx * linesPerItem;
            if (y >= startY + contentHeight) break;

            // Selection highlight
            if (isSelected)
            {
                int selectedHeight = Math.Min(linesPerItem - _itemSpacing, startY + contentHeight - y);
                buffer.FillRect((uint)startX, (uint)y,
                    (uint)contentWidth, (uint)selectedHeight,
                    _selectedBackgroundColor);
            }

            // Name
            string prefix = isSelected ? "▶ " : "  ";
            var nameColor = isSelected ? _selectedTextColor : textColor;
            buffer.DrawText(prefix + option.Name, (uint)startX, (uint)y, nameColor);

            // Description
            if (_showDescription && option.Description is { } desc && y + 1 < startY + contentHeight)
            {
                var descColor = isSelected ? _selectedDescriptionColor : _descriptionColor;
                buffer.DrawText("  " + desc, (uint)startX, (uint)(y + 1), descColor);
            }
        }

        // Scroll indicator
        if (_showScrollIndicator && _options.Length > maxVisible && contentWidth > 0)
        {
            int indicatorX = startX + contentWidth - 1;
            float ratio = maxVisible > 0 ? (float)_scrollOffset / Math.Max(1, _options.Length - maxVisible) : 0;
            int indicatorY = startY + (int)(ratio * (contentHeight - 1));
            buffer.DrawText("█", (uint)indicatorX, (uint)indicatorY, textColor);
        }
    }

    #endregion

    private (int X, int Y, int Width, int Height) GetContentBounds()
    {
        int baseX = _buffered ? 0 : (int)_screenX;
        int baseY = _buffered ? 0 : (int)_screenY;

        int topInset = (ActiveBorderSides & BorderSides.Top) != 0 ? 1 : 0;
        int bottomInset = (ActiveBorderSides & BorderSides.Bottom) != 0 ? 1 : 0;
        int leftInset = (ActiveBorderSides & BorderSides.Left) != 0 ? 1 : 0;
        int rightInset = (ActiveBorderSides & BorderSides.Right) != 0 ? 1 : 0;

        return (
            baseX + leftInset,
            baseY + topInset,
            Math.Max(0, _widthValue - leftInset - rightInset),
            Math.Max(0, _heightValue - topInset - bottomInset));
    }
}
