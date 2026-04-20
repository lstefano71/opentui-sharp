namespace OpenTui.Core;

/// <summary>
/// Options for <see cref="TabControllerRenderable"/>.
/// </summary>
public sealed class TabControllerOptions : RenderableOptions
{
    public Rgba? BackgroundColor { get; init; }
    public int TabBarHeight { get; init; } = 4;
    public int TabWidth { get; init; } = 20;
    public Rgba? TabBarBackgroundColor { get; init; }
    public Rgba? HelpTextColor { get; init; }
    public Rgba? TextColor { get; init; }
    public Rgba? SelectedBackgroundColor { get; init; }
    public Rgba? SelectedTextColor { get; init; }
    public Rgba? SelectedDescriptionColor { get; init; }
    public bool ShowDescription { get; init; } = true;
    public bool ShowUnderline { get; init; } = true;
    public bool ShowScrollArrows { get; init; } = true;
    public bool WrapSelection { get; init; }
}

/// <summary>
/// Declarative tab definition used by <see cref="TabControllerRenderable"/>.
/// </summary>
public sealed class TabControllerTab
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required Action<BoxRenderable> Initialize { get; init; }
    public Action<float, BoxRenderable>? Update { get; init; }
    public Action? Show { get; init; }
    public Action? Hide { get; init; }
}

internal sealed class TabControllerState
{
    public required TabControllerTab Definition { get; init; }
    public required BoxRenderable Group { get; init; }
    public bool Initialized { get; set; }
}

/// <summary>
/// Composes a <see cref="TabSelectRenderable"/> with lazily initialized tab pages.
/// Matches the upstream example tab-controller helper closely enough to support
/// faithful multi-tab sample ports without sample-local scaffolding.
/// </summary>
public sealed class TabControllerRenderable : Renderable
{
    public static class Events
    {
        public const string TabChanged = "tabChanged";
    }

    private readonly List<TabControllerState> _tabs = [];
    private readonly TabSelectRenderable _tabSelect;
    private readonly int _tabBarHeight;
    private readonly Rgba _helpTextColor;
    private Rgba _backgroundColor;
    private int _currentTabIndex = -1;

    public TabControllerRenderable(IRenderContext ctx, TabControllerOptions? options = null)
        : base(ctx, options ?? new TabControllerOptions())
    {
        options ??= new TabControllerOptions();

        _backgroundColor = options.BackgroundColor ?? Rgba.Transparent;
        _tabBarHeight = Math.Max(1, options.TabBarHeight);
        _helpTextColor = options.HelpTextColor ?? Rgba.FromHex("#FFFFFF");

        _tabSelect = new TabSelectRenderable(ctx, new TabSelectOptions
        {
            Id = $"{Id}-tabs",
            Position = PositionValue.Absolute,
            Left = DimensionValue.Point(0),
            Top = DimensionValue.Point(0),
            Width = DimensionValue.Percent(100),
            Options = [],
            TabWidth = options.TabWidth,
            BackgroundColor = options.TabBarBackgroundColor ?? _backgroundColor,
            TextColor = options.TextColor,
            SelectedBackgroundColor = options.SelectedBackgroundColor,
            SelectedTextColor = options.SelectedTextColor,
            SelectedDescriptionColor = options.SelectedDescriptionColor,
            ShowDescription = options.ShowDescription,
            ShowUnderline = options.ShowUnderline,
            ShowScrollArrows = options.ShowScrollArrows,
            WrapSelection = options.WrapSelection,
        });

        _tabSelect.On<(int Index, TabSelectOption? Option)>(
            TabSelectRenderable.Events.SelectionChanged,
            args => SwitchToTab(args.Index, syncTabStrip: false));

        Add(_tabSelect);
    }

    public Rgba BackgroundColor
    {
        get => _backgroundColor;
        set
        {
            _backgroundColor = value;
            RequestRender();
        }
    }

    public TabSelectRenderable TabStrip => _tabSelect;

    public int TabCount => _tabs.Count;

    public int GetCurrentTabIndex() => _currentTabIndex;

    public string GetCurrentHelpText() =>
        _tabs.Count == 0
            ? string.Empty
            : BuildHelpText(Math.Clamp(_currentTabIndex, 0, _tabs.Count - 1));

    public TabControllerTab? GetCurrentTab() =>
        _currentTabIndex >= 0 && _currentTabIndex < _tabs.Count
            ? _tabs[_currentTabIndex].Definition
            : null;

    public BoxRenderable? GetCurrentTabGroup() =>
        _currentTabIndex >= 0 && _currentTabIndex < _tabs.Count
            ? _tabs[_currentTabIndex].Group
            : null;

    public int AddTab(TabControllerTab tab)
    {
        ArgumentNullException.ThrowIfNull(tab);

        var group = new BoxRenderable(_ctx, new BoxOptions
        {
            Id = $"{Id}-tab-{_tabs.Count}",
            Position = PositionValue.Absolute,
            Left = DimensionValue.Point(0),
            Top = DimensionValue.Point(_tabBarHeight),
            Right = DimensionValue.Point(0),
            Bottom = DimensionValue.Point(0),
            Overflow = OverflowValue.Hidden,
            Visible = false,
        });

        Add(group);

        var state = new TabControllerState
        {
            Definition = tab,
            Group = group,
        };

        _tabs.Add(state);
        UpdateTabStripOptions();

        if (_tabs.Count == 1)
        {
            _currentTabIndex = 0;
            group.Visible = true;
            EnsureInitialized(state);
            state.Definition.Show?.Invoke();
            Emit<(int Index, TabControllerTab Tab, BoxRenderable Group)>(
                Events.TabChanged,
                (0, state.Definition, state.Group));
        }

        return _tabs.Count - 1;
    }

    public void SwitchToTab(int index) => SwitchToTab(index, syncTabStrip: true);

    public void NextTab()
    {
        if (_tabs.Count == 0)
            return;

        SwitchToTab((_currentTabIndex + 1 + _tabs.Count) % _tabs.Count);
    }

    public void PreviousTab()
    {
        if (_tabs.Count == 0)
            return;

        SwitchToTab((_currentTabIndex - 1 + _tabs.Count) % _tabs.Count);
    }

    public override void Focus() => _tabSelect.Focus();

    public override void Blur() => _tabSelect.Blur();

    protected override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);

        if (_currentTabIndex < 0 || _currentTabIndex >= _tabs.Count)
            return;

        var current = _tabs[_currentTabIndex];
        current.Definition.Update?.Invoke(deltaTime, current.Group);
    }

    protected override void RenderSelf(OptimizedBuffer buffer, float deltaTime)
    {
        if (_widthValue <= 0 || _heightValue <= 0)
            return;

        int startX = _buffered ? 0 : (int)_screenX;
        int startY = _buffered ? 0 : (int)_screenY;
        if (_backgroundColor.A > 0)
            buffer.FillRect((uint)startX, (uint)startY, (uint)_widthValue, (uint)_heightValue, _backgroundColor);

        int helpLineY = 1 + (_tabSelect.ShowUnderline ? 1 : 0) + (_tabSelect.ShowDescription ? 1 : 0);
        if (_tabs.Count > 0 && helpLineY < _tabBarHeight && helpLineY < _heightValue)
        {
            string helpText = TruncateText(GetCurrentHelpText(), _widthValue - 2);
            if (helpText.Length > 0)
                buffer.DrawText(helpText, (uint)(startX + 1), (uint)(startY + helpLineY), _helpTextColor);
        }
    }

    private void SwitchToTab(int index, bool syncTabStrip)
    {
        if (index < 0 || index >= _tabs.Count || index == _currentTabIndex)
            return;

        if (_currentTabIndex >= 0)
        {
            var previous = _tabs[_currentTabIndex];
            previous.Group.Visible = false;
            previous.Definition.Hide?.Invoke();
        }

        _currentTabIndex = index;

        if (syncTabStrip && _tabSelect.GetSelectedIndex() != index)
            _tabSelect.SelectedIndex = index;

        var next = _tabs[index];
        EnsureInitialized(next);
        next.Group.Visible = true;
        next.Definition.Show?.Invoke();

        Emit<(int Index, TabControllerTab Tab, BoxRenderable Group)>(
            Events.TabChanged,
            (index, next.Definition, next.Group));
    }

    private void EnsureInitialized(TabControllerState state)
    {
        if (state.Initialized)
            return;

        state.Definition.Initialize(state.Group);
        state.Initialized = true;
    }

    private void UpdateTabStripOptions()
    {
        _tabSelect.Options =
        [
            .. _tabs.Select((tab, index) => new TabSelectOption
            {
                Name = tab.Definition.Title,
                Description = tab.Definition.Description ?? BuildHelpText(index),
                Value = index,
            }),
        ];
    }

    private string BuildHelpText(int index) =>
        $"Tab {index + 1}/{_tabs.Count} - Use Left/Right arrows to navigate | Press Ctrl+C to exit | D or .: toggle debug | Ctrl+G: dump hit grid";

    private static string TruncateText(string text, int maxWidth)
    {
        if (maxWidth <= 0)
            return string.Empty;

        if (text.Length <= maxWidth)
            return text;

        return text[..(maxWidth - 1)] + "…";
    }
}
